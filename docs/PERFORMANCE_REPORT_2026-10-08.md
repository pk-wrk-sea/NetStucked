# NetStucked 0.1.0 performance verification — 2026-10-08

Implemented the authorized scheduling, traceroute and UI performance improvements. The application continues to display only real production API results. Deterministic network adapters below are test fixtures, never initial application data. Approved scope/reference PNG bytes and the two main page layouts remain unchanged. Version stays 0.1.0 as requested; no commit, push, updater or new page was created.

## Changes

- Ping schedules start-to-start deadlines with Stopwatch, staggered initial admission, bounded channels and fixed workers. It skips missed intervals instead of adding request duration to every interval or overlapping a target. DNS has separate bounded workers, a 30s cache, 5s failure backoff and a 3s real lookup timeout. Literal IPs bypass DNS. ICMP has a configurable token-bucket rate cap; pending/cancelled work contributes no packet loss.
- Trace discovers the path through bounded windows of distinct TTL probes (default 4), publishes destination replies before slower earlier hops finish, cancels/awaits speculative higher TTLs and then refines configured samples per visited hop. Cycles and probes for an identical TTL never overlap. Optional reverse DNS runs independently and cannot rename a hop whose address has changed.
- Adaptive continuous polling retains the known destination and rotates intermediate hops between full sweeps. It samples the full route after at most four partial polls, at the next eligible cycle after the full-sweep interval, and after detected alternates/missing destination. Unpolled hops retain their Updated timestamp and do not acquire fake timeout/loss. Disable Adaptive polling to trace the full path every cycle.
- The visible WPF page reads revision deltas every 50ms, coalesces by target/hop and applies row/event changes with a cooperative 4ms budget. Sorting/filtering during monitoring is throttled to 500ms; Trace Description edits defer view refresh. History and event rows append incrementally. Hidden pages stop presentation timers while engines continue. Existing Settings/Advanced Settings dialogs expose the new bounded controls.
- Session/cycle/probe IDs and immutable cached snapshots prevent stale presentation across restarts. Bounded diagnostics separate scheduler lag, queue wait, DNS duration, API duration and publish-to-UI lag from actual RTT columns. Stop/shutdown writes timing summaries to existing local logs.
- Final review moved speculative Trace cancellation callbacks outside the snapshot lock. An adversarial adapter callback that reads a snapshot on another thread verifies that cancellation cannot hold that lock while waiting for callbacks.

See [network semantics](NETWORK_BEHAVIOR.md) for defaults, rate bursts, retained statistics, optional DNS limitations and polling behavior.

## Before/after measurements

Same Windows host and deterministic adapter scenarios. These measure application scheduling, not WAN speed or remote-router behavior.

| Scenario | Before | After | Evidence |
|---|---:|---:|---|
| Ping requested interval 250ms, actual adapter operation 150ms | 411.15ms mean start-to-start; regression failed | 251.17ms mean start-to-start; passed | Baseline TRX under `artifacts/performance/before`; final TRX under `artifacts/performance/final-59-tests` |
| Nine-hop trace, 120ms per simulated timeout/reply | 1172ms to complete; regression failed | 487.07ms to complete; first destination 410.00ms; peak 4 | Same trace regression in both TRX runs |
| Three blocked hostname lookups plus literal IP, concurrency 2 | Literal IP received no reply within 1500ms; regression failed | Literal IP reply within the limit; blocked DNS adds no Sent | Separate DNS/ICMP regression |
| 1000 adapter targets, concurrency 8, rate 20/s | Not measured | 42 starts before pause at ~1.7s; peak 8; no target overlap; cancelled/queued work excluded | Final test output; missed intervals recorded rather than replayed |

Packet rate is an average replenishment limit with a bounded burst, rather than an exact inter-packet timing guarantee. Requested target intervals are best effort when targets, timeouts or packet limits exceed capacity. Parallel TTL probes can expose ICMP rate limiting; use TTL concurrency 1 when a router needs serial probing. No remote performance claim is made.

## Checks actually performed

| Check | Result | Scope |
|---|---|---|
| Release solution build | PASS | .NET SDK 10.0.401, Windows 10 x64; 0 warnings/errors |
| Automated tests | PASS | 59 passed, 0 failed/skipped, including actual IPv4/IPv6 loopback integration cases |
| Cadence, DNS isolation/cache/backoff, 1000-target rate/cancellation | PASS | Deterministic adapters |
| TTL concurrency, refinement, destination-first presentation, speculative cancellation | PASS | Deterministic adapters; actual loopback confirms endpoint replies |
| Adaptive full sweeps/unpolled statistics, route alternates and partial-cycle cancellation | PASS | Deterministic adapters |
| Stale reverse-DNS response cannot rename current route | PASS | Deterministic delayed resolver |
| Actual WPF commands, history, sorting/filtering, CSV and active Description edits | PASS | Isolated settings store and real loopback ICMP; no binding errors |
| Background page keeps probing and refreshes on return | PASS | Actual WPF navigation and loopback |
| Published application assemblies | PASS | QA loaded exact published Core/Infrastructure/Desktop DLL bytes; SHA-256 checked before real WPF/ICMP smoke |
| Actual 1536x1024 / 1280x800 and 125% / 150% WPF renders | PASS | Actual RenderTargetBitmap output inspected; no claim of physical monitor/human acceptance |
| Ten-minute Ping + continuous Trace soak | PASS | 254 Ping loopback targets plus Trace on 127.0.1.1; earlier frozen binary, see exact limit below |
| Final published-code WPF/ICMP regression + 60s soak | PASS | Includes the final cancellation callback hardening; loaded DLL hashes checked against final publish |
| Self-contained win-x64 publish | PASS | Includes .NET and WindowsDesktop 10.0.12; EXE metadata 0.1.0 / 0.1.0.0 |
| Fresh self-contained EXE host startup / clean no-runtime machine | NOT TESTED | Published DLL code was tested through the isolated QA host; user preferences were not overwritten by an EXE smoke |
| Remote multi-hop / filtered real route / actual route change | NOT TESTED | No authorized remote test targets supplied; adapter tests do not establish WAN behavior |
| Native settings/file dialog interaction, monitor DPI transitions, accessibility | NOT TESTED | Controls are implemented; no human pass |
| Inno compiler / clean install, uninstall, upgrade / Windows 11 | NOT TESTED | ISCC unavailable; installer source statically reviewed; no installer EXE created |

## Stability results

Ten-minute evidence: `artifacts/performance/windows-soak.log`, `windows-soak/soak-results.txt` and `windows-soak/engine-diagnostics.json`.

| Measurement | Observed |
|---|---:|
| Duration | 600.1s |
| Ping targets | 254 loopback addresses |
| Ping Sent / Received | 152,664 / 152,664 |
| Concurrent Trace cycles / retained Sent | 593 / 1,779 |
| Peak combined outstanding ICMP | 14 (allowed ≤36) |
| Same address/TTL overlap | 0 |
| Maximum Dispatcher heartbeat gap | 108.7ms |
| Managed memory before / after forced GC | 7,381,728 / 18,936,720 bytes |
| Ping scheduler lag mean / recent p95 | 5.87 / 13ms |
| Ping publish-to-UI lag mean / recent p95 | 38.26 / 63.07ms |

No session or binding errors; both engines drained to zero active requests. Histories fill their bounded retention rather than growing without a cap. Heartbeat gap includes the 50ms heartbeat delay and host load. p95 reflects at most 256 recent samples; mean/max/count cover the session. Hidden Trace's UI timing is not comparable with the visible Ping page. Trace API completion diagnostics include speculative higher-TTL operations that are not retained as path rows.

Final exact published-code run: **60.1s**, **15,503 / 15,503** Ping Sent/Received, **60 Trace cycles / 180 retained Trace samples**, peak **12 combined ICMP requests**, **0 same-address/TTL overlaps**, **127.1ms** maximum Dispatcher heartbeat gap, **0 binding/session errors**, and zero active requests after Stop/shutdown. Managed memory before/after forced GC: **7,321,872 / 9,799,344 bytes**. Ping publish-to-UI lag mean/recent p95: **37.60 / 62.20ms**. Evidence: `artifacts/performance/final/windows-qa.log`, `final/windows-qa/soak-results.txt`, `final/windows-qa/engine-diagnostics.json` and actual rendered PNGs. All three loaded application DLL byte hashes matched the final publish directory.

The long run used frozen Release QA assemblies from before the final Trace cancellation callback hardening. It is **not** an exact-byte soak of the final Core DLL. The final build is independently covered by all 59 tests and a fresh published-code WPF/ICMP smoke plus 60s concurrent soak. The Ping scheduling implementation and the main UI did not change between those runs. Exact final binary/source hashes accompany the package manifest; older frozen evidence and packages remain intact.

## Reproduce and run

The system PATH dotnet has no usable SDK on this machine. The working SDK is:

```powershell
$netDotnet = 'C:\Users\PK\.codex\cache\netstucked-tools\dotnet\dotnet.exe'
& $netDotnet build NetStucked.sln -c Release --no-restore
& $netDotnet test tests/NetStucked.Tests -c Release --no-build --logger trx
& ./scripts/Publish.ps1 -Dotnet $netDotnet -PublishDirectory artifacts/performance/final/publish
& $netDotnet artifacts/performance/qa-frozen/NetStucked.WindowsQa.dll artifacts/performance/windows-soak 600
& $netDotnet artifacts/performance/final/qa-runtime/NetStucked.WindowsQa.dll artifacts/performance/final/windows-qa 60 artifacts/performance/final/publish
```

For the last command, the QA runtime directory contains the QA host with all three application DLLs copied from the publish directory. The third argument verifies their exact bytes. Every QA run uses its own isolated data directory and only loopback targets.

Runnable build: `artifacts/performance/final/publish/NetStucked.exe` with its entire directory. Portable ZIP: `artifacts/performance/final/NetStucked-0.1.0-win-x64-performance.zip`. Payload/source SHA-256 and ZIP integrity are in `artifacts/performance/final/release-manifest.json` and the accompanying `.sha256` file. Earlier bootstrap/bug-audit/performance-stage packages remain intact. Keep the files adjacent or extract the entire ZIP before running.

Final package: **411 files / 147,213,291 bytes**. ZIP: **63,511,157 bytes**, SHA-256 `4A25E56629DF3EE8ADBD953C6B529BBDB14F1B80AA2C942374BFE7E29CA93DC0`. All **411/411** decompressed ZIP entries were verified against payload lengths/hashes. The manifest also records hashes of all 48 application/project/test source files. The installer AppId remains unchanged; ISCC is still unavailable and no installer binary is included.
