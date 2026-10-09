# Changelog

## 0.6.0 — 2026-10-09

- Added DNS Test with selected records/resolvers, validated UDP/TCP responses, bounded queries and measured results.
- Added HTTP/HTTPS Test with GET/HEAD, expected status, optional redirects, header timing and TLS/certificate details; normal certificate validation remains enforced.
- Grouped navigation with licensed outline icons and Coming Soon pages; retained Wi-Fi and removed History/Reports from navigation as requested.
- Fixed Dark version labels and native Taskbar/Alt-Tab icon/shortcut identity. New tools drain during Stop/shutdown/update.
- Verification and remaining human/remote/install limits: docs/QA_0.6.0.md.


## 0.5.0 — 2026-10-09

- Replace Network Info's placeholder with the approved compact saved-WLAN-profile table and current Light/Dark theme; add adapter selection, explicit scan/connect/disconnect, nonsecret descriptions/auto-connect, confirmed deletion and safe IT-profile import/secret-free export.
- Use Windows WLAN/EAP/certificate authentication, Personal key editing and supported PEAP user credentials; optional additional secrets stay in Windows Credential Manager. No plaintext preferences/logs/exports or fabricated Wi-Fi profiles.
- Add configured source-aware Gateway ICMP IPv4/IPv6, adapter-bound DNS/TCP/HTTP checks, cancellation/readiness, network-transition notices and bounded connection/test history with measured CSV results. HTTP retains TLS validation and never follows redirects/submits portal credentials.
- Integrate with all 0.4.2 diagnostics fixes, including selectable Wi-Fi tables and awaited Wi-Fi shutdown before update/recovery. Real Wi-Fi authentication still requires hardware acceptance; see `docs/QA_0.5.0.md`.

## Unreleased — milestone documentation (2026-10-09)

- Consolidate product vision, feature/navigation and approval/decision records, dependencies/security and an evidence-based implementation audit, separating published packages from working follow-ups.
- Prepare M00–M09, post-stable A01–A09 and On Hold B01–B05 specifications and the next approved acceptance task; reconcile proposed versions without changing application metadata.
- Synchronize repository instructions/skill and roadmap links. This planning task adds no product behavior, GitHub milestone, commit or release; existing concurrent defect changes are preserved.

## 0.4.2 — 2026-10-09

- Pace Live Ping at up to 128 native sends/s and reserve separate aggregate Traceroute slots/rate, preventing Ping from occupying trace capacity. Preview effective large-scope intervals.
- Explain Warn and use the last 20 completed attempts for recent-loss health while retaining accurate cumulative counters.
- Add actual Reply IP Address to Ping results and Host / Reply IP Address to history and CSV.
- Enable selectable/copyable table cells and headers, bounded whole-table copy/text views, and preserve editable descriptions, sorting and Port Test column preferences.
- Add disabled-by-default per-session direct TCP checks on responding hop IPs; show actual successful ports with independent bounded workers/cache and cancellation.

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
