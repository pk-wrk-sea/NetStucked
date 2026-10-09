# NetStucked 0.4.1 GitHub publication — 2026-10-09

[0.4.1 release](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.4.1) is published as stable GitHub Latest. Source/tag: `v0.4.1`, commit `df8ddd84c37abccf5ac25aac2f05dcbcdb784f33`. Release ID `407715352`. Scope: the human's defect report and explicit TCP payload clarification in [FEATURES_0.4.1](FEATURES_0.4.1.md), following this session's GitHub publication instruction. No machine installation was requested or performed.

## Published immutable files

| File | Bytes | SHA-256 |
|---|---:|---|
| NetStucked-0.4.1-win-x64-portable.zip | 65,993,597 | `7ee2e3cab660385719bb439fc24d4d51902e835e424daad4f2149acdfe67c553` |
| NetStucked-0.4.1-win-x64-setup.exe | 47,092,781 | `9eba31473284009f8d7a698bf71b8ae8af7ae25e12695258e56d87063a9f4838` |

The release has exactly six assets: both files, their SHA-256 sidecars, `package-manifest.json` and `installer-build.json`. The installer was compiled on Windows from the exact frozen/tested portable bytes, not a separate rebuild. All 411 ZIP entries were verified by size/hash. The native application reports 0.4.1; downloaded Inno installer product/version metadata is NetStucked/0.4.1. Authenticode is **NotSigned**, confirmed by WinVerifyTrust `0x800B0100`.

The tag/package source was built from a clean isolated managed checkout. Concurrent milestone planning documents are included in that source snapshot; they do not implement or authorize future features. Later shared-worktree documentation edits remain preserved and unstaged. This publication follow-up stages only its report/QA, release README lines and the installer-verification fixture correction.

## Verification

- **PASS:** fresh restore/five-project Release build from the tagged source, zero warnings/errors; **196 tests, zero failed/skipped**. Real owned TCP payload receipt includes IPv4/IPv6; UDP payload receipt/reply and 0/32/1400-byte boundaries are covered.
- **PASS:** exact published Core/Infrastructure/Desktop assembly hashes in WPF QA; requested dark carets/session text, caption/toolbar/Save fixes, five-session guards, real Description editor, popup packet/range validation, continuous multiple-target modes and templates. Actual renders include 1280×800/1536×1024 and 125%/150% scaling, no binding errors.
- **PASS:** native self-contained application opens its real WPF window and closes gracefully, exit 0.
- **PASS:** 60.2-second concurrent loopback soak: 254 Ping targets, 61,341 actual replies/attempts; 32 TCP endpoints, 7,768 successful attempts; two trace sessions, 241 completed cycles each. Per-address/TTL and TCP overlap zero; Stop drains all operations. Maximum combined ICMP concurrency 19; maximum Dispatcher gap 90.8 ms; 12 navigation samples, mean 5.9/max 37.3 ms. These observations are local, not performance guarantees or long-term acceptance.
- **PASS:** [Windows build](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37905140926) and [verified Windows installer](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37905827006), both on the tagged commit. Installer CI rebuilds/tests source, validates the frozen ZIP, exercises its exact application assemblies/native host and compiles Inno from that ZIP.
- **PASS:** actual public Updates catalog contains 0.4.1/0.4.0/0.3.1/0.3.0, reports no newer version for 0.4.1, enables Reinstall, supports selected 0.3.0 Recovery, and records 0.4.0 as the documented previous version. No installer executed.
- **PASS:** actual public 0.4.1 installer download: fixed-project HTTPS identity, bytes/hash, product/version, Windows trust, Internet Mark of the Web, rejection without unsigned acknowledgement, acceptance with acknowledgement, exact copied helper bytes, real isolated-helper readiness and wait-before-parent-exit. The owned helper was stopped while its owned parent remained alive, then the parent closed; no installer ran. Current-compatible isolated settings remained byte-for-byte unchanged and staged-job cleanup completed.
- **PASS:** original approved scope, both approved PNGs and supplied logo hashes unchanged. Earlier 0.4.0/0.3.1/0.3.0 release metadata and all asset identities/sizes/digests remain unchanged.

## Retained failed attempts and correction

The first immediate post-publication public-menu run did not satisfy the Reinstall assertion. After an anonymous catalog read confirmed 0.4.1 and its complete installer metadata, the repeated real-menu check passed; the original failure log remains retained. The precise first-response state was not captured, so propagation/cache timing is not asserted as a proven cause.

The first public installer verification passed asset/trust/helper checks but failed its final settings-byte assertion. The fixture had seeded legacy false Port mode flags; opening the native 0.4.1 application intentionally normalizes these to the authorized continuous/multiple-target defaults and saves them on close. A test-only follow-up seeds those current defaults before its updater-preservation comparison. The repeated actual download/trust/helper/settings test passed. No production DLL or published asset was changed for this correction.

## Evidence and limits

Local receipts: `artifacts/github-release/0.4.1/` contains before/published/verified release JSON, CI status, installer identity and release gates. `final/` contains immutable packages/manifests, source build/TRX logs, `exact-wpf-qa.log`, `windows-qa/` actual renders/soak measurements, `native-smoke.log`, `public-menu-qa-retry.log`, `live-menu-qa-retry/live-release-check.json` and `public-installer-verification-retry/actual-installer-verification.json` with its passing log. Failed first attempts remain alongside them.

**NOT TESTED:** actual install/upgrade/recovery/uninstall on this or clean Windows 10/11 machines, human GUI/accessibility/monitor-DPI acceptance, authorized remote multi-hop/private-subnet services, UDP IPv6/port-unreachable matrix and application-protocol validity. The installer is unsigned; explicit acknowledgement and UAC/SmartScreen remain. No scheduled/background installation, credentials or future milestone features were added. A one-minute soak does not close the longer-soak/machine/human milestone gates.
