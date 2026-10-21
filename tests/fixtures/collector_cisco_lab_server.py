"""Owned loopback SSH software fixture for real Collector -> offline analysis QA.

These are deliberately synthetic Cisco-format transcripts, not observations of Cisco
hardware. The ephemeral RSA key, credentials and documentation addresses are QA only.
No connections leave this machine. Requires Paramiko.
"""
import base64
import hashlib
import json
import os
import socket
import sys
import threading
import time
import paramiko
from console_input import ConsoleInput

key = paramiko.RSAKey.generate(2048)
stopping = threading.Event()
lock = threading.Lock()
sessions = []
listeners = []
audit = {"fixture": "owned software fixture; no real Cisco hardware", "auth": [], "commands": [], "blank_commands": 0, "negotiated": [], "bare_lf_bytes": 0, "cr_bytes": 0}


def output(command, name, peer, local_port, remote_port, address, serial):
    banner = ""
    if os.environ.get("NETSTUCKED_QA_BANNER") == "1":
        banner = "banner motd ^\nMFA / OTP is required for access.\nOTP verification code:\nAccess denied\nPress enter to continue [confirm]\n^\n"
    transcripts = {
        "terminal length 0": "",
        "show running-config": f"""! Owned software fixture; no real Cisco hardware
version 17.9
hostname {name}
{banner}\
!
interface {local_port}
 description Owned fixture link to {peer}
 switchport mode trunk
!
interface Vlan30
 ip address {address} 255.255.255.0
!
ip route 0.0.0.0 0.0.0.0 192.0.2.254
end
""",
        "show version": f"""Owned software fixture; no real Cisco hardware
Cisco IOS XE Software, Version 17.09.04a
{name} uptime is 2 days, 3 hours, 4 minutes
cisco C9300-24T (X86) processor with 1315636K/6147K bytes of memory.
Processor board ID {serial}
Model Number                       : C9300-24T
System serial number               : {serial}
""",
        "show inventory": f"""NAME: "Chassis", DESCR: "Owned software fixture C9300-format chassis"
PID: C9300-24T        , VID: V01, SN: {serial}
""",
        "show interfaces": f"""{local_port} is up, line protocol is up (connected)
  Description: Owned fixture link to {peer}
  MTU 1500 bytes, BW 1000000 Kbit/sec
  5 minute input rate 8000 bits/sec, 10 packets/sec
  5 minute output rate 16000 bits/sec, 20 packets/sec
""",
        "show ip route": """Gateway of last resort is 192.0.2.254 to network 0.0.0.0
C        192.0.2.0/24 is directly connected, Vlan30
S*       0.0.0.0/0 [1/0] via 192.0.2.254
""",
        "show cdp neighbors detail": f"""-------------------------
Device ID: {peer}
Entry address(es):
  IP address: {"192.0.2.2" if name.endswith("A") else "192.0.2.1"}
Platform: cisco C9300-24T, Capabilities: Switch IGMP
Interface: {local_port}, Port ID (outgoing port): {remote_port}
Holdtime : 120 sec
""",
        "show lldp neighbors detail": f"""Local Intf: {local_port}
Chassis id: 0200.0000.0002
Port id: {remote_port}
Port Description: Owned fixture link
System Name: {peer}
Management Address: {"192.0.2.2" if name.endswith("A") else "192.0.2.1"}
""",
    }
    return transcripts.get(command, "% Invalid input detected at '^' marker.\n")


class Server(paramiko.ServerInterface):
    def __init__(self, name):
        self.name = name
        self.shell = threading.Event()

    def get_allowed_auths(self, username):
        return "password"

    def check_auth_password(self, username, password):
        accepted = username == "DOMAIN\\fixture" and password == "owned-lab-password"
        with lock:
            audit["auth"].append({"device": self.name, "user": username, "accepted": accepted})
        return paramiko.AUTH_SUCCESSFUL if accepted else paramiko.AUTH_FAILED

    def check_channel_request(self, kind, channel_id):
        return paramiko.OPEN_SUCCEEDED if kind == "session" else paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED

    def check_channel_pty_request(self, *args):
        return True

    def check_channel_shell_request(self, channel):
        self.shell.set()
        return True


def serve(peer_socket, name, peer_name, local_port, remote_port, address, serial):
    transport = paramiko.Transport(peer_socket)
    if os.environ.get("NETSTUCKED_QA_SSH_CTR") == "1":
        transport.get_security_options().ciphers = ("aes256-ctr",)
        transport.get_security_options().digests = ("hmac-sha2-512-etm@openssh.com",)
    with lock:
        sessions.append(transport)
    try:
        transport.add_server_key(key)
        server = Server(name)
        transport.start_server(server=server)
        with lock:
            audit["negotiated"].append({"device": name, "cipher": transport.remote_cipher, "mac": transport.remote_mac})
        channel = transport.accept(10)
        if channel is None or not server.shell.wait(10):
            return
        prompt = (name + "#").encode("utf-8")
        channel.sendall(prompt)
        console = ConsoleInput()
        while not stopping.is_set():
            chunk = channel.recv(4096)
            if not chunk:
                break
            with lock:
                audit["bare_lf_bytes"] += chunk.count(b"\n")
                audit["cr_bytes"] += chunk.count(b"\r")
            if os.environ.get("NETSTUCKED_QA_SSH_CTR") == "1" and b"\n" in chunk:
                # This mode deliberately requires console CR, exposing the former LF bug.
                return
            for line in console.feed(chunk):
                if not line:
                    with lock:
                        audit["blank_commands"] += 1
                    continue
                command = line.decode("utf-8")
                with lock:
                    audit["commands"].append({"device": name, "command": command})
                data = (command + "\r\n" + output(command, name, peer_name, local_port, remote_port, address, serial).replace("\n", "\r\n")).encode("utf-8")
                # Fragment ordinary output as a real TCP/SSH stream can do.
                for offset in range(0, len(data), 43):
                    channel.sendall(data[offset:offset + 43])
                    time.sleep(0.002)
                channel.sendall(prompt)
    except (EOFError, OSError, ValueError, paramiko.SSHException):
        pass
    finally:
        transport.close()


def accept_loop(listener, device):
    while not stopping.is_set():
        try:
            peer, _ = listener.accept()
            threading.Thread(target=serve, args=(peer, *device), daemon=True).start()
        except socket.timeout:
            pass
        except OSError:
            break


devices = [
    ("LAB-SW-A", "LAB-SW-B", "GigabitEthernet1/0/1", "GigabitEthernet1/0/2", "192.0.2.1", "OWNED-LAB-A"),
    ("LAB-SW-B", "LAB-SW-A", "GigabitEthernet1/0/2", "GigabitEthernet1/0/1", "192.0.2.2", "OWNED-LAB-B"),
]
targets = []
for device in devices:
    listener = socket.socket()
    listener.bind(("127.0.0.1", 0))
    listener.listen(8)
    listener.settimeout(0.3)
    listeners.append(listener)
    targets.append({"name": device[0], "host": "127.0.0.1", "port": listener.getsockname()[1]})
    threading.Thread(target=accept_loop, args=(listener, device), daemon=True).start()
fingerprint = "SHA256:" + base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode().rstrip("=")
print(json.dumps({"targets": targets, "pin": fingerprint}), flush=True)
for line in sys.stdin:
    if line.strip() == "stats":
        with lock:
            print(json.dumps(audit), flush=True)
    elif line.strip() == "stop":
        break
stopping.set()
for listener in listeners:
    listener.close()
for session in sessions:
    session.close()
