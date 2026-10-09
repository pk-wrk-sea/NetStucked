# M04 — Local monitoring, incidents and alerts

## 1. Milestone and version

**M04** / 0.8.0 proposed (brief originally 0.5.0). Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Local monitoring, incidents and alerts. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Mockup — Awaiting Approval, reported but not located. Selected requirements retained; not implemented/tested/released. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

NOC engineers need bounded recurring checks and deduplicated incidents for critical remote devices while the desktop runs.

## 5. Scope and non-goals

Scope: Local grouped ICMP with TCP secondary evidence, thresholds/recovery, incidents, cooldown/maintenance, tray, notification/sound and a hybrid-ready execution contract.

Non-goals: No 24/7 guarantee after process exit/sleep, central collector deployment, automatic remediation or alerts to external people/services.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Model Healthy/Suspected/Incident/Recovering/Maintenance transitions from explicit approved failure/recovery thresholds and secondary outcomes.
- [ ] Track actual availability/latency and deduplicated incident start/end/evidence; maintain groups and intervals/timeouts.
- [ ] Suppress alerts during maintenance and apply cooldown/deduplication across repeated failures.
- [ ] Provide local Windows notifications/optional sound and explicit tray/background-close behavior.
- [ ] Report monitoring pauses/gaps on sleep/exit; define interfaces that later A08 can implement centrally.

## 7. UI and UX

UI implementation BLOCKED until the actual mockup/scope is approved. Compact device table, maintenance controls and visible process-lifetime monitoring status; no fake always-on status.

## 8. Architecture and module ownership

Propose Core monitor policy/state machine/collector contract; Infrastructure existing probes, notification/tray adapters and incident store; Desktop monitor/group/incident VMs/Views.

## 9. Data models and persistence

Stable MonitorId/DeviceId, check policy, transition evidence, IncidentId, acknowledgement/maintenance and observation gaps. M05-compatible SQLite incident store for durable history.

## 10. Dependencies and prerequisites

Existing ICMP/TCP engines; approved monitoring mockup and alert/close policy; versioned incident persistence contract coordinated with M05. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Monitor only explicit target groups. Local notifications contain no secrets; external shared alerts require later approval. Sleeping/closed apps never claim active collection. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

One active attempt per check, bounded global rate/history and nonblocking notification delivery. Stress thresholds and large approved scopes with test doubles before real networks.

## 13. Edge cases

ICMP filtered/TCP reachable, flap storms, notification denied, maintenance overlap, clock changes, sleep/resume, app crash/restart and duplicate delivery.

## 14. Unit and integration tests

Deterministic state/time fixtures for thresholds/deduplication/maintenance/gaps; owned listeners validate secondary evidence; persistence tests restore incident state safely. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Induce owned-service failure/recovery and verify one incident/alert per policy.
- [ ] Exercise maintenance, notification/sound, tray/close and Windows sleep/resume; verify gaps and no 24/7 claim.
- [ ] Restart and verify persisted incident history without duplicate recovery notifications.

## 16. Definition of Done

- [ ] Mockup and monitor/alert/close policies approved.
- [ ] Bounded engine/state machine and persistent incident schema implemented.
- [ ] Threshold/dedupe/gap and real secondary-check tests pass.
- [ ] Windows tray/notification/sleep acceptance and hybrid boundary documented.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Missing mockup/approval, notification/tray UX and persistence design. Process-lifetime monitoring must not be marketed as an unattended service.

## 19. Approval requirements

Approve design, local background/tray behavior and scope. Central service/shared notifications are A08, not implied.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked M04: Local monitoring, incidents and alerts.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/M04_LOCAL_MONITORING.md, docs/IMPLEMENTATION_STATUS.md,
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
