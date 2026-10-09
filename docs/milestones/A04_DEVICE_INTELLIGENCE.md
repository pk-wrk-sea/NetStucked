# A04 — Cisco Device Intelligence and parsers

## 1. Milestone and version

**A04** / Post-1.0.0 candidate; exact 1.x version unassigned. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Cisco Device Intelligence and parsers. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Concept; parser contracts and actual fixture corpus absent; not implemented/tested/released. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Collected command text must become structured facts while retaining provenance and uncertainty.

## 5. Scope and non-goals

Scope: Platform-specific Cisco OS-family/model/serial/software/uptime/interface/CDP-LLDP/routing extraction, normalized facts/confidence/timestamps/evidence and configuration diff.

Non-goals: No unsupported-vendor parsing, inferred facts claimed as observed or discarding original output.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Detect Cisco family from evidence and select versioned parser; unknown families return unknown rather than guessed facts.
- [ ] Extract model/serial/normalized OS version/uptime/interfaces/neighbors/routes with source command/offset/hash/time.
- [ ] Distinguish device/chassis/member identities, incomplete output and parse confidence.
- [ ] Expose validated facts and config diff through narrow contracts to Inventory/Topology/CVE; retain original evidence.

## 7. UI and UX

Concept; fact/details/confidence views BLOCKED until approved. A headless parser prototype needs explicit functional-only authority and sanitized inputs.

## 8. Architecture and module ownership

Propose Core immutable evidence/fact/parser contracts; Infrastructure Cisco-family parser adapters/raw storage; Desktop future details view. No one ViewModel combines collection, parsing, topology and CVE.

## 9. Data models and persistence

EvidenceId/hash/command/OS-family/parser-version/timestamp, structured field/value/confidence/source span and parse errors. Original command outputs separate from facts.

## 10. Dependencies and prerequisites

A02 raw collection contract/corpus and stable asset identity; sanitized Cisco fixtures across IOS/IOS XE/NX-OS/ASA; explicit parser scope approval. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Raw configurations are sensitive; fixtures redacted with provenance preserved where possible. Never send complete outputs to external services by default. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound input length/parser time and cancellation; avoid catastrophic regexes; cache by evidence hash+parser version and page large facts.

## 13. Edge cases

Different software trains, stack/chassis serials, truncated/paged text, localized banners, ambiguous OS strings and contradictory commands.

## 14. Unit and integration tests

Golden sanitized platform-specific fixtures plus malformed/fuzz/oversized/partial inputs, confidence/normalization/source traceability and deterministic diff tests. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Compare parsed facts with actual authorized Cisco command outputs and inspect exact supporting evidence.
- [ ] Review unknown/contradictory/truncated cases and verify no guessed facts are rendered as verified.

## 16. Definition of Done

- [ ] Versioned parser/evidence contracts and scope approved.
- [ ] Platform parsers and source-traceable facts implemented.
- [ ] Golden/adversarial/partial-output tests pass.
- [ ] Engineer compares lab facts with original evidence; uncertainty and consumer compatibility documented.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

No corpus/contracts and no approval. Model/version ambiguity can contaminate topology/CVE unless provenance is mandatory.

## 19. Approval requirements

Approve parser family/command corpus and fact/details UI separately; data collection authority stays in A02.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked A04: Cisco Device Intelligence and parsers.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/A04_DEVICE_INTELLIGENCE.md, docs/IMPLEMENTATION_STATUS.md,
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
