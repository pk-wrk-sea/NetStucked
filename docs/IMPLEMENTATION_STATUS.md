# Implementation status — 2026-10-09

Audit subject: clean `main` at `d60098cbaf6b7458c4de0285e369f0c72b60021d` before this documentation task; `Directory.Build.props` says **0.4.0**. This task changes documentation/instructions only. No product implementation, version bump, commit, push, GitHub milestone mutation or release publication is authorized here.

## State meanings

- **Approved** means explicit human approval of that scope/design, with an identifiable source. It does not cover every proposed enhancement in the same feature family.
- **Mockup — Awaiting Approval** means a mockup is reported/provided but not approved. Missing local assets are recorded separately.
- **Concept / Planned** describes a proposed requirement, not implementation authority.
- **Future Idea / On Hold** is a preserved backlog, not a committed schedule.
- **Implemented** requires real source; **Built** requires a successful build; **Unit Tested** requires appropriate passing automated cases; **Windows Manually Tested** requires actual human Windows acceptance; **Released** requires published packages.
- **Verified** always names the check and its limits. Programmatic WPF/loopback validation does not establish human GUI, remote-network or machine-installer acceptance. No milestone is fully complete with outstanding Definition of Done items.

## Actual feature audit

| Feature | Planned/design approval | Implemented source | Built | Unit/integration tested | Windows manually tested | Released |
|---|---|---|---|---|---|---|
| Live Ping: IPv4/IPv6, names, IPv4 CIDR, independent scheduling, counters, tables/history/CSV | Approved canonical PNG + 2026-10-08/0.4.0 revisions | Yes: `Core/MultiTargetPingService.cs`, `Desktop/ViewModels/LivePingViewModel.cs`, `Views/LivePingView.xaml` | PASS this audit | PASS; parsing/statistics/cancellation + actual loopback ICMP | NOT TESTED for full human/remote acceptance | Included in 0.3.x and 0.4.0 |
| Continuous IPv4 ICMP Traceroute, independent TTLs/sessions, route events, shared descriptions | Approved canonical PNG + revisions | Yes: `Core/TracerouteMonitoringService.cs`, trace workspace/VM/views, `Services/HopDescriptionService.cs` | PASS | PASS adapters, real loopback and prior WPF checks | Remote multi-hop/ECMP/filtered routes and human acceptance NOT TESTED | Included in 0.3.x and 0.4.0 |
| TCP/UDP Port Test: host/CIDR × ports, one-pass/continuous, templates, bounded scopes | Approved 0.2.0 + 0.4.0 extension | Yes: `Core/MultiTargetPortService.cs`, `PortPlanning.cs`, `Infrastructure/TcpPortProbe.cs`, `UdpPortProbe.cs`, Port VM/view | PASS | PASS; real owned TCP and UDP reply/silence, limits tested without broad scans | Remote services and UDP IPv6/port-unreachable matrix NOT TESTED | TCP from 0.2.0; current form 0.4.0 |
| Selected-build Updates & Recovery | Approved 0.3.0 explicit-click extension | Yes: release/update Core contracts, `GitHubReleaseSource`, `InstallerDownloader`, `WindowsUpdates`, `UpdateRunner`, Updates VM/view | PASS | PASS integrity/trust/orchestration; prior actual GitHub download/helper checks | Actual machine install/upgrade/recovery/UAC/uninstall NOT TESTED | 0.3.0, 0.3.1 TEST BUILD, 0.4.0 |
| Branding, Light/Dark/System, compact controls, collapsing/resizing | Approved 0.4.0 extension | Yes: `Services/ThemeService.cs`, `BrandAssets.cs`, `Resources/Brand/`, `Behaviors/SlideTrack.cs`, `SettingsView.xaml` | PASS | Prior actual WPF/native render/resize checks PASS | Real monitor-DPI, accessibility and Windows 11 NOT TESTED | 0.4.0 |
| Templates/preferences/column layouts/destination history | Approved existing tools | Yes: `Infrastructure/UserSettingsStore.cs`; schema-1 JSON | PASS | PASS round trips/validation/backwards-compatible preferences | Interactive disk-error/dialog matrix NOT TESTED | Included in current release |
| Dashboard / Network Info | Proposed; final designs pending | Neutral placeholders only in `MainViewModel.Navigate` | Shell builds only | No functional implementation tests | NOT TESTED | No functional page released |
| Dedicated DNS records, HTTP/HTTPS/TLS, target health profiles | Concept; tool-specific UI pending | Not implemented; `DnsResolver` only resolves probe names/reverse names | N/A | NOT TESTED | NOT TESTED | No |
| Network Monitoring/incidents/tray/alerts | Reported mockup awaiting approval | Not implemented; continuous diagnostic sessions are not an incident-monitoring product | N/A | NOT TESTED | NOT TESTED | No |
| SQLite session history/Reports | Planned; UI pending | Not implemented; selected-target histories are in memory, CSV export exists | N/A | NOT TESTED | NOT TESTED | No |
| Wi-Fi Manager / Terminal / Collector / Inventory / Device Intelligence / Topology / CVE / Upgrade Planner | Pending/concept; see registry | No production modules or corresponding package dependencies found | N/A | NOT TESTED | NOT TESTED | No |
| Commercial licensing/payments | Future Idea / On Hold | Not implemented | N/A | NOT TESTED | NOT TESTED | No |

Paths in the source column are relative to `src/NetStucked.*`. The repository contains three production projects and two test/QA projects; Infrastructure has no SQLite, SSH or payment dependencies. Traceroute is **IPv4 ICMP only**. Optional reverse-DNS machinery exists in Core but the compact desktop settings normalize it off; WAN descriptions are organization/ASN metadata, not hostname or hardware-vendor proof.

## Checks executed in this documentation audit

Using `C:\Users\PK\.codex\cache\netstucked-tools\dotnet\dotnet.exe`:

```powershell
dotnet restore NetStucked.sln
dotnet build NetStucked.sln -c Release --no-restore
dotnet test NetStucked.sln -c Release --no-build --logger "trx;LogFileName=milestone-audit.trx" --results-directory artifacts/milestone-audit/2026-10-09-docs/tests
```

**PASS:** restore; five-project Release build, zero warnings/errors; **176 tests passed, 0 failed, 0 skipped**. Logs and TRX: `artifacts/milestone-audit/2026-10-09-docs/`. This suite includes real loopback integration tests; deterministic adapters remain test-only. Original scope/PNG and supplied raster logo hashes match [prior immutable evidence](QA_0.4.0.md).

The published tag `v0.4.0` is `eb5bb0f880d1f9dc2ab4173c935e53cd92d0b22a`, with **175** tests at packaging. Main subsequently adds test-only HTTP cancellation coverage, giving **176**; these counts are different subjects, not conflicting results. [GitHub 0.4.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.4.0) was read during this audit and is Latest. No installer or new network scan was launched here.

## Prior evidence, not rerun here

[0.4.0 publication report](GITHUB_RELEASE_0.4_REPORT_2026-10-09.md) records self-contained publish, exact frozen assemblies/native executable, Windows CI/Inno compilation, public catalog/download verification and a 60.4-second simultaneous loopback soak with zero observed per-target/TTL/endpoint overlap. Its largest observed Dispatcher gap was 224.1 ms; responsive UI is not a claim of zero stalls. The short soak does not prove a long-term memory plateau. [Local 0.4.0 QA](QA_0.4.0.md) has different package bytes and is historical evidence.

**NOT TESTED:** full human Windows GUI/accessibility/monitor-DPI matrix, Windows 11, clean-machine install/upgrade/recovery/uninstall, actual remote multi-hop and authorized remote service scans, UDP IPv6/network port-unreachable matrix, long-running current-package memory plateau and production code signing/reputation.

## Completion and next action

M00 functionality and M07 updater are implemented and released through later versions, but full acceptance is open. M02 has an implemented TCP/UDP subset; DNS/HTTP/TLS are absent. The other proposed milestones remain unimplemented. Do not use a completion percentage that mixes approved scope with ideas.

The next approved work is [M00 acceptance closure on the current release](milestones/M00_FOUNDATION.md), with M07's actual installer/recovery matrix handled as the related release gate. It adds no future page. Execution instructions and environment blockers are in [CODEX_NEXT_TASK](CODEX_NEXT_TASK.md). Network Info is the next proposed new feature after separate UI/scope approval.
