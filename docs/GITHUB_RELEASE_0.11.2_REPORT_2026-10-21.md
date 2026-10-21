# NetStucked 0.11.2 publication and Collector SSH verification

Verified 2026-10-21, client Asia/Bangkok. [Published release](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.11.2), [merged PR 8](https://github.com/pk-wrk-sea/NetStucked/pull/8), [scope](FEATURES_0.11.2.md), [incident, fixes and QA](QA_0.11.2.md).

## Immutable identity

- Version/tag: 0.11.2 / v0.11.2; source **43217751218b77dd4f75bcf66291e14c350afbe2**.
- Merge: 49b2dcf8dd9c3ace108a3f59a1995b4eaacfa66c. Its tree is identical to the reviewed frozen source. The immutable tag identifies that exact source, separately from later documentation commits.
- Executable ProductVersion: 0.11.2+43217751218b77dd4f75bcf66291e14c350afbe2.
- GitHub reports published 2026-10-21 14:40:54 +07:00; draft=false, prerelease=false and Latest resolves to v0.11.2.
- Frozen local payload/package: artifacts/github-release/0.11.2/final-1. Existing published tags/assets remain intact.

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| NetStucked-0.11.2-win-x64-portable.zip | 81,083,676 | 3d331ba4a0d814b71af2484f7d09636d1ff0dd2f0f158f1b3ff1070097cfc249 |
| NetStucked-0.11.2-win-x64-setup.exe | 57,486,099 | 8b2916fe5bbf5e8c227f3cd43257c2545cc00bea1c2bc94f67dbdc90b7085790 |

All six assets were downloaded from the draft and verified against GitHub digests/sizes, both sidecars and both manifests. All 498 ZIP entries match their manifest paths, sizes and hashes. An independent audit also verified CRCs, the pinned official SSH.NET NuGet repository signature/content/DLL and upstream MIT license, unchanged canonical references and absence of the tested QA/state/secret markers. Setup identity is NetStucked / 0.11.2, Authenticode NotSigned. No Setup was executed.

## Verification

- Complete quiet unit/integration: **512 PASS**, 0 failed/skipped; source CI independently reports 512/512. The earlier concurrent-load ICMP timing failure and replaced SDK-worker crash remain documented in QA; no unrelated timing thresholds were changed.
- Exact frozen runtime: default WPF **252 PASS**, actual Collector WPF/production SSH/capture **14 PASS**, Terminal WPF/WebView/SSH **50 PASS**, engineering WPF **46 PASS**, CVE expiry/editor/export **17 PASS**, native startup/Exit **1 PASS** and native saved-monitor auto-resume/instance restoration **8 PASS**.
- Exact frozen SSH: Cisco-compatible matrix **60 PASS / 23 owned scenarios**, existing large-output wire **12 PASS** and banner regression **5 PASS**. Production DLL hashes match the package before and after execution. All 498 frozen publish file hashes remain unchanged.
- Independent focused production output/guard **14 PASS**, actual KEX/no-dial **2 PASS**, relay policy **6 PASS** and fragmented console parser **4 PASS**. The guarded adapter removes the SDK Shell input worker; startup output is explicitly capped at 64 KiB before the bounded streaming/drain path becomes ready.
- Actual CTR/SHA512-ETM WPF collection completes two owned fixtures, records 16 read-only commands with exactly one CR each and zero LF/blank commands, and creates protected TXT/JSON captures. Light/Dark images are actual WPF renders. No successful native Computer Use screenshot is claimed after the human stopped that tool with Esc.

All four required CI runs passed before publication: [source](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39562113059), [PR](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39562137088), [merged main](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39562810157), [exact-package installer](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39562970953).

## Public download and real updater

Anonymous direct public downloads of both ZIP and Setup returned HTTP 200; their byte counts and SHA-256 match the frozen/draft checks. Setup product/version and NotSigned were inspected without execution.

The actual published **0.11.1** checker discovers 0.11.2 and selects **Install v0.11.2: 8 assertions PASS**. Exact 0.11.2 sees no newer release, offers Reinstall v0.11.2 and permits Recover v0.11.1: **9 PASS**. These are production public requests from the respective real application DLLs, with isolated settings and no provider fixture.

The actual 0.11.1 production updater additionally downloads/stages the public 0.11.2 installer: **11 PASS**. It verifies size/hash/product/version/Windows trust, rejects the unsigned installer without acknowledgement, retains Internet Mark of the Web and copies exact helper bytes. Its isolated native helper acknowledges startup and waits while the QA parent remains alive; the helper is stopped before the parent exits. The test discards only its staged job and preserves its isolated settings. No installer, real profile replacement or actual installation/recovery runs.

Local evidence: artifacts/collector-cisco-release-0112/ci-runs.json and exact-checks.json; artifacts/github-release/0.11.2/draft-verification/draft-verification.json, public-download/public-download-verification.json, public-update-from-0111/public-update-check.json, public-update-current/public-update-check.json and public-installer-stage-from-0111/actual-installer-verification.json. Exact runtime logs/renders/captures are in final-1/exact-*; the independent package audit is in artifacts/collector-cisco-release-0112/package-audit.

## Remaining acceptance

NOT TESTED: definitive resolution on the user's device, physical Cisco/AAA/command corpus, real-network TCP/DNS stalls, physical/mixed DPI, screen reader/High Contrast, long soak, clean-machine Setup/recovery and human acceptance. The incident's successful syslog event cannot be attributed to the old Collector; the exact strong-pair software fixture reproduces its generic failure, and the corrected build passes that fixture. Real-device retry remains necessary. No broader milestone or commercial work is closed by this publication.
