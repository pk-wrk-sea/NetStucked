# Next Codex task

## Selected task and reason

**M00: close acceptance of existing functionality on the current 0.4.0 release.** This is the earliest incomplete milestone with approved implemented scope. Its automated/native/owned-loopback regression slice can run now without missing new-page UI approval. Full closure is partly environment-blocked by clean Windows 11, human monitor/accessibility acceptance and an explicitly authorized remote lab. Do not mark the entire milestone unblocked/complete because this slice is executable.

Related gate: **M07 real installed-app upgrade/recovery acceptance**. The updater already ships in 0.3.0+; build/download/helper checks passed, but no actual machine-install matrix is proven. Execute this only on an authorized isolated VM/snapshot; no permission to install onto the working machine follows from this planning document.

No code defect is established by this audit. Begin with verification and only fix failures actually observed within approved scope. **M01 Network Info** is the next new-feature candidate, but its UI is not approved; do not start it as a workaround for unavailable QA prerequisites.

## Exact reading list

1. `AGENTS.md` and `.agents/skills/netstucked-development/SKILL.md`.
2. `docs/milestones/M00_FOUNDATION.md` and `M07_UPDATES_RECOVERY.md`.
3. `docs/IMPLEMENTATION_STATUS.md`, `UI_APPROVAL_REGISTER.md`, `FEATURE_DEPENDENCIES.md`, `ARCHITECTURE.md`, `SECURITY_AND_DATA_POLICY.md`, `RELEASE_AND_VERSIONING.md`.
4. `docs/APPROVED_UI_SCOPE.md`, `design/references/LivePing_APPROVED.png`, `Traceroute_APPROVED.png`.
5. `docs/UI_REVISION_2026-10-08.md`, `FEATURES_0.2.0.md`, `FEATURES_0.3.0.md`, `FEATURES_0.4.0.md`.
6. `docs/QA_REPORT.md`, `QA_0.4.0.md`, `GITHUB_RELEASE_0.4_REPORT_2026-10-09.md`, `DEVELOPMENT.md`, `TEST_PLAN.md`.
7. Owning sources: `src/NetStucked.Core/{MultiTargetPingService,TracerouteMonitoringService,MultiTargetPortService,AsyncSession}.cs`; `src/NetStucked.Infrastructure/{IcmpPingProbe,TcpPortProbe,UdpPortProbe,WindowsUpdates,UpdateRunner,UpdateFiles,UserSettingsStore}.cs`; Desktop `App.xaml.cs`, `MainWindow.xaml.cs`, diagnostic/Updates VMs/views and `HopDescriptionService.cs`.
8. `tests/NetStucked.Tests/`, `tests/NetStucked.WindowsQa/{Program,BrandingScanQa,SelectedBuildQa}.cs`, `scripts/{Publish,SmokePublished}.ps1` and `installer/NetStucked.iss`.

## Acceptance checklist

- [ ] Inspect actual working changes and freeze source/package identity; preserve all prior published bytes.
- [ ] Restore/build/test current subject; separate tagged-package 175 tests from main's 176-test follow-up.
- [ ] Run actual owned-loopback WPF/native tests and a current-package >=10-minute simultaneous diagnostic soak; record cadence, overlap, Dispatcher timing, cleanup and memory without inventing thresholds or remote results.
- [ ] Inspect actual WPF at approved sizes/themes and verify history scrolling/column interactions/session/panel behavior.
- [ ] Human-test monitor work area/DPI, keyboard/assistive technology and Windows 10/11, where environments exist.
- [ ] On supplied authorized remote lab targets, verify actual multi-hop/filtered route and TCP/UDP outcomes. Without targets, mark NOT TESTED and continue independent local checks.
- [ ] On explicitly authorized clean VM snapshots, execute real install → upgrade → compatible recovery → uninstall; verify settings/version/restart/trust prompts and retain backup/log evidence.
- [ ] Fix observed in-scope defects with meaningful regressions; keep existing version unless separately instructed to release.
- [ ] Update milestone DoD/status and QA reports with PASS/FAIL/NOT TESTED, precise blockers and artifact paths; do not close gates merely because tests ran.

## Required build/test commands

Use fresh artifact directories; do not publish over frozen `artifacts/github-release/*/final` packages. `.NET 10` may be invoked by PATH or the known user-local SDK. Example PowerShell:

```powershell
$netDotnet = 'C:\Users\PK\.codex\cache\netstucked-tools\dotnet\dotnet.exe'
$qaRun = Join-Path 'artifacts/acceptance' (Get-Date -Format 'yyyyMMdd-HHmmss')
& $netDotnet restore NetStucked.sln
& $netDotnet build NetStucked.sln -c Release --no-restore
& $netDotnet test NetStucked.sln -c Release --no-build --logger trx --results-directory (Join-Path $qaRun 'tests')
& $netDotnet run --project tests/NetStucked.WindowsQa -c Release --no-build -- (Join-Path $qaRun 'windows') 0
& $netDotnet run --project tests/NetStucked.WindowsQa -c Release --no-build -- (Join-Path $qaRun 'soak') 600
```

Check each command's exit code before proceeding. Exact-package testing should copy the QA output into a private runtime, replace its three application assemblies from a fresh publish and pass that publish directory as the optional third harness argument (see DEVELOPMENT.md). `SmokePublished.ps1` exercises a QA-owned isolated executable, not machine installation. For non-Windows/non-ICMP environments use `dotnet test tests/NetStucked.Tests -c Release --filter "Category!=Integration"` and mark excluded scenarios NOT TESTED.

## Ready-to-copy prompt

```text
Finish NetStucked's approved M00 acceptance on current 0.4.0, with the M07
selected-build installation/recovery matrix as the related release gate.
Read docs/CODEX_NEXT_TASK.md and its exact reading list before editing.
Inspect git status and preserve user changes, canonical PNG/scope and frozen releases.
This is QA and established in-scope defect correction, not future feature implementation.
Run restore/build/current tests, owned-loopback WPF/native checks and a >=10-minute
current-subject simultaneous soak. Record real timings/overlap/counters/drain/memory.
Do not count mocks, renders or installer compilation as manual or machine acceptance.
Use an explicitly authorized remote lab and isolated Windows 10/11 VM snapshots for
remote diagnostics and actual installation/upgrade/recovery/uninstall. If unavailable,
complete independent local checks and report exact NOT TESTED prerequisites.
Never replace the working machine's installation or settings as an implied side effect.
Fix only actual approved-scope defects with meaningful regression tests. Preserve MVVM,
async cancellation and target/concurrency bounds. Never fabricate measurements/outcomes.
Keep version 0.4.0 unless a separate release instruction authorizes a change.
Update milestone checkboxes, IMPLEMENTATION_STATUS, QA and CHANGELOG from evidence.
Do not implement Network Info, Dashboard, DNS/HTTP, Wi-Fi, Monitoring, SQLite history,
Terminal, Collector, Inventory, Topology, CVE, device Upgrade Planner or licensing/payments.
Do not commit/push/tag/publish/send external messages without explicit instruction.
Report remaining blockers; do not mark a milestone complete while required gates are open.
```
