# 0.12.0 publication — 2026-10-22

[Release](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.12.0) is public, non-prerelease and GitHub Latest, published at **2026-10-22 06:23:16 UTC / 13:23:16 Asia/Bangkok**. [PR #9](https://github.com/pk-wrk-sea/NetStucked/pull/9) is merged. Scope/limits: [features](FEATURES_0.12.0.md), [QA](QA_0.12.0.md).

## Immutable source and assets

- Tag `v0.12.0`: `a082abb58b5f34fae1e75aa4790daf4e4686956d`.
- Main merge: `4dca97c47f5d090bbd2c01a1b73ed065c9bf7702`.
- Corrected frozen package: `artifacts/github-release/0.12.0/final-2/`; 2,738 files / 236,353,908 payload bytes. ProductVersion `0.12.0+a082abb58b5f34fae1e75aa4790daf4e4686956d`.
- The first candidate and failed CI evidence remain preserved in `final-1` and `artifacts/collector-inventory-20261022/`; they were not uploaded as the release package. Published older tags/assets and the three canonical UI-reference hashes remain unchanged.

| Asset | Bytes | SHA-256 |
|---|---:|---|
| NetStucked-0.12.0-win-x64-portable.zip | 97,549,729 | b1afa4712b39d3d92adebb4e8ef4544b3708c5a35415dd8ce035e3622f709d12 |
| NetStucked-0.12.0-win-x64-setup.exe | 71,600,379 | fd8291e045e458cab49e5768918aa9a6ee0c5c7fbf536a615996c0fe46653313 |

All six draft assets were downloaded and verified against GitHub sizes/digests, both sidecars and both manifests. Every ZIP entry matches its recorded size/hash and passes CRC validation. The 2,246 pinned runtime files match their original provisioning manifest and source lock; the worker is separately covered by the package manifest. Python was verified against the official ZIP SHA-256 and PSF publisher; wheel hashes match official PyPI metadata and original license files are retained. No Terminal emulator directory or stale build-copy artifact is present. Package verification uses extended Windows paths for the long checkout location. Setup identity is NetStucked / 0.12.0, Authenticode **NotSigned**; Setup was not executed.

## Verification

- Final source: **520 unit tests PASS**, no failures/skips. Two new deterministic regressions cover cancellation racing with a worker transport/authentication error, including prevention of AUTH2 after Cancel. No timing threshold was relaxed.
- Exact frozen application/runtime: **252 WPF PASS**, **40 bundled Netmiko/SSH/Telnet assertions PASS**, **14 Collector/Inventory/appearance WPF PASS**, **46 engineering WPF PASS**, **17 CVE PASS**, **1 native startup/Exit PASS**, **8 native Monitoring/profile-instance PASS**. All 2,738 frozen publish hashes remain unchanged after these checks.
- Owned software profiles cover IOS, IOS-XE, IOS XR, NX-OS and Huawei VRP, with verified trust before credentials, matching/changed keys, rejection-only AUTH2, MFA exclusion, cancellation during KEX/prompt wait, mixed-profile concurrency limit, 8,192 Unicode output lines, redaction, byte limits, partial retention and no command replay. The audit observes no configuration changes or automatic confirmation answers.
- WPF tests exercise actual Start/Save controls, password reveal/clearing, first-use key modal, real packaged workers, nullable Inventory records, shared IPs, template/export/edit/import with stable IDs, optional values cleared, both Dark switches and Terminal removal. Captures are actual WPF renders, including Light/Dark and narrow windows; they are not physical DPI/native Computer Use acceptance.

All four required CI runs passed before publication: [corrected source](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39732022675), [PR](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39732028043), [merged main](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39732719176), [exact-package installer](https://github.com/pk-wrk-sea/NetStucked/actions/runs/39732776693). The initial failing source/PR cancellation assertions are preserved, not counted as passes.

## Public download and updater

- Both anonymous direct public downloads return **HTTP 200** and match the byte counts/hashes above. Evidence: `public-download/result.json` under the version artifact directory.
- The exact published **0.11.2** checker discovers/selects **Install v0.12.0: 8 PASS**; exact **0.12.0** sees no newer version and offers Reinstall/current and Recover/0.11.2: **9 PASS**.
- Actual **0.11.2** production updater downloads/stages the public 0.12.0 Setup: **11 PASS**. Size/hash/product/version/Windows trust and Internet Zone are verified; unsigned execution is rejected without acknowledgement. Its native helper copies exact baseline bytes, acknowledges readiness and waits for its owned QA parent. The helper is stopped while that parent remains alive; only the isolated staged job is discarded and isolated settings remain identical. **No Setup runs and no installed user profile is replaced.**

## Remaining acceptance

**NOT TESTED:** physical Cisco/Huawei firmware/AAA/privilege/command compatibility and resolution of the reported real-device incident, 50-device capacity/soak, physical/mixed DPI/accessibility, clean-machine install/upgrade/recovery/uninstall and human UI acceptance. New XR/Huawei collection does not imply new topology/CVE parsers. Older apps may discard new optional Inventory fields when saving; full downgrade preservation is not claimed. Historical A02/A03/stable DoD remains open.
