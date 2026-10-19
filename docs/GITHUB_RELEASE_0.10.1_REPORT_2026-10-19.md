# 0.10.1 publication evidence - 2026-10-19

[Published stable GitHub Latest](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.10.1), published 06:15:13 UTC, six assets. Human-approved Terminal host-key follow-up; [scope, usage and QA](FEATURES_0.10.1.md). Full A01/M07 remain open.

[PR #5](https://github.com/pk-wrk-sea/NetStucked/pull/5) merged as `31eee4fb103e2221ffaaa2b5e9a13d05348673b5`. Immutable tag/source `v0.10.1`: `90d29f119f556bd2dc423375a2ec02ce878e2f02`; merge tree matches. Later documentation does not change tag or payload. Older releases/assets and approved canonical scope/PNGs remain unchanged.

| Asset | Bytes | SHA-256 |
|---|---:|---|
| NetStucked-0.10.1-win-x64-portable.zip | 80,725,090 | `47eff22ca9b28501f77eec985e2162b2bdf9d7a73de0a13656678eea4375e91b` |
| NetStucked-0.10.1-win-x64-setup.exe | 57,211,705 | `d35a1a3c2ec2da98316b5f2f9b5e83521377e231c231ce6628120fdecf8056bf` |

Every path/length/hash of the 496-file portable matches the frozen directory and manifest. ProductVersion is `0.10.1+90d29f119f556bd2dc423375a2ec02ce878e2f02`. Six downloaded GitHub assets (both binaries, both SHA sidecars and both provenance manifests) match digest/size/source/version. Windows identifies Setup as NetStucked 0.10.1, **NotSigned**. Setup was never executed.

PASS:

- Release build, no warnings/errors; **442/442 tests**, no skips, including **18** host-trust/storage cases: reject, once, save/reload, normalization/ports/IPv6, changed-key decisions, cancellation, concurrent approval, stale writes/removal, malformed data and invalid endpoints.
- Source whole-app WPF/owned-loopback **249 assertions**; exact published assemblies **252 assertions**, no binding errors. This patch does not claim another 60-second capacity run or physical DPI acceptance.
- Exact-payload actual WPF/WebView2/owned SSH **49 assertions**: first-use Save without manual input, 16-second review beyond connect timeout, no fingerprint control, saved-key recognition across policy instances, Accept Once re-prompt, previous/new-key display, explicit changed-key checkbox, replacement, cancellation without password, confirmed/declined removal, legacy pins not resurrecting trust and existing ANSI/UTF-8/control/resize/zoom/tab/output behavior. Server audit has exactly eight explicit authentication attempts; rejected/changed/cancelled approvals send no credentials. No working-network targets were used.
- Native startup/clean-exit smoke, plus two owned saved-monitor auto-resume/instance-restoration/exit launches. Installer CI also passes the existing actual owned Collector SSH (>8 MiB) regression; Collector policy is unchanged.
- [Branch CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39207923143), [PR CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39207941380), [merged-main CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39208333691), [exact-package installer CI](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39208356911): all success. Installer is compiled from the frozen, hash-checked ZIP at the immutable tag.
- Production **0.9.0 → 0.10.1** anonymous public updater: **8 assertions** pass, actual release notes and Install v0.10.1 selected/enabled. Latest **0.10.1**: **9 assertions**, no newer version, same-version Reinstall and previous **0.10.0 Recovery** eligible.
- Actual public installer/helper preparation from **0.9.0**: **11 assertions**, SHA-256/size/product/version/Windows trust, unsigned rejection without acknowledgement, Internet Mark of the Web, exact four helper binaries, native isolated-helper readiness/wait, cleanup and unchanged settings. The helper is stopped while its owned parent stays alive; unsigned execution is disallowed in the test handoff. **No Setup, install or recovery was run.**

GitHub API access succeeded for these production checks. Earlier 0.10.0/0.9.0 rate-limit failures remain historical evidence, not a host-key code fix. No API token, alternate catalog, proxy or quota workaround was added. Future provider limits remain possible.

NOT TESTED: real Cisco/vendor/IPv6 endpoints, human out-of-band identity verification, Windows Clipboard (previously unavailable), physical/mixed DPI, high contrast/screen reader, long-session resource plateau and clean-machine Runtime/Setup/upgrade/recovery/uninstall. Serial/SFTP/splits/recording are outside this slice. Host-key metadata is public and separate from memory-only authentication secrets. Legacy profiles retain their old fields for older builds; installed downgrade behavior is not certified.

Local evidence: `artifacts/github-release/0.10.1/final/`, `draft-verification.json`, `published-release.json`, `public-from-0.9.0/`, `public-from-0.10.1/`, `public-installer-from-0.9.0/` and corresponding logs. Actual dialog and browser captures are fixtures, not mockups or physical-DPI acceptance.
