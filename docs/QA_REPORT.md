# NetStucked verification reports

**Current 0.2.0:** [TCP Port Test and manual Updates QA](QA_0.2.0.md), including exact published-code WPF/ICMP/TCP tests, manual GitHub read and a simultaneous polling soak. Release build and 115 tests pass. Runnable output: `artifacts/port-test/0.2.0/final/publish/NetStucked.exe`. The measurements below are historical 0.1.0 evidence; they do not substitute for the current report.

**Previous 0.1.0 follow-up:** [2026-10-08 UI and independent-polling revision](UI_REVISION_2026-10-08.md). Release build and 70 automated tests pass. Current runnable output is `artifacts/ui-refresh/final/publish/NetStucked.exe`. The revision report records exact final published-code WPF/loopback checks, native maximize bounds, templates/history/multiple sessions, settings and performance limits. The [performance report](PERFORMANCE_REPORT_2026-10-08.md), [bug audit](BUG_AUDIT_2026-10-08.md) and bootstrap measurements below belong to older packages; those artifacts remain unchanged.

Run date: 2026-10-08, Asia/Bangkok. Host: Windows 10 Pro 22H2, x64, build 19045. SDK: .NET 10.0.401; self-contained runtime 10.0.12. The repository started with instructions/reference assets and no application code or Git metadata. No commit, push or release publication occurred.

## Acceptance

| Check | Result | Evidence / limit |
|---|---|---|
| Visual Studio solution / C# WPF / MVVM / DI | PASS | Five projects, including portable Core tests and Windows QA harness; real XAML controls |
| Restore and Release build | PASS | Zero warnings, zero errors |
| Automated tests | PASS | 42 passed, 0 failed, 0 skipped (40 portable tests + 2 loopback integration cases); TRX under `artifacts/test-results` |
| Live Ping real APIs | PASS | Actual IPv4 and IPv6 loopback echo, DNS loopback, completed measurements only |
| CIDR, limits, duplicate input | PASS | /24=254, /31=2, /32=1, overflow/cap/malformed/Unicode/line-ending tests |
| Concurrency, no same-target overlap | PASS | 254-target adapter test, bound 8; real soak results below |
| Pause/Resume/Stop/restart/cancellation | PASS | Adapter tests and actual Windows view-model smoke; cancelled attempts excluded |
| Traceroute TTL/statistics/change semantics | PASS | TTL progression, terminal unreachable, missing response, partial-cycle cancellation, jitter, ECMP alternate observations tested with deterministic adapters |
| Real continuous trace | PASS | Actual IPv4 loopback destination at TTL 1, repeated cycles, real event log |
| Actual remote multi-hop / filtered route | NOT TESTED | No authorized remote network was supplied; intermediate/route-change behavior tested through adapters only |
| Selected history, filters, sorting, export | PASS | Windows commands and collections; exact selected-host history; UTF-8/escaping/formula text unit tests |
| Column persistence | PASS | Binding-key layouts captured and local JSON round-trip tested; manual resize/chooser interaction remains untested |
| Native load/save/settings dialogs | NOT TESTED | Implemented with safe native file dialogs and validated settings; interactive dialog/file error matrix not manually exercised |
| Approved layout hierarchy | PASS | Actual WPF bitmap renders inspected at 1536x1024 and 1280x800; no binding errors; one action group per page; six Ping KPIs, zero Trace KPIs |
| 125% / 150% render scaling | PASS | Real WPF visual tree rendered at scaled bitmap DPI, with readable text |
| Real monitor DPI transitions / keyboard / screen reader | NOT TESTED | PerMonitorV2 manifest and accessible icon names are present; no human multi-monitor/accessibility pass |
| Self-contained win-x64 publish | PASS | Includes both .NET and WindowsDesktop runtimes; exe metadata 0.1.0 / 0.1.0.0 |
| Published application launch and graceful close | PASS | Actual WPF HWND found; WM_CLOSE waits for async cleanup; fresh published process exit 0 |
| Inno Setup configuration | PASS (static review) | Stable AppId, x64 Program Files, full publish payload, Start Menu/optional desktop shortcuts, uninstall registration |
| Installer compilation | NOT TESTED | ISCC not installed. Tool policy blocked the compiler installation command before it ran |
| Clean Windows 10/11 install/uninstall/upgrade | NOT TESTED | No installer or clean VM produced; Windows 11 not available |
| Skill validation | PASS | Bundled quick_validate.py: `Skill is valid!` (PyYAML installed only under ignored artifacts for validation) |
| NuGet known vulnerabilities | PASS | `dotnet list ... package --vulnerable --include-transitive` reports none against current configured source |
| Protected scope/reference files | PASS | SHA-256 values match the initial read (listed below) |

## Commands actually executed

The system `dotnet` initially had only .NET 6 and no SDK. Downloaded the .NET 10 SDK ZIP from Microsoft's release metadata, checked SHA-512 against that metadata, and extracted it under a user-local tools folder. No machine-wide SDK replacement was performed.

```powershell
$netDotnet = 'C:\Users\PK\.codex\cache\netstucked-tools\dotnet\dotnet.exe'
& $netDotnet new sln -n NetStucked --format sln
& $netDotnet sln NetStucked.sln add src/NetStucked.Core/NetStucked.Core.csproj src/NetStucked.Infrastructure/NetStucked.Infrastructure.csproj src/NetStucked.Desktop/NetStucked.Desktop.csproj tests/NetStucked.Tests/NetStucked.Tests.csproj
& $netDotnet sln NetStucked.sln add tests/NetStucked.WindowsQa/NetStucked.WindowsQa.csproj
& $netDotnet restore NetStucked.sln
& $netDotnet build NetStucked.sln -c Release
& $netDotnet test NetStucked.sln -c Release --no-build --logger trx --results-directory artifacts/test-results
& $netDotnet test tests/NetStucked.Tests -c Release --no-build --filter 'Category!=Integration'
& $netDotnet run --project tests/NetStucked.WindowsQa -c Release --no-build -- artifacts/windows-qa-final 0
& $netDotnet list NetStucked.sln package --vulnerable --include-transitive
& ./scripts/Publish.ps1 -Dotnet $netDotnet
& ./scripts/SmokePublished.ps1
& $netDotnet artifacts/qa-runtime-001/NetStucked.WindowsQa.dll artifacts/windows-qa-soak 600 2>&1 | Tee-Object -FilePath artifacts/windows-qa-soak.log
```

Final stdout summaries:

```text
Build succeeded. 0 Warning(s), 0 Error(s)
Passed! Failed: 0, Passed: 42, Skipped: 0, Total: 42
Portable filter: Failed: 0, Passed: 40, Skipped: 0, Total: 40
WINDOWS QA PASSED; images are actual WPF renders, not manual desktop screenshots.
PASS self-contained published application starts its actual WPF window and closes gracefully (exit 0).
```

Publish.ps1 executes the specified `dotnet publish src/NetStucked.Desktop/NetStucked.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64` operation. A copy of the tested Windows QA output in `artifacts/qa-runtime-001` ran the 600-second soak, keeping its binaries isolated from publish operations. Later input-validation and no-op trace-event hardening was rechecked by all final tests and fresh Windows smoke. Probe scheduling and statistics behavior is unchanged; the soak is not an exact-byte check of the subsequent build.

## Actual stability measurements

The ten-minute run probed **only** 254 loopback IPs (`127.0.0.0/24`, usable hosts .1–.254). Results are in `artifacts/windows-qa-soak/soak-results.txt` and the captured `artifacts/windows-qa-soak.log`.

| Measurement | Observed value |
|---|---:|
| Duration | 600.2 seconds |
| Unique expanded targets | 254 |
| Completed sent / received | 150,934 / 150,934 |
| Per-host overlap | 0 |
| Peak concurrent requests | 32 |
| Maximum Dispatcher heartbeat gap | 230.7 ms |
| Managed memory before / after forced GC | 6,434,872 / 16,955,920 bytes |

PASS: no session errors, coherent counters, actual WPF renders without binding errors and zero active requests after Stop. Memory grew as real bounded histories filled; retention is capped at 393 samples per target for this scope (99,822 samples globally). Dispatcher timing includes the 50ms heartbeat interval and reflects this host/load only, not a universal performance guarantee.

## Visual differences / deliberate limits

The approved three-zone Ping and two-zone Trace hierarchy is retained. The editable target area uses plain multiline grammar rather than the screenshot's illustrated aligned/numbered rows. DataGrid columns use native sorting, resizing and horizontal scrolling; the wide Trace table needs scrolling at these widths. Glyphs use built-in Windows symbols/vector shapes rather than copying raster icons. A small session-state line provides actual state/No final reply. Those are implementation differences, not extra pages or charts. Render PNGs show only actual loopback results, never screenshot example data.

Charts are explicitly disabled in 0.1.0. Optional history expansion is omitted. Traceroute supports IPv4 ICMP only; IPv6 Live Ping was verified on loopback. Echo RTT comes from PingReply; TTL-expired/terminal response RTT uses elapsed real-request time because Windows does not provide useful API RTT for every ICMP error response. ICMP loss and route alternates do not establish host/service outage. See `NETWORK_BEHAVIOR.md`.

Actual final WPF renders: `artifacts/windows-qa-final/LivePing.png`, `Traceroute.png`, corresponding narrow/scaled images, plus the 254-row soak render. They are WPF RenderTargetBitmap outputs, not human-operated desktop screenshots. No claim of pixel-perfect or human acceptance is made.

## Artifacts / release readiness

Runnable application: `artifacts/publish/win-x64/NetStucked.exe` with its full directory (required). Portable package: `artifacts/NetStucked-0.1.0-win-x64-portable.zip`. Final file counts/bytes and SHA-256 for every payload file are in `artifacts/release-manifest.json`; a `.sha256` file accompanies the ZIP. Installer source: `installer/NetStucked.iss`. **No installer EXE was produced.** Expected path after a successful ISCC build: `artifacts/installer/NetStucked-0.1.0-win-x64-setup.exe`.

Final publish: **411 payload files, 147,149,287 bytes**. Executable: **162,816 bytes** (requires adjacent runtime/files). Portable ZIP: **65,546,973 bytes**. ZIP SHA-256: `8C56D651C0F3F7D1A0E2D2A8C53398D0A6059D3E930A5702A374F7063A913276`. Every ZIP payload entry was decompressed and checked for matching length and SHA-256 against the release manifest: **411/411 PASS**.

Development implementation is usable and locally verified. Production release acceptance remains pending installer compilation, clean install/uninstall/upgrade, Windows 11, authorized remote network and human GUI/DPI/accessibility checks. Online updating/rollback is not implemented; manual release/recovery planning is documented in `VERSIONING.md` and `RELEASE.md`.

## Preserved canonical bytes

- `docs/APPROVED_UI_SCOPE.md`: `0F722DA815555532A330E1AE9C5121778AA184CC740C94DA82CB7E65D3003776`
- `design/references/LivePing_APPROVED.png`: `45F941795FA7347A824976B79F73CD61F029778F7AFECC246DF302E181D55772`
- `design/references/Traceroute_APPROVED.png`: `E8F8F3AD08D9D9CA844BC2F2F3326910DF74583AFE52DBC4730C19085F2CB2F1`

## API/tool references

Used primary sources to confirm available .NET API/package/compiler capabilities: [Microsoft Ping.SendPingAsync (.NET 10)](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ping.sendpingasync?view=net-10.0), [MVVM Toolkit](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/), [official NuGet package feed](https://api.nuget.org/v3/index.json), [Microsoft .NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json), [Inno Setup verification](https://jrsoftware.org/isdl-verify.php). Publisher release metadata was read; the Inno download/install command itself was blocked before execution, so its local compiler signature/install was not verified.
