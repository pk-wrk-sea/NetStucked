"""Owned loopback-only SSH fixture. Requires Paramiko; no production credentials/configs.

Invoked only by the explicit WindowsQa --collector-ssh-check mode. Never discovers
or connects to any network target. Generated ephemeral host key is not persisted.
"""
import base64
import hashlib
import json
import socket
import sys
import threading
import time
import paramiko

key = paramiko.RSAKey.generate(2048)
listener = socket.socket()
listener.bind(("127.0.0.1", 0))
listener.listen(8)
listener.settimeout(0.3)
stopping = threading.Event()
sessions = []
lock = threading.Lock()
audit = {"auth": [], "commands": [], "domain_literal": True, "no_bom": True}


class Server(paramiko.ServerInterface):
    def __init__(self):
        self.shell = threading.Event()

    def get_allowed_auths(self, username):
        return "keyboard-interactive" if username.startswith("mfa-") else "password"

    def check_auth_password(self, username, password):
        accepted = username in ("DOMAIN\\fixture", "mfa-after-password") and password == "owned-fixture-password"
        with lock:
            audit["auth"].append({"user": username, "accepted": accepted})
            if not username.startswith("mfa-"):
                audit["domain_literal"] &= username == "DOMAIN\\fixture"
        if username == "mfa-after-password" and accepted:
            return paramiko.AUTH_PARTIALLY_SUCCESSFUL
        return paramiko.AUTH_SUCCESSFUL if accepted else paramiko.AUTH_FAILED

    def check_channel_request(self, kind, channel_id):
        return paramiko.OPEN_SUCCEEDED if kind == "session" else paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED

    def check_channel_pty_request(self, *args):
        return True

    def check_channel_shell_request(self, channel):
        self.shell.set()
        return True


def serve(peer):
    transport = paramiko.Transport(peer)
    with lock:
        sessions.append(transport)
    try:
        transport.add_server_key(key)
        server = Server()
        transport.start_server(server=server)
        channel = transport.accept(8)
        if channel is None or not server.shell.wait(8):
            return
        for part in (b"fixt", b"ure", b"#"):
            channel.sendall(part)
            time.sleep(0.015)
        pending = b""
        while not stopping.is_set():
            chunk = channel.recv(4096)
            if not chunk:
                break
            pending += chunk
            if len(pending) > 2048:
                break
            while b"\n" in pending:
                line, pending = pending.split(b"\n", 1)
                with lock:
                    audit["no_bom"] &= not line.startswith(b"\xef\xbb\xbf")
                command = line.rstrip(b"\r").decode("utf-8")
                with lock:
                    audit["commands"].append(command)
                if command == "show wait":
                    channel.recv(1)  # Owned cancellation fixture: waits for the client to close.
                    return
                channel.sendall((command + "\r\n").encode("utf-8"))
                if command == "show running-config":
                    for i in range(8192):
                        channel.sendall(("fixture line # value > ไทย " + "x" * 1000 + "\r\n").encode("utf-8"))
                    channel.sendall(b"echo owned-fixture-password\r\n")
                else:
                    channel.sendall(b"owned fixture output\r\n")
                    channel.send_stderr(b"owned stderr fixture\r\n")
                channel.sendall(b"fixture#")
    except (EOFError, OSError, paramiko.SSHException):
        pass  # Expected for unknown pins, cancelled clients and rejected logins.
    finally:
        transport.close()


def accept_loop():
    while not stopping.is_set():
        try:
            peer, _ = listener.accept()
            threading.Thread(target=serve, args=(peer,), daemon=True).start()
        except socket.timeout:
            pass
        except OSError:
            break


threading.Thread(target=accept_loop, daemon=True).start()
fingerprint = "SHA256:" + base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode().rstrip("=")
print(json.dumps({"port": listener.getsockname()[1], "pin": fingerprint}), flush=True)
for line in sys.stdin:
    if line.strip() == "stats":
        with lock:
            print(json.dumps(audit), flush=True)
    elif line.strip() == "stop":
        break
stopping.set()
listener.close()
for session in sessions:
    session.close()
