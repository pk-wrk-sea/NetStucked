"""Owned loopback software devices. Never models actual vendor acceptance."""
import base64
import hashlib
import json
import logging
import socket
import sys
import threading
import time
import paramiko

logging.disable(logging.CRITICAL)
KEY = paramiko.RSAKey.generate(2048)
STOP = threading.Event()
LOCK = threading.Lock()
AUDIT = dict(auth=[], commands=[], active=0, peak=0, sessions=0)
CONNECTIONS = []
LISTENERS = []


class Server(paramiko.ServerInterface):
    def __init__(self, mode):
        self.mode = mode
        self.shell = threading.Event()

    def get_allowed_auths(self, username):
        return "keyboard-interactive" if username in ("mfa-user", "partial-user") else "password"

    def check_auth_password(self, username, password):
        accept = password == "fixture-password" and username != "mfa-user"
        with LOCK:
            AUDIT["auth"].append(dict(mode=self.mode, accepted=accept))
        if username == "partial-user" and accept:
            return paramiko.AUTH_PARTIALLY_SUCCESSFUL
        return paramiko.AUTH_SUCCESSFUL if accept else paramiko.AUTH_FAILED

    def check_channel_request(self, kind, number):
        return paramiko.OPEN_SUCCEEDED if kind == "session" else paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED

    def check_channel_pty_request(self, *args):
        return True

    def check_channel_shell_request(self, channel):
        self.shell.set()
        return True


def serve(sock, mode, name):
    if mode == "telnet":
        return serve_telnet(sock, name)
    if mode == "kex-stall":
        CONNECTIONS.append(sock)
        STOP.wait(30)
        sock.close()
        return
    transport = paramiko.Transport(sock)
    CONNECTIONS.append(transport)
    active = False
    try:
        security = transport.get_security_options()
        security.ciphers = ("aes256-ctr",)
        security.digests = ("hmac-sha2-512-etm@openssh.com",)
        transport.add_server_key(KEY)
        server = Server(mode)
        transport.start_server(server=server)
        channel = transport.accept(15)
        if channel is None or not server.shell.wait(10):
            return
        with LOCK:
            active = True
            AUDIT["active"] += 1
            AUDIT["sessions"] += 1
            AUDIT["peak"] = max(AUDIT["peak"], AUDIT["active"])
        prompt = f"<{name}>" if mode.startswith("huawei") else f"RP/0/RP0/CPU0:{name}#" if mode == "xr" else name + "#"
        if mode == "huawei-confirm":
            channel.sendall(b"There are security risks in the configuration file, continue? [y/n]")
        else:
            channel.sendall(prompt.encode())
        pending = bytearray()
        while not STOP.is_set():
            chunk = channel.recv(4096)
            if not chunk:
                break
            for char in chunk:
                if char in (10, 13):
                    command = pending.decode("utf-8")
                    pending.clear()
                    if not command:
                        if mode != "huawei-confirm":
                            channel.sendall(("\r\n" + prompt).encode())
                        continue
                    with LOCK:
                        AUDIT["commands"].append(dict(mode=mode, command=command))
                    channel.sendall((command + "\r\n").encode())
                    if mode == "wait" and command.startswith("show "):
                        while transport.is_active() and not STOP.wait(0.05):
                            pass
                        return
                    if mode == "disconnect" and command.startswith("show "):
                        return
                    if command in ("show running-config", "display current-configuration"):
                        text = f"! OWNED SOFTWARE FIXTURE; NOT REAL HARDWARE\r\nhostname {name}\r\ninterface loopback0\r\n description ไทย fixture-password\r\nend\r\n"
                        if mode == "large":
                            text += "! owned large output ไทย fixture-password\r\n" * 8192
                    elif command in ("show version", "display version"):
                        text = "Owned fixture version\r\n"
                    elif command == "show bad":
                        text = "% Invalid input detected\r\n"
                    else:
                        text = ""
                    time.sleep(0.015)
                    channel.sendall((text + prompt).encode("utf-8"))
                else:
                    pending.append(char)
    except (OSError, EOFError, paramiko.SSHException):
        pass
    finally:
        transport.close()
        if active:
            with LOCK:
                AUDIT["active"] -= 1


def serve_telnet(sock, name):
    CONNECTIONS.append(sock)
    pending = bytearray()
    def line():
        while True:
            ch = sock.recv(1)
            if not ch:
                raise EOFError()
            if ch == b"\n":
                continue
            if ch == b"\r":
                value = pending.decode("utf-8"); pending.clear(); return value
            pending.extend(ch)
    try:
        sock.sendall(b"Username: "); line()
        sock.sendall(b"Password: "); accepted = line() == "fixture-password"
        with LOCK:
            AUDIT["auth"].append(dict(mode="telnet", accepted=accepted))
        if not accepted:
            sock.sendall(b"Login incorrect\r\n"); return
        sock.sendall(("\r\n" + name + "#").encode())
        while not STOP.is_set():
            command = line()
            text = ""
            if command:
                with LOCK:
                    AUDIT["commands"].append(dict(mode="telnet", command=command))
                text = command + "\r\n"
            if command == "show running-config":
                text += "hostname " + name + "\r\n! OWNED TELNET FIXTURE\r\n"
            sock.sendall((text + name + "#").encode())
    except (EOFError, OSError):
        pass
    finally:
        sock.close()


def listen(listener, mode, name):
    while not STOP.is_set():
        try:
            sock, _ = listener.accept()
            threading.Thread(target=serve, args=(sock, mode, name), daemon=True).start()
        except socket.timeout:
            pass
        except OSError:
            break


if __name__ == "__main__":
    targets = []
    for mode, name in (("ios", "LAB-IOS"), ("xe", "LAB-XE"), ("xr", "LAB-XR"), ("nxos", "LAB-NX"), ("huawei", "LAB-VRP"), ("huawei-confirm", "LAB-STOP"), ("wait", "LAB-WAIT"), ("disconnect", "LAB-DROP"), ("kex-stall", "LAB-KEX"), ("telnet", "LAB-TELNET"), ("large", "LAB-LARGE")):
        listener = socket.socket()
        listener.bind(("127.0.0.1", 0))
        listener.listen(16)
        listener.settimeout(0.3)
        LISTENERS.append(listener)
        targets.append(dict(mode=mode, name=name, port=listener.getsockname()[1]))
        threading.Thread(target=listen, args=(listener, mode, name), daemon=True).start()
    pin = "SHA256:" + base64.b64encode(hashlib.sha256(KEY.asbytes()).digest()).decode().rstrip("=")
    print(json.dumps(dict(targets=targets, pin=pin)), flush=True)
    for line in sys.stdin:
        if line.strip() == "stats":
            with LOCK:
                print(json.dumps(AUDIT), flush=True)
        elif line.strip() == "stop":
            break
        elif line.strip() == "reset-peak":
            with LOCK:
                AUDIT["peak"] = AUDIT["active"]
    STOP.set()
    for listener in LISTENERS:
        listener.close()
    for connection in CONNECTIONS:
        connection.close()
