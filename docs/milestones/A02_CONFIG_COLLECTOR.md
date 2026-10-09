# A02 — Cisco Config Collector

## 1. Milestone and version

**A02** / Post-1.0.0 candidate; exact 1.x version unassigned. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Cisco Config Collector. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Mockup v2 — Awaiting Approval, reported but not located. Selected Cisco-only/no-scheduling constraints retained; not implemented. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need evidence-preserving collection from pasted device lists without manually opening every terminal.

## 5. Scope and non-goals

Scope: Cisco IOS/IOS XE/NX-OS/ASA SSH collection, multiline targets/import/inventory/groups, protected credential profiles/bounded fallback, parallel timeouts/retries, command templates, raw backups/results/logs/details/viewer/diff/export and observed-fact consumers.

Non-goals: Scheduled collection explicitly excluded; no arbitrary configuration changes, multi-vendor promise or silent human-inventory overwrite.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Match Live Ping-style multiline Host/IP plus optional description input; preview validated authorized devices and group/Select All scope.
- [ ] Read show running-config/show version/show inventory and approved operational templates with vendor/prompt/paging handling.
- [ ] Bound concurrent devices/timeouts/retries and authentication fallback to avoid lockout; await cancel and preserve completed evidence.
- [ ] Keep raw outputs/backups separate from parsed model/serial/software facts; expose per-device outcomes/logs/progress/diff/export.
- [ ] Provide stable evidence/fact contracts for Inventory/CVE; protect configuration contents.

## 7. UI and UX

UI BLOCKED until Collector v2 approved. Targets must resemble approved Live Ping panel, with dense results/logs and configuration detail/diff; do not invent v2 design.

## 8. Architecture and module ownership

Propose Core collection job/device/command contracts; Infrastructure shared SSH transport, Cisco command templates/raw store; Desktop collector VMs/XAML. A04 parsers consume outputs independently.

## 9. Data models and persistence

JobId/AssetId/endpoint/OS family, command/version, raw output hash/path/timestamp/status, credential reference and separate parsed-fact records. Local SQLite indexing + protected backup files.

## 10. Dependencies and prerequisites

Shared verified SSH transport (terminal UI not required), minimal stable Asset contract/A03 subset, protected raw store/M05 patterns, Cisco lab and approved mockup/templates. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Host-key verification, OS-protected credentials/keys, bounded fallback and read-only approved commands. Restrictive ACL/encryption decision and redacted logs before production collection. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound device concurrency, output bytes/job retention and per-command timeouts; handle paging without hanging dispatcher or retry storms.

## 13. Edge cases

IOS/IOS XE/NX-OS/ASA prompt/paging differences, authorization denied, enable-context requirement, huge configs, partial output, failover identity and changed IP.

## 14. Unit and integration tests

Sanitized captured-output prompt/paging fixtures, bounded auth/retry/cancel tests, real owned SSH Cisco lab integration; parser evidence tested separately. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Paste multiple authorized lab devices/descriptions, preview and collect approved commands; validate raw bytes/outcomes.
- [ ] Induce auth/timeout/paging/partial failures, cancel and inspect redacted logs/protected backups.
- [ ] Review configuration diff/export and explicitly approve any inventory reconciliation.

## 16. Definition of Done

- [ ] Collector v2 UI and Cisco command/security scope approved.
- [ ] Protected raw evidence and bounded SSH collection implemented.
- [ ] Cisco-family/credential/cancellation integration tests pass.
- [ ] Multi-device manual acceptance and evidence/export/reconciliation verified; scheduling absent.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

No v2 mockup/approval, Cisco lab, shared transport or credential/raw-output protection yet.

## 19. Approval requirements

Approve UI/command templates/credential policy and actual device scope. Collection approval does not authorize configuration write or scheduling.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked A02: Cisco Config Collector.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/A02_CONFIG_COLLECTOR.md, docs/IMPLEMENTATION_STATUS.md,
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
