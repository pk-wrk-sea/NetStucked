# A08 — Central collector and shared team monitoring

## 1. Milestone and version

**A08** / Post-1.0.0 candidate; exact 1.x version unassigned. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Central collector and shared team monitoring. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Future candidate; no approved architecture/protocol/deployment/UI or implementation. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Teams need collection that can persist beyond one desktop process and share actual incidents safely.

## 5. Scope and non-goals

Scope: Separate persistent collector execution of approved monitoring policies, authenticated desktop API and explicit shared incident/alert delivery.

Non-goals: No claim local monitoring becomes 24/7 automatically; no cloud/service deployment, remote messaging or credentials without authorization.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Reuse versioned M04 policy/observation/incident contracts with explicit collector ownership and heartbeat/gap state.
- [ ] Define authenticated transport/device authorization, deduplication and desktop/offline behavior.
- [ ] Persist monitoring on an actually deployed service independent of desktop lifetime.
- [ ] Provide shared incident/alert access only for approved teams/channels and audit configuration changes.

## 7. UI and UX

Collector status/team/endpoint configuration UI BLOCKED pending design; clearly distinguish local versus central evidence and freshness.

## 8. Architecture and module ownership

Propose separate service project/process plus versioned Core contracts, secured Infrastructure API/client and Desktop connection/status views; no service framework added during planning.

## 9. Data models and persistence

CollectorId/DeviceId/policy revision, leases/heartbeats/observations/gaps/incidents, tenant/team access/audit and compatible service/desktop schema versions.

## 10. Dependencies and prerequisites

M04 accepted local state machine, M05-compatible persistence, approved deployment/auth/alert and operations ownership; stable release baseline. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Explicit service target authority, least privilege, authenticated encrypted API and secret management. External alert sending requires explicit channel/recipient authorization. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound service queues/rate/retention and recover ownership without duplicate probes/alerts; measure disconnected clients/backpressure at approved scales.

## 13. Edge cases

Collector crash/network partition/clock skew, stale heartbeat, duplicate lease, old desktop protocol, alert outage and unauthorized endpoint.

## 14. Unit and integration tests

Service/client protocol and auth tests, durable incident dedupe/restart/failover fixtures and isolated deployment integration. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Deploy only to an authorized lab; close desktop and verify collector continues actual monitoring.
- [ ] Disconnect/restart service and validate gap/ownership/access controls and authorized shared alerts.

## 16. Definition of Done

- [ ] Separate service/deployment/protocol/auth/alert scope approved.
- [ ] Durable independent collection and secured client implemented.
- [ ] Partition/restart/dedupe/access tests pass.
- [ ] Lab proves desktop-independent operation; production availability claims match real deployment.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

No deployment/auth/operator ownership decision; local desktop cannot provide service continuity after exit/sleep.

## 19. Approval requirements

Explicit new service, deployment, targets and shared-alert recipients approval required; local milestone does not confer it.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked A08: Central collector and shared team monitoring.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/A08_CENTRAL_COLLECTOR.md, docs/IMPLEMENTATION_STATUS.md,
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
