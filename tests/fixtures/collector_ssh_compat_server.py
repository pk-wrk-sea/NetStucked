"""Owned loopback SSH compatibility/failure fixture. No real targets or secrets.

Each process owns an ephemeral key and bounded peer sockets. Password values and
raw authentication packets are never included in its audit or persisted.
"""
import base64
import hashlib
import json
import socket
import struct
import sys
import threading
import time
import paramiko
from paramiko.message import Message
from console_input import ConsoleInput

mode = sys.argv[1]
allowed_modes = {"ctr", "gcm", "deny-channel", "deny-pty", "deny-shell", "early-close", "rekey", "slow-auth-shell", "stall-kex", "stall-none", "stall-password", "stall-channel", "stall-pty", "stall-shell", "flood", "password-change", "initial-flood", "none-success"}
if mode not in allowed_modes:
    raise ValueError("Unsupported owned fixture mode")
key = paramiko.RSAKey.generate(2048)
stopping = threading.Event()
lock = threading.Lock()
peers = []
sessions = []
workers = []
audit = {"mode": mode, "none": [], "password": [], "channels": [], "pty": [], "shell": [], "commands": [], "closed": 0, "kex": 0, "rekey": 0, "cipher": "", "mac": "", "blank_commands": 0, "flood_bytes": 0, "password_change": 0, "initial_flood_bytes": 0}


def wait_for_close(peer):
    # Used only after a deliberately stalled stage: no further SSH parsing will
    # resume. Drain opaque bytes until EOF so pending encrypted disconnect data
    # cannot hide the caller's socket closure from the blocked callback.
    peer.settimeout(0.1)
    while not stopping.is_set():
        try:
            if not peer.recv(4096):
                return
        except socket.timeout:
            continue
        except OSError:
            return


class OwnedTransport(paramiko.Transport):
    def _send_user_message(self, data):
        # Observe the actual shell-success reply after it has been sent. The
        # callback precedes that reply, so waking there races normal flood/EOF
        # modes into the deliberately separate pre-shell-success test.
        payload = data.asbytes()
        super()._send_user_message(data)
        server = getattr(self, "fixture_server", None)
        if server is not None and payload and payload[0] == 99 and server.shell_reply_pending:
            server.shell_reply_pending = False
            server.shell_reply_sent.set()


class Server(paramiko.ServerInterface):
    def __init__(self, peer, transport):
        self.peer = peer
        self.transport = transport
        self.shell_reply_pending = False
        self.shell_reply_sent = threading.Event()
        self.partial = False

    def get_allowed_auths(self, username):
        return "keyboard-interactive" if username == "mfa-only" or self.partial else "password"

    def check_auth_none(self, username):
        with lock:
            audit["none"].append(username)
        if mode == "stall-none":
            wait_for_close(self.peer)
        return paramiko.AUTH_SUCCESSFUL if mode == "none-success" else paramiko.AUTH_FAILED

    def check_auth_password(self, username, password):
        accepted = username in ("fixture", "partial-mfa") and password == "owned-compat-password"
        with lock:
            audit["password"].append({"username": username, "accepted": accepted})
        if mode == "password-change" and accepted:
            # RFC4252 SSH_MSG_USERAUTH_PASSWD_CHANGEREQ (60): prompt + language.
            # Block until EOF afterward so an ordinary AUTH_FAILURE cannot mask it.
            response = Message()
            response.add_byte(bytes([60]))
            response.add_string("Owned fixture password change required")
            response.add_string("")
            with lock:
                audit["password_change"] += 1
            self.transport._send_message(response)
            wait_for_close(self.peer)
            return paramiko.AUTH_FAILED
        if mode == "stall-password":
            wait_for_close(self.peer)
            return paramiko.AUTH_FAILED
        if mode == "slow-auth-shell" and stopping.wait(3):
            return paramiko.AUTH_FAILED
        if username == "partial-mfa" and accepted:
            self.partial = True
            return paramiko.AUTH_PARTIALLY_SUCCESSFUL
        return paramiko.AUTH_SUCCESSFUL if accepted else paramiko.AUTH_FAILED

    def check_channel_request(self, kind, channel_id):
        with lock:
            audit["channels"].append(kind)
        if mode == "stall-channel":
            wait_for_close(self.peer)
            return paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED
        return paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED if mode == "deny-channel" or kind != "session" else paramiko.OPEN_SUCCEEDED

    def check_channel_pty_request(self, channel, term, width, height, *rest):
        with lock:
            audit["pty"].append({"term": term.decode("utf-8", errors="replace"), "width": width, "height": height})
        if mode == "stall-pty":
            wait_for_close(self.peer)
            return False
        return mode != "deny-pty"

    def check_channel_shell_request(self, channel):
        accepted = mode not in ("deny-shell", "stall-shell")
        with lock:
            audit["shell"].append(accepted)
        if mode == "stall-shell":
            wait_for_close(self.peer)
            return False
        if mode == "slow-auth-shell" and stopping.wait(3):
            return False
        if mode == "initial-flood":
            # Deliberately send channel data before this callback returns the shell
            # success reply. A client guard must already exist during construction.
            channel.settimeout(0.1)
            sent = 0
            block = b"p" * 4096
            while sent < 256 * 1024 and not stopping.is_set():
                try:
                    count = channel.send(block)
                except socket.timeout:
                    # Once intentionally stalled, discard opaque close/window data
                    # only to observe EOF; no successful shell will resume afterward.
                    wait_for_close(self.peer)
                    return False
                if count <= 0:
                    return False
                sent += count
                with lock:
                    audit["initial_flood_bytes"] = sent
        if accepted:
            self.shell_reply_pending = True
        return accepted


def read_exact(peer, size):
    data = bytearray()
    while len(data) < size and not stopping.is_set():
        try:
            chunk = peer.recv(size - len(data))
        except socket.timeout:
            continue
        if not chunk:
            raise EOFError()
        data.extend(chunk)
    if len(data) != size:
        raise EOFError()
    return bytes(data)


def stalled_kex(peer):
    peer.sendall(b"SSH-2.0-owned-compat-stalled-kex\r\n")
    identification = bytearray()
    while not identification.endswith(b"\n"):
        identification.extend(read_exact(peer, 1))
        if len(identification) > 255:
            raise ValueError("Oversized owned SSH identification")
    if not identification.startswith(b"SSH-2.0-"):
        return
    for _ in range(4):
        size = struct.unpack(">I", read_exact(peer, 4))[0]
        if size < 2 or size > 128 * 1024:
            raise ValueError("Oversized owned KEX packet")
        packet = read_exact(peer, size)
        if packet[1] == 20:
            with lock:
                audit["kex"] += 1
            wait_for_close(peer)
            return


def fragment(channel, data, error=False):
    send = channel.send_stderr if error else channel.send
    pending = memoryview(data)
    while pending and not stopping.is_set():
        try:
            count = send(pending[:7].tobytes())
        except socket.timeout:
            continue
        if count <= 0:
            raise EOFError()
        pending = pending[count:]
        time.sleep(0.001)


def command_output(channel, command):
    fragment(channel, (command + "\r\n").encode())
    if command == "show running-config":
        fragment(channel, b"! Owned compatibility fixture; no real device\r\nhostname fixture\r\nbanner motd ^\r\nMFA / OTP is required for access.\r\nAccess denied to unauthorized users.\r\n^\r\n")
        thai = "ไทย".encode("utf-8")
        stderr_word = "ก".encode("utf-8")
        channel.sendall(b"description " + thai[:2])
        channel.send_stderr(b"\r\nowned stderr " + stderr_word[:1])
        channel.sendall(thai[2:] + b"\r\n")
        channel.send_stderr(stderr_word[1:] + b"\r\n")
    elif command == "show version":
        fragment(channel, b"Cisco IOS XE Software, Version 17.12.4\r\nfixture uptime is 1 day\r\n")
    elif command != "terminal length 0":
        fragment(channel, b"% Invalid input detected at '^' marker.\r\n")
    fragment(channel, b"fixture#")


def flood(channel, transport):
    block = b"x" * 4096
    sent = 0
    while sent < 32 * 1024 * 1024 and not stopping.is_set() and transport.is_active():
        try:
            count = channel.send(block)
        except socket.timeout:
            continue
        if count <= 0:
            return
        sent += count
        with lock:
            audit["flood_bytes"] = sent


def serve(peer):
    transport = None
    try:
        peer.settimeout(0.2)
        if mode == "stall-kex":
            stalled_kex(peer)
            return
        transport = OwnedTransport(peer)
        with lock:
            sessions.append(transport)
        options = transport.get_security_options()
        options.ciphers = ("aes128-gcm@openssh.com",) if mode == "gcm" else ("aes256-ctr",)
        options.digests = ("hmac-sha2-512-etm@openssh.com",)
        transport.add_server_key(key)
        server = Server(peer, transport)
        transport.fixture_server = server
        transport.start_server(server=server)
        with lock:
            audit["kex"] += 1
            audit["cipher"] = transport.local_cipher or ""
            audit["mac"] = transport.local_mac or ""
        channel = transport.accept(10)
        if channel is None:
            return
        while not server.shell_reply_sent.wait(0.05):
            if stopping.is_set() or not transport.is_active():
                return
        channel.settimeout(0.2)
        if mode == "early-close":
            return
        if mode == "initial-flood":
            wait_for_close(peer)
            return
        if mode == "flood":
            flood(channel, transport)
            wait_for_close(peer)
            return
        channel.sendall(b"fixture#")
        console = ConsoleInput()
        while not stopping.is_set() and transport.is_active():
            try:
                chunk = channel.recv(4096)
            except socket.timeout:
                continue
            if not chunk:
                break
            for line in console.feed(chunk):
                if not line:
                    with lock:
                        audit["blank_commands"] += 1
                    continue
                command = line.decode("utf-8")
                with lock:
                    audit["commands"].append(command)
                if mode == "rekey" and command == "show version":
                    transport.add_server_key(paramiko.RSAKey.generate(2048))
                    with lock:
                        audit["rekey"] += 1
                    transport.renegotiate_keys()
                command_output(channel, command)
    except (EOFError, OSError, ValueError, paramiko.SSHException):
        pass
    finally:
        if transport is not None:
            transport.close()
        peer.close()
        with lock:
            audit["closed"] += 1


listener = socket.socket()
listener.bind(("127.0.0.1", 0))
listener.listen(8)
listener.settimeout(0.2)


def accept_loop():
    while not stopping.is_set():
        try:
            peer, _ = listener.accept()
            with lock:
                peers.append(peer)
            worker = threading.Thread(target=serve, args=(peer,), daemon=True)
            workers.append(worker)
            worker.start()
        except socket.timeout:
            continue
        except OSError:
            break


threading.Thread(target=accept_loop, daemon=True).start()
pin = "SHA256:" + base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode().rstrip("=")
print(json.dumps({"port": listener.getsockname()[1], "pin": pin}), flush=True)
try:
    for line in sys.stdin:
        if line.strip() == "stats":
            with lock:
                print(json.dumps(audit), flush=True)
        elif line.strip() == "stop":
            break
finally:
    stopping.set()
    listener.close()
    for peer in peers:
        try:
            peer.shutdown(socket.SHUT_RDWR)
        except OSError:
            pass
        peer.close()
    for transport in sessions:
        transport.close()
    for worker in workers:
        worker.join(timeout=1)
