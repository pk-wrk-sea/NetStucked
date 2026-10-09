# M06 — Operational Dashboard, notifications and UI refinement

## 1. Milestone and version

**M06** / 0.10.0 proposed (brief originally 0.7.0). Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Operational Dashboard, notifications and UI refinement. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Concept; Dashboard explicitly postponed and currently a placeholder. No final approved design or functional verification. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need a compact overview of actual diagnostic/monitoring work without a local-PC health dashboard.

## 5. Scope and non-goals

Scope: Approved operational summary and navigation shortcuts based on actual tool/session/incident/history data; shared notification presentation refinements only if approved.

Non-goals: No speculative widgets/health scores, fabricated measurements, oversized decorative KPIs or changing approved pages without authority.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Select compact useful summary fields and drill-down actions only after UI approval.
- [ ] Show actual active/session/incident counts with source/freshness and unknown/no-data states.
- [ ] Reuse notification/maintenance semantics and avoid duplicating probe Start controls.
- [ ] Preserve cached pages, active indicators, existing Light/Dark/System and accessibility behavior.

## 7. UI and UX

BLOCKED until Dashboard widgets/layout are explicitly approved. Original canonical Dashboard is TBD. This document does not approve a new mockup.

## 8. Architecture and module ownership

Propose Core read-only summary projections over approved services/stores; Desktop DashboardViewModel/View; Infrastructure supplies no new probes solely for rendering widgets.

## 9. Data models and persistence

Read-only aggregate snapshot with revision/freshness; small preference state only. Historical summaries come from M05, incidents from M04.

## 10. Dependencies and prerequisites

M00 shell; approved Dashboard design; M04/M05 for widgets that actually depend on them. Do not require unrelated future features to render no-data correctly. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

No hidden network scans or new credentials. Avoid exposing sensitive target/incident details through unapproved notifications. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Coalesce visible snapshot updates, stop hidden timers, virtualize lists and measure navigation with simultaneous existing tools.

## 13. Edge cases

No sessions/history, missing module, stale monitoring, many incidents, hidden page, theme/DPI changes and sort/edit conflicts.

## 14. Unit and integration tests

Snapshot projection tests for no-data/stale/count states; WPF bindings/navigation/visibility tests and existing real-loopback regression. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Compare actual approved Dashboard at narrow/wide windows and themes.
- [ ] Navigate during simultaneous diagnostics; verify actual active/freshness state and keyboard/screen-reader descriptions.

## 16. Definition of Done

- [ ] Dashboard design/fields and notification refinements approved.
- [ ] Actual-data projections implemented without fake samples or hidden probes.
- [ ] Binding/projection/performance regressions pass.
- [ ] Human compact-layout/accessibility acceptance documented.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

No approved Dashboard. Proposed roadmap does not overturn explicit postponement or authorize decorative widgets.

## 19. Approval requirements

Explicit Dashboard/UI approval or a narrowly authorized functional-only prototype is required.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked M06: Operational Dashboard, notifications and UI refinement.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/M06_DASHBOARD.md, docs/IMPLEMENTATION_STATUS.md,
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
