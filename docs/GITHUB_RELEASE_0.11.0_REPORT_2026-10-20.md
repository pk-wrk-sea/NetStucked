# 0.11.0 publication and integrated menu verification

[Release](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.11.0) is public, non-prerelease and GitHub Latest, published **2026-10-20 03:06:34 UTC**. [Scope](FEATURES_0.11.0.md), [review guide and acceptance limits](QA_0.11.0.md).

## Immutable source and assets

- Source/tag `v0.11.0`: `c7e743c0583e6125f789b1e158bceeb1fd4c4dbb`.
- [PR #6](https://github.com/pk-wrk-sea/NetStucked/pull/6), merge `67ea052e2f836322213e6fefe7e21fa5f9173eef`; verified merge tree equals frozen source tree before tagging. Publication documentation is a later code-free follow-up.
- Portable: **80,852,077 bytes**, **496 files/ZIP entries**, SHA-256 `f3d3c61e17aaeb3ec3f0acbf90f1c63c8f85877ff204204c8d8ce166bc2db4c3`.
- Installer: **57,314,972 bytes**, SHA-256 `87c079e4b3c20fafd6c2e01ae8e342e00407d3265dc2cd0a86b0898bf36b0dd8`.
- All six release assets were downloaded from the draft and checked against GitHub asset digests, sizes, hash sidecars and source/package/installer manifests. Installer Windows product identity is NetStucked/0.11.0, **NotSigned**. Setup was never executed.
- After publication, both portable and installer were downloaded anonymously from their actual public download URLs. Their sizes/hashes match the frozen assets; installer identity/trust was rechecked. This proves direct download, not automatic updater discovery or an actual installation.
- Earlier candidate payload (`5288da3`) remains locally retained and was not published. The final payload is `artifacts/github-release/0.11.0/final-2`; the first candidate was superseded by a bounded-workbook-output correction before publication. Existing older published tags/assets were preserved.

## Verification

| Check | Actual result |
|---|---|
| Release build | PASS, zero warnings/errors |
| Unit/integration suite | **462 PASS**, zero failed/skipped |
| Exact final WPF baseline and 60-second owned-load run | **272 assertions PASS** |
| Exact new-menu WPF workflow | **42 assertions PASS**, 28 Light/Dark/narrow/all-tab renders |
| Exact real owned Collector SSH | **12 assertions PASS**, host identity/auth fallback/MFA/large output/cancel |
| Exact real owned Terminal SSH/emulator/host trust | **49 assertions PASS** |
| Native published executable | Start/graceful Exit 0 PASS |
| Native saved-monitor resume/instance restore/Exit | **8 assertions PASS**, two owned loopback cycles |
| Independent workbook reader | openpyxl 3.1.5 reads both exported inventory/plan XLSX; no formula cells |
| Actual public NVD/CISA | PASS, CVE-2023-20198 metadata, Cisco reference and KEV; no device applicability asserted |
| Old binary/new data coexistence | Exact 0.10.1 native start/Exit 0; new engineering file SHA-256 unchanged in isolated profile |
| Human/real device/physical DPI/accessibility/long soak/Setup | **NOT TESTED**, retained open gates |

Owned constructed Cisco/advisory fixtures validate software behavior; they are not a golden corpus captured from real Cisco hardware. RenderTargetBitmap images establish WPF layout/bindings, not native physical-DPI or WebView pixel acceptance. The separate actual Terminal/WebView fixture suite verifies emulation and host-key behavior.

Windows CI all PASS:

- [Source branch](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39361682314)
- [PR](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39361686374)
- [Merged main](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39362099489)
- [Frozen package / exact QA / Inno installer](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39362122925)

## Public updater: mixed availability; do not claim a quota fix

The actual **0.11.0** production release source and ViewModel passed **9 assertions** on the first public request: no newer release, same-version Reinstall and previous 0.10.1 Recovery. Evidence: `public-current/public-update-check.json`, with the exact application assembly hash and timestamp.

The real **0.9.0** and **0.10.1** public updater requests were **BLOCKED by HTTP 403**. A subsequent 0.11.0 installer-staging request also received 403 before staging, so the new public installer/helper preparation gate is **BLOCKED**, not PASS. No installer helper or Setup ran during that attempt. This revision does not change GitHubReleaseSource or claim that a version bump fixes provider limits.

An anonymous diagnostic response reports `X-RateLimit-Limit: 60`, `Remaining: 0`, `Used: 60`. Its Date is 03:07:17 UTC and reset is 03:07:49 UTC; it was captured after 03:11 UTC. A stale/intermediate denial is possible, but its precise origin is unconfirmed. No token/proxy/quota workaround or repeated identical retry was used. Authenticated publisher confirmation of GitHub Latest is distinct from the anonymous production checker. Use the verified public release/direct download when the provider denies Check for Updates.

## Evidence locations and remaining acceptance

Local ignored evidence: `artifacts/all-menus` (development/independent/public-advisory/old-binary checks) and `artifacts/github-release/0.11.0` (final-2 frozen payload/QA, draft and anonymous download verification, updater result/failure logs). Earlier failed harness invocation and first release-history mismatch were corrected before the final PASS runs; they are not product acceptance passes.

No real working-network probes, personal VirusTotal key, Cisco PSIRT account, physical topology, actual device CVE/fixed/compatibility determination, firmware operation, Setup/VM upgrade/downgrade, screen-reader/High Contrast or long-duration field operation was tested. Human review remains open. The full historical A01/A03–A07/M06/M07/M08 milestones are not all closed by functional current-menu delivery.
