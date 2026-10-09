# APPROVED UI & Functional Scope — NetStucked v0.1.0

**Status: UX LOCKED.** This specification and the screenshots have priority over earlier concepts. The screenshot content is illustrative **only**; mock numbers, IPs, labels, dates and data are not actual telemetry.

Canonical visual references:
- `design/references/LivePing_APPROVED.png`
- `design/references/Traceroute_APPROVED.png`

## Product vision

NetStucked serves **Network Engineers** diagnosing *remote network IPs, IP ranges, hostnames and their routed paths*. It is **not** a PC health checking app. Version 0.1.0 must be a local-first Windows 10/11 x64 C# WPF app with an Inno Setup installer (self-contained .NET 10). English UI copy; Thai compatible UI/fonts; keyboard accessible controls.

## Global UI / visual rules

- Exact style: **minimal, compact, light, white/off-white, blue accent**, thin gray borders, subtle rounded cards, Segoe UI, no heavy gradients, minimal shadows, tight spacing.
- Prefer density over oversized cards. Tabs, headers, KPI cards and buttons must use minimal height, prioritize tables. At 1536x1024 the primary data is visible without long window scrolling.
- Sidebar on far left with NetStucked logo and concise nav. Implement Live Ping and Traceroute; Dashboard is **TBD/placeholder**; Network Info is planned/placeholder; Settings only minimum app preferences; additional pages must not be invented. Nav should not lead to fabricated live data.
- Main content uses remaining width. Make the window resizable and DPI-aware; avoid clipping; expose horizontal scrollbar for wide tables as needed. Use thin, minimal vertical scrollbars (approximately 4–6 px, widening on hover only if appropriate). Virtualize long result grids.
- No floating tips / tutorial cards / large tooltips. Do not add a global search bar, duplicate Start/Pause/Stop toolbar, large banner, decorative health score or redundant Clear button. Icon-only toolbar buttons must still have accessible AutomationProperties.Name.
- Green, orange and red communicate statuses *alongside words*, not as the sole distinction.
- Use shared WPF resources/styles for all colors, spacing, cards, controls, icon sizing, grids and scrollbars. If fonts/assets are not licensed for distribution, use Windows fonts and vector icons.

## Page 1 — Dashboard

**TBD**. The user explicitly postponed all Dashboard widgets. Show a minimal `Dashboard — Coming later` neutral empty state, not fake health cards or local network status. Do not implement Dashboard KPIs now.

## Page 2 — LIVE PING (FINAL APPROVED)

### Layout

- Three zones: (1) tall target editor panel **at left** occupying full content height; (2) a row of **six compact KPI cards** at the top of the right content; (3) on the right below KPIs, **Realtime Results** upper table and **Ping History** lower table, each in compact cards. Target panel does **not** end above Ping History; it extends to its bottom edge.
- Target panel heading `Targets (IP / Host + Description)`; toolbar **ABOVE targets**: `Load List`, `Save List`, compact gear icon opens `Probe Settings`. No visible probe parameter fields by default. Main area is multi-line editable target entries with minimal scrollbar.
- **ONLY ONE** control group, anchored **BELOW target list inside the left panel**: `Start`, `Pause` / `Resume`, `Stop`. No controls in top-level header; actions enabled/disabled correctly.
- Compact six KPIs, not huge: `Targets`, `Reachable`, `Unreachable`, `Sent`, `Received`, `Loss`. During Idle show 0 or `—` appropriately; never claim live data without actual probes.
- Realtime Results top-right: heading with target count, All status dropdown, small icon-only `Chart`, `Columns`, and `Export CSV` controls. Keep toolbar minimal. No Clear action. Table sortable; filter by status; user-configurable visible columns/order/width with persistence. Row click selects a target and changes bottom Ping History. Colors for up/warn/unreachable; after no probe yet display `Unknown` not `Down`.
- Main table default columns: `#`, `Status`, `Host`, `Description`, `Resolved IP`, `Last (ms)`, `Avg (ms)`, `Min (ms)`, `Max (ms)`, `Sent`, `Recv`, `Lost`, `Loss %`. Optional `Last Ping`, `Error`, `TTL` columns allowed in Columns chooser. Horizontal scrolling is preferable to microscopic unreadable fonts.
- Ping History bottom-right: header `Ping History — [selected host]`, dropdown e.g. `Last 100`, compact Expand icon if useful; table `Time`, `Seq`, `Result`, `Latency (ms)`, `TTL`, optionally `Details`; newest first. Use actual samples for selected host only. `Timeout` RTT/TTL show `—`. No oversized tips or Clear History.
- Chart icon opens an optional focused chart for the selected host (on-demand flyout/window), NOT a default graph filling the page. If it cannot be included robustly in v0.1.0, document and disable rather than pretend it works.

### Target input grammar

- Each nonblank line is `IPv4 | IPv6 | hostname | IPv4 CIDR` followed optionally by whitespace and a human-readable Description. Example:
  `8.8.8.8 Google DNS`
  `1.1.1.1 Cloudflare DNS`
  `192.168.1.1 Gateway`
  `192.168.0.0/24 Users VLAN`
  `core-switch.office.local Core Switch`
- The FIRST whitespace-separated token is the target, the rest of the line is Description. Blank lines ignored; invalid entries remain visible with line-specific validation errors. Save and load target lists locally (UTF-8 JSON or readable text), no cloud dependency. Duplicate hosts after expansion should be deduplicated deterministically with clear description precedence, documented in UI/spec.
- Expanding `192.168.0.0/24` normally yields 254 usable IPv4 host targets, one row **per host** in Realtime Results, not a single `/24` host row. Correct /32 and /31 edge cases; avoid IPv4 network and broadcast for prefixes where those addresses are not host targets. No silent huge scans: set an initial max expanded target cap (e.g. 1024), configurable with explicit acknowledgement for larger allowed scopes; enforce a bounded global concurrency limit and target scope authorization guidance.
- Hostnames resolve before probing; `Resolved IP` and DNS failures display correctly; prefer IPv4 for IPv4 CIDR and allow IPv6 single targets when supported. Retain input descriptions for each expanded IP.

### Probe settings (popover/dialog)

`Interval (ms)`, `Timeout (ms)`, `Packet Size (bytes)`, maximum concurrent probes, address family selection where applicable, and warning thresholds. Initial defaults: Interval 1000 ms *per target*, Timeout 1500–2000 ms, Payload 32 bytes, Concurrency 32 or 64. Validate bounds. Do not launch unlimited requests. Use one in-flight probe per target, no catch-up bursts. Do not use ICMP Ping to infer all services on the host are down.

### Runtime semantics

- `Start`: validate and expand targets, then begin real periodic ICMP using cancellable async .NET Ping API.
- `Pause`: stop scheduling new probes, retain metrics/history and session state; finish/cancel current requests according to documented policy. `Resume`: continue same session with no duplicate probes.
- `Stop`: cancel all work and wait for cleanup; preserve existing results; allow clean restart.
- `Targets`: number of **unique expanded target hosts**; `Reachable/Unreachable`: based on defined consecutive probe thresholds; targets not yet checked or with DNS error must be handled separately (e.g. Unknown); counts must be coherent.
- Stats `Sent`, `Received`, `Lost`, `% loss`, per-target `Last/Avg/Min/Max` from actual attempted samples only; N/A for no successful RTT samples. Counters consistent: Sent = successful replies + completed failed attempts, excluding cancellations before send. Loss percentage weighted across all attempted probes.
- Per-target history bounded (e.g. last 100 or 500 per host), all target data bounded globally. UI/DataGrid updates throttled/batched to remain responsive for 254 targets. View sorting/filtering doesn't break updates.
- Export real current Realtime Results to UTF-8 CSV with headers and properly escaped values, safe file-save flow.

## Page 3 — TRACEROUTE (FINAL APPROVED)

### Layout

- Same minimal light theme and left sidebar as Live Ping.
- **No KPI cards at all**. Use space for functional tables.
- Left (main, ~70%) = small compact top `Traceroute Target` panel, immediately below a large `Traceroute Results` table occupying most height.
- Right (~25–30%) = `Trace Event Log` panel occupying full height of target+results with event filter `All events`; table `Time`, `Type`, `Hop`, `Message`.
- Top target panel: text input `Target (IP / Hostname)`, `Advanced Settings` button, ONLY one Start/Pause/Stop row. No permanently shown Max Hops, Timeout, Interval etc.; button opens popup/dialog with those settings.
- Advanced Settings: `Max Hops`, `Timeout (ms)`, `Interval (ms)` between cycles, `Probes per Hop`, `Packet Size`, `Protocol`. Real v0.1.0 supports ICMP only; no fake TCP/UDP probing. If protocol selector appears, unavailable protocols must be disabled clearly. Defaults to sensible values (e.g. Max Hops 30, Timeout 1500 ms, Probes 1–3, Cycle Delay 10 s).
- Results header toolbar right: `All status` dropdown, compact icon-only Chart (only when implemented), Columns, Export CSV. Dense sortable/resizable DataGrid. Columns `Hop`, `Address`, `Hostname`, `Description`, `Status`, `Last (ms)`, `Best (ms)`, `Avg (ms)`, `Worst (ms)`, `Jitter (ms)`, `Sent`, `Recv`, `Loss`, `Route Changes`, `Updated`. Hide/disable fields when no real data; `*` / `—` on no ICMP reply. Minimal scrollbars. Description can be editable/local metadata, never guessed.
- Event Log logs **real** events: Info (start/stop), DNS, Error, Route (actual change in responding IP for the same TTL), including time and hop; filter by event type; scroll with minimal width. Do NOT invent ISP names or route changes.

### Runtime semantics

- ICMP traceroute via TTL-limited echo probes (`PingOptions.Ttl`), handling TTL-expired intermediate responses, Success at destination, Timeouts and unreachable statuses. Resolve target before run; IPv4 support mandatory; IPv6 support only if validated on supported API/platform, otherwise show a documented limitation.
- Supports **continuous route polling cycles** while active (as indicated by source reference), with optional run-once setting if convenient. Results aggregate per-hop observed samples across cycles. Repeated probes allow Last/Best/Avg/Worst, Sent/Recv/Loss, and Jitter (define it explicitly, e.g. mean absolute difference between consecutive valid RTTs). No RTT must be invented for timeout.
- Route-change detection compares observed responding hop IP for identical TTL across separate completed cycles, does not treat missed ICMP response / DNS hostname changes as definitive route changes; rate-limit duplicate events. Log actual changes with old/new IP and hop number. Handle ECMP/multipath variability (a change is an observed alternate response, not proof of an outage).
- `Pause`/`Resume`/`Stop` work asynchronously, with cancellation and no overlapping cycles. Keep partial path and event log. Failure to get final destination reply is reported as `No final reply`, not necessarily host offline.
- Event log, hop samples, and history retention must be bounded.

## Version & release

- Current development version = `0.1.0`, not `1.0.0`. Do not copy mock screenshot version strings into app metadata. SemVer `MAJOR.MINOR.PATCH`, Git tags `vX.Y.Z`; stable target `1.0.0` much later.
- Prepare version source and Inno Setup x64 installer supporting future upgrade (stable AppId). Self-contained .NET 10 WPF publish; Installer uses the full publish directory. Write settings/logs to `%LOCALAPPDATA%/NetStucked` instead of Program Files.
- Future GitHub Releases checker and secure update + backup/recovery remain documented as ROADMAP only for this iteration. Before releasing `1.0.0`, integrate update client if in-app update from `1.0.0` is desired. No GitHub token in EXE.

## Non-goals

No PC health Dashboard, no visual topology, no LAN discovery page, no SNMP, no TCP/UDP traceroute emulation, no remote service checking, no background 24/7 central monitoring, no SQLite requirement, no authentication, no GitHub update download/rollback implementation, no fabricated telemetry.

## Testing & acceptance

Windows 10/11 x64; build, unit tests, UI smoke test; test localhost and real authorized network targets, small CIDR and `/24` including 254-row expansion, mixed valid/invalid hostnames, duplicate hosts, 254 live probes at bounded concurrency, 10-minute stability, fast Start/Stop/Start, Pause/Resume, filtering/sorting, selected target history, missing ICMP responses, route cycling, route change log, event filters, CSV export, Inno Setup install/uninstall. Verify UI against both approved PNGs at target resolution and a narrower window; explicitly report deviations and any features not implemented.
