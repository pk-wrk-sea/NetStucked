# M03 — Target-oriented health checks and network profiles

## 1. Milestone and version

**M03** / 0.7.0 proposed (brief originally 0.4.0). Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Target-oriented health checks and network profiles. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Concept; UI/scope approval pending; not implemented, tested or released. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need reusable multi-protocol checks while retaining the cause and evidence of each finding.

## 5. Scope and non-goals

Scope: Named diagnostic profiles and explicit per-target check runs using approved ICMP/TCP/DNS/HTTP tools; explain aggregate results.

Non-goals: No PC health score, fabricated certainty, incident monitoring or automatic remediation.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Define profile target/check/timeout/expected-result rules and show exact authorized scope before Start.
- [ ] Execute independent bounded checks and preserve per-protocol raw outcomes/nullable timings.
- [ ] Report reachability, DNS, service and TLS evidence separately; a failed Ping cannot make the whole target definitively offline.
- [ ] Save/edit/clone/delete profiles and export actual run results with profile version and timestamps.

## 7. UI and UX

BLOCKED pending approved target/check editor and dense results design. Reuse current table/panel theme; show reasons rather than decorative health scores.

## 8. Architecture and module ownership

Propose Core profile/run models and composition coordinator; Infrastructure existing approved adapters; Desktop profile editor/run VM/View. Aggregate policy is testable outside WPF.

## 9. Data models and persistence

Stable ProfileId/version, target/check definitions, run/session IDs and evidence references. JSON may hold small profiles; persistent run history uses M05 contracts.

## 10. Dependencies and prerequisites

M00 and implemented M02 tools needed by the selected profile. Approve aggregation semantics and schema/backwards compatibility before shipping. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

No credentials embedded in shared profiles; preview authorized targets and prohibit profile import from executing network work automatically. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Global/per-target bounds and deadlines; one run owner per target/check; reuse adapters rather than spawn a new scheduler per UI row.

## 13. Edge cases

Mixed pass/fail/unknown, disabled check, missing tool capability, stale profile, malformed import, pause/cancel after partial results and identity changes.

## 14. Unit and integration tests

Fixture-test aggregation truth tables and imported profiles; real owned services exercise mixed outcomes, cancellation and nonoverlap. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Run a profile with ICMP filtered but a reachable owned TCP/HTTP service; ensure independent evidence remains visible.
- [ ] Edit/import a profile without executing it, then run/cancel/restart and export exact results.

## 16. Definition of Done

- [ ] Profile schema, outcome policy and UI approved.
- [ ] Composition uses only approved tools with scope/cancellation bounds.
- [ ] Truth-table/import and owned-service tests pass.
- [ ] Human results/export acceptance and compatibility documented.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Dependent tools and aggregate semantics are unapproved; universal healthy/unhealthy labels would overstate measurements.

## 19. Approval requirements

Approve profile scope/UI and each referenced tool; no automatic remediation or monitoring authority.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked M03: Target-oriented health checks and network profiles.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/M03_HEALTH_PROFILES.md, docs/IMPLEMENTATION_STATUS.md,
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
