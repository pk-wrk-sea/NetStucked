# A06 — Cisco CVE/advisory assessment and metadata sync

## 1. Milestone and version

**A06** / Post-1.0.0 candidate; exact 1.x version unassigned. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Cisco CVE/advisory assessment and metadata sync. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Concept; advisory/API/applicability design not approved or implemented. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need source-backed vulnerability applicability decisions for actual devices without false safe/affected claims.

## 5. Scope and non-goals

Scope: Cisco-first local assessment data, candidate official advisory metadata/manual-online sync, version/config/product/freshness inputs and traceable result states.

Non-goals: No version-string-only vulnerability truth, invented fixed releases, universal safe-version labels, full-config upload or automatic device upgrade.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Evaluate Cisco PSIRT/OpenVuln/Software Checker/official advisories and NVD/CISA KEV availability/access/licensing at implementation time.
- [ ] Map validated vendor/product/model/OS/version/config conditions with collection/advisory freshness and source evidence.
- [ ] Return Affected, Not Affected by evaluated advisory, Needs Review, Insufficient Data or Stale Assessment with reasons.
- [ ] Synchronize external advisory metadata only under approved online/manual policy; local assessments retain source revision/time.
- [ ] Use official vendor evidence for first-fixed candidates; do not recommend a universally safe train.

## 7. UI and UX

Assessment/findings/detail/sync UI BLOCKED pending approval; show source links/conditions/uncertainty/freshness alongside severity.

## 8. Architecture and module ownership

Propose Core assessment/applicability/evidence contracts, Infrastructure IAdvisorySource/cache and Cisco adapters, Desktop assessment/sync VMs/Views. Decouple from Collector/Topology via fact interfaces.

## 9. Data models and persistence

Advisory ID/source/revision/retrieved time/conditions/fixed evidence, AssetId/fact revision/config applicability, assessment state/reasons/freshness and synchronization ledger.

## 10. Dependencies and prerequisites

A03/A04 validated asset facts and raw evidence references; approved assessment/schema/UI; verified provider access/limits and metadata-only data policy. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Never upload full configurations by default; protect API credentials if later required. Treat external advisory text/links as untrusted data and retain original provenance. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound sync/HTTP/cache/retries with rate limiting and cancellation; compute local applicability off Dispatcher and paginate findings.

## 13. Edge cases

Missing platform mapping, incomparable train versions, advisory revisions, config unknown, offline/stale data, KEV changes and contradictory fixed-release sources.

## 14. Unit and integration tests

Official sanitized advisory fixtures, version/applicability truth tables, insufficient/stale states and HTTP throttling/cancellation; no fabricated production findings. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Against authorized Cisco lab facts, review each classification with original advisory and configuration conditions.
- [ ] Refresh then work offline; verify stale state and no config upload; inspect first-fixed source links.

## 16. Definition of Done

- [ ] Source/access/licensing, applicability policy and UI approved.
- [ ] Metadata sync and evidence-aware local assessment implemented.
- [ ] Truth-table/staleness/API-failure tests pass.
- [ ] Engineer verifies classifications/fixed evidence; no unsupported safe/version-only claims.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

No API contracts/access review or accurate applicability corpus. Device version alone is insufficient; online metadata is time-sensitive.

## 19. Approval requirements

Approve metadata-only online sync and assessment UI/policy; external credentials require explicit authorization.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked A06: Cisco CVE/advisory assessment and metadata sync.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/A06_CVE_ASSESSMENT.md, docs/IMPLEMENTATION_STATUS.md,
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
