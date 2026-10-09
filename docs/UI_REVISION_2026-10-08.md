# NetStucked 0.1.0 — authorized UI revision and independent polling

Request date and verification date: 2026-10-08, Asia/Bangkok. Platform: Windows 10 Pro x64 build 19045; SDK 10.0.401, self-contained runtime 10.0.12. The human's explicit revision supersedes the original screenshots only for the requested changes below. Original scope/PNG bytes are intact. No commit, push, GitHub release, installer installation or automatic updater was performed.

## Implemented revision

| Request | Implementation |
|---|---|
| Maximized window above taskbar | WM_GETMINMAXINFO uses current monitor rcWork in physical pixels; native HWND bounds verified |
| Minimal window controls/frame/tab header | Yellow minimize, green maximize/restore, red close; visible border and current-page tab |
| Simplified Live Ping settings | Only Interval >=250 ms, Timeout >=500 ms, Packet size; internal safety caps remain |
| Simplified Trace settings | Gear icon; only Max hops, Interval >=250 ms, Timeout >=500 ms, Packet size; continuous polling forced on |
| Important-only trace events | Lifecycle, outcome transitions, observed route alternatives and API/DNS errors; no normal reply/round entries; repeated identical errors/changes suppressed for 10 seconds |
| Addresses List to Ping/editor/card sizes | Renamed heading, framed editor, six 110-pixel compact summaries |
| Additional Ping columns | Last ping, Last success, Reachable since, Unreachable since, Result / error; all available by default |
| History status | Up/Unreachable with the same status colors; completed sample outcome rather than aggregate warning status |
| Address templates | Named local save/replace, dropdown load, × delete with Yes/No (default No); 100-template/2 MB limits |
| Trace destination history | Editable local dropdown; valid started addresses retained, case-insensitive deduplication, most recent first; × deletes immediately, retaining session text |
| Trace sessions | + creates independent engine/metrics/log/cancellation; × awaits that session's cleanup; up to eight; closing last leaves a fresh blank session |
| Active menu/session indication | Green dot/glow reflects actually Starting/Running tools; paused/stopped indicators are off |
| Faster menu navigation | Real page views retained; no synchronous snapshot refresh in navigation; only active page/selected trace paints results |
| Resize guide | Blue vertical drag guide with current pixel-width badge, removed when drag ends |
| Tooltips | Actual buttons/icons use accessible names and one-second hover delay, including window controls |
| Blank-space deselection | Clicking empty Ping results clears selection/history; later replies do not select a row again |
| Fit Columns | Ping results/history and Trace results buttons; WPF header/realized-cell auto-sizing; final visible column stretches right; column preferences persist |
| Stable manual history scroll | Preserve first visible actual sample/fraction during batched prepend; user wheel/scrollbar input takes precedence |
| Independent polling | Existing per-target Ping scheduler; rolling Trace discovery then separate monotonic per-TTL loops, staggered deadlines, safe limits and awaited rediscovery cleanup |

The new since timestamps start only with completed actual ICMP evidence; they mark reply/no-reply streaks. DNS errors and cancellation do not invent measurement times. The aggregate live status retains its three-failure threshold, so its Warn/Unreachable state may differ from an individual history sample. Extra columns remain horizontally scrollable on narrow displays. Fit uses realized content rather than scanning every retained offscreen row.

## Verification of the final build

| Check | Result / evidence |
|---|---|
| Release solution build | PASS — zero errors/warnings |
| Automated Core/Infrastructure tests | PASS — 70 passed, 0 failed, 0 skipped; includes actual IPv4/IPv6 loopback integration and deterministic slow-hop/session/transition/save tests |
| Independent slow vs fast TTL | PASS — fast-hop samples continue while a simulated slow TTL awaits; per-TTL overlap 0; cancellation excluded from loss |
| Native maximize/work area | PASS — actual maximized WPF HWND fits current monitor work area |
| Settings dialogs | PASS — actual WPF dialogs contain exactly 3/4 inputs; invalid 249 ms interval leaves the dialog open with error |
| Template workflow | PASS — naming/loading and No/Yes deletion paths through ViewModel test dialogs; local JSON persistence |
| Destination dropdown/history | PASS — actual editable ComboBox selection, immediate deletion, persisted history and retained selected destination text |
| Two independent real traces | PASS — pausing one retains its counters while the other advances; closing awaits cleanup |
| History scroll/deselect/sort | PASS — actual samples preserve viewport, blank click clears selection, static Host sorting does not reset per reply |
| Resize guide/Fit/tooltips | PASS — actual Thumb routed events show/remove adorner; final column star width; named one-second hints |
| Actual WPF renders | PASS — inspected final Live Ping, resize guide and two-session Trace renders; 1536x1024/1280x800 and 100/125/150% bitmap scaling; zero binding errors |
| Published DLL subject | PASS — QA loads and verifies SHA-256 of NetStucked.Core.dll, NetStucked.Infrastructure.dll and NetStucked.dll from final publish |
| Self-contained win-x64 executable | PASS — publish succeeded; hidden QA-owned executable exposes actual WPF HWND and closes gracefully, exit 0; isolated child data directory contains its own saved settings |
| Installer foundation | Static source retained; stable AppId/version/publish payload configuration; ISCC unavailable, compilation NOT TESTED |

Final exact published-code run: [stdout](../artifacts/ui-refresh/final/windows-qa.log), [results](../artifacts/ui-refresh/final/windows-qa/soak-results.txt), [engine diagnostics](../artifacts/ui-refresh/final/windows-qa/engine-diagnostics.json). Core TRX is under `artifacts/ui-refresh/final/test-results`.

| Measurement | Observed final run |
|---|---:|
| Duration | 60.1 seconds |
| Actual Ping targets / requested interval | 254 loopback addresses / 250 ms |
| Ping Sent / Received | 61,356 / 61,356 |
| Two independent continuous traces | 241 completed rounds / 241 retained samples each |
| Peak combined outstanding ICMP | 15 |
| Same address/TTL overlap | 0 |
| Ping skipped deadlines | 0 |
| Active requests after Stop/shutdown | 0 |
| WPF binding/session errors | 0 |
| Navigation samples / mean / maximum | 24 / 2.9 ms / 5.8 ms |
| Maximum Dispatcher heartbeat gap | 85.5 ms |
| Ping publish-to-UI lag mean / recent p95 | 29.17 ms / 46.88 ms |
| Managed memory before / after forced GC | 10,186,600 / 16,388,096 bytes |

Navigation times include switching and yielding to ApplicationIdle while both tools run; they do not establish human click-to-pixel latency on every device. The heartbeat includes its deliberate 50 ms delay and occasional navigation work. UI lag p95 covers at most 256 recent samples; the maximum observed UI lag was 115.24 ms. History fills bounded retention, explaining the memory increase. These measurements demonstrate this local run, not a guarantee for remote WAN response times.

The preceding development and published-stage runs are preserved under `artifacts/ui-refresh/windows-fourth` and `windows-published`. They are distinct evidence. The final executable adds only isolated smoke-data startup selection after the earlier published-stage run; final DLLs were independently tested again as described above. Earlier performance-stage ten-minute evidence does not establish a ten-minute soak of this revised build.

## Runnable output and reproduction

Run [NetStucked.exe](../artifacts/ui-refresh/final/publish/NetStucked.exe) with its entire directory, or extract the full [portable ZIP](../artifacts/ui-refresh/final/NetStucked-0.1.0-win-x64-ui-refresh.zip). Keep version 0.1.0; payload/source SHA-256 and ZIP integrity are recorded in `artifacts/ui-refresh/final/release-manifest.json`. Earlier packages remain unchanged.

```powershell
$netDotnet = 'C:\Users\PK\.codex\cache\netstucked-tools\dotnet\dotnet.exe'
& $netDotnet build NetStucked.sln -c Release --no-restore
& $netDotnet test tests/NetStucked.Tests -c Release --no-build --logger trx
& ./scripts/Publish.ps1 -Dotnet $netDotnet -PublishDirectory artifacts/ui-refresh/final/publish
& ./scripts/SmokePublished.ps1 -PublishDirectory artifacts/ui-refresh/final/publish
# Use the isolated QA runtime containing the three exact published application DLLs:
& $netDotnet artifacts/ui-refresh/final/qa-runtime/NetStucked.WindowsQa.dll artifacts/ui-refresh/final/windows-qa 60 artifacts/ui-refresh/final/publish
```

Scripts and commands were executed. The smoke helper opts its own child into NETSTUCKED_QA_DATA_DIRECTORY; ordinary launches continue using `%LOCALAPPDATA%\NetStucked`. Templates/history save atomically and immediately; settings/column preferences persist at normal shutdown. Back up settings.json before running an older development package: older serializers can drop newly added preference properties on save.

## Limits actually untested

- Actual remote multi-hop paths, router rate limiting, real ECMP/route changes or WAN mixed-timeout performance. Only loopback uses real networking here; intermediate-hop/route/cancellation behaviors use deterministic adapters.
- Human drag/hover/native Yes/No/CSV-save interaction and keyboard/screen-reader acceptance. Automated WPF control/workflow checks do not substitute for a human pass.
- Multiple monitors, actual DPI transitions, auto-hide/alternate taskbar positions, Windows 11 or clean machines. The native bound test covers this current Windows 10 monitor; bitmap scaling is not monitor-DPI testing.
- Inno compilation and install/uninstall/upgrade/rollback. ISCC is not installed; no installer EXE exists. No compiler installation or updater was attempted.

Canonical hashes rechecked:

- APPROVED_UI_SCOPE.md: `0F722DA815555532A330E1AE9C5121778AA184CC740C94DA82CB7E65D3003776`
- LivePing_APPROVED.png: `45F941795FA7347A824976B79F73CD61F029778F7AFECC246DF302E181D55772`
- Traceroute_APPROVED.png: `E8F8F3AD08D9D9CA844BC2F2F3326910DF74583AFE52DBC4730C19085F2CB2F1`

Final portable package: 411 files, 147,290,815 payload bytes; ZIP 63,546,739 bytes. All 411 decompressed entries and 55 current source hashes verified. ZIP SHA-256: `0da8ebeab648ffbef98cbadf67bb2200635b2fbd5671d52f4371d9d839cf0328`.
