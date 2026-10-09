# M08 — Release hardening, security and Windows QA

## 1. Milestone and version

**M08** / 0.11.0 proposed; compatible current-version fixes may be separately authorized 0.4.x. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Release hardening, security and Windows QA. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Required release-quality work, candidate scope/version not committed. Existing checks partial; full stable acceptance/signing/Windows matrix pending. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

A stable toolkit needs measured reliability, trust, compatibility and accessible Windows behavior beyond a passing build.

## 5. Scope and non-goals

Scope: Security review, installer/publisher signing plan, clean Windows 10/11 matrix, accessibility/DPI/manual QA, prolonged concurrent diagnostics and data/recovery compatibility.

Non-goals: No future feature implementation, unrelated keys/system changes, fabricated signature/reputation or unauthorized release/push.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Review real network bounds/cancellation, trust/download/job paths and credential-free logs for current scope.
- [ ] Define and execute clean installation/upgrade/downgrade/uninstall, multi-user/permission and interruption matrix.
- [ ] Measure longer memory/Dispatcher/cadence soak and confirm no same-target/TTL overlap; fix only established defects.
- [ ] Complete human keyboard/screen-reader/monitor-DPI/theme/dialog acceptance and dependency/licensing review.
- [ ] Establish an authorized Authenticode publisher/timestamping process and verify signed final packages when available.

## 7. UI and UX

Existing layouts/themes are Approved; fixes preserve them. New presentation/scope changes need approval. Human accessibility and real monitor transitions cannot be substituted by bitmap DPI renders.

## 8. Architecture and module ownership

Existing projects; tests/WindowsQa/scripts/installer/CI own measurable verification; production changes remain in responsible Core/Infrastructure/Desktop module and are regression-tested.

## 9. Data models and persistence

Immutable source/package hashes and test evidence; isolated QA settings/backups; current/future schema compatibility matrix. No production data used for fault injection.

## 10. Dependencies and prerequisites

M00/M07 acceptance; committed features selected for stable release; clean Windows VM/lab and separately authorized signing setup. M01–M06 candidates must be accepted or explicitly deferred. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Threat-model target scope, imports/export, installer trust/links/tokens and future raw config handling. Do not access signing secrets unless explicitly authorized. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Record numerical timing/heap/request limits under repeatable workloads; agree release criteria rather than claim zero jitter or infer long-term plateau from a short soak.

## 13. Edge cases

Sleep/resume/offline, disk/permission failures, malformed preferences/imports, many targets, concurrent tools, update handoff, Windows version and multi-monitor differences.

## 14. Unit and integration tests

Run current automated suite, relevant regression/fault/compatibility tests, exact-package WPF/native harness, long soak and installer compilation; preserve original failed artifacts. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Complete Windows 10/11 clean VM acceptance with actual installer/recovery/UAC and data retention.
- [ ] Human verify monitor taskbar/DPI, keyboard/assistive technology, dark popups and table interaction.
- [ ] Exercise explicitly authorized remote ICMP/TCP/UDP/multi-hop scenarios and record limitations.

## 16. Definition of Done

- [ ] Release scope/matrix and measurable acceptance criteria agreed.
- [ ] Required automated/native/remote/manual checks pass or limitations explicitly accepted.
- [ ] Publisher signing is implemented/verified, or any development-only unsigned status is explicitly scoped.
- [ ] Security/compatibility review findings resolved and exact final artifacts frozen.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Missing Windows 11/clean-install/authorized remote-lab evidence and publisher signing. No guarantee SmartScreen reputation follows immediately from signing.

## 19. Approval requirements

Approve stable scope and signing process; human approval is required for new UI/feature changes and publication, not routine reversible QA fixes.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked M08: Release hardening, security and Windows QA.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/M08_RELEASE_HARDENING.md, docs/IMPLEMENTATION_STATUS.md,
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
