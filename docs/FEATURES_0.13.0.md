# 0.13.0 — CVE Checker | IPS

The human instruction of 2026-11-09 explicitly authorizes the proposed 0.13.0 release, including source commit/push/reviewed merge, immutable tag, exact Portable/Installer workflow and public GitHub download/updater validation. This supersedes the initial attachment's no-publication boundary for this release. Do not execute Setup or change device/firewall configuration. Preserve all previous published assets/tags and the installed process/data.

## Approved behavior

- Exact menu **CVE Checker | IPS**, replacing the former planner desktop route; existing Light/Dark WPF theme.
- Read-only VA `.xlsx`/`.csv` import, sheet/header mapping, per-row issues, 34-unique/102-association sample acceptance, immutable input/runs and per-vendor unique lookup.
- Explicit Threat Vault protected key save/test, Vulnerability Protection-only IPS Found, separate Antivirus metadata, bounded cancellation/retry/429/auth handling and source-scoped cache/freshness.
- Local Check Point SmartConsole CSV import/mapping, optional Protection ID, provenance/import time/hash/user-supplied source date/package metadata, stale-source display and no online/global-catalog inference.
- CVE/plugin/asset grouping, evidence/signatures/raw source rows, complete/explicit partial multi-sheet Excel with original Remarks/Status tracking and formula-safe text cells.
- Retire Monitoring, Topology Explorer, Inventory and URL Checker desktop routes, Monitoring auto-resume and tray action. Dashboard is Coming soon. Preserve dormant legacy stores/adapters/credentials; do not migrate/delete them.

Full implementation contract: [approved scope](CVE_IPS_2026-11-09.md), [setup](CVE_IPS_SETUP.md), [implementation QA](QA_CVE_IPS_2026-11-09.md). Existing diagnostics/Collector and explicit-click update trust/helper rules remain.

## Release acceptance (actual evidence only)

- [ ] Version/source/tag and bundled release notes agree.
- [ ] Release build, unit tests and owned Collector fixtures pass.
- [ ] Exact portable WPF coverage/current-menu and native startup/retired-monitor checks pass.
- [ ] Frozen portable ZIP and hashes verified; installer CI compiles those same bytes.
- [ ] Source/PR/main/frozen-installer Windows CI pass.
- [ ] Public Portable/Installer downloads match immutable hashes and sizes.
- [ ] Actual 0.12.2 updater discovers 0.13.0; current Reinstall/Recovery and helper staging verified without Setup.
- [ ] Prior release assets/tags and canonical reference bytes unchanged.

Live Threat Vault, real SmartConsole export completeness, real vendor/AAA, physical/mixed-DPI/accessibility, long-soak capacity and actual Setup acceptance remain **NOT TESTED**; no blanket live/real-device DoD closure. Unsigned installer acknowledgement and Windows UAC/SmartScreen remain enforced.
