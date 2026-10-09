# M01 — Network Info and approved Wi-Fi slice

## 1. Milestone and version

**M01** / 0.5.0 approved Wi-Fi release slice (brief originally 0.2.0). The later human request explicitly authorizes integrating Wi-Fi with 0.4.2 and publishing a new version. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Network Info and separate Wi-Fi candidate. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

The human's subsequent 2026-10-09 instruction approves Wi-Fi Profile Manager inside Network Info with a compact table replacing individual cards. Reference is now preserved as `design/references/WiFiProfiles_APPROVED_2026-10-09.png`; [scope](../FEATURES_WIFI_2026-10-09.md), [guide](../WIFI_PROFILE_MANAGER.md), [QA](../QA_WIFI_2026-10-09.md). This slice is integrated into 0.5.0; exact combined release verification is recorded in [QA_0.5.0](../QA_0.5.0.md). Actual WLAN hardware/authentication acceptance is blocked by stopped WLAN AutoConfig on the development machine. Broader read-only route/neighbor/adapter-facts scope remains pending; M01 is partially implemented, not complete.

## 4. User problem

Engineers need local adapter/routing context to explain how remote diagnostics leave the machine.

## 5. Scope and non-goals

Approved current scope: Native WLAN adapter/profile reads, explicit scan/connect/disconnect/profile management, Windows-protected security, connection IPv4/IPv6/gateway/DNS/signal, network transitions and configured source-aware Connect & Test/history. Wi-Fi stays inside Network Info. Broader read-only adapter/prefix/mask/DHCP/MAC/speed/routes/neighbors/metrics remains proposed and is not implied implemented.

Non-goals: No PC-health widgets, route mutation, automatic adapter switching or unapproved Wi-Fi connection/credential operations. Advanced Enterprise Wi-Fi is A09.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Read and refresh adapter facts with source/time and distinguish disconnected, unavailable and permission-limited values.
- [ ] Display IPv4/IPv6 addresses/prefixes/masks, default gateways, DNS/DHCP, MAC/speed and interface metrics.
- [ ] Provide read-only routing and ARP/neighbor views correlated by stable adapter identity.
- [ ] For separately approved Wi-Fi scope, use Windows Native WLAN: scan/select adapter, saved profiles, SSID/signal/IP/Gateway/DNS, connect/disconnect and profile CRUD.
- [ ] Stage Personal and Enterprise/EAP/certificate capabilities separately; post-connect service tests must prove intended source adapter or display uncertainty.

## 7. UI and UX

Wi-Fi UI is approved with a compact sortable/configurable table, adapter toolbar, connection/history panels and right-side details/security/services in the existing theme. Actual WPF Light/Dark/1280/1536/scaling checks exist. Fictional profile names occur only in test fixtures. Approval does not extend to missing route/neighbor pages.

## 8. Architecture and module ownership

Propose Core network-context records/interfaces; Infrastructure Windows network/route/neighbor and optional Native WLAN adapters; Desktop NetworkInfoViewModel/View and Wi-Fi child page. No network calls on Dispatcher.

## 9. Data models and persistence

Snapshot adapter stable IDs, addresses/routes/neighbors with observed timestamp. Persist only nonsecret preferences; keep Windows-managed WLAN profiles/credentials. SQLite unnecessary for first read-only slice.

## 10. Dependencies and prerequisites

M00 stable shell and shared theme; final UI approval; capability/API validation on Windows 10/11. A09 requires the approved Wi-Fi base. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

Read-only first; elevation only for explicitly approved operations that actually need it. Respect policy/EAP restrictions; no plaintext Wi-Fi passwords or default profile export with secrets. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound refresh/coalesce events; cancellation must stop enumeration/diagnostic work. Do not continuously rescan SSIDs without an approved interval/policy.

## 13. Edge cases

Multiple/VPN/virtual/disabled adapters, no WLAN hardware, IPv6-only routes, changing DHCP, hidden SSIDs, GPO-managed profiles and unprovable source routing.

## 14. Unit and integration tests

Fixture-test normalized adapter/route/neighbor mapping and missing values. Windows integration reads actual adapters without mutation; Wi-Fi actions require a separately authorized lab. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Compare read-only facts against Windows network settings and routing data on Windows 10/11.
- [ ] Unplug/reconnect and switch adapters; verify refresh/cancel and no UI blocking.
- [ ] Only if Wi-Fi action scope is approved, connect a lab SSID, enforce credential/policy behavior and test source-adapter attribution.

## 16. Definition of Done

- [x] Wi-Fi slice UI explicitly approved and implemented using the compact-table override.
- [ ] Broader read-only route/neighbor UI and remaining adapter-fact scope approved/implemented.
- [ ] Read-only facts and source timestamps implemented and automated-tested.
- [ ] Real Windows adapter/routing acceptance and regression build/tests pass.
- [ ] Wi-Fi candidate independently approved/tested or explicitly deferred; no false completion claim.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

Wi-Fi reference/explicit authorization located; hardware acceptance remains blocked. Wider routing/neighbor UI remains unapproved. WLAN/EAP/policy/location/driver support needs real Windows lab validation; fixture-based UI and owned-loopback tests do not substitute for it.

## 19. Approval requirements

The current instruction approves explicit Wi-Fi mutation/connection and Windows-managed authentication in the app. Development must not change the working machine's connection/services without a designated authorized lab. Expanded EAP editors/certificate provisioning and broader Network Info UI require additional scope approval.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked M01: Network Info and separate Wi-Fi candidate.
The Wi-Fi compact-table slice is approved and locally implemented. Verify existing code first; use an explicitly designated Wi-Fi lab for real mutation/authentication QA. Do not rebuild it as a placeholder or assume broader route/neighbor UI approval.
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/M01_NETWORK_INFO.md, docs/IMPLEMENTATION_STATUS.md,
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
