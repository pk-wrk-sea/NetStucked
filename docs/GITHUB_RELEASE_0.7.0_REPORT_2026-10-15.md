# GitHub release 0.7.0 — 2026-10-15

[v0.7.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.7.0) is published as **GitHub Latest**, stable, not draft/prerelease, at `2026-10-15T07:06:13Z`. The human clarified that the prior GitHub publication request includes an actual updater release. The earlier source-only PR did not expose a new version to the updater; this publication completes that step.

Source tag: `24b1b84db39c2f286ff1a05937339105d43a59c3`. [PR #1](https://github.com/pk-wrk-sea/NetStucked/pull/1) merged as `cb50a063fb0ca2cbce3629af759eb8b9ce0ea26b`, with an identical source tree. Package ProductVersion is `0.7.0+24b1b84db39c2f286ff1a05937339105d43a59c3`. Later documentation commits do not replace the immutable tag or packages. Approved implementation/provider limitations are in [the utility revision](TOOLS_REVISION_2026-10-15.md).

## Verification

- Release build: PASS, zero warnings/errors. Unit/adapter tests: **315 passed, 0 failed, 0 skipped** locally and on the tagged installer CI source.
- Precommit actual WPF checks: **221 assertions**, new tools/navigation, live subnet updates, Light/Dark, owned DNS responses, TCP-only UI, existing diagnostic lifecycle and no binding errors.
- Exact frozen portable DLL/native checks: PASS. The QA runtime loaded the three exact published application DLLs; actual WPF startup/graceful exit returned 0. The payload includes the private .NET 10 Windows runtime and requires no machine runtime installation.
- Exact-package short soak: PASS, **60.6 seconds**, 254 owned-loopback Ping targets, 32 owned TCP endpoints, two traces and repeated owned DNS batches. Ping sent/received 4118/4118; maximum combined ICMP concurrency 36; same-address/TTL overlap 0; each trace completed 241 cycles. Observed maximum loop gap 517.2 ms includes awaited work and is not a pure Dispatcher latency or long-soak claim. This exact run recorded 244 assertions including package/soak checks.
- [Branch Windows build](https://github.com/pk-wrk-sea/NetStucked/actions/runs/38695704373): PASS; [PR Windows build](https://github.com/pk-wrk-sea/NetStucked/actions/runs/38695708975): PASS; [merged main Windows build](https://github.com/pk-wrk-sea/NetStucked/actions/runs/38696065324): PASS.
- [Verified Windows installer workflow](https://github.com/pk-wrk-sea/NetStucked/actions/runs/38696030718): PASS. It checked out the existing version tag, tested its source, downloaded the frozen ZIP by the supplied SHA-256, exercised its exact application DLLs/native executable, and compiled Inno from those bytes. The workflow was dispatched from main before the PR merge; its verified checkout and `installer-build.json` identify the actual tagged source above.
- All six downloaded draft assets: SHA-256/size/uploaded-state checks PASS against GitHub metadata, hash sidecars and both manifests. Installer product/version normalized exactly as the production updater does: NetStucked / 0.7.0. Windows signature status: NotSigned.
- **Real published 0.6.0 updater path: PASS.** A QA harness compiled with 0.6.0 assembly references runs beside the original application bytes extracted from the actual public 0.6.0 portable ZIP. Production `GitHubReleaseSource` and `UpdatesViewModel` report `Version 0.7.0 available`, `Minor update`, and enabled `Install v0.7.0`, with the actual public installer ID, size, SHA-256 and fixed-project HTTPS URL. No fixture/version spoofing of application DLLs is used.
- Published 0.7.0 checker: PASS, no newer version, same-version Reinstall, documented previous 0.6.0 and selectable 0.6.0 Recovery. This is checker/ViewModel acceptance, separate from manually clicking the installed UI.
- Actual public installer preparation from **both 0.6.0 and 0.7.0**: PASS. Downloaded public installer hash/size/product/version, Windows WinVerifyTrust, Internet Mark of the Web, unsigned rejection without acknowledgement, exact four-file helper copy and native helper acknowledgement/wait-for-parent were verified. Owned helpers stopped while their owned parent remained alive; settings stayed byte-identical. **Setup was never executed.**
- Published 0.6.0 asset IDs, sizes, digests and update timestamps remain unchanged. Original approved scope/reference bytes remain unchanged.

## Frozen assets

| Asset | Bytes | SHA-256 |
|---|---:|---|
| `NetStucked-0.7.0-win-x64-portable.zip` | 66157861 | `4203c0162e1f07f0bb45579c16c5b742f69a310b827ece33cce095f8db1f8cbc` |
| `NetStucked-0.7.0-win-x64-setup.exe` | 47227154 | `6835322de93c338e78e91fc1ffe5393c087ff33e04b24f8609b39335981677f9` |

Six assets include both `.sha256` files, `package-manifest.json` and `installer-build.json`. ZIP entries: **412**, each verified against the frozen payload before upload. Evidence is retained in Git-ignored `artifacts/github-release/0.7.0/`: `tests/`, `exact-wpf/`, `final/`, `draft-download/`, `draft-verification.json`, `installer-ci-evidence/` and `public/`. Public checker JSON is under `public/from-060` and `public/from-070`; installer results are under `public/installer-from-060` and `public/installer-from-070`. Baseline public source is under `baseline/`.

## Remaining acceptance

**NOT TESTED:** actual clean-machine setup/upgrade/recovery/uninstall, Windows 11 matrix, physical DPI/mixed-monitor/accessibility or manual installed-UI acceptance, remote DNS/network matrices and long soak. Installer signing/reputation remains open; explicit unsigned acknowledgement and normal UAC/SmartScreen are retained. No background checking/installation was added.

URL Checker is an explicitly unconfigured in-app layout: VirusTotal/Palo Alto account keys and a supported Palo Alto API are still required before live provider integration. Publication does not close M02/M07/M08 or those provider/manual gates.

To update, open **Updates → Check for Updates**, select **Install v0.7.0**, acknowledge the hash-verified unsigned project installer and follow the ordinary Windows prompts. The public release page also provides direct downloads.
