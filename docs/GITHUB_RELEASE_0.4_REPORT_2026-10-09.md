# NetStucked 0.4.0 publication — 2026-10-09

Published under the human instruction “เอาขึ้น github”. [NetStucked 0.4.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.4.0) is GitHub Latest, release ID `407643387`. Source tag `v0.4.0` points to `eb5bb0f880d1f9dc2ab4173c935e53cd92d0b22a`; the changes were fast-forwarded to `main`. Earlier 0.3.0/0.3.1 release metadata, tags and assets were checked and preserved.

## Exact downloadable packages

| Package | Bytes | SHA-256 |
|---|---:|---|
| Portable ZIP | 65,992,537 | `1f612f9adffd5e246610061d38de29f11311052eb3ce646378f6fe8e6f465731` |
| Inno Setup installer | 47,087,627 | `b4e89a8d1a3d03a6d5d60a70881b6411ada4fe7626bcf56202b29b3017ed8a0a` |

The release includes both SHA-256 files, `package-manifest.json` and `installer-build.json`. Every one of the 411 ZIP payload files was decompressed and size/hash verified. Windows CI downloaded the frozen tested ZIP, verified its hash/version, checked its exact application assemblies and native executable, then compiled the installer from those same files. The installer was downloaded back and its GitHub digest, size, product/version and Windows trust checked. Product is `NetStucked`, version `0.4.0`, trust `NotSigned`.

Frozen local package and evidence: `artifacts/github-release/0.4.0/final`. Desktop assembly SHA-256: `4f4aa29406b75ca4cdcc2c5cfc88b3e8ec980a8d55695c2ebd3686b6e17f737d`; executable product version `0.4.0+eb5bb0f880d1f9dc2ab4173c935e53cd92d0b22a`. The earlier uncommitted package and [local UI QA](QA_0.4.0.md) remain historical evidence with different hashes. No release asset or tag was overwritten.

## Verification

- **PASS:** tagged source Release build, zero warnings/errors, and 175 automated tests, zero failures/skips.
- **PASS:** [Windows source build](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37898570307) and [verified installer job](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37898865469).
- **PASS:** exact frozen WPF assemblies, Light/Dark and popup renders, input guides, semantic status colors, real panel/splitter resizing, five-session rules, shared descriptions and bounded scan/acknowledgement/template behavior. No WPF binding errors.
- **PASS:** actual owned loopback ICMP/TCP/UDP diagnostics, single-pass UDP reply/silence, pause/resume/Stop, CSV export and shutdown. Self-contained native startup and graceful close exits 0.
- **PASS:** 60.4-second simultaneous-diagnostics soak: 254 Ping targets with 61,349 actual replies, 32 TCP listeners with 7,763 successful attempts and 241 completed cycles in each of two trace sessions. Per-address/TTL and TCP overlap was zero. Navigation mean 13.2ms, maximum 85.6ms; maximum observed Dispatcher gap 224.1ms. Managed memory grew from 15,513,760 to 23,494,256 bytes while histories filled; this short run does not establish a long-term plateau.
- **PASS:** the actual published 0.4.0 app reads the real public catalog `0.4.0 / 0.3.1 / 0.3.0`, reports no newer build, offers Reinstall 0.4.0 and enables Recover 0.3.0 when selected. No retired 0.2.x download is listed.
- **PASS:** the actual app updater downloads the real 0.4.0 installer, verifies identity/hash/size/product/version/Windows trust, preserves Internet ZoneId=3, rejects unsigned bytes without acknowledgement and accepts them with acknowledgement. The exact copied private-runtime helper acknowledges startup and waits for its real parent. QA stops its owned helper while the parent remains alive with unsigned execution disallowed; no Setup runs and isolated settings remain unchanged.
- **PASS:** canonical scope/reference hashes and unchanged supplied logo bytes match the baseline.
- **NOT TESTED:** actual machine installation/upgrade/recovery/uninstall, interactive UAC/SmartScreen, Windows 11, human accessibility/monitor-DPI acceptance, remote multi-hop/private CIDR/remote TCP/UDP services and the real remote UDP/IPv6 port-unreachable matrix.

Installer compilation and staging do not prove machine installation. Installers remain unsigned and require the existing acknowledgement and Windows permission flow when the user chooses to install. No installed application was replaced or closed by this task.

## QA follow-up

The first post-publication full QA run passed the actual public Updates menu assertions but failed an additional metadata fixture assertion limiting adapter entries to three during cancellation. Cancellation callbacks and semaphore completion can overlap; a fixture entry is not an HTTP send. The instrumented isolated rerun passed with zero active operations, maximum two adapter slots and three entries. Both original logs are retained.

The follow-up changes tests only: assert bounded admitted work, zero active operations after awaited cancellation and suspension after handoff, then add a production RIPE adapter HTTP cancellation regression proving that 32 concurrent lookups drain and queued lookups send no extra HTTP requests. Application implementation and published bytes remain unchanged. **PASS:** follow-up Release build has zero warnings/errors, 176 tests pass with zero failures/skips, and final exact-package WPF/public-menu checks pass with no binding errors. These results are recorded separately from the tagged package's 175-test results.

Evidence: `core-tests.log`, `test-results/tests.trx`, `windows-qa.log`, `windows-qa/`, `executable-smoke.log`, `live-menu-qa.log`, `live-menu-qa-diagnostic.log`, `live-menu-qa-final.log`, `qa-followup-tests.log`, `test-results/qa-followup.trx`, `public-installer-verification.log` and `public-installer-verification/actual-installer-verification.json` beneath the final artifact folder. Publication receipts, uploaded asset comparisons and installer PE/trust metadata are in `published.json`, `verified-draft.json` and `installer-identity.json` one directory above it.
