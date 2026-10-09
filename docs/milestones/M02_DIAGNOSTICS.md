# M02 — Dedicated DNS, TCP/UDP and HTTP/TLS diagnostics

## 1. Milestone and version

**M02** / 0.6.0 proposed (brief originally 0.3.0; TCP shipped earlier). Planning snapshot 2026-10-09; not a version bump or publication instruction. [Milestone index](../MILESTONE_INDEX.md).

## 2. Feature title

Dedicated DNS, TCP/UDP and HTTP/TLS diagnostics. See retained requirements in [feature registry](../FEATURE_REGISTRY.md).

## 3. Approval, implementation and verification

TCP/UDP scope Approved/implemented/released. Dedicated DNS/HTTP/TLS Concept; UI pending; no implementation or tests for those new tools. See [actual audit](../IMPLEMENTATION_STATUS.md) and [UI approval register](../UI_APPROVAL_REGISTER.md). None of the outstanding checks below are implied complete by documentation.

## 4. User problem

ICMP alone cannot diagnose DNS, application connectivity or TLS certificate problems.

## 5. Scope and non-goals

Scope: Reuse existing Port Test; add independently approved DNS record and HTTP/HTTPS/TLS inspection slices under a future Diagnostics grouping.

Non-goals: No port-template service identification, intrusive vulnerability scans, automatic credentials, invalid-certificate bypass or TCP/UDP traceroute.

## 6. Functional acceptance criteria

These checkboxes track remaining milestone acceptance, not just code presence.

- [ ] Query A/AAAA/CNAME/MX/TXT/PTR/SRV with explicit query/resolver/result/time semantics and bounded cancellation.
- [ ] Retain current TCP/UDP scope limits, measured connection/reply timings and inconclusive UDP silence.
- [ ] For HTTP/HTTPS report actual status/redirects/timing and bounded response metadata without downloading unbounded bodies.
- [ ] Inspect certificate chain/hostname/expiry/TLS negotiation with clear validation failures; do not represent reachability as application health.
- [ ] Allow export and independent target-oriented protocol findings; document resolver/transport limitations.

## 7. UI and UX

New DNS/HTTP/TLS UI BLOCKED pending approval/prototype authority. Keep existing Port Test unchanged until a later approved navigation grouping exists; compact tool-specific tables and input controls.

## 8. Architecture and module ownership

Propose UI-free DNS query/HTTP/TLS contracts in Core, cancellable clients/transport in Infrastructure and focused VMs/XAML in Desktop. Existing DnsResolver is probe-name resolution, not a record-query toolkit.

## 9. Data models and persistence

Typed query/record/certificate/request result with source/time/outcome and nullable timing; optional profile settings, bounded transient results. M05 owns durable sessions.

## 10. Dependencies and prerequisites

M00 shell; existing TCP/UDP engines; approve UI and DNS library/Windows API choice based on record/transport/cancellation capabilities; no unnecessary NuGet dependencies. See [dependency map](../FEATURE_DEPENDENCIES.md). Proposed contracts do not exist merely because named here.

## 11. Security and permissions

User-authorized names/URLs/endpoints only; validate schemes/redirect limits and preserve normal TLS verification. Avoid exporting secrets from headers, URLs or certificate/private-key stores. Apply [security/data policy](../SECURITY_AND_DATA_POLICY.md).

## 12. Performance and scalability

Bound DNS/HTTP concurrency, timeout, redirects and bytes. Slow DNS/TLS targets must not serialize independent checks or stall the Dispatcher.

## 13. Edge cases

NXDOMAIN/NODATA/SERVFAIL/truncation, IDNs/IPv6, timeout/cancellation, redirect loops, proxy behavior, TLS name/chain/expiry errors and no UDP reply.

## 14. Unit and integration tests

Deterministic DNS packet/record/status fixtures, owned local DNS/HTTP/TLS servers and delayed/cancelled connections; keep actual certificate failures distinct from network failures. Report deterministic, loopback, remote and native/machine results separately.

## 15. Manual Windows acceptance

- [ ] Against explicitly authorized lab services, validate each record type and error state.
- [ ] Exercise HTTP redirect/timeout and HTTPS certificate errors without bypassing trust.
- [ ] Run alongside Ping/Trace/Port, then Stop and verify all clients drain and CSV/export retains accurate outcomes.

## 16. Definition of Done

- [x] Current Port Test implementation/release and bounded behavior are recorded as a completed subset.
- [ ] New tool scope/UI/API choice approved.
- [ ] DNS/HTTP/TLS implementations and meaningful tests pass.
- [ ] Owned-service and authorized remote acceptance plus full regressions documented.

An open required item prevents complete status; record environment blockers or explicit human deferrals rather than invent PASS.

## 17. Deliverables

Scoped implementation (only when authorized), meaningful automated tests, actual Windows acceptance evidence, any required approved UI/schema/API contract, and synchronized IMPLEMENTATION_STATUS/FEATURE_REGISTRY/UI_APPROVAL_REGISTER/CHANGELOG/QA report. For On Hold commercial work the current deliverable is this preserved specification, not production code.

## 18. Risks and blockers

No new tool UI approval; selected APIs must support complete record queries and cancellable work. Existing hostname resolution does not satisfy DNS tool requirements.

## 19. Approval requirements

Approve each new tool and any navigation change. TCP/UDP regression work stays within current approval.

## 20. Ready-to-copy Codex task prompt

```text
Work on NetStucked M02: Dedicated DNS, TCP/UDP and HTTP/TLS diagnostics.
This is an unapproved candidate. Start implementation only after explicit scope approval and approved UI (or explicit functional-only prototype authority).
Read AGENTS.md, .agents/skills/netstucked-development/SKILL.md,
docs/milestones/M02_DIAGNOSTICS.md, docs/IMPLEMENTATION_STATUS.md,
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
