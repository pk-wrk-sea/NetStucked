# Milestone index — 2026-10-09

**2026-10-21 / 0.11.1 verification:** Five-menu actual-program fixes and captured evidence are tracked in [scope](FEATURES_0.11.1.md) and [QA](QA_0.11.1.md). This patch does not close broader A01/A02/A03/A05/A06 criteria or real-device/physical-DPI/accessibility/Setup acceptance. Publication follows exact-payload verification.

**2026-10-20 / 0.11.0 published as GitHub Latest:** All current menus have functional existing-theme workflows, including actual Dashboard, stable-ID MA Inventory/Excel/audit, offline Cisco evidence/one-hop Topology and engineer-reviewed CVE/device change plans. [Scope](FEATURES_0.11.0.md), [review guide](QA_0.11.0.md), [immutable source/assets/CI and updater evidence](GITHUB_RELEASE_0.11.0_REPORT_2026-10-20.md). 462 tests, exact WPF/60s, engineering, SSH and native checks pass; both public direct downloads match frozen hashes. The first 0.11.0 updater catalog check passed; older/subsequent requests and installer staging were blocked by GitHub HTTP 403, not fixed by this version. Human/real-device/physical-DPI/accessibility/Setup gates remain open; broader historical milestone criteria and commercial On Hold status are retained.

**2026-10-19 / 0.10.1 published as GitHub Latest:** Terminal uses first-use Accept Once / Accept & Save / Cancel, saved-key recognition, explicit changed-key review and saved-key management in the existing theme. [Scope/QA](FEATURES_0.10.1.md), [immutable source/assets/CI and public updater evidence](GITHUB_RELEASE_0.10.1_REPORT_2026-10-19.md). Release build, 442 tests, 49 actual SSH/WPF terminal assertions, 252 exact-payload WPF assertions, native checks and four Windows CI runs pass. The real 0.9.0 updater now discovers Install v0.10.1; public installer/hash/trust/helper preparation passes without executing Setup. Earlier rate-limit failures remain historical evidence; this patch does not change the API. Real-device/manual and full A01/M07 gates remain open.

**2026-10-19 / 0.10.0 published as GitHub Latest:** The approved first Terminal slice is implemented and published. [Scope](FEATURES_0.10.0.md), [architecture](TERMINAL_2026-10-19.md), [QA](QA_0.10.0.md), [immutable source/assets/CI/public-download evidence and updater limits](GITHUB_RELEASE_0.10.0_REPORT_2026-10-19.md). Release build, 424 tests, 30 actual owned SSH/emulator assertions, 272 exact-payload WPF/60-second assertions, native checks and four Windows CI runs pass. The anonymous production updater is BLOCKED here by GitHub HTTP 403/rate limit, including an after-reset retry; this is not a PASS. Full A01 and real-device/Windows Clipboard/manual acceptance remain open. Earlier Terminal placeholder gates below are superseded only for this slice.

**2026-10-19 / 0.9.0:** Config Collector is approved and implemented in the existing theme; [scope](FEATURES_0.9.0.md), [architecture and failure matrix](CONFIG_COLLECTOR_2026-10-19.md), [QA and open real-device gates](QA_0.9.0.md). Published as GitHub Latest; [asset, CI and updater evidence including staging limits](GITHUB_RELEASE_0.9.0_REPORT_2026-10-19.md). Older collector-v2/backlog snapshots below are superseded only for this named slice.

**0.8.0 is published as GitHub Latest (2026-10-15).** Local Monitoring/SNMP/tray/auto-resume and utility/URL/result-zoom refinements passed 354 tests, exact portable/installer Windows CI, native saved-monitor resume/instance restoration and the actual public 0.7.0 updater/preparation path. See [publication evidence and limits](GITHUB_RELEASE_0.8.0_REPORT_2026-10-15.md), [scope](FEATURES_0.8.0.md) and [usage/QA](QA_0.8.0.md). Real Cisco/key/captcha/manual/clean-machine acceptance remains open; earlier snapshots below are historical.

**0.7.0 is published as GitHub Latest (2026-10-15).** The approved utility/menu revision, exact portable/installer checks, 315 tests, Windows CI and actual public 0.6.0 → 0.7.0 updater/preparation checks passed. See [the publication evidence and limits](GITHUB_RELEASE_0.7.0_REPORT_2026-10-15.md) and [tool scope](TOOLS_REVISION_2026-10-15.md). URL provider APIs and manual/clean-machine acceptance remain open. Earlier snapshots below are historical.

Earlier source-review snapshot (superseded by published 0.7.0): approved Subnet Cal / DNS Resolver / MAC Checker and in-app URL Checker layout replace the retired desktop DNS/HTTP/Wi-Fi surfaces; Port Test is TCP-only. Subnet uses one field and automatic inline validation. URL API integration awaits account keys and a supported Palo Alto endpoint. See [scope, verification and open gates](TOOLS_REVISION_2026-10-15.md). Published 0.6.0 descriptions below refer to the earlier release.

Current **0.6.0 is published as GitHub Latest**: DNS Test, HTTP / HTTPS Test, grouped navigation, Dark version labels and native Taskbar branding. Build, 265 tests, exact portable WPF/native/soak/package/public checks and both release-source CI workflows PASS. See [GITHUB_RELEASE_0.6.0_REPORT_2026-10-09.md](GITHUB_RELEASE_0.6.0_REPORT_2026-10-09.md) and [manual acceptance checklist](FEATURES_0.6.0.md#human-acceptance-checklist). Wi-Fi remains unchanged. Broader adapters/routes/ARP and durable History/Reports were declined; future menu pages show Pending - Coming Soon. Earlier planning/version snapshots below are historical.

Prior 0.5.0 integration record (historical, 2026-10-09): **0.5.0** combines the approved Network Info Wi-Fi slice with published **0.4.2** diagnostics. Version bump/publication were explicitly authorized; **0.5.0 is now GitHub Latest**, with 242 tests, exact WPF/native/soak/package/public checks and both Windows CI workflows PASS. [Publication report](GITHUB_RELEASE_0.5.0_REPORT_2026-10-09.md). See [0.5.0 scope](FEATURES_0.5.0.md) and [combined verification](QA_0.5.0.md); earlier audit/preview version and release statements below are historical snapshots. Wi-Fi hardware authentication and broader M01 routes/neighbors remain open.

Published baseline verified here: **0.4.0**; current source includes the separately authorized **0.4.1** follow-up, with an isolated audit build and **196 passing tests**. Stable target: **1.0.0**. No milestone is fully complete merely because code/release exists. [IMPLEMENTATION_STATUS](IMPLEMENTATION_STATUS.md) distinguishes subjects, approval and verification.

At this audit's final API read, 0.4.1 is a staged GitHub **Draft**, not a released public updater build. Read the subsequent publication report if the separate release task completes later.

## Pre-stable implementation milestones

| ID / document | Proposed or actual version | Design/scope status | Implementation / remaining work |
|---|---|---|---|
| [M00 Foundation/Ping/Trace](milestones/M00_FOUNDATION.md) | Historical 0.1.0, retained in 0.4.x | Approved canonical UI + revisions | Implemented; baseline 176/captured 0.4.1 196 tests PASS; human/remote/clean Windows acceptance open — next approved task |
| [M01 Network Info/Wi-Fi](milestones/M01_NETWORK_INFO.md) | 0.5.0 proposed; working version unchanged | Wi-Fi compact table approved 2026-10-09; broader read-only UI pending | Wi-Fi implemented and automated-tested locally; hardware acceptance blocked by stopped WLAN service; broader routes/neighbors pending |
| [M02 DNS/TCP/HTTP diagnostics](milestones/M02_DIAGNOSTICS.md) | 0.6.0 proposed | Ports approved; new tool UI pending | TCP/UDP delivered; DNS record/HTTP/TLS absent |
| [M03 Health profiles](milestones/M03_HEALTH_PROFILES.md) | 0.7.0 proposed | Concept / UI pending | Not implemented |
| [M04 Local monitoring](milestones/M04_LOCAL_MONITORING.md) | 0.8.0 proposed | Mockup — Awaiting Approval | Not implemented; missing mockup reference |
| [M05 SQLite History/Reports](milestones/M05_HISTORY_REPORTS.md) | 0.9.0 proposed | Planned / schema/UI pending | Current live history/CSV subset; no SQLite |
| [M06 Dashboard/operational UI](milestones/M06_DASHBOARD.md) | 0.10.0 proposed | Concept; Dashboard postponed | Placeholder only |
| [M07 Updates/Recovery](milestones/M07_UPDATES_RECOVERY.md) | Delivered 0.3.0+, acceptance on 0.4.0 | Approved explicit-click design | Implemented/released; public asset/helper checks PASS; machine matrix open |
| [M08 Release hardening](milestones/M08_RELEASE_HARDENING.md) | 0.11.0 proposed; separately authorized 0.4.x fixes possible | Quality scope; stable criteria pending | Signing, clean Windows, accessibility/remote/long-run acceptance open |
| [M09 First stable](milestones/M09_STABLE_RELEASE.md) | 1.0.0 target | Future scope/release approval required | Not ready; choose/accept or explicitly defer candidate scope first |

The original brief assigned M01/M02/M03 to 0.2/0.3/0.4, which now contain different actual releases. [PRODUCT_ROADMAP](PRODUCT_ROADMAP.md) retains that mapping and proposed replacements without relabeling shipped functionality or changing `Directory.Build.props`. M07 was delivered earlier than the proposal; do not rebuild an updater as a future feature.

## Post-stable candidates — exact 1.x versions unassigned

| ID / document | State / dependency |
|---|---|
| [A01 Terminal/Remote Access](milestones/A01_TERMINAL.md) | Reported mockup pending; verified SSH transport plus real terminal emulator/security |
| [A02 Cisco Config Collector](milestones/A02_CONFIG_COLLECTOR.md) | Reported v2 mockup pending; shared SSH/raw evidence/stable asset contract; no scheduled collection |
| [A03 MA Inventory/Excel](milestones/A03_MA_INVENTORY.md) | Concept; stable identity, SQLite and reviewable versioned import |
| [A04 Device Intelligence](milestones/A04_DEVICE_INTELLIGENCE.md) | Concept; Cisco raw evidence + platform-specific parser corpus |
| [A05 One-hop Topology](milestones/A05_TOPOLOGY_EXPLORER.md) | Reported mockup pending; neighbor/interface evidence, explicit confidence/freshness |
| [A06 Cisco CVE](milestones/A06_CVE_ASSESSMENT.md) | Concept; validated device facts + verified official advisory sources/applicability |
| [A07 Upgrade Planner](milestones/A07_UPGRADE_PLANNER.md) | Concept; reviewed CVE/inventory + Engineer approval of targets |
| [A08 Central/team collector](milestones/A08_CENTRAL_COLLECTOR.md) | Future candidate; monitoring contracts, independent deployed service/auth |
| [A09 Advanced Wi-Fi/EAP](milestones/A09_ADVANCED_WIFI.md) | Future candidate if not separately delivered with approved base Wi-Fi |

## Commercial research — On Hold, unscheduled

| ID / document | Retained ideas |
|---|---|
| [B01 Entitlements](milestones/B01_ENTITLEMENTS.md) | Freemium tier/feature options; no enforcement |
| [B02 Offline/per-user license](milestones/B02_OFFLINE_LICENSE.md) | Signed cache, proposed two activations/30-day policy, privacy/recovery |
| [B03 License service/portal](milestones/B03_LICENSE_PORTAL.md) | API/account/renewal/annual prepaid options; no deployment |
| [B04 QR payments/activation](milestones/B04_QR_PAYMENTS.md) | PromptPay/gateway/webhook options; no provider/payment packages |
| [B05 Team/Enterprise](milestones/B05_TEAM_LICENSE.md) | Organization/seat/portal options; no final pricing/policy |

Each document has twenty required sections, acceptance/DoD checkboxes, module/data ownership, meaningful tests, blockers and a copyable task prompt. On Hold prompts preserve the stop gate; unapproved UI prompts require approval/prototype authority. See [next authorized queue item](CODEX_NEXT_TASK.md) and [dependency map](FEATURE_DEPENDENCIES.md). This is a repository milestone index; no GitHub Issues/Milestones were created or changed.
