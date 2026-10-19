# NetStucked 0.10.0

Windows x64 network toolkit built with C# / WPF / .NET 10 / MVVM. [Download 0.10.0 — GitHub Latest](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.10.0).

<img src="docs/branding/NetStuckedBanner.png" width="480" alt="NetStucked — Network Engineer's Toolkit" />

Terminal 0.10.0 adds real offline ANSI/xterm emulation, five independent SSH/Telnet tabs, verified SHA256 host pins, password/OpenSSH-key authentication, guarded paste, zoom and nonsecret profiles. Sessions persist across navigation/Tray and drain on Exit/update. [Usage](docs/FEATURES_0.10.0.md) - [QA](docs/QA_0.10.0.md) - [release evidence and updater limits](docs/GITHUB_RELEASE_0.10.0_REPORT_2026-10-19.md).

Config Collector 0.9.0 adds explicit-start SSH/Telnet collection, verified host pins, memory-only AUTH profiles and protected streamed TXT/JSON captures in the existing theme. [Usage and limits](docs/CONFIG_COLLECTOR_2026-10-19.md) - [QA](docs/QA_0.9.0.md). Published packages and actual verification limits: [release evidence](docs/GITHUB_RELEASE_0.9.0_REPORT_2026-10-19.md).

## Tools

- Live Ping, continuous ICMP Traceroute and TCP-only Port Test use real bounded network probes.
- Subnet Calculator calculates single-field IPv4/prefix or dotted masks while typing, with incomplete/error feedback.
- DNS Resolver supports selected records/servers, optional active-adapter IPv6 DNS servers independent of AAAA, and wrapped/resizable answers.
- MAC Checker extracts MACs and same-line IP mappings from pasted MAC/ARP text only after Check, preserving line order and repeated mappings; bounded MACVendors.com lookup remains available.
- URL Checker retrieves existing VirusTotal reports using your personal account key stored in Windows Credential Manager. Palo Alto displays the actual Test A Site inside the app and retains its form/captcha controls. **Edge WebView2 Runtime is required for the embedded website.**
- Monitoring supports up to 50 configured devices: ICMP/TCP secondary and read-only SNMP v1/v2c/v3, Test SNMP, selected interfaces/bandwidth/errors/system/custom numeric OIDs, incidents, maintenance and local notifications. Defaults: ICMP 10s/SNMP 30s, failure 3/recovery 2. Actual check times and observation gaps remain visible.

Only devices explicitly started resume on launch; Stop clears that intent. X hides to System Tray and continues collection. Restore or double-click its icon; tray Exit stops the app. Relaunch restores the existing data-profile instance. Sleep/local-network gaps are separate from device downtime. Settings and monitoring JSON contain no provider/SNMP secrets. Ctrl+mouse wheel zooms result tables/read-only text, Ctrl+0 resets. Light/Dark/System themes, copyable tables and explicit Updates & Recovery remain.

Diagnostics/old DNS/HTTP test and Network Info/Wi-Fi Manager desktop surfaces were retired in 0.7.0. Compatible legacy settings and older immutable releases remain preserved. Dashboard and other future modules remain pending. [Approved scope](docs/FEATURES_0.8.0.md), [usage and QA limits](docs/QA_0.8.0.md).

## Update or install

**Updates → Check for Updates → Install v0.10.0** (when GitHub public API access is available). Acknowledge the hash-verified unsigned project installer and follow ordinary Windows UAC/SmartScreen prompts. Checking/installation starts only after an explicit user action.

If GitHub reports an API rate limit, use the published release page for a manual download. The production 0.9.0 updater check from this development connection remains blocked; see the publication report.

The [release page](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.10.0) provides a self-contained .NET Windows x64 portable ZIP, setup EXE, SHA-256 files and provenance manifests. Keep the portable directory together. Preferences/logs live under `%LOCALAPPDATA%\NetStucked`; keys/credentials use Windows-protected storage.

## Build and verification

Install a .NET 10 SDK, then run from the repository root:

```powershell
dotnet restore NetStucked.sln
dotnet build NetStucked.sln -c Release --no-restore
dotnet test tests/NetStucked.Tests -c Release --no-build
dotnet run --project tests/NetStucked.WindowsQa -c Release --no-build -- artifacts/windows-qa 0
pwsh -File scripts/Publish.ps1
```

`Directory.Build.props` supplies the application/installer version. Inno Setup 6 is needed to compile Setup. Release operations use fresh directories, immutable source tags and exact payload verification. Diagnose only explicitly authorized targets; ICMP loss alone does not establish a remote service outage.

0.10.0: **424 tests passed**, 30 actual terminal/owned SSH checks, 272 exact-payload WPF/60-second assertions, native smoke/monitor-resume checks and branch/PR/main/installer Windows CI pass. All six GitHub asset hashes/sizes and the anonymous public installer download match. Setup was never executed. [Publication evidence and limits](docs/GITHUB_RELEASE_0.10.0_REPORT_2026-10-19.md).

**NOT TESTED:** Windows Clipboard copy/paste (clipboard unavailable here), real terminal devices, physical/mixed DPI/accessibility and long sessions; positive live Cisco/v3/authentication/OID matrices, personal VirusTotal key, successful Palo category verification through captcha, human tray/notification delivery, physical DPI/accessibility, actual clean-machine install/upgrade/recovery/uninstall and long/remote production soak. Automated Test A Site SEARCH was captcha-rejected; no category verdict was invented. Publication does not close full milestone acceptance.

## Product and historical records

[Milestones](docs/MILESTONE_INDEX.md), [implementation status](docs/IMPLEMENTATION_STATUS.md), [features](docs/FEATURE_REGISTRY.md), [next task](docs/CODEX_NEXT_TASK.md), [UI approvals](docs/UI_APPROVAL_REGISTER.md), [architecture](docs/ARCHITECTURE.md), [security](docs/SECURITY_AND_DATA_POLICY.md) and [release procedure](docs/RELEASE.md) distinguish approved implementation from future scope. Original design references remain unchanged. Earlier supported releases remain available for explicit recovery; their reports describe historical features. Only the explicitly authorized historical 0.2.x download retirement is an exception to immutable published assets. [Release/version register](docs/RELEASE_AND_VERSIONING.md).
