# A07 — Engineer-reviewed device Upgrade Planner

## 1. Milestone and version

**A07** / Post-1.0.0 candidate; exact 1.x version unassigned. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Engineer-reviewed device Upgrade Planner. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Concept; no implemented plan/recommendation/reports or approved UI. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need evidence-backed compatible maintenance plans that remain distinct from automatic device execution.

## 5. Scope and non-goals

Scope: CVE/KEV prioritization, official first-fixed evidence, compatible engineer-approved target, change owner/ID/waves/windows, before/after verification/reassessment and Excel/CSV/HTML plans.

Non-goals: No automatic firmware/configuration deployment, universal safe-release recommendations or inferred maintenance approval.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Show per-device advisory severity/KEV/evidence/applicability/freshness from A06.
- [ ] Select candidate target release with official fixed/compatibility evidence; record Engineer approval before marking recommended/approved.
- [ ] Plan waves/windows/owner/ChangeId and dependencies with explicit draft/review/approved states.
- [ ] Compare observed before/after software and re-assess advisory applicability after engineer-operated upgrade.
- [ ] Export versioned Excel/CSV/HTML change plans with reasons/evidence and pending-data limitations.

## 7. UI and UX

BLOCKED pending plan/editor/approval/evidence design; keep NetStucked application Updates separate from network-device upgrade plans.

## 8. Architecture and module ownership

Propose Core plan/approval/compatibility models and IUpgradePlanStore; Infrastructure evidence/report adapters; Desktop planner VMs/Views consuming inventory/assessment interfaces.

## 9. Data models and persistence

PlanId/revision/AssetId, advisory assessment revision, official target compatibility/fixed evidence, engineer approval, owner/ChangeId/wave/window and before/after observations.

## 10. Dependencies and prerequisites

A03 inventory and A06 reviewed assessments; optional A04 refresh evidence; approved compatibility/Engineer workflow and report/UI design. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Plans authorize no device commands; protect asset/change information in exports. Never promote a draft target to approved without human Engineer approval. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound plan size/paged findings/report generation; cache by assessment revision and invalidate when facts/advisories change.

## 13. Edge cases

Unsupported platform/train, stale findings, changed target evidence, conflicting change windows, unsuccessful upgrade and insufficient after facts.

## 14. Unit and integration tests

Approval-state/compatibility/stale-plan tests and export escaping/schema round trips; fixtures distinguish supported from unknown fixed release. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Review a lab plan, choose/approve supported target evidence, assign wave/window and export.
- [ ] Import real post-change observations and verify mismatch/reassessment without issuing any upgrade commands.

## 16. Definition of Done

- [ ] Planner UI/Engineer approval and compatibility policy approved.
- [ ] Evidence-linked plans/states/exports implemented without device execution.
- [ ] Approval/staleness/compatibility/export tests pass.
- [ ] Engineer verifies before/after/reassessment and actual source-backed recommendations.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

No assessment/compatibility corpus or approval workflow. Application update/recovery must not be confused with router firmware changes.

## 19. Approval requirements

Approve planner design and each target recommendation; execution of device changes remains outside this milestone.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked A07: Engineer-reviewed device Upgrade Planner.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/A07_UPGRADE_PLANNER.md, docs/IMPLEMENTATION_STATUS.md,
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
