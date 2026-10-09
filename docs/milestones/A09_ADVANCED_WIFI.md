# A09 — Advanced Wi-Fi / Enterprise EAP enhancements

## 1. Milestone and version

**A09** / Post-1.0.0 candidate if not completed in separately approved F02 slice. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Advanced Wi-Fi / Enterprise EAP enhancements. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Mockup — Awaiting Approval for base Wi-Fi; advanced Enterprise/EAP is a future candidate, not implemented. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Enterprise engineers need policy-compatible protected WLAN profiles with clear certificate/EAP and source-interface diagnostics.

## 5. Scope and non-goals

Scope: Stage validated WPA2/WPA3 Enterprise/802.1X/EAP/certificate profile capabilities, protected editing/history and Connect & Test Services on intended adapter.

Non-goals: No GPO bypass, plaintext credentials, unsupported EAP claims or silent certificate installation/profile connection.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Detect supported Windows Native WLAN capabilities and policy restrictions per adapter/profile.
- [ ] Support explicitly selected EAP/certificate workflows using Windows-managed credentials/stores.
- [ ] Validate profile changes and connect/disconnect actions with clear failures and bounded histories.
- [ ] Demonstrate diagnostic source interface or report uncertainty; retain connected IP/gateway/DNS/SSID/signal context.

## 7. UI and UX

BLOCKED pending approved base and advanced Wi-Fi designs. Advanced profile/security editor must not obscure Windows-managed trust decisions.

## 8. Architecture and module ownership

Extend proposed M01 Core WLAN capability/profile contracts and Infrastructure Native WLAN/certificate references; Desktop advanced child editor/tests within Network Info.

## 9. Data models and persistence

Nonsecret profile/capability references, adapter stable ID, certificate identity references, connection outcome/time and history; credentials remain Windows-managed.

## 10. Dependencies and prerequisites

Approved/accepted F02 base, enterprise lab/RADIUS/certificate/GPO test environment and verified Windows 10/11 feature matrix. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Honor Group Policy/EAP restrictions; no password/key export/plaintext cache; explicit certificate/profile mutation and connection actions only. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound scan/connect/retry operations and logs, await disconnect/cancel; do not hang the UI while auth/policy resolution occurs.

## 13. Edge cases

Expired/wrong certificate, unsupported EAP, GPO profile, roaming/multiple radios, WPA3 unavailable, no adapter and uncertain routing.

## 14. Unit and integration tests

Profile/capability/policy/credential-reference fixtures and authorized real enterprise WLAN auth/adapter integration. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] In authorized enterprise Wi-Fi lab test supported EAP/certificate connections and policy-denied paths.
- [ ] Verify no plaintext secrets in preferences/logs and source-adapter attribution for post-connect service tests.

## 16. Definition of Done

- [ ] Base/advanced UI, EAP matrix and mutation authority approved.
- [ ] Windows-managed security/capabilities implemented without policy bypass.
- [ ] Supported/denied/expiry/source-adapter tests pass.
- [ ] Enterprise lab manual acceptance documents unsupported/uncertain combinations.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Missing base mockup/approval and enterprise test environment; Windows support/policy varies and must be measured.

## 19. Approval requirements

Approve explicit EAP/certificate/profile slice and lab mutation scope; do not infer Enterprise support from Personal profiles.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked A09: Advanced Wi-Fi / Enterprise EAP enhancements.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/A09_ADVANCED_WIFI.md, docs/IMPLEMENTATION_STATUS.md,
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
