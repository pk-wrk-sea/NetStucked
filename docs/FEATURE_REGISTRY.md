# Feature and navigation registry

Current **0.6.0 is published as GitHub Latest**: DNS Test, HTTP / HTTPS Test, grouped navigation, Dark version labels and native Taskbar branding. Build, 265 tests, exact portable WPF/native/soak/package/public checks and both release-source CI workflows PASS. See [GITHUB_RELEASE_0.6.0_REPORT_2026-10-09.md](GITHUB_RELEASE_0.6.0_REPORT_2026-10-09.md) and [manual acceptance checklist](FEATURES_0.6.0.md#human-acceptance-checklist). Wi-Fi remains unchanged. Broader adapters/routes/ARP and durable History/Reports were declined; future menu pages show Pending - Coming Soon. Earlier planning/version snapshots below are historical.

Prior 0.5.0 integration record (historical, 2026-10-09): **0.5.0** combines the approved Network Info Wi-Fi slice with published **0.4.2** diagnostics. Version bump/publication were explicitly authorized; **0.5.0 is now GitHub Latest**, with 242 tests, exact WPF/native/soak/package/public checks and both Windows CI workflows PASS. [Publication report](GITHUB_RELEASE_0.5.0_REPORT_2026-10-09.md). See [0.5.0 scope](FEATURES_0.5.0.md) and [combined verification](QA_0.5.0.md); earlier audit/preview version and release statements below are historical snapshots. Wi-Fi hardware authentication and broader M01 routes/neighbors remain open.

Use this canonical inventory with [approval evidence](UI_APPROVAL_REGISTER.md) and the [actual code audit](IMPLEMENTATION_STATUS.md). “Selected/proposed requirements” from the planning brief are not final approval of a page or an implementation commitment.

| ID | Feature / retained requirements | Design approval | Implementation | Verification | Milestone |
|---|---|---|---|---|---|
| F00-P | Live Ping: individual IPv4/IPv6/names, bounded IPv4 CIDR, descriptions/templates, independent ICMP, interval/timeout/payload, Sent/Received/Lost/Loss/RTT, filter/sort/columns, selected history, CSV, pause/resume/Stop | Approved + named revisions | Implemented/released | Build/tests + prior loopback/WPF PASS; human/remote acceptance open | M00 |
| F00-T | IPv4 ICMP trace: real TTL discovery/polling, per-hop statistics/jitter/loss, meaningful route events/CSV, five sessions, shared manual/WAN descriptions | Approved + named revisions | Implemented/released; IPv6/TCP/UDP trace absent | Build/tests + prior loopback/WPF PASS; remote route matrix open | M00 |
| F01 | Network Info: adapters, IPv4/IPv6/prefix/mask, gateway/DNS/DHCP/MAC/speed, routes, neighbors/ARP, metrics | Broader read-only UI still pending | Wi-Fi slice provides adapter/IP/gateway/DNS; routes/neighbors/remaining adapter facts pending | Native hardware read blocked by stopped WLAN service; no route acceptance | M01 partial |
| F02 | Wi-Fi Manager inside Network Info: compact saved profile table, explicit scan/adapter/connect/disconnect, profile CRUD/import/export, Windows security, SSID/signal/IP/Gateway/DNS, configured Connect & Test/history | Approved human 2026-10-09 reference with explicit table override | Released in 0.5.0; Native WLAN, Windows-managed EAP/TLS, optional Credential Manager, bounded source-aware service tests | Automated/owned loopback/WPF checks PASS; real Wi-Fi/Enterprise hardware NOT TESTED | M01 Wi-Fi slice in 0.5.0; broader M01 and hardware acceptance open |
| F03 | Diagnostics: dedicated DNS records, TCP/UDP ports, HTTP/HTTPS/TLS, troubleshooting; current Port ranges and configurable payload | TCP/UDP Approved including 0.4.1 clarification; other tools Concept/UI pending | Port Test implemented; desktop continuous/multiple-target, Core retains single-pass; dedicated DNS/HTTP/TLS absent | Captured 0.4.1 build/196 tests PASS; published 0.4.0 evidence separate; future tools NOT TESTED | M02 |
| F03-H | Target-oriented health check and diagnostic/network profiles; preserve independent protocol findings | Concept; UI pending | Not implemented | NOT TESTED | M03 |
| F04 | Local Monitoring, hybrid-ready: ICMP + TCP secondary, compact groups/table, thresholds/incidents, dedupe/cooldown, maintenance, tray, Windows notification/sound, availability/latency | Mockup — Awaiting Approval; reference missing | Not implemented | NOT TESTED | M04 |
| F05 | Terminal/Remote Access: real terminal emulator, SSH/password/key/host verification, tabs/split panes/saved sessions/reconnect, explicitly authorized Telnet, serial COM, logs/snippets/SFTP/copy-paste | Mockup — Awaiting Approval; reference missing; post-stable | Not implemented | NOT TESTED | A01 |
| F06 | Cisco-only IOS/IOS XE/NX-OS/ASA Collector: multiline targets/import/inventory/groups, credential profiles/bounded fallback/keys, parallel timeout/retry, command templates, running-config/version/inventory/operational outputs, results/details/viewer/diff/backup/export/log/progress, facts to inventory/CVE | Mockup v2 — Awaiting Approval; reference missing | Not implemented; scheduled collection explicitly excluded | NOT TESTED | A02 |
| F07 | MA Inventory: stable Asset ID, hostname/IP/vendor/model/serial/site/role, contract and MA dates/status, observed version/collection time, search/filter/sort, versioned Excel templates/import-preview/header/errors/duplicates/reconciliation/export | Concept | Not implemented | NOT TESTED | A03 |
| F08 | Device Intelligence: OS-family parsers, model/serial/version/uptime/interfaces/CDP/LLDP/routes, confidence/time/source evidence and config diff | Concept | Not implemented | NOT TESTED | A04 |
| F09 | One-hop device-centric Topology/Explorer: overview/neighbors, node/link selection, interfaces/VLAN/port-channel, routing/next-hop/default/protocols, config/diff/CVE/MA; evidence freshness and Verified/Discovered/Inferred/Unknown | Mockup — Awaiting Approval; reference missing | Not implemented | NOT TESTED | A05 |
| F10 | Cisco CVE Assessment: local facts, online/manual advisory metadata sync, applicability/config/version/freshness, Affected/Not Affected by evaluated advisory/Needs Review/Insufficient Data/Stale; official fixed-release evidence | Concept | Not implemented | NOT TESTED | A06 |
| F11 | Upgrade Planner: findings/severity/evidence/KEV, first-fixed and compatible engineer-approved target, change owner/ID/waves/windows, before/after verification/reassessment, Excel/CSV/HTML plans | Concept | Not implemented | NOT TESTED | A07 |
| F12 | Durable Ping/Trace/Collector/incident/device histories, search/filter, CSV/JSON/HTML reports, inventory/vulnerability reports, retention | Planned; UI pending | In-memory tool histories and CSV subset only; no SQLite history/report pages | Existing CSV tests PASS; durable/report requirements NOT TESTED | M05; later A02–A07 |
| F13 | GitHub selected-build Updates & Recovery: SemVer/current version, public notes/catalog, streamed verified download/progress/cancel, explicit action/trust acknowledgement, settings backup, isolated helper/restart/recovery | Approved 0.3.0 | Implemented/released; no background installation | Build/tests + prior public-download/helper checks PASS; machine acceptance open | M07 |
| F14 | Commercial licensing: Freemium tiers, per-user/two-device proposal, signed offline entitlement/30-day proposal, license API/portal/annual prepaid/renewal/recovery, PromptPay QR/gateway/webhooks/activation | Future Idea / On Hold | Not implemented | NOT TESTED | B01–B05 |
| F15 | Dashboard/notifications/operational summary | Concept; final UI pending | Dashboard placeholder; tool activity indicators exist | Dashboard NOT TESTED | M06 |
| F16 | Appearance/branding/compact operational UI including caption/caret/toolbar/spacing follow-up | Approved 0.4.0 + 0.4.1 | Implemented; public baseline 0.4.0 | Prior WPF/native + separate 0.4.1 QA PASS; human acceptance open | M00 retained + M08 acceptance |
| F17 | Shared central collector/team monitoring | Future candidate, no approved protocol or service | Not implemented | NOT TESTED | A08 |

TCP Probe Packet Size is application payload, default 32 and range 0–1400 bytes in 0.4.1. Connect/send completion does not prove an application response. UDP silence stays inconclusive. See [approved follow-up](FEATURES_0.4.1.md); its source version is not itself evidence of publication.

## Proposed product navigation versus current application

| Proposed menu order | Current state / placement |
|---|---|
| 1 Dashboard | Neutral placeholder |
| 2 Live Ping | Functional |
| 3 Traceroute | Functional |
| 4 Network Info | Implemented approved Wi-Fi Manager slice; read-only route/neighbor UI remains pending |
| 5 Diagnostics | Proposed grouping; current **Port Test** stays its approved separate menu |
| 6 Network Monitoring | Future; not a functional current menu |
| 7 Terminal / Remote Access | Post-stable candidate |
| 8 Config Collector | Post-stable candidate |
| 9 MA Inventory | Post-stable candidate |
| 10 CVE & Upgrade Planner | Post-stable candidate |
| 11 Topology Explorer | Post-stable candidate |
| 12 History | Planned persistent history; tool history panels already exist |
| 13 Reports | Proposed |
| 14 Settings | Functional appearance/preferences; bottom group |
| Updates & Recovery | Functional separate item beside Settings in bottom group; logical settings category |

No navigation changes are made in this planning task. Future Account/License/Subscription/Billing stay out of current navigation. This registry can later provide entitlement identifiers; it does not activate licensing.
