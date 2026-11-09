# 0.13.0 publication — 2026-11-09

[NetStucked 0.13.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.13.0) is public, non-prerelease and **GitHub Latest**, published at **2026-11-09T08:56:27Z / 15:56:27 Asia/Bangkok**. [Scope](FEATURES_0.13.0.md), [release QA](QA_0.13.0.md), [setup](CVE_IPS_SETUP.md).

**The installed 0.12.0/0.12.1 helper still requires one explicit manual Setup upgrade.** Publication cannot replace the old executing staging algorithm. A read-only process check observed the user's installed 0.12.0 still running; it was left intact. Exit via System Tray → Exit before manually running Setup. No Setup was executed during this task.

## Immutable source and packages

- Tag `v0.13.0` points to source `0e80c8dff224a854c10f274a95c5dfc4c430e1e1`.
- [PR #13](https://github.com/pk-wrk-sea/NetStucked/pull/13) merged as `2d78e4601afaac860059138d16ce06aaae2ecb55`.
- Frozen root `artifacts/github-release/0.13.0/final-1/`: **2,737 files / 236,633,931 payload bytes**.
- ProductVersion `0.13.0+0e80c8dff224a854c10f274a95c5dfc4c430e1e1`; EXE SHA-256 `4a14bf58004e5678e156e4a7d952c2495dee7cf29ecd1933c445d086a74be6ec`.

| Asset | Bytes | SHA-256 |
|---|---:|---|
| NetStucked-0.13.0-win-x64-portable.zip | 97,995,896 | 05afdc44d031beac549fbb8f9a029d39ea37af203346f20c3ec1f6d59413cbc8 |
| NetStucked-0.13.0-win-x64-setup.exe | 71,638,599 | 588c305fa18f34500ea72662739bb9f028064e60feabfbfd160b29c31783bf97 |

All six draft assets were downloaded and verified against GitHub size/digest metadata, both sidecars, `package-manifest.json` and `installer-build.json`. Portable CRC and every ZIP-entry SHA-256 match the frozen payload. Installer CI compiled those exact portable bytes. Windows PE identity is NetStucked / 0.13.0, Authenticode **NotSigned**. Anonymous public Portable and Setup downloads returned HTTP 200 and match the frozen hashes. Signing/Windows prompts are not bypassed.

## Implemented scope and checks

- CVE Checker | IPS replaces the former planner desktop route: bounded read-only VA Excel/CSV mapping, immutable rows/associations/runs, unique vendor jobs, protected Threat Vault key setup, Vulnerability Protection-only IPS coverage, separate Antivirus, local Check Point CSV snapshots, provenance/freshness/cache, signatures/raw source rows, grouping and complete/acknowledged partial Excel export.
- Monitoring, Topology Explorer, Inventory and URL Checker routes are retired; Dashboard is Coming soon. Existing stores/secrets/trust data remain. Monitoring does not auto-resume and has no tray action.
- Release solution build: **zero warnings/errors; 564 unit tests PASS**, zero skips. Actual user-supplied sample was read-only verified during implementation as 3 rows / 3 assets / 1 plugin / 34 unique CVE / 102 associations. No customer rows or real API keys were bundled/published.
- Exact frozen Portable coverage WPF: **37 assertions PASS**, including three loaded-assembly hash comparisons; 16 actual Light/Dark renders at 1536×1024 / 1100×700, no binding errors. These use explicitly labelled owned fixtures and skip the optional private sample in release QA. Implementation QA separately captured read-only sample counts.
- Exact standard WPF: **241 assertions PASS**, including three payload hash assertions; actual owned-loopback diagnostics/current navigation and awaited shutdown.
- Exact Collector/current-navigation WPF: **11 PASS**; actual bundled Netmiko, first-use trust before credentials, memory-only secret controls, owned captures, retired Inventory rejection and Dark switch. Historical Inventory UI tests are version-gated rather than falsely claimed for retired routes.
- Native packaged startup/Exit smoke PASS. Native retirement/instance restoration: **8 PASS**, two actual launches, zero owned listener connections during the observation windows, unchanged legacy monitor-file bytes, duplicate-instance restoration and exit 0.
- Retained legacy CVE data/expiry regression: **17 PASS**, without exposing the retired planner route.

## Windows CI

All four completed successfully:

- [Source CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/42587338765).
- [PR CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/42587394048).
- [Main CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/42588191776).
- [Frozen installer CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/42588513077).

CI exercises owned SSH/Netmiko/Cisco-compatible framing/banner/CTR fixtures, current-menu/CVE WPF, legacy data regressions, actual executable startup and retired Monitoring behavior. The installer workflow verifies tag/source version, draft identity and the approved ZIP hash, tests exact application bytes and compiles Inno without publishing automatically. Local logs are retained under `preparation/`; exact local checks, downloaded bytes and manifests remain under `final-1/`.

## Actual public updater/helper verification

- Before publication, hash-verified authentic 0.12.2 checks the public baseline catalog and stages the baseline installer with helper acknowledgement and unchanged isolated settings. No Setup executes.
- After publication, authentic **0.12.2 discovers 0.13.0: 8 PASS**. The real production checker selects **Install v0.13.0** and displays provider release notes.
- Current **0.13.0 Reinstall/Recovery: 9 PASS**, with previous documented/public 0.12.2 still selectable.
- **Both authentic 0.12.2 and exact 0.13.0 stage the actual public 0.13.0 installer: 11 PASS each.** Download size/hash/product/version/Windows trust and Internet Zone verify; unsigned installation without acknowledgement is rejected. The real helper copies exact application bytes, acknowledges startup and waits for its owned original parent. QA stops the helper before parent exit, discards only the owned job and confirms isolated settings unchanged. **No Setup runs.**
- This establishes discovery/download/preparation compatibility from 0.12.2; it does not establish an actual installed upgrade or repair the older executing 0.12.0/0.12.1 helper.

Evidence: `update-from-0122/`, `update-current/`, `stage-from-0122/`, `stage-current/`, `downloaded-public/`, `public-setup.exe`, `draft-final-verification.json` and associated logs.

## Integrity and separate acceptance

All **18 preexisting releases/assets and 26 preexisting tag refs** remain unchanged; all 2,737 frozen payload files still match their hashes after testing/publication. Canonical UI scope/reference files match the preceding baseline. Evidence: `final-integrity.json`; Git connectivity validation also passed after an automatic local maintenance/repack diagnostic, without deleting objects or changing repository policy.

**NOT TESTED:** real Threat Vault key/subscription/organizational permissions, real SmartConsole export completeness, real vendor/AAA, physical/mixed DPI, accessibility/screen readers, production large-report/long-soak capacity and actual Setup/installed upgrade/recovery/uninstall. Catalog signatures do not establish deployed policy/enforcement, traffic-path coverage, applicability or exploit success. No working-network probes, device/firewall configuration changes or installed-process/data replacement occurred. Broader historical A06/M07/manual DoD remains open.
