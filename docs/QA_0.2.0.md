# NetStucked 0.2.0 QA — 2026-10-09

This report preserves the original local-development evidence before publication. Later GitHub release, installer compilation and real release-menu checks are recorded in [the publication report](GITHUB_RELEASE_REPORT_2026-10-09.md).

Host: Windows 10 Pro x64, .NET SDK 10.0.401, self-contained runtime 10.0.12. Source version, executable metadata, page/sidebar version and installer version definition all derive from `Directory.Build.props` (0.2.0). This is a local development package; no commit, push, tag or GitHub release was created.

## Verified

| Check | Result and evidence |
|---|---|
| Release solution build | PASS, zero warnings/errors; all five projects |
| Automated Core/Infrastructure tests | PASS, **115 passed, 0 failed/skipped**, `artifacts/port-test/0.2.0/final/test-results/tests.trx` |
| Actual TCP IPv4/IPv6 | PASS, owned loopback listeners return Connected with measured connect time; reserved non-listening sockets return Refused with 5-second timeout; pre-cancellation throws without sending |
| TCP scheduling/lifecycle | PASS, blocked endpoint and slow DNS do not stop other endpoints; one item per endpoint, pause cancellation not counted as attempts/failures, repeated restart cleanup, rate/concurrency bounds and bounded history |
| WPF Port Test | PASS, real localhost DNS and literal-IP sockets, outcomes/history, sorting/filtering, pause/resume/stop, CSV, Fit Columns, blank deselection, named templates/deletion persistence, activity on another page |
| Manual Updates | PASS, actual public GitHub API check completed, returned no published stable releases; recorded status/date in `windows-qa-visible/github-check.txt` |
| Available release/error/close behavior | PASS through test-only release fixtures: newer SemVer notes/installer-page action, stale action removed after failure, cancellation awaited, repeatable disposal. No browser or installer is launched by fixtures |
| Sidebar grouping | PASS, actual WPF layout places Settings and Updates below all diagnostic tools, anchored at bottom |
| Existing Ping/Trace regression | PASS, actual ICMP, independent trace sessions, scrolling/selection, edit-safe refresh, CSV, templates/address history, resize guide, one-second button hints, cached navigation, native maximize bounds |
| Actual visual rendering | PASS, WPF visual trees inspected at 1536×1024 and 1280×800, Port Test and Updates match the authorized layout. Zero WPF binding errors. Canonical scope/PNG hashes unchanged |
| Self-contained Windows publish | PASS, `artifacts/port-test/0.2.0/final/publish` |
| Published executable launch/close | PASS, actual WPF HWND opens and WM_CLOSE completes with exit 0; isolated QA user-data directory; `published-smoke.log` |
| Inno configuration | Static review only: stable AppId retained, central version injected as 0.2.0, full payload files included, expected asset name `NetStucked-0.2.0-win-x64-setup.exe` |

The final Windows harness runs the **exact three published application DLL bytes**, loaded from its isolated `qa-runtime-visible` folder and checked by SHA-256 against `publish`. Its transcript is `windows-qa-visible.log`. A previous simultaneous run remains in `windows-qa.log`; current metrics below come from the extended visible-page run. Both used the same published application binaries.

## Simultaneous real-network polling

60.2 seconds of actual loopback traffic at 250 ms intervals: 254 Ping targets, two independent Traceroute sessions and 32 owned TCP listeners. Each diagnostic page stays visible for five seconds during rotation; inactive pages continue probing without presentation work. All requests drain at Stop.

| Measurement | Observed |
|---|---:|
| ICMP Ping completed / replies | 61,316 / 61,316 |
| First / second trace completed cycles | 241 / 241 |
| TCP completed / connected | 7,759 / 7,759 |
| ICMP same-address/TTL overlap | 0 |
| TCP same-address/port overlap | 0 |
| Maximum simultaneous native ICMP / TCP | 17 / 4 |
| Missed polling intervals, Ping / TCP | 0 / 0 |
| Outstanding native operations after Stop | 0 |
| Navigation layout samples / mean / maximum | 12 / 5.2 ms / 27.9 ms |
| Maximum Dispatcher heartbeat gap | 91.0 ms (includes deliberate 50-ms sampling delay) |
| Ping publish-to-UI lag mean / recent p95 / maximum | 33.40 / 46.15 / 255.73 ms |
| TCP publish-to-UI lag mean / recent p95 / maximum | 32.85 / 165.08 / 262.30 ms |
| TCP start queue mean / recent p95 | 0.041 / 0.132 ms |
| Managed memory after GC, before / after soak | 14,051,936 / 22,137,360 bytes |

UI lag includes applying the last completed sample after returning from an inactive page and the final stopped snapshot; its age can approach the 250-ms probe cadence. It is separate from TCP connect time and is not an assertion that every displayed row is under 50 ms. The bounded recent p95 is measured over the last 256 timing samples. This minute-long run fills history buffers; it does not establish long-term memory or WAN behavior.

Raw results: `windows-qa-visible/soak-results.txt`, `tcp-soak-results.json`, `engine-diagnostics.json`; actual WPF renders: `PortTest.png`, `PortTest-32-soak.png`, `Updates.png`, `Updates-narrow.png`, `Updates-recovery.png`, plus Ping/Trace regression renders.

An initial integration test expected immediate Refused within 1000 ms, but Windows returned timeout before its actual refusal response (approximately two seconds here). The test was corrected to wait 5000 ms for the real refusal; production deadlines and result classification were not fabricated or relaxed. All final tests pass. A shorter user-selected timeout legitimately remains Timeout.

## Not tested

- Authorized remote WAN TCP endpoints, real multi-hop remote paths/route changes, filtered firewall behavior and long-duration ephemeral-port pressure. Real integration traffic was restricted to owned loopback targets; deterministic adapters cover timeout/error/scheduling branches.
- Human native file-dialog/browser/download/installer actions, signed publisher verification, installing or actually rolling back a version. No release is published yet; available-release behavior is checked with explicit test-only fixtures.
- Human keyboard/screen-reader acceptance, physical multi-monitor DPI transitions and Windows 11. Bitmap scale tests for the retained Ping/Trace UI do not substitute for real monitor changes.
- **Installer compilation/install/uninstall/upgrade: NOT TESTED.** ISCC is absent from PATH and the standard Inno Setup 6 locations. No installer EXE was produced.

## Reproduction

```powershell
$netDotnet = 'C:\Users\PK\.codex\cache\netstucked-tools\dotnet\dotnet.exe'
& $netDotnet build NetStucked.sln -c Release --no-restore
& $netDotnet test tests/NetStucked.Tests -c Release --no-build --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/port-test/0.2.0/final/test-results
& ./scripts/Publish.ps1 -Dotnet $netDotnet -PublishDirectory artifacts/port-test/0.2.0/final/publish
# For a fresh repeat, use new artifact directories to preserve this evidence.
# Copy QA build output to an isolated runtime; replace its three application DLLs with published bytes.
& $netDotnet artifacts/port-test/0.2.0/final/qa-runtime-visible/NetStucked.WindowsQa.dll artifacts/port-test/0.2.0/recheck 60 artifacts/port-test/0.2.0/final/publish
& ./scripts/SmokePublished.ps1 -PublishDirectory artifacts/port-test/0.2.0/final/publish
```

The local portable ZIP, payload/source hashes and metadata are recorded in `artifacts/port-test/0.2.0/final/package-manifest.json`; retain the full extracted directory when running the EXE.
