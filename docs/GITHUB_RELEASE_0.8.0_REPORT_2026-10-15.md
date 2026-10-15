# GitHub release 0.8.0 — 2026-10-15

[v0.8.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.8.0) is **GitHub Latest**, stable, not draft/prerelease, published `2026-10-15T09:54:43Z`. [Approved scope](FEATURES_0.8.0.md) and [usage/development QA/limits](QA_0.8.0.md) describe Monitoring/SNMP/tray/auto-resume, utility refinements, protected personal VirusTotal reports and the actual embedded Palo Alto Test A Site.

Immutable source tag: `aeb78a3c3f1b66c8275d4692aba6d037ea0aa4d4`. [PR #2](https://github.com/pk-wrk-sea/NetStucked/pull/2) merged as `41a3286c41de6ba8ac866e33a5ebfcba0da9ef47`, with the identical tagged tree. Package ProductVersion: `0.8.0+aeb78a3c3f1b66c8275d4692aba6d037ea0aa4d4`. Documentation follow-ups do not replace this source tag or frozen payload.

## Verification

- Build: PASS, zero warnings/errors. Unit/adapter suite: **354 passed, 0 failed, 0 skipped** locally and in tagged installer CI. Real owned UDP SNMP v1/v2c and protected Windows credential storage are included; v3 security selections/cancellation and fake-adapter UI tests do not establish live Cisco acceptance.
- Development WPF: **242 assertions** observed; exact portable WPF/short-soak run: **265 assertions** observed, no binding errors, Light/Dark and lifecycle checks. Actual renders are separate from manual installed-UI or physical DPI acceptance.
- Exact published application DLLs/native host: PASS. All **483 ZIP payload entries** were hash/length-verified before upload. Native startup/graceful exit PASS in installer CI.
- Exact package short soak: **60.4 seconds**, 254 owned-loopback Ping targets, 32 owned TCP endpoints, two traces and repeated owned DNS batches. Ping sent/received 4040/4040, combined ICMP maximum 36, same-address/TTL overlap 0, both traces 241 cycles. Observed loop gap 412.4ms includes awaited work; it is not a pure Dispatcher latency or remote/long-soak capacity claim.
- **Actual native Monitoring auto-resume: PASS** locally and in CI from the frozen portable package. Two app launches poll a saved explicitly started monitor against an owned loopback TCP listener, preserve Start intent and exit 0. Relaunch while each instance is alive restores it and exits the duplicate process without a second collector.
- [Branch Windows CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/38714503977): PASS. [PR Windows CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/38714712886): PASS. [Merged main Windows CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/38715394125): PASS.
- [Verified installer CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/38715399459): PASS. Tests tagged source, verifies the uploaded frozen ZIP SHA-256, exercises exact DLLs/native startup/native monitor resume/instance restoration and compiles Inno from the same bytes. Its build manifest identifies the actual source commit above.
- Downloaded draft's six assets: size/state/GitHub digest/sidecar/manifest/provenance checks PASS. Installer product/version normalize to NetStucked / 0.8.0; Windows signature status is **NotSigned**.
- **Real public 0.7.0 checker → 0.8.0: PASS.** Original 0.7.0 app bytes are extracted from its actual public portable ZIP; a QA harness compiled with 0.7.0 assembly references runs the original production checker/ViewModel. It discovers the actual 0.8.0 installer and enables Install. All four app files match the downloaded package manifest. Public 0.8.0 offers Reinstall and documented previous 0.7.0 Recovery.
- Actual public installer preparation from **both 0.7.0 and 0.8.0: PASS**: public HTTPS download/hash/size/product/version, Windows trust, Internet Mark of the Web, unsigned rejection without acknowledgement, exact helper copy and native ready/wait-for-parent behavior. Owned helpers stop while owned parent processes remain alive, settings stay byte-identical. **Setup was never executed.**
- Canonical scope/reference assets and earlier published tags/packages are unchanged.

## Frozen packages

| Asset | Bytes | SHA-256 |
|---|---:|---|
| `NetStucked-0.8.0-win-x64-portable.zip` | 77551699 | `d03b74abaf6c9c599095d1d5a0314da152e2286aa6ac385b73aa0484179c16e1` |
| `NetStucked-0.8.0-win-x64-setup.exe` | 54663620 | `446f2b52a49d908c6b922dcea87954b1e50c142d54940a628508c724b97ab37c` |

Six public assets include both SHA-256 sidecars, package-manifest.json and installer-build.json. Packaged runtime is self-contained .NET 10 Windows x64; **embedded Palo Alto separately requires Edge WebView2 Runtime**. Third-party dependency notices are included.

Evidence: Git-ignored `artifacts/github-release/0.8.0/` contains `final/`, `exact-wpf/`, `exact-native-monitor/`, `draft-download/`, `draft-verification.json`, `installer-ci-evidence/`, `baseline/` and `public/`. Development/provider evidence remains under `artifacts/development-080/`. Public exact application bytes are recorded in `public/exact-app-bytes.json`; actual checker and installer JSON are under `public/from-070`, `public/from-080`, `public/installer-from-070` and `public/installer-from-080`.

## Acceptance limits

**NOT TESTED:** positive live Cisco SNMP v3/authentication/OID matrices, live personal VirusTotal key, successful Palo category result through provider captcha, human tray/notification delivery, physical DPI/mixed monitors/accessibility, clean-machine actual Setup/upgrade/recovery/uninstall, Windows matrix and prolonged/remote production capacity. The actual Test A Site/search form loads; automated example.com SEARCH is captcha-rejected, which is recorded rather than bypassed or presented as a category result.

Use your personal VirusTotal key via the app's protected configuration. Use Palo Alto's displayed form and verification controls. X hides to Tray; explicit tray Exit stops the app. Monitoring operates while its process runs and records observation gaps. Publication does not close full M04/M07/M08 or authorize future central-service/SQLite/commercial work.

To update: **Updates → Check for Updates → Install v0.8.0**, acknowledge the hash-verified unsigned project installer and follow ordinary Windows prompts.
