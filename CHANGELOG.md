# Changelog

## 0.4.1 — 2026-10-09

- Remove redundant caption labels; fix dark input carets, session text and address-template Save/Probe Settings spacing.
- Move shared Hop Description before the Traceroute Results filter; show stable lifecycle state without changing per-probe outcome text.
- Make Port Test continuous and multiple-target by default with both checkboxes removed; support inclusive, deduplicated port ranges in inputs/templates while retaining scope limits.
- Add bounded Packet Size settings and actual TCP/UDP zero-filled payload transmission, with real sent-byte details, partial-send handling, cancellation and explicit send-failure outcomes. TCP timing measures the connection; UDP silence remains inconclusive.

## 0.4.0 — 2026-10-09

- Apply the supplied NetStucked brand, Light/Dark/System themes, themed application dialogs, input guides, status colors and smooth collapsible/resizable menu, addresses, history and event panels.
- Share multiline IP-to-description mappings across Traceroute sessions; look up public WAN organization/ASN through RIPEstat with bounded cached requests and local mapping priority.
- Keep one undeletable main trace session, require a valid destination before adding another new session, and limit the workspace to five sessions.
- Separate port targets, TCP/UDP and port numbers; add single-pass/continuous selection, bounded Host/IP/CIDR × port scans, editable saved/restorable common-port templates and explicit acknowledgement above 4,096 checks. UDP silence remains inconclusive.
- Preserve previous update/recovery behavior and all canonical approved reference files.

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
