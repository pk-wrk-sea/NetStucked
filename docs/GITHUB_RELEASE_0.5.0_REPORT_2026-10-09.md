# Published NetStucked 0.5.0 — 2026-10-09

**PASS: public GitHub Latest v0.5.0**, [release/downloads](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.5.0), release ID 407856089. The later human instruction explicitly authorizes combining approved Wi-Fi with 0.4.2 and publishing a new version. No installation or Wi-Fi/service mutation was performed on the working machine.

## Source and preservation

- Immutable source/tag: `dac604d91acf78015759801349a7aab55cd31663`; all 0.4.2 diagnostics changes are retained. The new source adds the approved Network Info Wi-Fi slice and shared selectable tables/update lifecycle. No broader route/neighbor/Dashboard/EAP-editor scope is claimed.
- 52 original preview/planning WIP files were copied byte-for-byte, hash-verified and retained in `artifacts/github-release/0.5.0/wifi-source-snapshot`; original Git stash `68819347aafd69c5d8e8802ad0b99de490346fd2` remains recoverable. The original main checkout was safely fast-forwarded to the merged source.
- Original scope, Ping/Trace PNGs, supplied brand and approved Wi-Fi PNG hashes remain unchanged. Older published release metadata/assets remain unchanged.
- Review covered Native WLAN explicit actions, Windows credential boundaries, source-bound service probes, bounded history/cancellation, MVVM/DI, settings/recovery and shared theme/table behavior. Personal keyless re-import now replaces only its key, preserving hidden SSID/nonsecret Windows XML configuration.

## Exact verification

| Check | Result and limits |
|---|---|
| Release build / automated suite | PASS: zero warnings/errors; **242 passed, 0 failed, 0 skipped** on exact source. Includes Wi-Fi loopback/DNS/TCP/HTTP/TLS/OS Credential Manager and 0.4.2 diagnostics regressions, plus combined recovery preservation and keyless import regressions. |
| Exact published WPF assemblies | PASS: all three app assemblies match frozen self-contained output; Light/Dark, 1280/1536/150% Wi-Fi renders, selectable cells/headers/sorting, column/navigation caching, network-context invalidation/history and pending-Wi-Fi shared updater drain. Zero binding errors. |
| Waiting Wi-Fi operation | PASS bounded responsiveness check after queued layout settles; results/logs record actual timer observations. This does not replace human/long-duration acceptance. |
| Simultaneous diagnostics | PASS: 60.4 s; 254 owned-loopback Ping targets, **4,161/4,161** replies; 32 owned TCP endpoints, **8,228/8,228** connections; two traces **241 cycles/replies each**. Per-address/TTL and TCP overlap 0; all work drained at Stop. |
| UI/resource observations | Navigation 12 samples, mean 7.9 ms/max 36.9 ms; recorded Dispatcher gap max 298.0 ms; managed memory 35,033,784 → 42,033,264 bytes. Short soak is not a long-term memory or remote-network guarantee. |
| Native published executable | PASS: actual owned WPF HWND startup and graceful shutdown, exit 0, isolated settings. |
| Portable package | PASS: **411 ZIP entries** verified against exact size/SHA-256; clean source and version identity recorded. |
| Windows build CI | PASS: [37924794343](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37924794343). |
| Exact-package installer CI | PASS: [37925139153](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37925139153), tests/frozen ZIP/native WPF/Inno from verified package. |
| Actual public Updates | PASS: actual 0.5.0 reports no newer version, offers **Reinstall v0.5.0**, allows **Recover v0.3.0**, and records documented previous version **0.4.2**. Public catalog excludes retired 0.2.x. |
| Actual public installer preparation | PASS: fixed-project HTTPS identity/hash/size/product/version, Windows unsigned trust, reject without acknowledgement, Internet MOTW and exact isolated helper; helper waits for parent before backup/Setup. Settings including Wi-Fi and hop-TCP metadata remain exact. **Setup was not executed.** |
| Native WLAN read | BLOCKED / NOT TESTED: actual enumeration returns Windows 1062, WLAN AutoConfig stopped/Manual. No service enablement, scan, connect/disconnect or profile mutation in QA. |
| Real machine / hardware acceptance | NOT TESTED: Wi-Fi/Personal/PEAP/EAP-TLS/policy/location/driver/captive portal/multi-adapter physical egress, full installation/upgrade/uninstall/recovery, human accessibility/DPI and broad remote-network scenarios. |

## Frozen packages

| Asset | Bytes | SHA-256 |
|---|---:|---|
| `NetStucked-0.5.0-win-x64-portable.zip` | 66,108,049 | `2f9739e505b36ecca5477117aa9fed1644327b75fa1ff36df86ab8f1d08333f8` |
| `NetStucked-0.5.0-win-x64-setup.exe` | 47,183,193 | `900eea81814fd204e92c78d7648383c699236d110a48daadba4b49c72232f51e` |

Six assets include both SHA-256 sidecars, `package-manifest.json` and `installer-build.json`. Installer ProductName/Version = NetStucked/0.5.0; Windows status = NotSigned. Explicit unsigned acknowledgement and normal UAC/SmartScreen remain required. Existing versions/tags/assets are preserved.

Publication verification caught the new release using a generated tag. Its current-release metadata was corrected to the existing exact `v0.5.0` tag while preserving all six asset IDs/digests. Anonymous Latest and actual public updater/installer checks confirm the corrected identity; no older release changed. See `tag-correction.json` in the evidence directory.

Evidence root: `artifacts/github-release/0.5.0/`; exact build/tests/publish/WPF/soak/public checks/downloads are under `final/`. Runnable complete directory: `artifacts/github-release/0.5.0/final/publish/NetStucked.exe`.

Schema-1 recovery backs up exact bytes including Wi-Fi and diagnostics. Older versions can drop unknown Wi-Fi fields on save; retain/restore the matching 0.5.0 backup. Windows-managed WLAN profiles and OS credentials are outside JSON. See [manual acceptance checklist](FEATURES_0.5.0.md#human-acceptance-checklist); hardware acceptance remains unchecked.
