# CODEX TASK 02 — Implement the actual approved Live Ping and Traceroute pages

Act as a senior Windows Desktop/WPF engineer and network diagnostics specialist. This is a **functional implementation** task. Do not deliver a mockup, pseudocode, or simulated telemetry.

## Mandatory first steps

1. Inspect existing repo and user modifications. Read `AGENTS.md`, `.agents/skills/netstucked-development/SKILL.md`, and `docs/APPROVED_UI_SCOPE.md`.
2. Open/inspect both approved screenshots in `design/references/` and treat them as **layout acceptance references**.
3. Inspect existing .NET solution, project structure, DI, styles, pages and test suite. Keep these intact unless an identified problem requires a focused change.
4. Write a concise implementation plan, then **implement it**. Do not ask questions where the approved screenshots/spec already resolve them.

### Non-negotiable scope

- **ONLY two real tools:** Multi-target Live Ping and continuous ICMP Traceroute.
- **Live Ping approved layout:** long editable target panel on left, Load/Save/settings at its top, Start/Pause/Stop ONLY at its bottom; six **small** top-right cards; real-time target table right middle; selected host history table right bottom; no header global search, no extra Clear, no tutorial/tooltips, no giant chart.
- **Traceroute approved layout:** no KPIs, compact Target field and Advanced Settings button, Start/Pause/Stop; results table on left, real-time Trace Event Log full-height on right. No permanently visible Max Hops/Timeout/Interval input.
- `Dashboard` still TBD, any other pages placeholders; DO NOT develop broad future features, LAN discovery module, SNMP, central collector, account login, Update/Recovery downloader, packet capture, etc.
- Implement with C# + WPF .NET 10 and MVVM. No Shell `ping.exe`/`tracert.exe`, no random/mock responses in production.

## LIVE PING FUNCTIONAL REQUIREMENTS

### A. Target input / CIDR expansion / Save Lists

1. Accept **one target per line**, syntax `<IP|hostname|IPv4 CIDR> [optional description words]`. Examples:
   - `8.8.8.8 Google DNS`
   - `1.1.1.1 Cloudflare DNS`
   - `192.168.1.1 Gateway`
   - `192.168.0.0/24 Branch Users`
   - `core-switch.office.local Core Switch`
2. Use the first whitespace token as target and remaining tokens as description; clear errors with line numbers for malformed entries, do not silently skip malformed addresses. Validate hostnames and CIDR ranges.
3. Expand valid IPv4 CIDR using correct network/broadcast semantics; `/24` -> 254 usable IPs, one result row per IP. Correct `/31` and `/32` behavior, beware integer overflow/endian errors. IPv6 individual addresses may be supported when .NET permits; do not invent IPv6 CIDR expansion.
4. Deduplicate expanded hosts deterministically; ensure `Targets` counter counts actual unique expanded hosts; retain logical descriptions and source-CIDR metadata.
5. Set a safe default cap of 1024 expanded targets and user-approved overrides for permitted ranges; limit concurrency (default 32 or 64) with `SemaphoreSlim` or equivalent. Do not probe disallowed scopes automatically. Show estimated target count before Start when CIDR expansion is large.
6. `Load List` / `Save List` at TOP LEFT with versioned local JSON or UTF-8 text, proper errors and user-chosen files; do not store credentials. Keep the target input edit-friendly and compact; list scrollbars should be thin.
7. Probe settings button in top-left panel opens compact dialog/popover; configurable: interval ms, timeout ms, packet payload size, max concurrent probes, warning thresholds. Default approximately 1000ms per target, 1500-2000ms timeout, 32 bytes. Validate bounds and no self-inflicted high-rate traffic.

### B. Live ICMP execution and session lifecycle

1. Use .NET `System.Net.NetworkInformation.Ping` async `SendPingAsync` with real `PingReply`, `PingOptions` when applicable, and cancellation support. Choose correct overload available in target framework. For DNS, async resolve and cache result appropriately. Handle IPv4/IPv6 where supported; avoid UI thread blocking.
2. Per target at most ONE outstanding probe. Schedule next probe at a sensible interval with no catch-up bursts; max global concurrency bounded; no Thread.Sleep, busy loops, `async void` service APIs, uncontrolled Task.Run per target, or unhandled task exceptions.
3. Implement explicit states: Idle, Starting/Resolving, Running, Pausing, Paused, Stopping, Stopped, Error. Start twice cannot create concurrent sessions. `Pause` stops scheduling; `Resume` continues same run; `Stop` cancels all, waits for proper cleanup and keeps visible results. Rapid start/pause/stop/start and page navigation cannot leak tasks.
4. Report actual `IPStatus` including Success, TimedOut, Unreachable and failures, along with errors with accurate target context. No definitive host-offline assertion from a single ICMP timeout. Handle hostnames that cannot resolve without crashing the app.
5. For the six compact KPI cards calculate exact values from real results: unique Targets, Reachable, Unreachable, Sent, Received, Loss %. Distinguish Unknown/not yet sampled. Do not include unsent/cancelled probes in Sent; Received + Lost = Sent for completed attempts. Loss is aggregate across completed probes; prevent division by zero. Do not confuse number of hosts down with probe loss.
6. Per-target Last/Avg/Min/Max use only successful RTTs; show `—` when none. Status logic configurable (e.g. 3 consecutive failures -> Unreachable, warning RTT/loss thresholds); label `Unknown` before first result, not false red.
7. Keep bounded sample/log buffers (e.g. per target latest 100/500 and total memory cap), batch UI dispatches and virtualize WPF DataGrids so 254 targets at 1s intervals work without freezing. Automatically remove/dispose timers and clients at Stop/window exit.

### C. Realtime Results and History

- Table in right upper pane: status, host, description, resolved IP, last/avg/min/max ms, sent/recv/lost/loss%, with sortable headers. Filter by All/Reachable/Unreachable/Warn/Unknown. Column chooser persists widths, order, and visibility. No oversized data cards.
- Toolbar at right: All status, compact icon-only Chart/Columns/Export CSV. Export actual visible/current results, valid UTF-8 CSV, properly escaped fields, no overwrite without confirmation. Chart icon is an on-demand selected-target chart, not a default persistent graph; if not implemented, disable it explicitly and disclose that in final report.
- Click result row -> Ping History bottom pane immediately switches to that host (maintain selection across sorting/filtering if possible); show newest first Time/Seq/Result/RTT/TTL, optional details, selectable latest 100. Show Timeout as `—` RTT. Bound rows; optional compact expand button only if functional.
- Scrolling thin/minimal throughout. No gratuitous tooltips, no Clear buttons or extra Start controls.

## TRACEROUTE FUNCTIONAL REQUIREMENTS

### A. Approved visual hierarchy

- One target + compact controls card above large left route table. No KPI cards, no visible Max Hops/Timeout/Interval outside Advanced Settings.
- `Advanced Settings` dialog: Max Hops default 30, timeout default 1500ms, per-cycle interval default 10,000ms, probes per hop 1-3, 32-byte payload; validate bounds. Protocol ICMP-only for v0.1.0; if TCP/UDP shown, disable as future/not implemented.
- Only one Start/Pause/Stop group. Right Trace Event Log with time, type, hop, message and All events filter, continuous scroll with minimalist bar.
- Left table sortable/resizable/virtualized, with actual Hop, Address, Hostname (optional async reverse lookup), Description (blank/user data, never fabricated), Status, Last, Best, Avg, Worst, Jitter, Sent, Recv, Loss, Route Changes, Updated. Table toolbar All status + icon-only Chart/Columns/Export. Disable a nonfunctional chart explicitly; keep rows compact.

### B. Real ICMP hop probes and repeated route cycles

1. Use TTL-limited ICMP Echo with `System.Net.NetworkInformation.PingOptions.Ttl`, inspect `PingReply.Status` and responding `PingReply.Address`. Handle `TtlExpired`, `Success`, timeout and destination/network unreachable. Resolve destination before starting. Support IPv4 first; ensure any IPv6 claim has working proof/tests or document limitation.
2. For each cycle increment TTL 1..Max Hops; send selected 1..3 probes per hop; emit partial hop results as soon as possible; stop route at verified destination response or appropriate terminal status; otherwise allow unreplied hops (`*`) and continue until max hops. All Timeout rows display real Timeout only, RTT `—`, never 0ms fabricated.
3. Run repeatedly while **Continuous** mode is active: after completing one cycle wait configured delay, then repeat. Optional Run Once mode is fine if added in Advanced Settings. No overlap between cycles, responsive Pause/Stop even while waiting, retain partial results.
4. Maintain aggregate per-hop Last/Best/Avg/Worst response RTTs and Sent/Recv/Loss across cycles. Jitter must be explicitly defined (e.g. average absolute successive RTT delta for that hop) and show N/A where insufficient successful samples. TTL exhausted or filtered path does not mean service down; status may be `No final reply`.
5. Detect route changes only using a real change in observed responding IP at SAME TTL between measured cycles; log old/new IP, hop and timestamp. Missing response is UNKNOWN, not by itself a route change. Avoid logging repeated changes caused by reverse DNS naming or naive comparisons. Document ECMP limitations. Count observed changes by hop, throttle spam if required.
6. Trace Event Log must show real start/stop, DNS, error and route observations. Event filters work. Logs bounded. Allow export of real route data to CSV.

## ARCHITECTURE & STABILITY

Suggested interfaces/classes (align to existing architecture, rename only with reason):

- Core: `TargetInputParser`, `Ipv4CidrExpander`, `ProbeSettings`, `PingSample`, `TargetStatistics`, `TraceHopStatistics`, `TraceEvent`, state models, validation.
- Infrastructure: `IPingProbe` / `IcmpPingProbe`, `ITraceProbe` / `IcmpTraceProbe`, DNS abstraction; platform API adapters.
- Application services: `MultiTargetPingService` / `TracerouteMonitoringService`, cancellation/scheduling, bounded events.
- WPF: `LivePingViewModel`, `TracerouteViewModel`; XAML Views/styles; DI lifetime managed; main window shutdown coordinates active sessions.

No network operations in code-behind. Error handling must be accurate, logged and visible without terminating UI. Keep UI update rate bounded. Make statuses accessible to color-blind users. Support different Windows scale factors and reflow/scroll instead of truncating data.

## TESTING — REQUIRED

Write unit tests using fake ICMP adapters, no public internet dependency. Minimum cases:

- Token+description parsing, blank/malformed lines, hostname/IP validation, CIDR `/24` -> 254, `/32`, `/31`, boundary cases, duplicate expansion, max target guard.
- Count calculations, zeros/N/A, success/failure and cancellation distinctions, accurate loss percent and min/avg/max.
- 254 targets with globally bounded concurrency, no overlapping ping for a given host, Pause/Resume/Stop, rapid restart, exceptions, bounded retention.
- UI ViewModel state transitions and selected-target history binding (testable logic), table filter/sort independence.
- TTL progression, arrival/reply, intermediate hop, no final response, partial timeouts, max hops, cancellation, multi-cycle aggregation, jitter calculation, route-change detection including missing hop and ECMP considerations, no duplicated events.
- CSV escaping, non-ASCII descriptions.

Create a manual verification checklist for Windows with at least:

- ping `127.0.0.1`, `8.8.8.8` if authorized, invalid DNS, user-allowed subnet `/24` (254 expanded hosts) with safe concurrency;
- confirm table shows 254 hosts and updates; filter/sort/select changes lower history for correct host;
- repeatedly Start/Pause/Resume/Stop; run 10 minutes; window minimize/nav; test cancellation;
- traceroute localhost, internal gateway, permitted remote IP, route that times out, repeated cycles, Event Log, paused/stop states;
- resize/DPI 100/125/150%, visual compare against both approved screenshots; installer install/uninstall/upgrade preparedness;
- CSV list save/load and CSV export.

## BUILD AND INSTALLER

- Restore/build/test using the actual .NET 10 SDK in a Windows environment. Fix compile/test issues.
- `dotnet restore NetStucked.sln`
- `dotnet build NetStucked.sln -c Release`
- `dotnet test NetStucked.sln -c Release`
- `dotnet publish src/NetStucked.Desktop/NetStucked.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64`
- Inno Setup `.iss`: ensure installer references all artifacts in publish directory. Compile it if Inno Setup is installed; otherwise provide precise instructions and mark installer build NOT TESTED.
- Verify the version is `0.1.0` everywhere, not copied mock `1.0.0`; do not implement GitHub Update/Recovery yet.
- Keep `AGENTS.md`, the Skill, `docs/ARCHITECTURE.md`, `docs/FEATURE_SPEC.md`, `docs/TEST_PLAN.md`, `docs/RELEASE.md`, `README.md` and `CHANGELOG.md` accurate after implementation.

## DEFINITION OF DONE / FINAL RESPONSE

A feature can be called complete **only when it truly works**. Provide:

1. Changed files and architecture summary.
2. Completed vs incomplete checklist (Live Ping, Traceroute, theme, installer).
3. Exact restore/build/test commands executed and results.
4. Actual tests with passes/failures; screenshot/GUI check results on Windows if possible.
5. Known ICMP and routing limitations, untested parts, runtime prerequisites, and export/installer paths.
6. Any deviations from the approved image and why (do not silently redesign).

**Execute code changes now. Do not respond only with an implementation plan.**
