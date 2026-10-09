# A01 — SSH Terminal / Remote Access

## 1. Milestone and version

**A01** / Post-1.0.0 candidate; exact 1.x version unassigned. Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

SSH Terminal / Remote Access. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

Mockup — Awaiting Approval, reported but not located. No implementation/test/release. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

Engineers need an interactive trusted remote/serial console with isolated sessions and usable terminal emulation.

## 5. Scope and non-goals

Scope: SSH password/key/host trust, terminal emulator, tabs/splits/saved sessions/reconnect/copy-paste/log/snippets/SFTP; separately approved Telnet and serial COM capabilities.

Non-goals: No transport-only textbox claimed as terminal, host-key bypass, automatic credential submission or device configuration changes without operator commands.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Select real ANSI/xterm terminal-emulation and SSH transport components after capability/license review.
- [ ] Verify/pin SSH host identity; implement password/key auth through OS-protected references.
- [ ] Isolate tabs/splits, disconnect/reconnect/cancel, clipboard/paste and optional redacted session logging.
- [ ] Use explicit legacy authorization for Telnet; validate serial port parameters/ownership and SFTP operations separately.

## 7. UI and UX

BLOCKED pending mockup and session UX approval; show device/protocol/trust state and protect against accidental multiline paste. Keep current themes and compact navigation.

## 8. Architecture and module ownership

Propose Core session/host-trust/credential-reference contracts; Infrastructure reusable ISshTransport and serial/SFTP adapters; Desktop terminal control/session manager. Emulator and transport are different responsibilities.

## 9. Data models and persistence

SessionId, endpoint/protocol, host-key fingerprints/trust history, nonsecret saved settings and OS-protected credential references; opt-in log retention.

## 10. Dependencies and prerequisites

Post-stable scope approval; actual mockup; supported emulator/transport licensing/cancellation review; authorized SSH/Telnet/serial lab. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

No plaintext credentials/private keys in JSON/logs; host-key mismatch blocks; optional Telnet is visibly insecure and explicit. SFTP write/delete requires user action. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound session count/output buffers/log rotation; async transport read/write and UI output batching; await disconnect before tab disposal.

## 13. Edge cases

Host-key change, auth lockout, passphrase/cert key, escape sequences, huge output, disconnect mid-transfer, serial busy and dangerous pasted commands.

## 14. Unit and integration tests

Host-trust/auth/paste/session lifecycle tests, emulator rendering fixtures and owned SSH/SFTP endpoint integration; no actual production commands. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Use authorized SSH host for trust-first-connect/change-key, key/password auth, resize/ANSI, reconnect and tab close.
- [ ] Exercise SFTP cancellation and explicit serial/Telnet lab sessions only if those slices are approved.

## 16. Definition of Done

- [ ] Mockup/components and first protocols approved.
- [ ] Real emulation, host trust and protected credential/session handling implemented.
- [ ] Lifecycle/trust/terminal integration tests pass.
- [ ] Windows terminal/clipboard/transfer acceptance and sensitive-log policy verified.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Missing mockup, component/license selection and credential/trust design. SSH library alone is not terminal emulation.

## 19. Approval requirements

Approve session/UI/protocol scope after stable release; explicit legacy and file-mutation authority remains per user operation.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked A01: SSH Terminal / Remote Access.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/A01_TERMINAL.md, docs/IMPLEMENTATION_STATUS.md,
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
