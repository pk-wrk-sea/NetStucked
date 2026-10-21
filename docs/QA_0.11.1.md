# 0.11.1 - Actual-program five-menu verification

Scope: [FEATURES_0.11.1.md](FEATURES_0.11.1.md). Test date: 2026-10-21 (client Asia/Bangkok). Work in progress; publication and final exact-payload checks are recorded only after completion.

The user's installed 0.11.0 process and data are retained. A separate instance of the same installed executable uses an isolated QA profile. Real SSH tests bind only owned loopback software fixtures, explicitly labelled in their captures. No physical Cisco/working-network acceptance is implied. WPF RenderTargetBitmap images are actual control renders; Terminal also captures actual WebView pixels; native Computer Use screenshots are identified separately.

## Reproduced defects

- Native installed 0.11.0: edit Hostname without Save, then type Search; the pending edit is replaced by stored data. Before image: artifacts/menu-acceptance-20261021/native-captures/inventory-before-unsaved-edit-lost.png.
- Original source regressions: running-config banner creates a false neighbor; partial CDP/LLDP records leak across boundaries; a contradictory peer address can be labelled Verified; the first duplicate workbook ID/serial remains selectable. Corrected parser/preview regressions preserve source line/hash and reject all conflicting rows.
- Real Collector command-body MFA/OTP/access/confirmation words can abort an ordinary banner. Live waiting prompts must still stop, with no command replay.
- Original CVE adapter can discard NVD success when optional CISA times out; body reads outlive header timeout. Bound collections reset dirty review/plan fields. Expired approvals can remain visible/exportable until another workspace write.

## Evidence register

Local evidence root: artifacts/menu-acceptance-20261021. Inventory parser regressions also use artifacts/menu-review-20261021.

| Check | Actual result | Evidence |
| --- | --- | --- |
| Final solution Release build | PASS, 0 warnings/errors | final-build.log |
| Final unit/integration | 487 PASS, 0 failed/skipped | final-unit.log; final-test-results/final-unit.trx |
| Actual WPF engineering regression | 46 PASS; dirty editor/filter-out/refresh, reanalysis, persistence, Excel, explicit review/approval and invalidation | engineering-regression.log |
| Real Collector-produced capture consumers | 18 PASS; production parser/store, actual source hashes, Excel/audit/observations, graph/CSV and public CVE reports | real-engineering.log; real-engineering/result.json |
| Fresh public NVD/CISA consumer call | PASS; actual CVE-2023-20198 metadata; 2 assets remain Needs Review, 0 reviews/plans | real-engineering/actual-public-advisory-displayed.json |
| CVE expiry/editor/export regression | 17 PASS with manual clock, each export independently refreshes expiry | cve-regression-integrated.log |
| Production Collector SSH wire | 12 PASS; host identity before password, bounded authentication fallback, large output, redaction, awaited cancel | ssh-qa/collector-wire-fixed.log |
| Actual Collector WPF + production SSH/capture | 12 PASS; 2/2 complete, server audit 2 logins/16 read-only commands | ssh-qa/collector-real-fixed.log |
| Real fragmented SSH banner regression | 5 PASS after fix; exact released 0.11.0 fails before fix | ssh-qa/banner-real-comparison.json; banner-real-before.log; banner-real-after-verified.log |
| Actual Terminal WPF/WebView/xterm + real SSH | 50 PASS, including cleared stale five-tab warning, host trust before credentials, independent tabs, emulation, bounded scrollback, paste/zoom/cleanup | ssh-qa/terminal-ui-final.log |

Native Computer Use images in native-captures:

- inventory-before-unsaved-edit-lost.png: installed 0.11.0 replaces unsaved hostname after Search.
- inventory-after-search-keeps-unsaved-edit.png: actual 0.11.1 development executable retains pending hostname, stored row and capture provenance.
- terminal-real-ssh-xterm-fixed.png: actual live WebView/xterm pixels, SSH Connected, one tab and normal status text. Earlier terminal-real-ssh-xterm.png exposes the obsolete max-tab warning, which is now corrected.
- collector-real-ssh-completed.png: actual WPF production SSH/capture, both devices Completed.
- topology-real-collector-graph.png: actual 0.11.1 executable reads persisted real Collector captures, shows reported interfaces and honest conflicting-identity review (loopback endpoints differ from reported management addresses). CDP/LLDP records remain separately traceable.
- cve-real-public-metadata-needs-review.png: actual 0.11.1 executable reloads the freshly retrieved NVD/CISA record and shows both assets Needs Review.

Additional Light/Dark WPF renders and actual Terminal WebView previews are retained with the corresponding test folders. These are not manufactured mockups. Frozen-package identity and post-commit exact-payload/release verification will be recorded separately.

One initial full test run had an existing ICMP admission timing assertion failure during concurrent build activity (480 pass/1 fail at that point); the focused pacing check and two later complete suites passed, finally 487/487. No unrelated pacing implementation/test thresholds were changed. One real SSH AFTER attempt used a stale QA dependency file and failed before login; it is retained as invalid staging, then repeated successfully with a restored runtime. Public NVD returned HTTP 503 on an intermediate call; the later consumer fresh call passed. No availability or quota guarantee is claimed.

Collector remains conservative for literal command-error lines inside unusual banners (for example lines beginning `% Invalid`); arbitrary configuration-language parsing and platform command compatibility require real-device corpus acceptance. Such failure does not trigger credential fallback or command replay.

## Open acceptance

NOT TESTED: real Cisco IOS/IOS XE/NX-OS/ASA command corpus, real network topology/cabling, genuine product/configuration CVE applicability and fixed/compatible target approval, physical/mixed DPI, screen reader/High Contrast, clean Windows Setup/recovery and long soak. Public NVD/CISA availability is external and may be BLOCKED; cached actual metadata never establishes device vulnerability. No Setup is executed on this working machine.
