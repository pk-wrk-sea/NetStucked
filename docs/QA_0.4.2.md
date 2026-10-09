# NetStucked 0.4.2 verification

Run date: 2026-10-09, Asia/Bangkok. Scope: [authorized diagnostics fixes and additions](FEATURES_0.4.2.md). The shared checkout's 52 existing Wi-Fi/planning WIP files were hashed before work and preserved; implementation uses a separate managed checkout based on published 0.4.1. No user targets/settings or machine installer are used by QA.

| Check | Result | Evidence / limit |
|---|---|---|
| Release build | PASS | Five supported projects, .NET 10, zero warnings/errors |
| Automated tests | PASS | 214 tests, zero failures/skips; existing networking/updater regressions retained |
| ICMP admission isolation | PASS | Full Ping slots do not consume trace slots; concurrent paced sends, cancelled waits and slot cleanup tested |
| Recent warning health | PASS | Historical loss warning clears after 20 good completed replies while cumulative loss remains; RTT/missing/recent-loss explanations |
| Actual reply IP / history host | PASS | Deterministic differing responder/target and no-response checks; real loopback IP/hostname results/history; CSV address field |
| TCP hop checks | PASS | Disabled means zero TCP calls; owned listening port produces actual successful result with zero payload; invalid/custom/range/16-port limits; duplicate IP caching; slow TCP does not hold ICMP; Pause/Stop cancellation; late old-route result cannot populate a replacement hop; one-shot completion |
| Settings/UI propagation | PASS | Old defaults off, JSON roundtrip, malformed saved text rejected, actual popup validation and ViewModel preservation of optional custom ports |
| Selectable tables | PASS | Actual readonly cell/header text selection, caret/I-beam, visible order/headers in tab-separated text, themed continuous whole-table text selection, sorting and Description editing retained |
| Port column chooser | PASS | Actual checkbox hide, order change and saved-settings reload; cached views retain column width/visibility |
| WPF/native measurements | PASS for source WPF | Light/Dark/System, 1536×1024 / 1280×800 and 125%/150% renders, real networking and zero binding errors. Exact frozen/native package receipts are recorded separately on publication |
| Development concurrent soak | PASS | 60.3 seconds; 254 loopback Ping targets, 4,184 actual completed replies, 32 owned TCP endpoints and two trace sessions with 241 cycles each. Interval 250 ms / timeout 500 ms; no per-address/TTL or TCP overlap; Stop drains all requests. Maximum combined queued/in-flight ICMP 36, max Dispatcher gap 166.1 ms, 12 navigation samples mean 9.7 / max 35.3 ms. These are local observations, not remote or timing guarantees |
| Preserved references/WIP | PASS | Approved scope, both original screenshots and source logo match canonical SHA-256; 52 original checkout WIP bytes unchanged |
| User's remote/private CIDR instability | NOT TESTED remotely | High desktop send limit, lack of reservation and cumulative warning persistence verified in source. Actual LAN/WAN loss requires the user's measurements; only owned loopback is probed here |
| Human drag-copy/accessibility/monitor/DPI/Windows 11 acceptance | NOT TESTED | Programmatic WPF control checks do not substitute for the unchecked human checklist |
| Machine install/upgrade/recovery/uninstall and Windows prompts | NOT TESTED | No installer executed |

Review found and corrected lost TCP settings during ViewModel normalization, readonly TextBox two-way binding (including header binding), correct WPF sorting invocation, preservation of editable descriptions, explicit table-copy truncation, cancellation cleanup, stale route/IP protection and saved-input validation. Failed development QA attempts remain under `artifacts/diagnostics-stability`; one failure was a test fixture changing the column visibility it later expected, corrected by restoring that fixture. A render naming issue was corrected by explicitly switching to Dark for the new Dark screenshots.

Source receipts: `artifacts/diagnostics-stability/verified-source-qa.log`, actual renders in `verified-source-qa/`, `qa-attempt-4/soak-results.txt`, `qa-attempt-4/engine-diagnostics.json` and TRX under the isolated checkout. Publication runs clean-source tests and exact frozen-package WPF/native/soak checks again under `artifacts/github-release/0.4.2/final` and records immutable source/package hashes, CI, actual public catalog/installer preparation and remaining limits in its publication report.

Use [the unchecked human checklist](FEATURES_0.4.2.md#human-acceptance-checklist) for program acceptance. Optional hop TCP is direct connect-only testing on responding IPs; it does not replace ICMP route discovery or prove application protocol health. Effective Ping cadence is bounded by global pacing, timeouts and OS scheduling, and can be slower than the requested 250 ms.

Published result: [0.4.2 publication report](GITHUB_RELEASE_0.4.2_REPORT_2026-10-09.md). Exact frozen WPF/native/60-second soak, both CI workflows and actual public catalog/installer preparation pass; no installer was executed. Published bytes remain unchanged after the tag.
