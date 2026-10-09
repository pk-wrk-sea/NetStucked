# NetStucked 0.2.1 — TEST BUILD

This branch is a manual update/recovery exercise. Version metadata and recorded notes change from 0.2.0 to 0.2.1; diagnostic behavior and preferences schema stay the same. The main/latest release remains [0.2.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.2.0). See [the exercise steps](docs/GITHUB_RELEASE_EXERCISE.md). Download this test package from [v0.2.1](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.2.1); the inherited 0.2.0 validation below is historical and the test release has its own published evidence.

Windows 10/11 x64 desktop diagnostics for remote IP addresses, hostnames and authorized IPv4 subnets. C# / WPF / .NET 10 / MVVM. Live Ping, continuous ICMP Traceroute and TCP Port Test use actual network APIs. Updates & Recovery provides manual GitHub release checks, release/version notes and recovery guidance. The approved layouts in `design/references/` are preserved; sample telemetry is never loaded.

## Build

Install a .NET 10 SDK, then from the repository root:

```powershell
dotnet restore NetStucked.sln
dotnet build NetStucked.sln -c Release
dotnet test NetStucked.sln -c Release
dotnet run --project src/NetStucked.Desktop
pwsh -File scripts/Publish.ps1
```

Inno Setup 6 is needed only to compile the installer. Preferences/logs are per-user under `%LOCALAPPDATA%\NetStucked`. No administrator privilege or .NET installation is required for the self-contained application. Choose only targets you are authorized to diagnose. ICMP loss does not establish that a remote host or service is offline.

See [0.2.0 behavior](docs/FEATURES_0.2.0.md), `docs/FEATURE_SPEC.md`, `docs/NETWORK_BEHAVIOR.md`, `docs/DEVELOPMENT.md` and [current QA](docs/QA_0.2.0.md). Dashboard, Network Info and automatic download/install/rollback remain deferred. No commit, push or release publication is performed by this implementation.

## Current verification and runnable output

The user-requested UI revision includes compact probe dialogs, named address templates, destination history, independent traceroute session tabs, result timestamps, stable manual history scrolling, Fit Columns/resize guide/tooltips, cached navigation, activity indicators and framed monitor-aware window controls. Traceroute polls each known TTL independently; the event log records important changes. Only actual API outcomes populate results. See [the revision report](docs/UI_REVISION_2026-10-08.md) for the implemented behavior and measured validation. Earlier bug-audit and performance packages/reports are preserved as historical evidence.

Run `artifacts/port-test/0.2.0/final/publish/NetStucked.exe`, keeping its entire directory together, or extract `artifacts/port-test/0.2.0/final/NetStucked-0.2.0-win-x64-portable.zip`. Previous 0.1.0 packages remain under `artifacts/ui-refresh/final`. The SDK used on this machine is `C:\Users\PK\.codex\cache\netstucked-tools\dotnet\dotnet.exe`; use its full path until .NET 10 is on PATH. The portable application contains its runtime. Charts, future pages and automatic updates remain unavailable.

Release build and 115 tests pass. Exact published application assemblies have real Windows WPF/ICMP/TCP and self-contained executable checks. See [the current QA report](docs/QA_0.2.0.md) for simultaneous polling and measured UI timing. Remote multi-hop networking/TCP, human GUI/monitor-DPI/accessibility acceptance, Windows 11 and installer compile/install/uninstall remain **NOT TESTED**. ISCC is unavailable; no installer EXE is produced. The live GitHub check currently finds no published stable release, so installation buttons do not invent one.
