# NetStucked 0.4.1 — defect verification

Run date: 2026-10-09, Asia/Bangkok. Scope follows [the human's defect report and TCP payload clarification](FEATURES_0.4.1.md). Earlier packages and canonical scope/PNGs remain preserved. No machine installation or replacement of user settings occurs in QA.

| Check | Result | Evidence / limit |
|---|---|---|
| Release build | PASS | Five supported projects; .NET 10; zero warnings/errors |
| Automated tests | PASS | 196 passed, zero failed/skipped; includes actual TCP payload receipt on IPv4/IPv6 and UDP payload receipt/reply at 0, 32 and 1400 bytes |
| Port ranges | PASS | Inclusive endpoints, mixed numbers/ranges, overlapping deduplication, malformed/reversed/out-of-bounds/oversized rejection and host × port expansion |
| Payload propagation | PASS | Scheduler passes the selected size to TCP and UDP; settings size persists and older schema-1 files receive the 32-byte default |
| Window caption | PASS | Redundant product/page labels removed; existing window controls/drag behavior retained |
| Dark text inputs | PASS | Actual WPF carets use the theme text brush on all three pages, editable dropdowns, modal settings and the generated Description cell editor; existing inputs follow theme switches |
| Save / Probe Settings | PASS | Actual narrow-window bounds retain 36px Save width and 8px gap for Ping and Port; selected dropdown content clips within its own bounds |
| Traceroute sessions / toolbar | PASS | Actual five-session Dark render; shared Hop Description binds once before All status; protected primary/session guards remain |
| Traceroute state | PASS | Two actual loopback trace sessions display Running without per-probe outcome suffix; real outcomes remain in result/event data |
| Port modes / templates | PASS | Both checkboxes removed; multiple-target continuous mode remains enabled; /22 × six-port preview still requires acknowledgement; range template saves and restores; actual range editor expands/deduplicates |
| Actual UDP polling | PASS | Owned replying/silent loopback sockets; configured payload receipt, repeated attempts, measured reply timing, no invented RTT/closed claim for silence, Stop/cancellation and CSV |
| Actual TCP polling | PASS | Owned loopback listeners consume payloads; actual connection/send outcomes, pause/resume/cancellation, history, CSV and cleanup |
| WPF rendering | PASS | Actual controls at 1536×1024, 1280×800 and 125%/150% render scaling; zero binding errors |
| Canonical references | PASS | Original scope, both approved PNGs and supplied logo source unchanged |
| Human GUI/accessibility/monitor transitions, Windows 11 | NOT TESTED | Programmatic renders are not human or multi-monitor acceptance |
| Remote/private CIDR/TCP/UDP application services | NOT TESTED | Large scopes parsed/previewed; only owned loopback diagnostic targets probed |
| Machine install/upgrade/recovery/uninstall, UAC/SmartScreen | NOT TESTED | No installer executed |

Pre-publication source evidence is under `artifacts/defects/0.4.1`: `verified-build.log`, `verified-tests.log`, `tests/verified.trx`, `verified-windows-qa.log` and actual WPF renders in `verified-windows-qa/`. Publication rebuilds committed source and verifies the exact frozen self-contained package separately under `artifacts/github-release/0.4.1/final`; the subsequent publication report records its immutable hashes and CI results.

Packet Size describes zero-filled application payload bytes, not headers or OS-controlled TCP segmentation. TCP timing is the measured connection duration; successful sends do not prove an application protocol response. Size zero explicitly preserves connect-only TCP/empty UDP behavior. UDP silence stays inconclusive. Limits, large-scope acknowledgement and explicit-click updater trust/backup/shutdown rules remain unchanged.
