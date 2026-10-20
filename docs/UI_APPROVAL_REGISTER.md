# UI approval register

**2026-10-20 / 0.11.0 published as GitHub Latest:** All current menus have functional existing-theme workflows, including actual Dashboard, stable-ID MA Inventory/Excel/audit, offline Cisco evidence/one-hop Topology and engineer-reviewed CVE/device change plans. [Scope](FEATURES_0.11.0.md), [review guide](QA_0.11.0.md), [immutable source/assets/CI and updater evidence](GITHUB_RELEASE_0.11.0_REPORT_2026-10-20.md). 462 tests, exact WPF/60s, engineering, SSH and native checks pass; both public direct downloads match frozen hashes. The first 0.11.0 updater catalog check passed; older/subsequent requests and installer staging were blocked by GitHub HTTP 403, not fixed by this version. Human/real-device/physical-DPI/accessibility/Setup gates remain open; broader historical milestone criteria and commercial On Hold status are retained.

**2026-10-19 / 0.10.1 published as GitHub Latest:** Terminal uses first-use Accept Once / Accept & Save / Cancel, saved-key recognition, explicit changed-key review and saved-key management in the existing theme. [Scope/QA](FEATURES_0.10.1.md), [immutable source/assets/CI and public updater evidence](GITHUB_RELEASE_0.10.1_REPORT_2026-10-19.md). Release build, 442 tests, 49 actual SSH/WPF terminal assertions, 252 exact-payload WPF assertions, native checks and four Windows CI runs pass. The real 0.9.0 updater now discovers Install v0.10.1; public installer/hash/trust/helper preparation passes without executing Setup. Earlier rate-limit failures remain historical evidence; this patch does not change the API. Real-device/manual and full A01/M07 gates remain open.

**2026-10-19 / 0.10.0 published as GitHub Latest:** The approved first Terminal slice is implemented and published. [Scope](FEATURES_0.10.0.md), [architecture](TERMINAL_2026-10-19.md), [QA](QA_0.10.0.md), [immutable source/assets/CI/public-download evidence and updater limits](GITHUB_RELEASE_0.10.0_REPORT_2026-10-19.md). Release build, 424 tests, 30 actual owned SSH/emulator assertions, 272 exact-payload WPF/60-second assertions, native checks and four Windows CI runs pass. The anonymous production updater is BLOCKED here by GitHub HTTP 403/rate limit, including an after-reset retry; this is not a PASS. Full A01 and real-device/Windows Clipboard/manual acceptance remain open. Earlier Terminal placeholder gates below are superseded only for this slice.

**2026-10-19 / 0.9.0:** Config Collector is approved and implemented in the existing theme; [scope](FEATURES_0.9.0.md), [architecture and failure matrix](CONFIG_COLLECTOR_2026-10-19.md), [QA and open real-device gates](QA_0.9.0.md). Published as GitHub Latest; [asset, CI and updater evidence including staging limits](GITHUB_RELEASE_0.9.0_REPORT_2026-10-19.md). Older collector-v2/backlog snapshots below are superseded only for this named slice.

**0.8.0 is published as GitHub Latest (2026-10-15).** Local Monitoring/SNMP/tray/auto-resume and utility/URL/result-zoom refinements passed 354 tests, exact portable/installer Windows CI, native saved-monitor resume/instance restoration and the actual public 0.7.0 updater/preparation path. See [publication evidence and limits](GITHUB_RELEASE_0.8.0_REPORT_2026-10-15.md), [scope](FEATURES_0.8.0.md) and [usage/QA](QA_0.8.0.md). Real Cisco/key/captcha/manual/clean-machine acceptance remains open; earlier snapshots below are historical.

**0.7.0 is published as GitHub Latest (2026-10-15).** The approved utility/menu revision, exact portable/installer checks, 315 tests, Windows CI and actual public 0.6.0 → 0.7.0 updater/preparation checks passed. See [the publication evidence and limits](GITHUB_RELEASE_0.7.0_REPORT_2026-10-15.md) and [tool scope](TOOLS_REVISION_2026-10-15.md). URL provider APIs and manual/clean-machine acceptance remain open. Earlier snapshots below are historical.

Earlier source-review snapshot (superseded by published 0.7.0): approved Subnet Cal / DNS Resolver / MAC Checker and in-app URL Checker layout replace the retired desktop DNS/HTTP/Wi-Fi surfaces; Port Test is TCP-only. Subnet uses one field and automatic inline validation. URL API integration awaits account keys and a supported Palo Alto endpoint. See [scope, verification and open gates](TOOLS_REVISION_2026-10-15.md). Published 0.6.0 descriptions below refer to the earlier release.

Current **0.6.0 is published as GitHub Latest**: DNS Test, HTTP / HTTPS Test, grouped navigation, Dark version labels and native Taskbar branding. Build, 265 tests, exact portable WPF/native/soak/package/public checks and both release-source CI workflows PASS. See [GITHUB_RELEASE_0.6.0_REPORT_2026-10-09.md](GITHUB_RELEASE_0.6.0_REPORT_2026-10-09.md) and [manual acceptance checklist](FEATURES_0.6.0.md#human-acceptance-checklist). Wi-Fi remains unchanged. Broader adapters/routes/ARP and durable History/Reports were declined; future menu pages show Pending - Coming Soon. Earlier planning/version snapshots below are historical.

Prior 0.5.0 integration record (historical, 2026-10-09): **0.5.0** combines the approved Network Info Wi-Fi slice with published **0.4.2** diagnostics. Version bump/publication were explicitly authorized; **0.5.0 is now GitHub Latest**, with 242 tests, exact WPF/native/soak/package/public checks and both Windows CI workflows PASS. [Publication report](GITHUB_RELEASE_0.5.0_REPORT_2026-10-09.md). See [0.5.0 scope](FEATURES_0.5.0.md) and [combined verification](QA_0.5.0.md); earlier audit/preview version and release statements below are historical snapshots. Wi-Fi hardware authentication and broader M01 routes/neighbors remain open.

Snapshot: 2026-10-09. Source bytes and explicit human decisions govern approval; this planning task does not approve future screens.

| Screen/change | Design approval | Located evidence | Current implementation / remaining verification |
|---|---|---|---|
| Ping capacity/health, reply address columns, selectable/copyable tables and optional responding-hop TCP checks | Explicit human defect/feature extension, 2026-10-09 | `docs/FEATURES_0.4.2.md` | Focused implementation; automated evidence and unchecked human acceptance in the 0.4.2 QA report |
| Original Live Ping | Approved | `design/references/LivePing_APPROVED.png`, `docs/APPROVED_UI_SCOPE.md` | Implemented; actual WPF renders verified historically; full human acceptance open |
| Original Traceroute | Approved | `design/references/Traceroute_APPROVED.png`, same scope | Implemented IPv4 ICMP; remote multi-hop acceptance open |
| Compact probe dialogs, framed window, templates/history, tables, independent sessions/polling | Approved human revision | `docs/UI_REVISION_2026-10-08.md` | Implemented; older eight-session limit superseded by five in 0.4.0 |
| Port Test and bottom Settings/Updates group | Approved human extension | `docs/FEATURES_0.2.0.md` | Implemented; current TCP/UDP scope extends it |
| Selected-build Install/Reinstall/Recover | Approved explicit-click extension | `docs/FEATURES_0.3.0.md` | Implemented; machine installer scenarios NOT TESTED |
| Supplied logo, Light/Dark/System, resizable/folding panels, shared hop mapping, five trace sessions, multiple-target TCP/UDP scans | Approved human-reviewed mockup/revision | `docs/FEATURES_0.4.0.md`; actual WPF QA in `artifacts/github-release/0.4.0/final/windows-qa/` | Implemented/released; interactive source mockup is not checked into `design/references/` |
| Caption/caret/spacing/Trace toolbar/state fixes, continuous multiple-target ports/ranges/configurable payload | Approved subsequent defect/payload clarification | `docs/FEATURES_0.4.1.md`, separate `docs/QA_0.4.1.md` | Implemented by concurrent development; captured source build/196 tests PASS; do not infer public release or machine acceptance |
| Network Info: Wi-Fi Profile Manager | Approved human 2026-10-09 with compact-table override | `design/references/WiFiProfiles_APPROVED_2026-10-09.png`, `docs/FEATURES_WIFI_2026-10-09.md` | Implemented with current Light/Dark theme; actual WPF renders/fixture behavior and loopback checks PASS; real Wi-Fi authentication NOT TESTED |
| Broader Network Info routes/neighbors / Dashboard / dedicated DNS-HTTP-Health pages / History-Reports | Concept; final UI pending | Planning brief only | Wi-Fi approval does not approve these other pages |
| Monitoring / Terminal / Topology | Mockup — Awaiting Approval, reported by planning brief | No matching mockup file located in tracked repository | Request/locate supplied mockup before review; do not reconstruct or assume approval |
| Config Collector v2 | Mockup — Awaiting Approval, reported by brief | No v2 mockup file located | Multiline targets requirement preserved; UI implementation BLOCKED |
| MA Inventory / Device Intelligence / CVE / Upgrade Planner | Concept | Planning brief only | Approval and implementation pending |
| Account/license/billing | Future Idea / On Hold | Explicitly postponed in planning brief | No current navigation or payment integration |

## Located branding

The supplied unchanged raster is `src/NetStucked.Desktop/Resources/Brand/NetStuckedLogo.png`, SHA-256 `ed3030618f1d49de647e621f6db57d965313e0950881af44b1edd8b6e9049938`. Current derived/distributed resources are `NetStucked.ico`, `NetStuckedBanner.png`, `WizardLogo.png`, `WizardMark.png` in the same folder and `docs/branding/NetStuckedBanner.png`.

The planning brief calls the selected concept “Concept 1” and describes N/orbit/server/Wi-Fi motifs and a brand pack. The exact concept label, complete brand-pack approval and SVG/full-horizontal vector variants cannot be established from located files. No SVG was found. Preserve supplied assets; do not claim missing artwork exists or replace it.

## Precedence and immutable baseline

Original scope and screenshots remain byte-for-byte unchanged. Their obsolete version/scope statements describe 0.1.0 only. Later explicit human extensions govern only the changes they name. Minimal Light remains the baseline; approved Dark/System are supported. Settings and Updates remain separate bottom-group navigation items under the approved current design; the proposed logical Settings > Updates grouping does not authorize moving them now.

| Protected file | SHA-256 verified in this audit |
|---|---|
| `docs/APPROVED_UI_SCOPE.md` | `0f722da815555532a330e1ae9c5121778aa184cc740c94da82cb7e65d3003776` |
| `design/references/LivePing_APPROVED.png` | `45f941795fa7347a824976b79f73cd61f029778f7afecc246df302e181d55772` |
| `design/references/Traceroute_APPROVED.png` | `e8f8f3ad08d9d9ca844bc2f2f3326910df74583afe52dbc4730c19085f2cb2f1` |
| New approved Wi-Fi reference (table override applies) | `39fd622f6ce502d5c4b2027368009c951230b2eddbe38c25ec78c2669eeb2481` |

0.5.0 combined-source review: original Wi-Fi reference/compact-table authorization is preserved; selectable text replaces table TextBlocks without changing approved hierarchy. Later human request authorizes the Wi-Fi/0.4.2 merge and new GitHub version. Reviewed exact-publish WPF checks PASS; human/native hardware acceptance stays open.
