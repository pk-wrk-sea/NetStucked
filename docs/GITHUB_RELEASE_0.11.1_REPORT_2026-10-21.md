# NetStucked 0.11.1 publication and five-menu acceptance

Verified 2026-10-21, client Asia/Bangkok. [Published release](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.11.1), [merged PR 7](https://github.com/pk-wrk-sea/NetStucked/pull/7), [scope](FEATURES_0.11.1.md), [workflow/defect evidence](QA_0.11.1.md).

## Immutable identity

- Version/tag: 0.11.1 / v0.11.1, source **314c2f3da937232e5f9441c0863e9390016af8e7**.
- Merge: 81e5b73070b3de252748071ea0594029de7edeea. Merged tree was compared with frozen source and is identical. The tag points to the exact reviewed/package source, not a later documentation commit.
- Executable ProductVersion: 0.11.1+314c2f3da937232e5f9441c0863e9390016af8e7.
- GitHub reports published UTC 2026-10-21T05:44:58Z, draft=false, prerelease=false; authenticated Latest resolves to v0.11.1.
- Final local package is artifacts/github-release/0.11.1/final-2. Earlier final-1 at 1535b02 is retained as a failed local candidate; its bytes were never published. Older public tags/releases/assets remain intact.

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| NetStucked-0.11.1-win-x64-portable.zip | 80,859,405 | c5f1a8acc56382be389d171fa218fed2a196dfd44f47b801327789c803e90a46 |
| NetStucked-0.11.1-win-x64-setup.exe | 57,315,677 | f4b8530d40f3c5d1692ff4461f2d1e9dcf0bd502dff579865b994ec434be4c8e |

All six release assets were downloaded from the draft and checked against GitHub SHA-256 digests and byte sizes. Both sidecars, package-manifest.json and installer-build.json match version/source/package hashes. All 496 portable payload entries match their exact manifest names, sizes and hashes. Setup resource identity is NetStucked / 0.11.1, Authenticode NotSigned. No Setup was executed; no actual install/rollback acceptance is claimed.

## Actual verification

- Local complete unit/integration: **493 PASS**, 0 failed/skipped. CI source, merged main and installer workflow independently report 493/493.
- Exact frozen default WPF: **252 PASS**, including actual loopback diagnostics and zero binding errors; exact engineering WPF: **46 PASS**, including real persistence/XLSX, dirty-editor/filtering and explicit legacy reanalysis.
- Exact real SSH Collector wire **12 PASS**, banner regression **5 PASS**, actual Terminal WPF/WebView/SSH **50 PASS**, CVE clock/editor/export **17 PASS**, native app start/Exit and native monitor auto-resume **8 PASS**. DLL hashes are checked against the full frozen payload.
- Exact production EngineeringStore: **500/500 OneDrive + 500/500 LocalAppData** saves after the contention fix. Original failure, deterministic lock regressions and original data are retained; no permissions were widened.
- Actual WPF production consumers of two real Collector-produced SSH captures: **18 PASS**, including source hashes, Inventory/Excel/audit, one-hop inspector/export and fresh public NVD/CISA. Both software-fixture assets stay Needs Review, with no fabricated vulnerability verdict or device change plan.
- Independent openpyxl 3.1.5 reads actual exported Inventory and plan workbooks, preserves expected schema/table dimensions and finds no formulas. This is independent file-format validation, not manual desktop Excel acceptance.

CI links: [source](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39550426930), [PR](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39550431322), [merged main](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39550819153), [exact-package installer](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39550881842). All four succeeded before publication.

## Public download and updater

Anonymous direct public downloads of both portable and Setup returned HTTP 200. Both byte counts and SHA-256 values match the frozen/draft checks; Setup identity and NotSigned were inspected without execution. No token, cookies, API quota bypass or proxy workaround was used for these downloads.

The actual old **0.11.0+c7e743c0583e6125f789b1e158bceeb1fd4c4dbb** checker discovers 0.11.1 and selects Install v0.11.1: **8 assertions PASS**. The exact 0.11.1 checker sees no newer version, offers Reinstall v0.11.1 and permits selecting Recover v0.11.0: **9 assertions PASS**. No Install/Recover command was executed. This verifies current availability, not a guarantee of continuous external API availability or a claimed fix for historical provider HTTP403.

Local public evidence: artifacts/github-release/0.11.1/public-download/public-download-verification.json, draft-verification/draft-verification.json, public-update-from-0110.log and public-update-current.log, with per-check JSON records. Exact runtime evidence lives under final-2/exact-*.

## Native captures and remaining acceptance

Native screenshots and before/after images for all five menus: artifacts/menu-acceptance-20261021/native-captures. Review gallery: artifacts/menu-acceptance-20261021/review.html; portable review archive: NetStucked-0.11.1-five-menu-review.zip in the same folder. These contain actual WPF/native/WebView pixels, not mockups. Capture provenance is listed in QA_0.11.1.md. Test instances use isolated profiles and owned loopback fixtures; the user's installed 0.11.0 process/profile was retained and all owned test windows/fixtures were closed.

NOT TESTED: real Cisco hardware/command corpus and physical adjacency, actual advisory/configuration applicability and fixed/compatible target approval, clean-machine Setup/recovery, physical/mixed DPI, screen reader/High Contrast, long soak and human integrated review. Collector conservatively stops on some literal command-error banner lines; provider availability can vary. No pending broader milestone or commercial work is closed by this publication.
