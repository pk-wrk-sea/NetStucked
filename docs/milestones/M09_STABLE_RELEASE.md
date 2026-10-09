# M09 — First stable Windows release

## 1. Milestone and version

**M09** / 1.0.0 target; not released. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

First stable Windows release. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Future stable target. No complete stable Definition of Done; no authority to tag/publish 1.0.0 in this task. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Users need a trustworthy versioned installer and a supportable documented toolkit with tested update/recovery.

## 5. Scope and non-goals

Scope: Freeze the explicitly approved stable feature set, package signed/self-contained Windows x64 artifacts, release notes/support/install docs and tested compatible update path.

Non-goals: No automatic inclusion of every candidate, post-stable module, billing, unsupported protocol or untested upgrade claims.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Confirm which M01–M06 candidate features are committed versus explicitly deferred; record final supported scope.
- [ ] Close M00/M07/M08 required acceptance and remaining security/data compatibility findings.
- [ ] Include the already-implemented update client so a subsequent authorized 1.0.1 can update the stable install.
- [ ] Freeze source/version/notes/package manifests and verify exact installer/portable bytes and clean-machine behavior.
- [ ] Publish only under explicit release authority with support limitations and rollback compatibility.

## 7. UI and UX

Only approved final screens/themes/assets; stable does not approve pending mockups. Human accessibility/DPI acceptance required for chosen scope.

## 8. Architecture and module ownership

Existing architecture plus only actually approved/implemented milestone modules; scripts/CI/installer produce immutable packages from reviewed source.

## 9. Data models and persistence

Versioned schema compatibility/backup/migration ledger and installation recovery policy; immutable package/evidence manifest. No untested old-version data-read assumption.

## 10. Dependencies and prerequisites

Accepted chosen scope, M00/M07/M08 closure, publisher signing, Windows matrix, public release infrastructure and explicit publish instruction. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Verified publisher/integrity, no embedded secrets, documented local data/permissions and supported recovery. Final package vulnerability/licensing review is time-specific. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Release criteria come from M08 measurements under the supported target/concurrency bounds, not marketing estimates.

## 13. Edge cases

First install versus portable, future patch upgrade, older compatible recovery, offline checks, user cancellation and schema incompatibility.

## 14. Unit and integration tests

Full relevant automated suite, exact tagged self-contained/native/WPF validation, trusted installer compilation and signed bytes verification; stage a test update only with authorization. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Install/upgrade/recover/uninstall exact stable candidates on clean Windows 10/11 with settings compatibility.
- [ ] Validate actual supported remote diagnostics and human accessibility; inspect notes and on-demand update catalog.
- [ ] Confirm fresh install already contains update client and unsupported recovery fails clearly.

## 16. Definition of Done

- [ ] Stable feature list and all required acceptance/security/data gates accepted.
- [ ] Exact 1.0.0 source/package/signature/notes frozen and verified.
- [ ] Clean-machine install/update/recovery and current update client tested.
- [ ] Human explicitly authorizes publication and durable release report records actual outcomes.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Many roadmap features lack approval; 0.x release success alone does not make 1.0.0 ready. Publisher/environment prerequisites remain open.

## 19. Approval requirements

Approve stable scope and final release separately. Never infer publish/tag authority from a version proposal or passing tests.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked M09: First stable Windows release.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/M09_STABLE_RELEASE.md, docs/IMPLEMENTATION_STATUS.md,
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
