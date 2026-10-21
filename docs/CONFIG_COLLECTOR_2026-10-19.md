# Config Collector — approved adaptation, 2026-10-19

**2026-10-21 correction:** The infrastructure-provider paragraph below records the original 0.9.0 design. Collector-only SSH is being replaced by the strong AES-CTR/SHA-2 compatible adapter with authenticated, owned loopback socket cancellation and bounded streams in [0.11.2](FEATURES_0.11.2.md). Terminal retains Tmds. The original host-pin, memory-only credential, rejection-only fallback, capture and no-command-replay policy remains; [incident/verification](QA_0.11.2.md).

Authority: the human supplied the collector requirements and requested implementation in the existing NetStucked theme. This supersedes A02's missing-v2-mockup gate for this slice only. The standing GitHub update publication instruction carries forward; the additive module uses 0.9.0. No scheduling, inventory reconciliation, topology, CVE, terminal emulator or device configuration writes.

## Architecture recorded before implementation

Core: input parser → validated immutable run snapshot → bounded run controller → authentication policy → prompt-aware session runner. Interfaces isolate transport and capture writer. Secrets are memory-only objects with redacted string representations, excluded from serialized metadata. Every worker receives the same frozen options/credentials; never reads UI controls.

Infrastructure: pinned Tmds.Ssh 0.26.0 with SHA-256 host-key pins (unknown/mismatched keys fail closed), disabled ambient credentials/known-host files/reconnect, a 256 KiB SSH receive window and independent stdout/stderr decoding; separate Telnet IAC/login state machine enabled only by explicit selection and plaintext acknowledgement. No child process/argv credentials. Capture writer streams UTF-8 without BOM to unique temporary files in a per-run Windows directory restricted to current user and SYSTEM. Exact entered credential values are redacted across chunk boundaries. Full configurations remain sensitive: access protection is not full configuration sanitization. JSON schema 1 contains provenance and streamed output chunks; TXT contains redacted terminal output. SHA-256 identifies the redacted capture, not the original wire bytes.

Desktop: cached MVVM page in existing Light/Dark/System resources. Resizable target/options panel; Targets, Commands and AUTH tabs; dense results with wrapped/resizable columns; selected bounded preview and Ctrl+wheel zoom. Start validates/freezes; Stop cancels and drains before Start is enabled. Folder selection and password entry are view concerns. No credentials or captures committed to the repository; no automatic collection on startup.

## State transitions

Batch: Idle → Validating → Running → Completed / Partial / Failed. Running → Cancelling → Partial (some completed/partial evidence) or Failed (none completed). Validation errors return Idle without networking. Device: Queued → Connecting → Authenticating → Preparing → Collecting → Saving → Completed. Errors after collection begins preserve Partial capture; earlier errors are Failed; cancellation is Cancelled with any captured evidence retained. Every result includes run ID; presenter rejects stale callbacks. Ping preflight is omitted: failed ICMP never gates collection.

## Failure and fallback matrix

| Condition | AUTH2 fallback | Retry / action |
|---|---|---|
| Explicit password authentication rejection | Once, only without auth=1/2 override | One attempt per profile; new connection |
| Unknown or changed host key | Never | Stop; show observed SHA-256; engineer verifies independently and enters pin |
| MFA/OTP or unsupported authentication | Never | Stop with unsupported-authentication reason |
| Connect timeout/reset/session limit | Never | No timeout/session-limit retry; socket failures before authentication may reconnect once with the same profile |
| Command timeout/error/confirmation/paging | Never | Preserve partial capture; no command replay or automatic confirmation |
| Enable rejected | Never | Stop; AUTH fallback does not retry enable credentials |
| Disconnect after command sent | Never | Preserve partial; no replay |
| Cancel/disk/permission/output limit | Never | Drain/dispose; preserve any finalized evidence, report real outcome |

Defaults: SSH/22, concurrency 2 (1–8), 15s connect, 30s command (5–300s), 256 targets, 32 commands, 64 MiB/device, bounded 8 KiB preview. Explicit Telnet defaults to port 23. Input accepts host[:port] or [IPv6]:port, optional description and auth=1/2; blank and # comment lines ignored; first duplicate endpoint wins. No CIDR expansion/discovery. Approved basic commands only; collection commands restricted to supported read-only show/display templates without command separators/interactive modifiers.

## Test plan

Automated: parser bounds/dedup/IPv6/profile override; immutable snapshot; fallback matrix and exact DOMAIN\username; split/no-newline prompts and false #/>; enable/MFA/confirmation/command errors; bounded connection retries/no replay; large streamed captures/redaction across chunks; cancellation during queue/login/command/save; failures and unique filenames; batch run ownership. Real owned loopback Telnet verifies IAC and UTF-8 bytes; owned SSH server verifies actual transport host-pin/authentication/stream/cleanup. WPF checks actual navigation, Light/Dark layout, resizing, result zoom, password clearing, frozen options and shutdown. Working-network/Cisco/multivendor/manual accessibility tests stay NOT TESTED unless actually performed.

Implementation and QA evidence will be appended after checks; the full older A02 backlog is not marked complete by this adaptation.
