# NetStucked 0.3.x publication and legacy-build retirement — 2026-10-09

Completed with the human's explicit publication/removal instruction. The public release catalog now contains **0.3.0 baseline** and **0.3.1 TEST BUILD**. 0.3.0 is GitHub Latest; 0.3.1 is normal SemVer, explicitly TEST BUILD and not Latest. No release asset/tag was overwritten.

## Published source and packages

| Release | Immutable source commit | Status |
|---|---|---|
| [0.3.0 baseline](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.3.0) | `48a8a9a0430273a5576788cd2598b4ae0d125c3e` | Published; Latest; release ID 407531136 |
| [0.3.1 TEST BUILD](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.3.1) | `d0fd7df09d475de752c63e5d509b745b323d9769` | Published; not Latest; release ID 407531154; branch `codex/release-test-0.3.1` |

The test branch changes only central version metadata, bundled release-history notes and explanatory docs from 0.3.0. Diagnostic/update implementation and schema-1 settings are unchanged. The checker excludes prereleases, so this test version deliberately uses normal 0.3.1 SemVer with a conspicuous title/notes.

Each release has the exact tested portable ZIP, Inno Setup installer, both SHA-256 files, `package-manifest.json` and `installer-build.json`. ZIP entries were decompressed and size/hash verified against every one of the 411 payload files. Windows CI downloaded the frozen tested ZIP, checked its approved hash/version, exercised its exact application DLLs and executable and compiled an installer from those same files. Downloaded installer assets matched GitHub digests, source/package hashes and NetStucked PE product/version. Installers are unsigned.

| Package | Bytes | SHA-256 |
|---|---:|---|
| 0.3.0 portable ZIP | 65,727,471 | `82b134a18f30d0248a8602b02861de4780831b4f5b327f66986bbca854e45193` |
| 0.3.0 installer | 46,876,136 | `f16da742413a8311b7c2d110b88c00a1dc4572db8291adfa9b021f63d05730b7` |
| 0.3.1 portable ZIP | 65,727,906 | `7f8a3c9d67cf5a6bb13041168875199128e35d00d93ad5fd02d1f63348969dbb` |
| 0.3.1 installer | 46,875,893 | `69ada71221077b3b1caab97bf18bafaf93e1971f9e48814c4d0f0a513ac0f30d` |

Local frozen packages/evidence: `artifacts/github-release/0.3.0/final` and `artifacts/github-release/0.3.1/final`. Keep the complete publish folder together. The earlier uncommitted local package and [original QA](QA_0.3.0.md) remain preserved; their hashes differ because publication rebuilt committed source with its Git revision metadata.

## Actual verification

- **PASS:** local Release build and 150 tests for each version, with 0 failures/skips.
- **PASS:** [0.3.0 Windows build](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37886869152), [0.3.1 Windows build](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37887038957), [0.3.0 installer job](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37887216612) and [0.3.1 installer job](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37887313656).
- **PASS:** exact final application assemblies have real WPF/loopback ICMP/TCP checks before publication and again against the published release catalog after retirement. Self-contained native application startup/graceful close exits 0 for both.
- **PASS:** the actual 0.3.0 package discovers 0.3.1, selects its real installer metadata and enables **Install v0.3.1**. The actual 0.3.1 package reports no newer release, offers Reinstall, then selects the actual baseline and enables **Recover v0.3.0**. Both build lists contain exactly 0.3.1/0.3.0 and no retired 0.2.x.
- **PASS:** the 0.3.0 published app backend downloaded the actual 0.3.1 installer (asset ID 623967041), verified its hash/size/product/version and Windows trust, preserved Internet ZoneId=3, accepted unsigned bytes only with acknowledgement and rejected them without it. Its exact app/private-runtime helper acknowledged startup in a real process and waited for the real parent to exit. The harness stopped its owned helper while the parent stayed alive, with unsigned execution explicitly disallowed. No Setup was executed.
- **PASS:** canonical scope/reference hashes remain unchanged, and both publication source worktrees were clean when packaged.
- **NOT TESTED:** actual NetStucked machine installation/upgrade/downgrade/uninstall, interactive UAC/SmartScreen, partial-Setup repair/reboot/settings restoration, Windows 11, human visual/DPI/accessibility acceptance and authorized remote multi-hop/TCP. Installer compilation, button eligibility and helper acknowledgement do not prove those scenarios.

Evidence: each version's `test-results/tests.trx`, `windows-qa.log`, `executable-smoke.log`, `live-menu-qa.log`, `live-menu-qa/live-release-check.json` and actual WPF renders. Public installer verification is in `0.3.0/final/public-installer-verification/actual-installer-verification.json`. Draft/publication metadata and asset comparisons are in `verified-draft.json` / `published.json` beside those final folders. No signing certificate, unrelated key, production credentials or fabricated network result was used.

## Completed 0.2.x retirement

Deleted GitHub release **407447157 / v0.2.0** and **407454285 / v0.2.1**, including their twelve download assets. Deleted six older Actions download artifacts: **11593351150, 11593116016, 11592931964, 11592733865, 11592642330, 11592503543**. Each was identified from its exact source-version/run metadata and fully backed up before deletion. All deletion responses were HTTP 204. New 0.3.x releases/artifacts were retained.

Verified backups and immutable metadata are under `artifacts/github-release/retired-0.2.x`: versioned release assets, six Actions ZIPs, `retirement-inventory.json` and `deletion-receipts.json`. Every backup was hash/size checked again immediately before deletion. Final authenticated checks found no 0.2.x release or listed legacy artifact. **Git source history, v0.2.0/v0.2.1 tags, old source branch and workflow run logs remain**; removal concerned runnable download builds, not source history. The [older publication report](GITHUB_RELEASE_REPORT_2026-10-09.md) is historical evidence.

## Try Upgrade and Recovery

Install/extract 0.3.0 once. Open Updates & Recovery, Check for Updates, choose 0.3.1 TEST BUILD, acknowledge unsigned installers if desired and press Install. Windows may request permission. In 0.3.1 select 0.3.0 and press Recover. Saved settings are backed up before Setup; a verified backup for the selected older version is restored when available. Logs/backups are under `%LOCALAPPDATA%\\NetStucked\\updates`. Use a single installed app instance during replacement. No background check/install runs. See [behavior and failure recovery](FEATURES_0.3.0.md); a partially failed installer may need manual repair.
