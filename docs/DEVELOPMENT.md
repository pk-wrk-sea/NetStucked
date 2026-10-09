# Development

0.2.0 includes TCP Port Test and manual release checks; [FEATURES_0.2.0.md](FEATURES_0.2.0.md) records the added scope. Current portable output is `artifacts/port-test/0.2.0/final/publish`; use a fresh explicit `-PublishDirectory` to preserve earlier packages. The Windows QA soak now also runs 32 owned TCP loopback listeners and rotates visible pages every five seconds, alongside 254 Ping targets and two continuous trace sessions. It records actual TCP overlap/concurrency, connection counts and UI timing as well as the ICMP measurements. Network fixtures remain test-only. ISCC compilation remains a separate prerequisite.

Use Windows 10/11 x64 and .NET 10 SDK. Core/tests are platform-independent; WPF GUI requires Windows. `global.json` accepts compatible .NET 10 feature bands. Restore exact project package versions from NuGet; use a Visual Studio version supporting .NET 10 or the CLI.

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
