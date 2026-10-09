# Milestone index — 2026-10-09

Current product: **0.4.0 development**, published Latest. Stable target: **1.0.0**. No milestone below is marked fully complete merely because code or a release exists. Approval, implementation and verification are separate in [IMPLEMENTATION_STATUS](IMPLEMENTATION_STATUS.md).

## Pre-stable implementation milestones

| ID / document | Proposed or actual version | Design/scope status | Implementation / remaining work |
|---|---|---|---|
| [M00 Foundation/Ping/Trace](milestones/M00_FOUNDATION.md) | Historical 0.1.0, retained in 0.4.0 | Approved canonical UI + revisions | Implemented/released; current 176 tests PASS; human/remote/clean Windows acceptance open — next approved task |
| [M01 Network Info/Wi-Fi candidate](milestones/M01_NETWORK_INFO.md) | 0.5.0 proposed | Concept / Wi-Fi mockup pending | Network Info placeholder; Wi-Fi absent; UI BLOCKED |
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
