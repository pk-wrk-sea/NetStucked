# A03 — MA Inventory and versioned Excel import/export

## 1. Milestone and version

**A03** / Post-1.0.0 candidate; exact 1.x version unassigned. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

MA Inventory and versioned Excel import/export. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Concept; no approved UI/schema or implementation/test/release. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need stable asset/contract identity and reviewable reconciliation between human records and observed device facts.

## 5. Scope and non-goals

Scope: Asset/MA CRUD and search/filter/sort, stable IDs, contract dates/status, observed software/time, versioned Excel templates/header/duplicates/preview/errors/export and reviewed Collector reconciliation.

Non-goals: No IP-based primary identity, silent import commit, automatic human-value overwrite or licensing/billing.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Use stable AssetId with hostname/IP/vendor/model/serial/site/location/role and contract/MA start/end fields.
- [ ] Validate versioned workbook schema/headers/types, dates and duplicates before showing import preview/error report.
- [ ] Apply selected valid rows atomically with explicit reconciliation choices; retain provenance and audit of human edits.
- [ ] Display observed software/version/last collection separately from manually entered fields; export a round-trip template.

## 7. UI and UX

BLOCKED pending asset/table/import-preview/reconciliation design. Dense operational fields and explicit conflict choices.

## 8. Architecture and module ownership

Propose Core asset/contract/provenance/import records and IAssetStore; Infrastructure SQLite and workbook adapter; Desktop inventory/import-preview VMs/XAML. Collector consumers use interfaces.

## 9. Data models and persistence

Stable AssetId, alternate identifiers/management addresses, MA dates/contract, human vs observed values, provenance/change history, template schema and import-batch ID.

## 10. Dependencies and prerequisites

M05 storage/compatibility patterns, approved schema/UI and Excel dependency choice; A02/A04 only required for observed-fact synchronization, not standalone manual assets. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Treat workbook input as untrusted; no macro execution. Validate sizes/formulas/dates, protect asset details and escape export formulas. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound workbook/row count, stream/page imports/queries, validate off Dispatcher and apply transactions with cancellation before commit.

## 13. Edge cases

Same IP on multiple sites, replaced device/same hostname, serial/model conflict, duplicate AssetId, expired/overlapping contracts, locale dates and unknown template version.

## 14. Unit and integration tests

Real workbook schema/header/date/duplicate/preview round trips, temporary SQLite atomic commit/rollback and reconciliation provenance tests. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Import a lab workbook with valid/errors/duplicates, preview without mutation and apply selected rows.
- [ ] Reconcile conflicting collected facts without overwriting human fields; verify search/MA status/export.

## 16. Definition of Done

- [ ] Asset/MA schema and import/reconciliation UI approved.
- [ ] Stable identity and versioned preview/transactional import/export implemented.
- [ ] Workbook/storage/conflict tests pass.
- [ ] Human reconciliation and data compatibility acceptance documented.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

No stable asset schema/UX or workbook component selected. IPs alone cannot establish durable identity.

## 19. Approval requirements

Approve UI/schema/import limits and conflict resolution; destructive bulk delete/replacement must be explicit.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked A03: MA Inventory and versioned Excel import/export.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/A03_MA_INVENTORY.md, docs/IMPLEMENTATION_STATUS.md,
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
