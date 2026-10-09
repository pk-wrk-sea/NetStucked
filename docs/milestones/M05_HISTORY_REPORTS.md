# M05 — SQLite history, retention and reports

## 1. Milestone and version

**M05** / 0.9.0 proposed (brief originally 0.6.0). Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

SQLite history, retention and reports. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Planned; persistent data/UI/schema decisions pending. Existing in-memory history/CSV is only a subset; SQLite not implemented. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need durable searchable diagnostic/incident evidence without unbounded memory or unsafe downgrade behavior.

## 5. Scope and non-goals

Scope: Versioned SQLite diagnostic sessions/samples/incidents, retention, history search/filter and actual CSV/JSON/HTML reports; extension contracts for future Collector/device history.

Non-goals: No automatic remote database, vendor configuration collection, invented historical data or commercial analytics.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Persist session/probe/check identities and real results asynchronously with bounded queues and explicit write failures.
- [ ] Provide paged session/history queries, timestamps, target/outcome filters and bounded detail loading.
- [ ] Enforce retention/storage limits and export truthful CSV/JSON/HTML with formula/HTML escaping.
- [ ] Implement versioned migration, compatible backup and tested downgrade strategy before updating from/to database builds.
- [ ] Keep current preference/column/template compatibility; future modules use narrow store interfaces.

## 7. UI and UX

History/Reports UI BLOCKED pending approval/prototype authorization. Dense paged tables and compact export/retention controls; do not silently replace existing live history behavior.

## 8. Architecture and module ownership

Propose Core IHistoryStore/IIncidentStore contracts and event records; Infrastructure SQLite migrations/single writer/queries; Desktop history/report VMs/XAML. Core never owns WPF or a concrete database connection.

## 9. Data models and persistence

Schema version/migration ledger, SessionId/ProbeId/IncidentId, nullable RTT/outcome/time/target metadata, source version and retention policy. Specify transactional backup and old-schema restore rules.

## 10. Dependencies and prerequisites

M00 events; M04 incident contract when included; M07 compatibility design and approved SQLite dependency/license review; History/Reports UI approval. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Per-user ACLs, no credentials in history. Protect exported network details, validate output paths and escape CSV formulas/HTML; avoid loading an untrusted database as executable configuration. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound writer queue/batches and query pages; keep all writes/queries off Dispatcher, measure disk growth/retention and cancellation. Define loss/backpressure policy explicitly.

## 13. Edge cases

Disk full/locked/corrupt DB, abrupt shutdown/WAL, migration failure, huge query/export, changed timezone, same IP across assets and old-version rollback.

## 14. Unit and integration tests

Temporary real SQLite integration tests for migration/rollback/atomic backup/retention/corruption and report escaping; delayed writer tests prove UI/probe independence. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Run owned diagnostics, restart and recover exact searchable history; exercise retention/export.
- [ ] Simulate disk failure and interrupted migration in an isolated data directory.
- [ ] On VM test upgrade and compatible downgrade with old/new schemas before claiming Recovery support.

## 16. Definition of Done

- [ ] Schema, migration/downgrade policy, dependency and UI approved.
- [ ] Durable bounded writer/query/export implementation and tests pass.
- [ ] Disk failure/interruption and schema-compatible recovery are verified.
- [ ] Human history/report and storage-retention acceptance documented.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Current updater only supports schema-1 JSON; SQLite cannot inherit a compatibility claim. UI, retention and migration decisions are open.

## 19. Approval requirements

Approve persistent-data scope/schema/UI and destructive retention behavior; preserve existing files until migrations/backup are verified.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked M05: SQLite history, retention and reports.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/M05_HISTORY_REPORTS.md, docs/IMPLEMENTATION_STATUS.md,
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
