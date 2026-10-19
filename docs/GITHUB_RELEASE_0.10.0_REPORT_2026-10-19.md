# 0.10.0 publication evidence - 2026-10-19

[Published release](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.10.0), published 04:06:57 UTC, stable, GitHub Latest, six assets. Standing human publication intent applies to the approved first Terminal slice. [Scope](FEATURES_0.10.0.md), [QA and manual gates](QA_0.10.0.md).

[PR #4](https://github.com/pk-wrk-sea/NetStucked/pull/4) merged as `c466d3a6ccfc70d5acf34d95e4786f4159197226`. Immutable source/tag `v0.10.0`: `440b5a90639c07716ef291ec0e7876128036f805`; merge tree matches that source. Original scope/approved PNGs and all older published tags/assets remain unchanged. Later publication documentation does not change the tag or payload.

| Payload | Bytes | SHA-256 |
|---|---:|---|
| NetStucked-0.10.0-win-x64-portable.zip | 80,704,599 | `df4e72489b1d48af748b9ba6244cdab046634ad599ea7405e58f9e1f404a37ea` |
| NetStucked-0.10.0-win-x64-setup.exe | 57,198,619 | `1f8d01a4d9844ae9f09dc01e852927bb21c26e52651c729df1619b59c86a6f9c` |

The portable has 496 files. Every ZIP path/length/hash matches the frozen publish directory and package manifest; executable ProductVersion is `0.10.0+440b5a90639c07716ef291ec0e7876128036f805`. Bundled xterm assets/licenses and verified vendor provenance are present; application settings, terminal profiles and private keys are absent. Six assets comprise both binaries, both SHA sidecars, package-manifest.json and installer-build.json. All downloaded asset sizes/digests, source/version/provenance and sidecars match. Windows identifies Setup as NetStucked 0.10.0, **NotSigned**. Setup was never executed.

PASS:

- Release build: no warnings/errors; **424/424 tests**, no skips (26 focused Terminal cases, including actual owned raw Telnet).
- Source WPF: **249 assertions**. Exact published application assemblies: **272 WPF/60-second assertions**, no binding errors; owned 254-target loopback Ping, 32 TCP endpoints, two traces and DNS coexistence. This is not physical DPI/manual/network-capacity acceptance.
- Exact payload: **30 actual WebView2/xterm + owned SSH assertions**, password/encrypted OpenSSH key, no credentials on bad pins, ANSI/UTF-8/cursor/alternate screens, actual Ctrl+C, guarded paste, OSC52 API-write suppression, resize/zoom, navigation/tab isolation/limits/close and bounded output. Actual browser captures are separate from WPF chrome renders.
- Shared Collector SSH regression: **12 assertions**, all 8192 lines (>8 MiB), stdout/stderr/UTF-8, trust, bounded AUTH fallback/MFA and cancellation.
- Native smoke and two saved-monitor auto-resume/instance-restoration/clean-exit launches, owned loopback only.
- [Branch CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39197226958), [PR CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39197234022), [merged main CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39197526699), [exact-package installer CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39197545375): all success. Installer workflow checks out the immutable tag and compiles from the frozen ZIP, never a rebuilt application payload.
- Anonymous public installer URL downloads the exact 57,198,619-byte file with the hash above and Windows NotSigned status. This direct-download check is separate from production updater/catalog acceptance.

BLOCKED: production **0.9.0 → 0.10.0 public updater** from the actual previous release assemblies receives anonymous GitHub API HTTP 403, rate-limit remaining=0. After advertised reset **04:12:03 UTC**, the retry still fails; at 04:12:16 the response Date is 04:07:44 with the same zero quota/reset. Therefore automatic catalog discovery/Install eligibility, latest Reinstall/Recovery and public-catalog installer/helper staging are **not claimed PASS** for this release. Authenticated administrative Latest metadata confirms v0.10.0 with six stable assets; it is not substituted for the anonymous app path. No application token, proxy, quota bypass or alternate catalog was added. Public manual download works.

BLOCKED / NOT TESTED: Windows Clipboard could not be opened in this working session. Paste policy, declined multiline paste and emulator-to-SSH input pass independently; successful OS clipboard copy/paste is not claimed. Real Cisco/vendor/IPv6 endpoints, unusual authentication/terminal variants, long-session memory plateau, physical/mixed DPI, high contrast/screen-reader/manual acceptance, clean-machine Runtime/Setup/upgrade/recovery/uninstall remain NOT TESTED. Serial/SFTP/splits/recording are outside this first slice. Full A01 remains open.

Local evidence: `artifacts/github-release/0.10.0/final/`, `draft-verification.json`, `published-release.json`, `public-download-verification.json`, `public-api-response.json`, `public-api-after-reset.json`, and `public-from-0.9.0-after-reset.log`. Initial failed attempts are retained and excluded from PASS evidence. No real device targets or credentials were loaded by development tests.
