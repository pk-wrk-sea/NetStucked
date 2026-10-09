# Development

Current integration (later human authorization, 2026-10-09): **0.5.0** combines the approved Network Info Wi-Fi slice with published **0.4.2** diagnostics. Version bump/publication were explicitly authorized; **0.5.0 is now GitHub Latest**, with 242 tests, exact WPF/native/soak/package/public checks and both Windows CI workflows PASS. [Publication report](GITHUB_RELEASE_0.5.0_REPORT_2026-10-09.md). See [0.5.0 scope](FEATURES_0.5.0.md) and [combined verification](QA_0.5.0.md); earlier audit/preview version and release statements below are historical snapshots. Wi-Fi hardware authentication and broader M01 routes/neighbors remain open.

Read [implementation status](IMPLEMENTATION_STATUS.md), selected [milestone](MILESTONE_INDEX.md) and [UI approval](UI_APPROVAL_REGISTER.md) before code changes. Published 0.4.0 output is `artifacts/github-release/0.4.0/final/publish`; the separate approved 0.4.1 follow-up is described in [FEATURES_0.4.1.md](FEATURES_0.4.1.md). Earlier 0.2.0 output is historical. Use fresh explicit `-PublishDirectory` paths to preserve packages. Windows soak runs 32 owned TCP listeners, 254 loopback Ping targets and two continuous traces while rotating pages. It records actual overlap/concurrency/counters/UI timing; UDP reply/silence and themes/panels/descriptions are also tested by the harness. Fixtures stay test-only and ISCC compilation is a separate prerequisite.

Use Windows 10/11 x64 and .NET 10 SDK. Core is UI-free `net10.0`; the complete integration suite now includes Windows Native ICMP/Credential Manager checks and requires Windows, as does the WPF GUI. `global.json` accepts compatible .NET 10 feature bands. Restore exact project package versions from NuGet; use a Visual Studio version supporting .NET 10 or the CLI.

Wi-Fi implementation/QA: [guide](WIFI_PROFILE_MANAGER.md), [approval](FEATURES_WIFI_2026-10-09.md), [exact results](QA_WIFI_2026-10-09.md). Native WLAN read-only smoke uses the WindowsQa `--wifi-read <output>` mode; a stopped service is a blocker, not authority to enable it. Real connection/profile/certificate mutation acceptance uses an explicitly designated lab. Delivery preview output is `artifacts/wifi-qa/delivery/publish`; no version bump or GitHub release is implied.

```powershell
dotnet restore NetStucked.sln
dotnet build NetStucked.sln -c Release
dotnet test NetStucked.sln -c Release --logger trx
dotnet run --project src/NetStucked.Desktop
dotnet publish src/NetStucked.Desktop/NetStucked.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

`scripts/Publish.ps1` runs the publish and extracts the authoritative version into installer defines. Use `-PublishDirectory artifacts/ui-refresh/final/publish` to keep an earlier published build intact. Logs are bounded rotating local files, including bounded engine timing summaries on Stop/shutdown. No credentials or GitHub token are needed. Do not change the supplied scope/reference assets. Use fake adapters only in tests, real loopback for integration and explicitly permitted targets for manual diagnostics.

Windows automation and actual WPF bitmap renders:

```powershell
dotnet run --project tests/NetStucked.WindowsQa -c Release -- artifacts/windows-qa 0
dotnet run --project tests/NetStucked.WindowsQa -c Release -- artifacts/windows-qa-soak 600
```

The second command probes only 254 loopback addresses for ten minutes at 250 ms interval while two independent continuous traceroutes run on other loopback addresses. It writes actual counters, memory/Dispatcher timing, bounded engine diagnostics and render PNGs. For published-code QA, copy the QA build output into an isolated runtime folder, replace its NetStucked.Core.dll, NetStucked.Infrastructure.dll and NetStucked.dll with those from the publish output, and invoke its NetStucked.WindowsQa.dll with output directory, soak seconds and publish directory as arguments. The optional third argument verifies SHA-256 of all three loaded application assemblies against the publish directory; settings stay isolated. This validates published application code; it does not test the self-contained EXE host on a clean machine. The harness tests the actual settings field counts/invalid input, cached navigation, template/history workflows, resize guide, column fitting, native maximized work-area bounds, stable history scroll and independent real sessions. Human hover/drag/confirmation interaction, real monitor DPI switching and clean installer testing remain untested. The executable smoke helper supplies NETSTUCKED_QA_DATA_DIRECTORY only to its own child process, keeping normal per-user preferences untouched.

For portable Core/adapter unit tests on a machine that cannot run WPF or ICMP, use `dotnet test tests/NetStucked.Tests -c Release --filter "Category!=Integration"`. Integration-tagged tests perform actual loopback ICMP. Test the published WPF executable with `pwsh -File scripts/SmokePublished.ps1`; it launches a hidden QA-owned process and closes its exact window gracefully.
