# 0.12.2 publication — 2026-10-22

[0.12.2](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.12.2) is public, non-prerelease and GitHub Latest, published at **07:56:26 UTC / 14:56:26 Asia/Bangkok**. It retains the Inventory corrections and fixes future helper staging. [Scope](FEATURES_0.12.2.md), [QA](QA_0.12.2.md), [preceding release/failure](GITHUB_RELEASE_0.12.1_REPORT_2026-10-22.md).

**Already installed 0.12.0/0.12.1 clients need one explicit manual Setup upgrade.** Their executing old helper-copy code cannot be repaired by server publication. Exit the app through System Tray → Exit before manually installing; X normally hides to tray. Setup was not executed during this verification.

## Immutable source and packages

- Tag `v0.12.2`: `65cc9b31b6414cb6ae39bbcc26f69793e3b9c1ab`.
- [PR #12](https://github.com/pk-wrk-sea/NetStucked/pull/12) merged as `30a085cd72dd203aedfb333e7822881b0be69de0`.
- Frozen root `artifacts/github-release/0.12.2/final-1/`: 2,738 files / 236,360,428 payload bytes. ProductVersion `0.12.2+65cc9b31b6414cb6ae39bbcc26f69793e3b9c1ab`; EXE SHA-256 `5a75d6f27a3f7c0c97ebbdd0784ad25b880499686af5055a3a2acf255f45f026`.

| Asset | Bytes | SHA-256 |
|---|---:|---|
| NetStucked-0.12.2-win-x64-portable.zip | 97,553,160 | 87045a9c175cfabe1889b629224ce0472f9d978ef2698f68c8863877a2515da2 |
| NetStucked-0.12.2-win-x64-setup.exe | 71,600,631 | 08407c05c8debd87f534177d42a1989cd6bca323edecf7aa9d9881fdf243343c |

All six draft assets were downloaded and checked against GitHub sizes/digests, both sidecars/manifests and every ZIP entry/CRC. The full pinned Python/Netmiko runtime remains in the application; only the restart helper excludes it. Setup identity is NetStucked / 0.12.2, Authenticode NotSigned. Anonymous public ZIP and Setup downloads return HTTP 200 and match these hashes.

## Inventory measurements and correctness

The actual MainWindow/DataGrid/production store/workbook workflow uses isolated synthetic component records: 708, then 2,000 additional records (2,708 total). Timings include a UI-idle boundary on this machine; they are not universal latency guarantees or physical mouse/DPI acceptance.

| Operation | Original 0.12.0, 708 / 2,708 rows | Exact 0.12.2, 708 / 2,708 rows |
|---|---:|---:|
| Eight search changes, including clear | 1,110.6 / 1,495.8 ms | 131.4 / 159.2 ms |
| Twenty viewport jumps and return to top | 29,541.3 / 19,218.5 ms | 570.4 / 642.4 ms |
| Selected import apply | 428.0 / 807.5 ms | 227.9 / 421.6 ms |
| Unchanged workbook roundtrip apply | 620.7 / 885.4 ms | 91.9 / 193.3 ms |
| Audit entries after unchanged roundtrip | 1,416 / 6,124 | 708 / 2,708 |

Exact Inventory: **29 assertions PASS**, including dirty editors/selection, single-snapshot import notifications, query-bounded updates, actual delayed typing, Brand binding/save, selectable lazy cells/row selection, nullable/shared-IP components, stable IDs and unchanged audit/reload. Light/Dark renders are in `inventory-exact/Inventory-scale-Light.png` and `Inventory-scale-Dark.png`; these are actual WPF renders with synthetic data. Native packaged startup/Exit also passes.

## Checks and updater result

- Release build: zero warnings/errors. **527 unit tests PASS**, zero skips. Four new tests cover the large excluded bundle, retained private .NET/resources, exact exclusion boundary, unchanged required-file bound and cancellation. Test scheduling isolation retains every existing assertion/deadline.
- [Source CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39742300496), [PR CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39742307713), [main CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39743321755) and [frozen installer CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39742893272): **PASS**. Installer CI exercises the exact full WPF, owned Netmiko/SSH and Collector UI, integrated engineering/CVE, scaled Inventory and native Monitoring workflows before compiling/uploading Setup. Logs remain locally and on GitHub.
- Before publication, the exact fixed client stages public 0.12.1 as a recovery candidate: **11 PASS**, including actual helper acknowledgement without Python and settings preservation. No Setup executes.
- After publication, hash-verified 0.12.0 discovers **0.12.2: 8 PASS**; current 0.12.2 Reinstall/Recovery catalog: **9 PASS**.
- Exact 0.12.2 stages the actual public 0.12.2 installer: **11 PASS**. Download hash/size/product/version/Windows trust and Internet Zone verify; unsigned use without acknowledgement is rejected. The native helper copies exact app bytes, acknowledges and waits while its owned QA parent remains alive. QA stops the helper before parent exit, discards only the owned job and confirms isolated settings unchanged. **No Setup runs.**
- Old-client installation remains **BLOCKED** by its unchanged executing copy algorithm; the actual 0.12.0 → 0.12.1 failure is preserved, not relabeled or repeatedly retried. Use manual Setup once to acquire the fixed helper.

## Integrity and remaining acceptance

After testing/publication, all 2,738 frozen payload hashes remain unchanged. All 16 preexisting releases/assets and 24 preexisting tag refs remain unchanged, and 0.12.1 assets are separately rechecked unchanged. Three canonical UI reference/scope files match 0.12.0 bytes. Evidence: `final-integrity.json`, `downloaded-draft/verification.json`, `stage-current/actual-installer-verification.json` and the source/CI evidence under `artifacts/inventory-performance-20261022/`.

The user's original installed 0.12.0 process remains running. Its real 708-asset workspace validates read-only and has the same before/after SHA-256; no real inventory rows are included in QA images/public assets.

**NOT TESTED:** human physical mouse/keyboard/IME, screen readers, physical/mixed DPI, production editing soak, real vendor/AAA/50-device capacity, and actual Setup/installed upgrade/recovery/uninstall. Existing byte/record bounds remain; dense 5,000-row export is not claimed to fit the retained 8 MiB workbook-part limit. Broader A03/M07/stable acceptance remains open.
