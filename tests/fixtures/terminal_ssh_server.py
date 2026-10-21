"""Owned loopback interactive SSH fixture; ephemeral keys, never a real device."""
import base64
import hashlib
import json
from pathlib import Path
import socket
import sys
import threading
import paramiko
from console_input import ConsoleInput
from cryptography.hazmat.primitives import serialization

host_key = paramiko.RSAKey.generate(2048)
client_key = paramiko.RSAKey.generate(2048)
key_file = Path(sys.argv[1]).resolve()
key_file.write_bytes(client_key.key.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.OpenSSH, serialization.BestAvailableEncryption(b"owned-key-passphrase")))
listener = socket.socket()
listener.bind(("127.0.0.1", 0))
listener.listen(8)
listener.settimeout(.2)
stop = threading.Event()
lock = threading.Lock()
connections = []
audit = {"auth": [], "pty": [], "resize": [], "input": [], "blank_commands": 0}


class Server(paramiko.ServerInterface):
    def __init__(self):
        self.shell = threading.Event()

    def get_allowed_auths(self, username):
        return "publickey" if username == "key-fixture" else "password"

    def check_auth_password(self, username, password):
        accepted = username == "password-fixture" and password == "owned-terminal-password"
        with lock:
            audit["auth"].append({"method": "password", "accepted": accepted})
        return paramiko.AUTH_SUCCESSFUL if accepted else paramiko.AUTH_FAILED

    def check_auth_publickey(self, username, key):
        accepted = username == "key-fixture" and key == client_key
        with lock:
            audit["auth"].append({"method": "key", "accepted": accepted})
        return paramiko.AUTH_SUCCESSFUL if accepted else paramiko.AUTH_FAILED

    def check_channel_request(self, kind, channel_id):
        return paramiko.OPEN_SUCCEEDED if kind == "session" else paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED

    def check_channel_pty_request(self, channel, term, width, height, *rest):
        with lock:
            audit["pty"].append({"term": term.decode(), "cols": width, "rows": height})
        return True

    def check_channel_window_change_request(self, channel, width, height, *rest):
        with lock:
            audit["resize"].append({"cols": width, "rows": height})
        return True

    def check_channel_shell_request(self, channel):
        self.shell.set()
        return True


def serve(peer):
    transport = paramiko.Transport(peer)
    with lock:
        connections.append(transport)
    try:
        transport.add_server_key(host_key)
        server = Server()
        transport.start_server(server=server)
        channel = transport.accept(60)  # Operator may review the first-use host-key dialog.
        if channel is None or not server.shell.wait(5):
            return
        channel.sendall("\x1b[2J\x1b[H\x1b[31mRED\x1b[0m\r\nUnicode ไทย\r\nfixture#".encode())
        console = ConsoleInput(65536)
        special_commands = (b"ansi-test", b"osc-test", b"cursor-test", b"large-test")
        while not stop.is_set():
            data = channel.recv(4096)
            if not data:
                break
            with lock:
                audit["input"].append(base64.b64encode(data).decode())
            if data == b"\x03":
                console.clear_pending()
                channel.sendall(b"\r\n^C\r\nfixture#")
                continue
            for command in console.feed(data):
                if not command:
                    with lock:
                        audit["blank_commands"] += 1
                elif command == b"ansi-test":
                    channel.sendall(b"\x1b[?1049h\x1b[HAlternate screen\x1b[?1049l\r\nPrimary restored\r\nfixture#")
                elif command == b"osc-test":
                    channel.sendall(b"\x1b]52;c;TkVWRVItQ0xJUEJPQVJE\x07\r\nClipboard blocked\r\n")
                elif command == b"cursor-test":
                    channel.sendall(b"\r\nfirst\rSECOND\x1b[K\r\nfixture#")
                elif command == b"large-test":
                    for i in range(6000):
                        channel.sendall((f"owned bounded line {i:04d}\r\n").encode())
                    channel.sendall(b"large complete\r\nfixture#")
                else:
                    channel.sendall(b"\r\nECHO " + command + b"\r\nfixture#")
            # This emulator fixture echoes generic typing/pastes immediately, while
            # known synthetic test commands may span arbitrary SSH read boundaries.
            if console.pending and not any(command.startswith(console.pending) for command in special_commands):
                channel.sendall(b"\r\nECHO " + bytes(console.pending) + b"\r\nfixture#")
                console.clear_pending()
    except (OSError, EOFError, ValueError, paramiko.SSHException):
        pass
    finally:
        transport.close()


def accept():
    while not stop.is_set():
        try:
            peer, _ = listener.accept()
            threading.Thread(target=serve, args=(peer,), daemon=True).start()
        except socket.timeout:
            pass
        except OSError:
            break


threading.Thread(target=accept, daemon=True).start()
pin = "SHA256:" + base64.b64encode(hashlib.sha256(host_key.asbytes()).digest()).decode().rstrip("=")
print(json.dumps({"port": listener.getsockname()[1], "pin": pin, "key": str(key_file)}), flush=True)
try:
    for line in sys.stdin:
        if line.strip() == "stats":
            with lock:
                print(json.dumps(audit), flush=True)
        if line.strip() == "stop":
            break
finally:
    stop.set()
    listener.close()
    for transport in connections:
        transport.close()
    key_file.unlink(missing_ok=True)
