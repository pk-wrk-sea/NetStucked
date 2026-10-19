# 0.10.0 Terminal verification

0.10.0 is published as GitHub Latest. [Publication report](GITHUB_RELEASE_0.10.0_REPORT_2026-10-19.md) identifies immutable source, exact payload, CI and public-updater limits. Source verification remains separate from frozen-package and publication verification. Scope: [FEATURES_0.10.0](FEATURES_0.10.0.md). Full A01 acceptance remains open.

PASS during implementation: .NET 10 Release build with no warnings/errors; 26 focused Terminal cases covering input/paste validation, secret serialization exclusion, geometry limits, real owned loopback raw Telnet negotiation/interactive login/IAC escaping/split CRLF, cancellation during connect and blocked output, generation isolation, input saturation, resize, disconnect, no auto reconnect, safe errors, nonsecret profile roundtrip and corrupt-profile backup. The final full suite passes 424/424 tests (no skips).

PASS actual owned Paramiko SSH and WebView2/xterm: unknown/changed pin rejection before credentials, one rejected password attempt, encrypted OpenSSH key authentication, password authentication, xterm PTY/resize, actual ANSI color cells, UTF-8 Thai, cursor/erase-line and alternate-screen restoration, actual browser keyboard Ctrl+C, OSC52 not invoking the browser clipboard API, guarded-paste pipeline and declined multiline paste, Ctrl+wheel font zoom, live navigation/tab buffer isolation, bounded 6000-line output and 5000-line scrollback, Light/Dark palettes, nonsecret profile saving and update drain. Additional actual-browser checks pass for the five-tab limit, closing other tabs while the original SSH session stays live, and closing the final disconnected tab to obtain a fresh usable tab. Actual browser preview captures are separate from WPF chrome renders because RenderTargetBitmap cannot capture WebView HWND pixels. Evidence: `artifacts/terminal/qa-ui-final/`. Initial failed key-format, busy Windows clipboard and connected-profile-save test runs are retained; they are not passing evidence.

PASS existing shared Collector regression: actual pinned owned SSH streams all 8192 lines (>8 MiB), independent stdout/stderr UTF-8, correct bounded AUTH fallback/MFA behavior, host-key failure and prompt-wait cancellation. Final source whole-app WPF/loopback checks pass 249 assertions with no binding errors (`artifacts/terminal/qa-whole-final/`, `artifacts/terminal/whole-final.log`). This does not establish real-device acceptance.

BLOCKED / NOT TESTED: the working session's Windows Clipboard could not be opened during the first Copy/Paste attempt (`CLIPBRD_E_CANT_OPEN`). No successful Windows Clipboard copy/paste is claimed. Paste policy and the emulator-to-SSH pipeline were tested independently, without modifying the user's clipboard. Manual clipboard/selection/accessibility, physical/mixed DPI, long-lived resource plateau, clean-machine/runtime installation, real Cisco/vendor devices and unusual terminal/authentication variants remain NOT TESTED. Serial/SFTP/splits/recording are outside this slice. Setup is never executed on this machine.

Reproduce with the .NET 10 SDK and Python 3.13 / Paramiko 4.0.0:

```powershell
dotnet build NetStucked.sln -c Release
dotnet test tests/NetStucked.Tests -c Release --no-build
dotnet tests/NetStucked.WindowsQa/bin/Release/net10.0-windows/NetStucked.WindowsQa.dll --terminal-ui-check artifacts/terminal-qa tests/fixtures/terminal_ssh_server.py
dotnet tests/NetStucked.WindowsQa/bin/Release/net10.0-windows/NetStucked.WindowsQa.dll --collector-ssh-check artifacts/collector-ssh-qa tests/fixtures/collector_ssh_server.py
```

The terminal fixture binds only 127.0.0.1, generates ephemeral host/client keys and deletes its client key on exit. Test input/commands are sent only to that owned endpoint. No working-network targets or real credentials are loaded.

Final exact-payload PASS: 496-file ZIP verified entry-by-entry, 272 WPF/60-second loopback assertions, 30 actual terminal/SSH/emulator assertions, 12 actual Collector SSH assertions, native smoke and two saved-monitor resume/instance-restoration launches. Branch, PR, merged-main and installer Windows workflows pass. All six downloaded GitHub asset digests/sizes/provenance and installer Windows identity/trust match. Anonymous public Setup download also matches SHA-256; Setup was not executed.

Production public 0.9.0 updater: BLOCKED by anonymous GitHub API HTTP 403, remaining=0. Retry after the advertised 04:12:03 UTC reset still receives an earlier-dated 403 response. Same-version/Reinstall/Recovery and installer/helper preparation via that public catalog are therefore NOT VERIFIED for 0.10.0; authenticated administrative Latest metadata is checked separately and is not substituted for production acceptance. No quota workaround or token was added to the application.
