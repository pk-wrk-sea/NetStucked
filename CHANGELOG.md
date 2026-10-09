# Changelog

## 0.3.1 — 2026-10-09 — TEST BUILD

- Exercise selected-build Upgrade from 0.3.0 and Recovery back to 0.3.0 through actual GitHub installer assets.
- Change version metadata and bundled release-history notes only. Diagnostic/update code and schema-1 settings match the 0.3.0 baseline.
- Keep this clearly labeled TEST BUILD separate from the regular Latest release. Normal SemVer is required by the checker; no background installation is introduced.

## 0.3.0 — 2026-10-09

- Added user-triggered installation/recovery of a selected published build, with actual release notes, a dynamic Install/Reinstall/Recover button, download progress and preparation cancellation.
- Verify fixed-project HTTPS installer metadata, hash/size, product/version and Windows trust. Explicit acknowledgement is required for unsigned builds; invalid signatures are blocked and Windows prompts remain enabled.
- Drain diagnostic sessions, save preferences, stage an isolated application/runtime helper, keep bounded verified versioned settings backups, run Inno Setup and restart. Recovery restores a matching settings backup when available; compatible schema-1 preferences otherwise remain in place.
- Keep release-page/manual fallback and on-demand checking. Published 0.2.x packages are immutable; their own update menus remain manual after downgrading to them.

## 0.2.0 — 2026-10-09

- Added TCP Port Test for explicit IPv4/IPv6/hostname endpoints, with independent bounded polling, actual connect time/outcomes, pause/resume/stop, local named templates, selected-endpoint history and CSV export.
- Added Updates & Recovery: user-triggered GitHub Releases checks, real executable version, release notes, recorded version history, manual download links and rollback guidance. No automatic check, download, installation or rollback.
- Moved Settings and Updates to the bottom sidebar application group; added Port Test activity indication and cached navigation.
- Preserved Live Ping and Traceroute behavior and the canonical approved references; extended real TCP, release parsing and Windows integration tests.

## 0.1.0

- Establish .NET 10 WPF/MVVM solution, shared compact theme and approved two-page layout.
- Add bounded, cancellable ICMP Live Ping and continuous IPv4 TTL traceroute, real statistics, target lists, history, event log and CSV export.
- Add persisted column preferences, settings, deterministic network-adapter tests, self-contained publishing and stable Inno Setup installer identity.
- Keep Dashboard/Network Info neutral placeholders; updates and recovery remain roadmap only.
- Fix Traceroute refresh during Description editing, immediate Description CSV export, final-hop probe counts, horizontal scrollbar direction and nullable settings validation; add regression coverage (2026-10-08 bug audit).
- Improve Ping cadence with monotonic deadlines, bounded worker queues, packet-rate limits and separate cached/backed-off DNS; add parallel TTL discovery, adaptive continuous polling, asynchronous reverse DNS, incremental UI updates and bounded timing diagnostics (2026-10-08 performance work).

- Apply the user-requested compact settings, named address templates, destination history, independent traceroute sessions, result timestamps, stable history scrolling, cached views, activity indicators, Fit Columns/resize guide/tooltips and monitor-aware framed window (2026-10-08 UI revision). Replace adaptive cycle batches with independent per-hop continuous polling and important-only events.

Validation and known limitations are recorded separately in `docs/QA_REPORT.md`.
