"""One owned device/session. JSON stdin/stdout only; never log or persist credentials.

Netmiko drivers handle prompts and session preparation. Restrict their writes to
the reviewed read-only/session command set: notably Huawei's upstream first-login
configuration-security confirmations MUST NOT run automatically.
"""
import base64
import hashlib
import json
import logging
import re
import sys
import time

import paramiko
import paramiko.transport
from paramiko.auth_handler import AuthHandler
from paramiko.message import Message
from netmiko import NetmikoAuthenticationException, NetmikoTimeoutException, ReadTimeout
from netmiko.ssh_dispatcher import ssh_dispatcher

logging.disable(logging.CRITICAL)
PASSWORD_REJECTED = False
sys.stdin.reconfigure(encoding="utf-8")
sys.stdout.reconfigure(encoding="utf-8", write_through=True)


def emit(kind, **data):
    sys.stdout.write(json.dumps(dict(kind=kind, **data), ensure_ascii=True) + "\n")


class Failure(Exception):
    def __init__(self, code):
        self.code = code


class PasswordOnlyAuth(AuthHandler):
    def _parse_userauth_failure(self, message):
        global PASSWORD_REJECTED
        copy = Message(message.get_remainder())
        allowed, partial = copy.get_list(), copy.get_boolean()
        # Only an actual SSH_MSG_USERAUTH_FAILURE allowing password, without
        # partial success (MFA), authorizes AUTH2. Timeouts never set this flag.
        self.transport.password_rejected = (
            self.auth_method == "password" and "password" in allowed and not partial
        )
        PASSWORD_REJECTED = self.transport.password_rejected
        return super()._parse_userauth_failure(message)


paramiko.transport.AuthHandler = PasswordOnlyAuth


class StrongTransport(paramiko.Transport):
    password_rejected = False

    def auth_password(self, username, password, event=None, fallback=False):
        # Paramiko otherwise automatically tries keyboard-interactive after password.
        return super().auth_password(username, password, event, fallback=False)


class VerifiedClient(paramiko.SSHClient):
    def connect(self, *args, **kwargs):
        kwargs["transport_factory"] = StrongTransport
        return super().connect(*args, **kwargs)


def main(request):
    if request.get("schema") != 1 or request["protocol"] not in ("Ssh", "Telnet"):
        raise Failure("Transport")
    counters = dict(total=0, active=False, tail="", authenticated=False, telnet_username=False, telnet_password=False)
    client = None
    connection = None
    allowed = set(request["basic"] + request["commands"])
    allowed.update(("terminal width 511", "terminal width 512", "terminal length 0",
                    "terminal pager 0", "screen-length 0 temporary", "no page"))
    if request["enable"]:
        allowed.update(("enable", request["enable"]))

    class TrustPolicy(paramiko.MissingHostKeyPolicy):
        def missing_host_key(self, ssh_client, hostname, key):
            fingerprint = "SHA256:" + base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode().rstrip("=")
            emit("hostkey", algorithm=key.get_name(), fingerprint=fingerprint)
            reply = sys.stdin.readline(4096)
            if not reply.endswith("\n") or json.loads(reply).get("accept") is not True:
                raise Failure("HostKey")
            emit("state", state="Authenticating")
            # Trust belongs to this live transport, not a reconnect which could change keys.

    driver = ssh_dispatcher(request["driver"] + ("_telnet" if request["protocol"] == "Telnet" else ""))

    class BoundedDriver(driver):
        def write_channel(self, data):
            command = data.strip("\r\n")
            telnet_login = request["protocol"] == "Telnet" and not counters["authenticated"] and command in (request["username"], request["password"])
            if telnet_login:
                active_line = counters["tail"].replace("\r", "\n").split("\n")[-1].strip()
                phase = "telnet_username" if re.fullmatch(r"(?i)(username|user name|user|login)\s*:", active_line) and command == request["username"] else "telnet_password" if re.fullmatch(r"(?i)password\s*:", active_line) and command == request["password"] else None
                if phase is None or counters[phase]:
                    raise Failure("UnsupportedAuthentication")
                counters[phase] = True
                # Some Telnet servers do not echo a newline between login prompts.
                # The next prompt must be recognized independently of the previous one.
                counters["tail"] = ""
            if command and command not in allowed and not telnet_login:
                raise Failure("Confirmation")
            return super().write_channel(data)

        def read_channel(self):
            text = super().read_channel()
            counters["total"] += len(text.encode("utf-8"))
            if counters["total"] > request["maxBytes"]:
                raise Failure("OutputLimit")
            counters["tail"] = (counters["tail"] + text)[-8192:]
            if request["protocol"] == "Telnet" and not counters["authenticated"]:
                if re.search(r"(?im)^(?:login incorrect|authentication failed|access denied|permission denied)[.!:]?\s*$", counters["tail"]):
                    raise Failure("AuthenticationRejected")
            if counters["active"]:
                for start in range(0, len(text), 4096):
                    emit("output", text=text[start:start + 4096])
            return text

        def special_login_handler(self, *args, **kwargs):
            if request["driver"] == "huawei":
                # Unlike the upstream Huawei handler, never confirm configuration
                # security changes/password rotation. Only wait for a user prompt.
                self._test_channel_read(pattern=r"[<>]")
            else:
                super().special_login_handler(*args, **kwargs)

        def _build_ssh_client(self):
            nonlocal client
            client = VerifiedClient()
            client.set_missing_host_key_policy(TrustPolicy())
            return client

    # Explicitly exclude legacy algorithms instead of opportunistically weakening.
    disabled = {
        "ciphers": [v for v in paramiko.Transport._preferred_ciphers if v not in
                    ("aes128-ctr", "aes192-ctr", "aes256-ctr", "aes128-gcm@openssh.com", "aes256-gcm@openssh.com")],
        "macs": [v for v in paramiko.Transport._preferred_macs if not v.startswith("hmac-sha2-")],
        "kex": [v for v in paramiko.Transport._preferred_kex if "sha1" in v],
        "keys": [v for v in paramiko.Transport._preferred_keys if v in ("ssh-rsa", "ssh-dss")],
        "pubkeys": ["ssh-rsa", "ssh-dss"],
    }
    try:
        emit("state", state="Connecting")
        connection = BoundedDriver(
            device_type=request["driver"] + ("_telnet" if request["protocol"] == "Telnet" else ""), host=request["host"], port=request["port"],
            username=request["username"], password=request["password"], secret=request["enable"],
            auto_connect=False, allow_agent=False, use_keys=False, ssh_config_file=None,
            system_host_keys=False, alt_host_keys=False, ssh_strict=True,
            disabled_algorithms=disabled, conn_timeout=request["connectTimeout"],
            auth_timeout=request["connectTimeout"], banner_timeout=request["connectTimeout"],
            blocking_timeout=request["commandTimeout"], session_timeout=request["commandTimeout"],
            read_timeout_override=request["commandTimeout"], fast_cli=False,
            session_log=None, default_enter="\r" if request["protocol"] == "Ssh" else "\r\n",
        )
        connection.establish_connection()
        counters["authenticated"] = True
        emit("state", state="Preparing")
        # Avoid upstream cleanup commands on a preparation error; close owned
        # sockets in finally instead. Drivers still own session preparation.
        connection.write_channel(connection.RETURN)
        time.sleep(0.1)
        connection.session_preparation()
        if request["enable"] and request["driver"].startswith("cisco") and request["driver"] != "cisco_xr":
            try:
                connection.enable()
            except ValueError:
                raise Failure("EnableRejected") from None
        prompt = connection.find_prompt()
        hostname = prompt.strip().strip("<>[]#>").split(":")[-1]
        hostname = re.sub(r"\([^)]*\)$", "", hostname)
        if not re.fullmatch(r"[A-Za-z0-9_.-]{1,100}", hostname):
            hostname = ""
        peer = connection.remote_conn.getpeername()[0] if request["protocol"] == "Ssh" else connection.remote_conn.sock.getpeername()[0]
        emit("identity", prompt=prompt[:256], hostname=hostname, address=peer)
        for command in request["basic"] + request["commands"]:
            counters["active"], counters["tail"] = True, ""
            emit("command", command=command)
            output = connection.send_command(command, read_timeout=request["commandTimeout"], strip_prompt=False, strip_command=False, cmd_verify=True)
            counters["active"] = False
            if re.search(r"(?im)^(?:%\s*(?:invalid|error|unknown|incomplete|ambiguous|authorization)|error:|unrecognized command|invalid input|permission denied)", output):
                raise Failure("Command")
            emit("command_done")
        emit("done")
    except NetmikoAuthenticationException:
        raise Failure("AuthenticationRejected" if not counters["authenticated"] and PASSWORD_REJECTED else "UnsupportedAuthentication") from None
    except (NetmikoTimeoutException, ReadTimeout, TimeoutError):
        raise Failure("Timeout") from None
    finally:
        # Do not invoke driver cleanup commands (exit/configuration transitions).
        if client is not None:
            client.close()
        if connection is not None and connection.remote_conn is not None:
            connection.remote_conn.close()


if __name__ == "__main__":
    try:
        line = sys.stdin.readline(131073)
        if len(line) > 131072 or not line.endswith("\n"):
            raise Failure("Transport")
        main(json.loads(line))
    except Failure as error:
        emit("error", code=error.code)
    except Exception:
        # Do not print exception text, traceback, device buffers or credentials.
        emit("error", code="Transport")
