# M07 — GitHub selected-build installation and recovery acceptance

## 1. Milestone and version

**M07** / Implemented in 0.3.0+; remaining validation on 0.4.0 (brief proposed 0.8.0). Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

GitHub selected-build installation and recovery acceptance. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Approved explicit-click scope; implemented/released. Build/unit/public download/helper checks PASS. Actual machine install/upgrade/recovery/uninstall NOT TESTED; milestone acceptance OPEN. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need to install/recover a chosen project build while protecting compatible settings and preserving Windows trust prompts.

## 5. Scope and non-goals

Scope: Existing stable-SemVer catalog/notes, streamed verified download/progress/cancel, explicit unsigned acknowledgement, backup/helper/awaited diagnostics shutdown/Inno/restart; close real-machine acceptance gaps.

Non-goals: No unattended/background updates, token storage, arbitrary downloaded programs, signature bypass, implicit schema compatibility or transactional Program Files recovery claim.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Retain fixed GitHub project/HTTPS asset identity, size <=256 MiB, SHA-256, product/version and Windows trust checks with Mark of the Web.
- [ ] Keep Install/Reinstall/Recover based on selected actual build; drafts/prereleases/incomplete assets cannot execute.
- [ ] Wait diagnostic/metadata cleanup, settings save and real isolated-helper acknowledgement before parent exit.
- [ ] Back up exact compatible settings, reverify before Setup, await Setup, verify installed version and restart with original user token.
- [ ] Exercise cancel/failure/3010/other-instance/restart/backup paths; preserve installed package/job evidence and assess previous-installer offline preservation explicitly.

## 7. UI and UX

Current Updates UI approved; preserve bottom Settings/Updates group and dynamic action. Do not promise that one click bypasses acknowledgement/UAC/SmartScreen.

## 8. Architecture and module ownership

Existing Core Updates/Releases contracts, Infrastructure GitHubReleaseSource/InstallerDownloader/WindowsUpdates/UpdateFiles/UpdateRunner, Desktop UpdatesViewModel/View and App helper mode.

## 9. Data models and persistence

Schema-1 settings backups with version/date/hash; <=20 verified backups, two latest prior completed jobs plus new job; unfinished jobs retained. Current cache is bounded by job retention, not guaranteed offline archival of every old installer.

## 10. Dependencies and prerequisites

M00 shutdown/settings foundation, real 0.3.0/0.3.1/0.4.0 packages, isolated clean Windows 10/11 VM and machine-test authority. Future M05 needs new migration/downgrade tests. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Reject invalid signatures; unsigned packages require explicit acknowledgement. Preserve elevation/prompts, fixed arguments, path/link validation and original user-token restart. Never use unrelated signing keys. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Stream/bound downloads and cancellation; one update mutex per data path; wait setup without force killing. Measure shutdown drainage without overlap or closing unrelated processes.

## 13. Edge cases

Offline/rate limit, asset changes/missing digest, corrupt download/settings, trust failure, UAC cancel, concurrent app, disk full, restart-required, partial Setup failure and absent matching backup.

## 14. Unit and integration tests

Current trust/identity/hash/orchestration tests PASS. Preserve actual GitHub/native-helper checks; add meaningful regressions if VM acceptance exposes defects. No mocks used to claim a real installer ran. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] On an isolated VM snapshot install 0.3.0, Upgrade to 0.3.1/0.4.0 and Recover to a compatible 0.3.x build; verify app version, original-token restart and exact settings.
- [ ] Exercise unsigned acknowledgement, SmartScreen/UAC cancellation, interrupted preparation, multiple instances and insufficient space with retained logs.
- [ ] Verify reinstall/uninstall/data retention and actual restart-required/failure behavior where a controlled scenario is available.

## 16. Definition of Done

- [x] Selected-build production implementation and automated integrity/orchestration tests pass.
- [x] Published assets, actual public catalog/download and isolated-helper startup evidence are recorded.
- [ ] Clean-machine upgrade/reinstall/recovery/uninstall and Windows trust interaction matrix passes.
- [ ] Compatible schema/settings preservation and failure recovery are proven on real installed builds.
- [ ] Previous-installer preservation semantics and remaining unsupported scenarios are explicitly accepted/documented.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

No real-machine installer acceptance yet; installers are unsigned and recovery is not transactional. Future SQLite schema compatibility and guaranteed offline previous-installer archival remain unresolved.

## 19. Approval requirements

Existing explicit selected-build actions are Approved. Run installed-app matrix only on authorized isolated machine/snapshot; code signing requires separately authorized publisher credentials. New unattended/offline-archive features need explicit approval.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked M07: GitHub selected-build installation and recovery acceptance.
Close approved acceptance gaps; implement only established in-scope defect fixes.
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/M07_UPDATES_RECOVERY.md, docs/IMPLEMENTATION_STATUS.md,
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
