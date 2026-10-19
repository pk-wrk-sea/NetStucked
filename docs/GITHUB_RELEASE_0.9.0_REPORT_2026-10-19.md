# 0.9.0 publication evidence — 2026-10-19

Source: `3c558da1e367fd19d7c8518d4844e824c31eb503`, PR #3. Human-approved Config Collector adaptation and standing distributable GitHub instruction; full A02 backlog/real-device acceptance remains open.

Release source: 398 tests passed, 44 collector cases; zero build warnings/errors. Exact frozen portable: 487 files, 80,523,511 bytes; ZIP entries/hashes verified; SHA-256 `cc1fe8363830cf85b05ab45c03b6598b041e14c1a5baf55cd9ed66989c1600be`. Native startup/exit and two saved-monitor resume/instance-restoration runs passed against the real self-contained executable. Exact WPF/60-second combined owned-loopback soak passed with no binding errors. Actual owned SSH: all 8192 >8 MiB lines, stdout/stderr, Thai Unicode, pinned host keys, six bounded authentication attempts across scenarios, both MFA cases and active cancellation passed. These are fixture/loopback checks, not Cisco certification or a long-running resource plateau.

Evidence under `artifacts/github-release/0.9.0/final/`: `package-manifest.json`, `windows-qa/`, `ssh-qa/`, `native-monitor-qa/`; source TRX under `artifacts/collector/tests-release-source/`. No real credentials/configurations were used or committed. No Setup was executed on this machine.

Published stable GitHub Latest at **2026-10-19T03:08:30Z**: https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.9.0. PR #3 merged as `b93651ead28e98b3a2dc3f24454f63a852570dc7`; its tree matches the immutable tagged source. Branch CI `39192535681`, PR CI `39192552464`, merged-main CI `39192859081` and exact-package installer workflow `39192885840` all succeeded. The workflow checked out the tag, rebuilt/tested source, verified the frozen ZIP, exact WPF/native and SSH checks, then compiled Inno from that payload.

All **six downloaded public assets** verified against GitHub size/digest, SHA-256 sidecars, source manifest and installer-build provenance. Installer: **57,049,875 bytes**, SHA-256 `e85d95512f542e30f32ed2f8f0d02b10766ff71f0001b3f29f62997ed1576d11`; product NetStucked 0.9.0, Windows trust **NotSigned**, expected unsigned status. Setup was not executed. Original published tags/assets were preserved.

Exact WPF run: **273 assertions**, 60.4s soak, 4061/4061 owned ICMP responses, zero per-address/TTL overlap; maximum observed Dispatcher gap **409.1ms**, navigation maximum 24.2ms, managed memory 33,522,912 -> 46,159,664 bytes. This does not establish a long-running plateau, zero UI stalls or physical DPI/accessibility acceptance.

**PASS actual public updater check:** the real 0.8.0 application assemblies discover 0.9.0, select Install and enable the explicit installer action using actual public metadata. The real 0.9.0 assemblies find no newer release, select Reinstall and retain 0.8.0 Recovery. Evidence: `artifacts/github-release/0.9.0/public/from-080/` and `from-090/`.

**BLOCKED additional public installer staging checks:** both 0.8.0 and 0.9.0 production preparation harnesses received HTTP 403 rate-limit responses from the anonymous GitHub releases endpoint. One downloaded installer reached native signature/version checking before catalog revalidation was denied. Response reported remaining=0 and reset=2026-10-19T03:10:49Z; the same older response persisted after that time, possibly cached. No successful staging/helper-preparation PASS is claimed for this release. Direct asset download/hash/native-identity verification passed independently; users can download the verified assets from the public Release page. No provider-control workaround, credential injection or machine installation was performed.

**NOT TESTED:** real Cisco/other vendor devices and VTY limits; hardware privilege/paging variants; real IPv6 SSH/Telnet; long-term memory/handle plateau; hostile-server fuzzing; actual disk-full/ACL-denial matrix; physical/mixed DPI, high contrast/screen readers/manual keyboard acceptance; clean-machine Setup/upgrade/recovery/uninstall. Full A02 acceptance remains open.

## Usage

1. Open Config Collector; paste only authorized `host[:port] description [auth=1|2]` entries. Bracket IPv6 with an explicit port. Blank/# comment lines are ignored; first equivalent endpoint wins. No CIDR discovery.
2. Select the device profile/basic and read-only collection commands. Enter AUTH1 and optionally AUTH2 in AUTH / Trust. Credentials are memory-only and cleared from the fields after Start/leaving the page.
3. For SSH, independently verify each device fingerprint and enter `host:22 SHA256:base64`. An initial blocked attempt may show an observed key, which must be independently verified before trusting it. Unknown/changed keys never auto-accept. For Telnet, explicitly select it and acknowledge plaintext transport.
4. Choose an output folder, TXT/JSON, bounded concurrency/timeouts, then Start. Options are frozen for that run. Stop cancels and awaits cleanup. X hides to Tray and retains active work; Exit/update drains it. Collection does not auto-start on launch.
5. Select a result for its bounded preview; resize panels/columns or use Ctrl+wheel. Open folder exposes full captures. JSON schema 1 records protocol/profile/run/endpoint/auth slot/status/times/prompt/command completion/hash and streamed `outputChunks`; concatenating chunks gives the retained transcript. Error CSV contains only non-completed devices. Captures remain sensitive, with manual retention in per-user/SYSTEM-protected run folders.

