# NetStucked 0.4.0 — local UI and diagnostics verification

Run date: 2026-10-09, Asia/Bangkok. This is a local, uncommitted build on `codex/ui-branding-port-scan`. No push, public release, machine installation or replacement of existing user settings occurred. Earlier published packages, tags and canonical UI reference files remain unchanged.

Runnable application: `artifacts/ui-branding/0.4.0/final/publish/NetStucked.exe` (keep the whole directory). Installer: `artifacts/ui-branding/0.4.0/final/NetStucked-0.4.0-win-x64-setup.exe`. Exact file hashes and sizes are in `final/package-manifest.json` beneath that artifact root.

## Results

| Check | Result | Evidence and scope |
|---|---|---|
| Release build / restore | PASS | .NET 10; zero warnings and errors; five supported projects |
| Core/infrastructure tests | PASS | 175 passed, zero failed/skipped; `final/tests/verified-core.trx` |
| Exact published WPF assemblies | PASS | Harness loads and SHA-256 checks published Core, Infrastructure and Desktop assemblies; `final/verified-windows-qa.log` |
| Native executable startup/shutdown | PASS | Self-contained executable creates its actual native WPF window and exits cleanly; isolated QA data directory |
| Supplied logo | PASS | Original PNG copied without byte changes; sidebar/settings mark, executable ICO with eight sizes, installer artwork and README banner |
| Light/Dark themes and popups | PASS | Actual WPF renders; themed Hop Description, Probe Settings and column chooser; input guides, semantic status brushes and dark table rows |
| Panel/menu toggles and resizing | PASS | Main-menu toggle remains available while collapsed; address/history restore compact/expanded dimensions; trace events and Port panels fold; 200ms grid-track transition; actual horizontal/vertical GridSplitter drag events move boundaries by 20px (`final/panel-resize-qa.log`) |
| Independent trace sessions | PASS | Protected main session, invalid/blank new session blocks another Add, maximum five including main, adjacent Add card, disposal waits network cleanup |
| Shared hop descriptions | PASS | Space-separated multiline IP/description input, line-specific validation, mapping changes applied to two real loopback sessions, removal and CSV export |
| WAN metadata | PASS | Production RIPEstat adapter actually returned `CLOUDFLARENET - Cloudflare, Inc. · AS13335` for 1.1.1.1; metadata read sent zero probe packets; `final/wan-metadata.json` |
| WAN privacy/cache/lifecycle | PASS | Private IP filtering, manual priority, shared cache, bounded pending requests, two adapter slots, cancellation and suspension before helper handoff; deterministic adapter tests |
| Port input and scope | PASS | Separate hosts/protocol/ports; /22 × six ports = 6,132 checks is previewed and blocked before acknowledgement; scope changes reset acknowledgement; no /22 network probe launched in QA |
| Port templates | PASS | Edited preset saved and restored to built-in defaults; schema-1 settings round trip preserves new preferences |
| TCP behavior | PASS | Owned loopback listeners and a closed socket; real connection timing, failure without invented RTT, history, filter/sort, CSV, pause/resume/stop and cleanup |
| UDP and single pass | PASS | Real responding and silent loopback sockets; exactly two completed attempts, one Responded and one No response, zero failed/closed claims for silence; cancellation and one-pass pause retry tested |
| Continuous / simultaneous diagnostics | PASS | 60-second soak: 254 loopback Ping targets, 32 owned TCP ports and two independent trace sessions; zero overlap, coherent counters and awaited Stop |
| Render sizes/scaling | PASS | Actual WPF visual tree at 1536×1024, 1280×800 and 125%/150% bitmap DPI; no binding errors; screenshots in `final/verified-windows-qa/` |
| Selected-build recovery regression | PASS | Existing integrity/trust tests and fixture-driven prepare/cancel/handoff checks; actual diagnostic sessions drained before mocked installer handoff |
| Inno installer compile | PASS | Existing signed Inno Setup 6.7.3 compiler; stable AppId; new executable/installer logo resources; no installer executed |
| Canonical references | PASS | Original approved scope and both approved PNG SHA-256 values match the baseline |
| Actual remote multi-hop, private CIDR scans, remote TCP/UDP services | NOT TESTED | Only owned loopback targets were probed; large ranges were parsed and previewed |
| UDP IPv6 and real remote ICMP port-unreachable matrix | NOT TESTED | UDP actual tests cover IPv4 reply/silence/cancellation; outcome accounting and socket mapping are deterministic-tested |
| Human GUI/accessibility, real monitor DPI transitions, Windows 11 | NOT TESTED | Programmatic WPF/native-window verification does not substitute for human or multi-monitor acceptance |
| Machine install/upgrade/recovery/uninstall | NOT TESTED | Installer compiled and inspected; unsigned package requires existing explicit acknowledgement and Windows prompts when used |

The 60-second soak measurements are recorded in `final/verified-windows-qa/soak-results.txt`, `tcp-soak-results.json` and `engine-diagnostics.json`. These are local loopback measurements; they are not estimates for remote networks or large CIDR scans.

Measured final soak: 60.3 seconds, 61,403 completed Ping replies, 7,773 successful TCP attempts, and 241 completed cycles in each trace session. Same-address/TTL and TCP endpoint overlap were zero. Maximum concurrent ICMP operations was 21; TCP adapter maximum was five. Navigation averaged 13.0ms across twelve samples (maximum 94.8ms); maximum observed Dispatcher gap was 144.6ms. Managed memory grew from 15,286,000 to 23,503,792 bytes while retained histories filled; this short run does not establish a long-term memory plateau.

Final Desktop assembly SHA-256: `86d832ca41a3a37b5cbe5471d6af429756e33faccf580de31bc893633ad53a01`. Final installer: 47,089,639 bytes, SHA-256 `79c0296f7133764ec5c458dc7c171199c38d9400059525fa2ae452e2b6e6df73`, Windows signature status `NotSigned`. Native startup/exit evidence is `final/verified-native-smoke.log`.

## Scope and retained behavior

See [the approved extension](FEATURES_0.4.0.md). Port scans are capped at 1,024 expanded hosts, 64 ports and 16,384 checks, with 32 concurrent operations and 128 send admissions/second. Scopes over 4,096 checks require a fresh acknowledgement when targets, ports, protocol or multiple-target mode change. Unchecked Continuous means one pass; cancelled work does not count as a completed attempt. UDP response time measures a received datagram; silence is inconclusive.

RIPEstat describes the registered network organization/ASN, not the router hardware manufacturer. Public hop identities are cached for 24 hours, unavailable responses for ten minutes; at most 512 cached IPs and 32 pending jobs are retained. Local mappings take priority. Disabling lookups, application shutdown and installer handoff cancel and await pending requests. No API credential is stored.

Native file dialogs and Windows UAC/SmartScreen/trust prompts remain platform-managed. Source settings retain schema 1, and old common-port endpoint input is migrated only when it has one unambiguous port. Old mixed-port endpoint text is retained for explicit conversion instead of silently expanding its target scope. Dashboard and Network Info remain placeholders.

## Immutable source evidence

| File | SHA-256 |
|---|---|
| `docs/APPROVED_UI_SCOPE.md` | `0F722DA815555532A330E1AE9C5121778AA184CC740C94DA82CB7E65D3003776` |
| `design/references/LivePing_APPROVED.png` | `45F941795FA7347A824976B79F73CD61F029778F7AFECC246DF302E181D55772` |
| `design/references/Traceroute_APPROVED.png` | `E8F8F3AD08D9D9CA844BC2F2F3326910DF74583AFE52DBC4730C19085F2CB2F1` |
| Original `NetStucked_Logo.png` and copied brand resource | `ED3030618F1D49DE647E621F6DB57D965313E0950881AF44B1EDD8B6E9049938` |
