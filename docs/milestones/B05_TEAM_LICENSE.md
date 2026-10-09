# B05 — Team / Enterprise commercial licensing

## 1. Milestone and version

**B05** / Unassigned; commercial research backlog, no scheduled release. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Team / Enterprise commercial licensing. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Future Idea / On Hold by explicit postponement. No implementation, approval of policy, tests or release. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Evaluate organizational seat administration and entitlement recovery while keeping future team monitoring independent.

## 5. Scope and non-goals

Scope: Documentation/research candidate only after fresh explicit authorization. Preserve ideas now.

Non-goals: No billing/activation/entitlement enforcement, payment dependencies, provider selection, live APIs or commercial UI in this task.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Draft per-user/org/seat roles and invitation/recovery/revocation as options.
- [ ] Separate monitoring access authorization from commercial entitlement.
- [ ] Evaluate audit/offline policies and portal integration; choose no enterprise price or contract now.

## 7. UI and UX

No approved UI. Future Account/License settings only; UI implementation BLOCKED and current navigation unchanged.

## 8. Architecture and module ownership

Research proposed entitlement/service/client interfaces only. No production Core/Infrastructure/Desktop module or dependency added.

## 9. Data models and persistence

Proposed OrgId/seat/user-role/entitlement revision/audit; no real users, invitations or subscription enforcement.

## 10. Dependencies and prerequisites

B01–B03 research and explicit Team/Enterprise policy approval; A08 only when linking central-service access. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

No secrets/PII/payment collection now. Future signing/webhook/auth/privacy controls require their own approved design. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Research offline continuity, bounded retries/cache and service failure semantics; no performance result claimed.

## 13. Edge cases

Offline/stale claims, duplicate/replayed requests, cancellation, account/device recovery and inconsistent policy versions.

## 14. Unit and integration tests

NOT TESTED. If research is authorized, define deterministic claim/state/replay/failure contract tests without live payments; implementation requires later authority. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] No Windows product acceptance while On Hold. After separate authorization, validate the approved design using isolated test accounts/sandbox only.

## 16. Definition of Done

- [ ] Human explicitly resumes commercial research; policies/providers remain proposals until reviewed.
- [ ] Research/specification, threat/privacy model and testable acceptance contracts reviewed.
- [ ] Separate future implementation/activation/UI/payment authority obtained; no automatic promotion from research.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Seat/shared-service models and privacy/access governance are unapproved; licensing must not silently grant network access.

## 19. Approval requirements

Fresh explicit authorization is mandatory to resume this postponed scope. Milestone documentation does not authorize implementation, services, payments or activation.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked B05: Team / Enterprise commercial licensing.
This milestone is ON HOLD. Do not implement or activate it; only proceed with research after fresh explicit human authorization.
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/B05_TEAM_LICENSE.md, docs/IMPLEMENTATION_STATUS.md,
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
