# 0.12.1 publication and subsequent updater finding โ€” 2026-10-22

[0.12.1](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.12.1) was published at **07:35:27 UTC / 14:35:27 Asia/Bangkok**, non-prerelease. It is superseded by [0.12.2](GITHUB_RELEASE_0.12.2_REPORT_2026-10-22.md); original tag/assets remain unchanged. Its release notes disclose the old-client helper limitation found after publication.

- Immutable source/tag: `68ca95bcb3202d387fa78a5dc4ef01523a723235`; [PR #10](https://github.com/pk-wrk-sea/NetStucked/pull/10) merged as `b4fe81ef9d4d6af07f41ca8933b5c777e4d28c32`.
- Frozen payload: `artifacts/github-release/0.12.1/final-1/`, 2,738 files / 236,359,908 bytes; ProductVersion `0.12.1+68ca95bcb3202d387fa78a5dc4ef01523a723235`.
- ZIP: 97,552,887 bytes, SHA-256 `7f2e9774f2d8443ba73a2951aaa30fe9e4a4bd89076127375fe04ba6f1150186`.
- Setup: 71,594,168 bytes, SHA-256 `47d25c5c3f6f44dc55d305472ddf7cb9d9663a5460b886d5bdaffbdfd6a5e5a8`; Windows identity NetStucked / 0.12.1, Authenticode NotSigned. No Setup ran.

All six downloaded draft assets match GitHub sizes/digests, both sidecars/manifests and all 2,738 ZIP entries. Both anonymous public downloads return HTTP 200 and match the same hashes. Exact local checks pass: 29 scaled Inventory, 252 WPF, 46 engineering, 17 CVE and 8 native Monitoring assertions, plus native startup. The complete unit suite passes 523 tests without skips. [Detailed Inventory measurements/limits](QA_0.12.1.md).

## CI and scheduling evidence

- [Source](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39739276924): first attempt fails the existing partial-cycle timing test; complete retry passes.
- [PR](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39739297845) and [frozen installer](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39740210391): PASS.
- [Main before test isolation](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39740271407): both attempts FAIL, in different Ping-cadence/SSH-buffer timing tests. Raw failed logs remain in `artifacts/inventory-performance-20261022/`.
- QA-only [PR #11](https://github.com/pk-wrk-sea/NetStucked/pull/11), source `160f5779492443f364f8a78b2be8e858512fc0cd`, isolates bulk and scheduler-sensitive fixtures without changing assertions/deadlines/test counts. Local 523-test run and [source](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39741277777), [PR](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39741284234), [merged main](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39742192606) checks PASS. Main merge is `8ea3a7dd0d4426cde8ed3a8c6e928f8dafe17d64`. No released 0.12.1 bytes/tag were replaced.

## Actual public updater result

Hash-verified 0.12.0 assemblies discover 0.12.1: **8 PASS**. The current 0.12.1 catalog/Reinstall/Recovery check: **9 PASS**. These are discovery results, not installation acceptance.

Actual public staging from 0.12.0 **FAILS** at `Application helper payload exceeds the copy limit`, after Setup download/validation and before helper startup. The 2,248-file Collector directory exceeds the existing 1,000 copied-file helper bound. The owned failed job is removed; no helper/Setup starts, no original installed process is closed and real workspace bytes remain unchanged. This failure must not be represented as an update PASS. Evidence: `stage-from-0120.log`; the bounded fix and old-client manual-upgrade requirement are documented in [0.12.2](FEATURES_0.12.2.md).

Physical input/DPI/accessibility, real-device and clean-machine installation/recovery acceptance remain NOT TESTED.
