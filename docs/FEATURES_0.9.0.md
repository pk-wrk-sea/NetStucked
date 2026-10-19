# 0.9.0 — Config Collector

The 2026-10-19 human request approves adapting the supplied collector requirements into the existing app theme. The standing instruction to publish completed work to GitHub applies; the new additive module uses minor version 0.9.0. Published older tags/assets remain immutable. [Architecture, transitions, failure matrix and test plan](CONFIG_COLLECTOR_2026-10-19.md) were recorded before implementation.

Implemented: explicit multiline targets/import, endpoint dedup/port/auth override validation; SSH host-key pinning, password-only AUTH1/AUTH2 with one attempt each and authentication-only fallback; explicitly acknowledged Telnet with IAC/login handling; read-only command validation and profile pagination; enable and prompt state machine; frozen run options; bounded concurrency, timeouts, transport retry before commands, cancellation/drain; streamed protected TXT/JSON schema-1 captures, SHA-256/provenance, compact live progress/preview, per-device results and error CSV. UI uses shared Light/Dark/System resources, resizable splitters/columns, wrapped details and result zoom.

Credentials are entered for each run, never persisted. The separate capture directory does not change schema-1 user preferences or monitoring data. Closing to Tray retains active collection; Exit/update preparation drains it. No scheduled or startup collection. Commands may change terminal pagination/privilege for the session; configuration write commands are blocked. Huawei privilege escalation is not implemented; use a display-authorized account.

Template choices: Cisco IOS/IOS XE, NX-OS, ASA, Huawei VRP and Aruba/ProCurve. These are preparation/read-command profiles, **not hardware certification**. Real device acceptance, unusual prompts/banners, older SSH algorithms, device-specific enable/pagination and live VTY/session limits remain NOT TESTED. No automatic SSH algorithm weakening. Unknown keys fail closed and expose a fingerprint for independent verification/manual pin entry.

Inventory groups/reconciliation, diff, facts/CVE/topology, terminal UI, SQLite, scheduling and key/certificate/MFA authentication are outside this adaptation. Full configurations remain sensitive even after exact entered credentials are redacted; per-user/SYSTEM ACLs protect captures. Retention is manual in the chosen output folder. No captures are uploaded to provider APIs.

Acceptance: [actual QA and remaining gates](QA_0.9.0.md). The older full A02 backlog remains open.
