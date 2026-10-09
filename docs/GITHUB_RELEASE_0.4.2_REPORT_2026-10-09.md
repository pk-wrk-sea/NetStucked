# NetStucked 0.4.2 GitHub publication — 2026-10-09

[0.4.2](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.4.2) is published as stable GitHub Latest. Source/tag: `v0.4.2`, commit `143212c71fc00c55e2875973bc71971d05dbd708`. Release ID `407830888`. Scope is the explicit human diagnostics defect/feature request and automatic review/publication instruction, recorded in [FEATURES_0.4.2](FEATURES_0.4.2.md). No machine installation was performed.

## Immutable published assets

| File | Bytes | SHA-256 |
|---|---:|---|
| NetStucked-0.4.2-win-x64-portable.zip | 66,006,962 | `d0680d29365b801974bbb643e962dc69aac41ceed22f9922c8b38630cde5945c` |
| NetStucked-0.4.2-win-x64-setup.exe | 47,093,715 | `0d038f590ed6434aa7f1b9ca78723a4d47053b48831e7ff78ca6ccff26d416fa` |

Exactly six assets: ZIP/setup, both SHA-256 sidecars, `package-manifest.json` and `installer-build.json`. All 411 ZIP entries were verified by size/hash. Windows CI compiled the installer from those frozen portable bytes. Product/version metadata is NetStucked/0.4.2; Authenticode is NotSigned, with actual public-download WinVerifyTrust `0x800B0100`. Tags/assets from 0.4.1/0.4.0/0.3.1/0.3.0 remain unchanged.

## Review and verification

- **PASS:** clean committed source; five-project Release build with zero warnings/errors; **214 tests, zero failures/skips**. Coverage includes separate admission/pacing/cancellation, accurate reply IP/host, recent-loss recovery with cumulative counters, real TCP listener/no payload, disabled checks, duplicate cache, slow-TCP/ICMP independence, stale-route exclusion, one-shot completion and settings validation/roundtrip. Existing payload, range, UDP, updater and cancellation regressions remain.
- **PASS:** exact published Core/Infrastructure/Desktop assembly hashes in WPF QA. Actual cells/headers expose readonly selectable text with a caret, headers still sort, Description remains editable, table text allows selection across rows/headers, new reply/history/TCP columns copy actual data, Port column chooser persists visibility/order, popups validate optional/custom ports, and table fields suppress input hints. Light/Dark/System and 1536×1024/1280×800 plus 125%/150% renders have zero binding errors. Human mouse/keyboard/accessibility acceptance remains open.
- **PASS:** native self-contained application opens its real WPF window and closes gracefully, exit 0.
- **PASS:** exact-package **60.3-second** owned-loopback soak: **254 Ping targets, 4,172 completed replies/attempts**, **32 TCP endpoints, 8,297 successful attempts**, and two continuous trace sessions with **241 cycles each**, at interval **250 ms / timeout 500 ms**. ICMP per-address/TTL and TCP overlap zero; Stop drains all operations. Maximum combined queued/in-flight ICMP calls 36; max Dispatcher gap 156.8 ms; 12 navigation samples mean 15.8 / max 102.6 ms. Managed memory before/after full GC 34,793,224 / 38,654,376 bytes. These are local observations, not remote or long-term performance guarantees.
- **PASS:** [Windows build](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37921058856) and [verified Windows installer](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37921520120) on the tagged source. Installer workflow checks source/ZIP identity, exercises exact frozen WPF/native code and compiles Inno from the ZIP.
- **PASS:** anonymous public catalog exposes complete 0.4.2 with six assets. Actual 0.4.2 Updates reports no newer build, offers Reinstall, permits selected 0.3.0 Recovery and records 0.4.1 as documented previous version. Catalog contains 0.4.2/0.4.1/0.4.0/0.3.1/0.3.0.
- **PASS:** actual public 0.4.2 installer download through production updater: fixed-project HTTPS identity, size/hash, product/version, Windows trust and Internet Mark of the Web; unsigned rejected without acknowledgement and accepted with acknowledgement. Exact helper bytes, real isolated-helper readiness and wait-before-parent-exit verified. Owned helper stopped while owned parent remained alive; settings bytes preserved and staged-job cleanup completed. **No installer executed.**
- **PASS:** approved scope, both original PNGs and supplied logo match canonical SHA-256. **All 52 original checkout WIP files retain their original bytes**; none are committed/staged by this task.

Review corrected TCP settings normalization, readonly/header binding direction, proper WPF header sorting, Description edit access, cancellation/cache ownership, table-copy truncation notices, readonly placeholder suppression and malformed saved-port validation. Initial development failures were retained and corrected before freezing the package; development fixture visibility/render-name issues were also corrected. Frozen-package/public/CI checks pass without product changes after the tag.

## Cause and practical limits

0.4.1's desktop forced 64 Ping workers / 2,048 admissions/s and lacked reserved native trace capacity. A /24 at 250 ms requests 1,016 sends/s. 0.4.2 limits Ping to 32 workers / up to 128 paced native sends/s, with a separate 32-slot / 128/s aggregate trace budget. These are application capacity fixes. A /27 requests 120/s, so the code evidence alone does not prove the cause of the user's particular all-Warn observation. Actual RTT/loss/error and LAN/WAN behavior remain needed for that diagnosis. Warn previously used all historical loss; it now explains current RTT/misses or the last 20 completed attempts while total Loss % stays accurate.

Pacing and OS scheduling make a large scope slower than its requested interval; no queue delay is reported as ICMP RTT or loss. Optional hop TCP directly connects to responding IPs, never changes ICMP route discovery, sends no payload and displays only successful connections. Empty TCP results do not prove application-service absence. Remote/private CIDR/multi-hop behavior, human drag-copy/monitor/DPI/accessibility acceptance, Windows 11 and machine install/upgrade/recovery/uninstall remain **NOT TESTED**.

Use the [unchecked human checklist](FEATURES_0.4.2.md#human-acceptance-checklist) to accept the program changes.

## Evidence and preserved working state

Receipts: `artifacts/github-release/0.4.2/` contains published/draft/verified metadata, CI status, installer identity, public catalog, preservation checks and release gates. `final/` contains immutable files/manifests, source tests/TRX logs, exact WPF/native logs, actual renders, soak metrics, actual public-menu/installer receipts. Development attempts remain in `artifacts/diagnostics-stability/`.

Implementation uses attached managed checkout `C:\Users\PK\.codex\worktrees\diagnostics-stability\NetStucked` on `codex/diagnostics-stability`. The original `D:\Projects\NetStucked` checkout remains at its prior local main commit with unreleased Wi-Fi/planning WIP; it was not rebased or reset. Future Wi-Fi work must integrate the published diagnostics changes before its next release. `artifacts/diagnostics-stability/preserved-root-wip.json` and the publication's preservation receipt record the exact 52-file boundary. Documentation follow-up advances GitHub main without modifying the published tag or package bytes.
