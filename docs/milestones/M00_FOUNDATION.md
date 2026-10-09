# M00 — Foundation and approved Live Ping / Traceroute

## 1. Milestone and version

**M00** / Historical 0.1.0; retained implementation and remaining acceptance on 0.4.0. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Foundation and approved Live Ping / Traceroute. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Design Approved (canonical PNGs + recorded revisions). Implemented and released through 0.4.0. Fresh build/176 tests PASS; prior WPF/loopback/native checks PASS. Full acceptance remains OPEN. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need accurate simultaneous remote-target probes without waiting for slow targets or freezing the desktop.

## 5. Scope and non-goals

Scope: Existing solution/DI/MVVM, actual Ping and continuous IPv4 ICMP Trace, current compact settings/templates/columns/history/CSV, installer foundation and approved 0.4.0 appearance/session revisions.

Non-goals: No new DNS/HTTP pages, monitoring incidents, Dashboard, IPv6/TCP/UDP trace or persistent-history feature.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Preserve IP/hostname/IPv4 CIDR parsing, per-row descriptions, deduplication and 1,024 desktop-target cap.
- [ ] Keep interval >=250 ms, timeout >=500 ms and payload-only Ping settings; Trace adds Max Hops and continuous polling by default.
- [ ] Prove independent per-target/TTL cadence, no self-overlap, correct completed-attempt counters, real RTT/loss/jitter and missing-response/route-change semantics.
- [ ] Preserve Start/Pause/Resume/Stop/restart, five independently cancellable trace sessions, protected main session and shared hop mappings.
- [ ] Verify sortable/filterable/configurable tables, Fit Columns/resize guide, selected history/CSV and stable manual scroll; retain named templates and destination history.

## 7. UI and UX

Use both canonical PNGs plus UI_REVISION_2026-10-08.md and FEATURES_0.4.0.md. Minimal Light/Dark/System, compact six Ping metrics, no Trace KPIs, one control group, virtualized tables and bottom Settings/Updates. Human DPI/keyboard/accessibility validation is still required.

## 8. Architecture and module ownership

Existing Core MultiTargetPingService / TracerouteMonitoringService / AsyncSession / statistics; Infrastructure IcmpPingProbe / DnsResolver / UserSettingsStore; Desktop Ping/Trace VMs, XAML and view behaviors; DI in App.xaml.cs.

## 9. Data models and persistence

Keep schema-1 JSON preferences. Diagnostic samples are bounded in memory (Ping total sample budget 100,000 with <=500 per target; Trace <=1,000 events). No SQLite requirement for this milestone.

## 10. Dependencies and prerequisites

Existing .NET 10 SDK, Windows QA harness, Inno compiler; clean Windows 10/11 VM, human GUI acceptance and explicitly authorized remote lab needed for remaining gates. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

No probing beyond selected authorized targets. Preserve bounds and actual-API-only telemetry; never treat ICMP filtering/ECMP as proof of outage. Isolate QA data from normal preferences. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Measure independent slow/fast targets, queue/UI timing, cancellation drains and a current-package >=10-minute soak. Record observations rather than invented pass thresholds; existing 60-second 0.4.0 soak is not a long-term plateau.

## 13. Edge cases

IPv6 literal Ping versus IPv4-only Trace, invalid/DNS-only failures, /31-/32, filtered TTLs, changed/alternate routes, missing final reply, pause mid-round, rapid restart, histories filling and monitor work-area transitions.

## 14. Unit and integration tests

Current 176 tests PASS. Add only necessary regressions for discovered defects; use deterministic delayed/filtered/alternate-hop adapters and real owned IPv4/IPv6 loopback ICMP. No fake production results. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Compare actual WPF with approved hierarchy at 1280x800/1536x1024; exercise keyboard, one-second icon hints, column drag/fit/selection and stable history scrolling.
- [ ] On real monitor(s), maximize without taskbar overlap, switch DPI and verify panel/theme behavior; exercise five trace sessions and awaited close.
- [ ] In an authorized remote lab, test multi-hop/filtered route and a permitted small CIDR; record destinations, settings, observations and limitations.
- [ ] On clean Windows 10/11, verify self-contained startup, basic owned diagnostics, installer/uninstaller and settings retention.

## 16. Definition of Done

- [x] Existing implementation and fresh Release build/176 automated tests remain passing.
- [x] Historical exact-package WPF/native/loopback and Inno compilation evidence is linked without being relabeled as manual acceptance.
- [ ] Current-package longer soak and authorized remote-route acceptance are recorded.
- [ ] Human monitor-DPI/accessibility and clean Windows 10/11 acceptance are completed, or explicitly scoped/deferred by the human.
- [ ] Remaining defects are fixed/tested and all required acceptance results are documented before declaring this milestone complete.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Missing clean Windows 11/remote-lab/human test evidence. Some canonical 0.1.0 requirements are superseded only by named approved revisions. No source defect was established by this documentation task.

## 19. Approval requirements

Existing scope is Approved. Regression QA/fixes can remain within it; new features or UI redesign need separate approval. Obtain actual test environment and explicit target/machine authority before destructive installed-app tests.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked M00: Foundation and approved Live Ping / Traceroute.
Close approved acceptance gaps; implement only established in-scope defect fixes.
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/M00_FOUNDATION.md, docs/IMPLEMENTATION_STATUS.md,
docs/UI_APPROVAL_REGISTER.md, docs/ARCHITECTURE.md,
docs/SECURITY_AND_DATA_POLICY.md, docs/FEATURE_DEPENDENCIES.md,
and the directly required milestone documents.
For retained/current UI and release behavior also read:
docs/APPROVED_UI_SCOPE.md, docs/UI_REVISION_2026-10-08.md, docs/FEATURES_0.2.0.md, docs/FEATURES_0.3.0.md, docs/FEATURES_0.4.0.md, docs/QA_REPORT.md.
Inspect git status and the actual owning source/tests before edits. Preserve user changes.
Implement only this milestone's approved slice and its acceptance criteria; do not add
unapproved UI, dependencies, network scope or unrelated future features.
Use Core UI-free contracts, Infrastructure adapters and Desktop MVVM/DI;
async/cancellation, bounded work and actual evidence only. Keep secrets out of files/logs.
Test the listed meaningful unit/integration edge cases. Build and test with .NET 10:
dotnet restore NetStucked.sln
dotnet build NetStucked.sln -c Release --no-restore
dotnet test NetStucked.sln -c Release --no-build --logger trx
If dotnet is not on PATH, use the user-local SDK path recorded in DEVELOPMENT.md.
Run appropriate Windows/installer checks only on authorized owned targets/machines,
with isolated QA data and fresh artifact directories; never execute an installer on the
working machine as an implied side effect. Record PASS/FAIL/NOT TESTED precisely.
Update milestone checkboxes/status/CHANGELOG/QA only from actual evidence.
Do not bump release version, commit, push, tag, publish, send alerts or activate payments
without explicit instruction. Preserve canonical references and frozen releases.
```
