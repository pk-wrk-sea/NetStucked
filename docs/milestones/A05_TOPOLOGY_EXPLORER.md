# A05 — Device-centric one-hop Topology Explorer

## 1. Milestone and version

**A05** / Post-1.0.0 candidate; exact 1.x version unassigned. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Device-centric one-hop Topology Explorer. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Mockup — Awaiting Approval, reported but not located. No topology implementation/verification. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need inspectable adjacency/interfaces/routes tied to real collected evidence rather than an invented physical map.

## 5. Scope and non-goals

Scope: One selected device/one-hop neighbors, interactive node/link inspector, overview/interfaces/VLAN/port-channel/routing/next-hop/config-diff/CVE/MA links and freshness/evidence.

Non-goals: No whole-network discovery, config-only physical certainty, port-speed/name-derived uplink direction or implicit new collection.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Analyze existing collected data first; offer separately confirmed Refresh & Analyze for authorized new collection.
- [ ] Represent relationships as Verified/Discovered/Inferred/Unknown with source/time and confidence.
- [ ] Correlate CDP/LLDP/interfaces/status/config/routes/OSPF-BGP/ARP-MAC evidence; explain contradictions and stale data.
- [ ] Select nodes and links for local/remote interfaces, speed/status/trunk/routed/port-channel details and default/next-hop/protocol facts.
- [ ] Link existing configuration/MA/CVE consumers without directly coupling their storage or triggering network work.

## 7. UI and UX

BLOCKED until supplied mockup and one-hop interaction design approved. Use compact graph plus inspectable tables; example CSW-HQ/DSW/FW/RTR labels are not real inventory.

## 8. Architecture and module ownership

Propose Core graph/evidence reconciliation and ITopologyEvidenceSource; Infrastructure fact adapters; Desktop graph interaction/inspector VMs/Views. Graph layout never generates facts.

## 9. Data models and persistence

Stable node/AssetId, interface/link IDs, endpoint association, evidence class/time/source/confidence and conflicting observations; cache projections by evidence revision.

## 10. Dependencies and prerequisites

A02/A04 raw and structured neighbor/interface/route evidence, A03 identity; approved mockup; graph control/component/license decision. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Read existing local evidence by default; Refresh collection requires explicit target authorization. Avoid exposing sensitive configs through graph exports. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound one-hop nodes/edges, incremental layout and inspector queries; analysis/correlation off Dispatcher with cancellation.

## 13. Edge cases

Missing/stale/contradictory neighbors, asymmetric links, bundles, unmanaged peer, virtual links, address collisions and missing remote interface.

## 14. Unit and integration tests

Evidence reconciliation fixtures, stable graph IDs, classification/no-uplink-inference rules and WPF node/link selection tests. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Analyze captured Cisco lab evidence offline and inspect supporting source for every link/class.
- [ ] Select node/link and verify actual interfaces/routes; cancel separately confirmed Refresh without losing old evidence.

## 16. Definition of Done

- [ ] One-hop mockup and evidence classifications approved.
- [ ] Existing-data analysis and graph/inspectors implemented without fabricated links.
- [ ] Reconciliation/unknown/stale/interaction tests pass.
- [ ] Engineer verifies lab topology/evidence and explicit Refresh collection behavior.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Missing mockup and neighbor/interface corpus. Running-config alone cannot prove physical adjacency or direction.

## 19. Approval requirements

Approve graph/UI/classification policy; collection and broader discovery need separate explicit authority.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked A05: Device-centric one-hop Topology Explorer.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/A05_TOPOLOGY_EXPLORER.md, docs/IMPLEMENTATION_STATUS.md,
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
