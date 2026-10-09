# NetStucked engineering rules

- The human-reviewed 2026-10-09 extension in `docs/FEATURES_0.4.0.md` authorizes supplied branding, Light/Dark/System themes, collapsible/resizable panels, five trace sessions with shared hop descriptions and bounded TCP/UDP multiple-target port scans. The subsequent human instruction authorizes GitHub publication of 0.4.0. Preserve earlier canonical references and published assets.

- The later human release instruction of 2026-10-09 authorizes publishing 0.3.0 plus a 0.3.1 TEST BUILD and retiring 0.2.x GitHub releases/download assets/Actions build artifacts after preserving local verified backups. Keep 0.2.x Git source history and tags. This is the explicit exception to published-asset preservation below; other published releases remain immutable. The public update/recovery catalog starts at 0.3.0.

- The latest human authorization of 2026-10-09 adds selected-build installation/recovery in 0.3.0 and supersedes the earlier manual-only update restriction for explicit user clicks. See `docs/FEATURES_0.3.0.md`. Always verify fixed-project HTTPS asset identity, SHA-256, size, product/version and Windows trust status. Unsigned installers require explicit acknowledgement; invalid signatures are rejected. Preserve UAC/SmartScreen, await diagnostics shutdown, back up compatible settings and use an isolated restart helper. No scheduled/background installation or credential storage. Preserve published tags/assets and canonical references.

- The user-authorized 2026-10-09 extension adds TCP Port Test and a manual Updates & Recovery page in 0.2.0; see `docs/FEATURES_0.2.0.md`. Settings and Updates belong at the sidebar bottom. Updates may read public GitHub Releases and open project release pages, but never download, install or roll back automatically. Preserve the original 0.1.0 canonical references.

- The human-authorized UI revision of 2026-10-08 supersedes the original PNG layout only for the changes recorded in `docs/UI_REVISION_2026-10-08.md`: compact settings, named templates/history, independent trace sessions, window chrome, tables and polling. Keep the original scope/PNG bytes intact.

- Only Live Ping and continuous ICMP Traceroute are functional in 0.1.0. Dashboard is TBD; other navigation is neutral placeholder content. Preserve `docs/APPROVED_UI_SCOPE.md` and both approved PNGs byte-for-byte. Read the scope and `.agents/skills/netstucked-development/SKILL.md` for relevant work.
- XAML Views define layout; ViewModels define presentation/commands; Core defines UI-free models, contracts and scheduling; Infrastructure calls network/platform APIs. Wire dependencies at WPF startup.
- Use MVVM, async/await and cancellation. No synchronous networking on Dispatcher, Thread.Sleep, static singleton probe tasks or shell ping/tracert. Dispose clients, await Stop, bound memory and concurrency, virtualize grids. Keep code-behind limited to view concerns and shutdown.
- All displayed measurements come from actual API responses. Never generate production telemetry or infer service outage from ICMP loss. Test doubles stay in tests.
- Diagnose only user-authorized targets; cap CIDR expansion and probe concurrency, require acknowledgement for larger scopes. Never store credentials or blindly execute downloaded programs.
- No oversized cards, tips, global search, duplicate action groups, default persistent charts, wide scrollbars or Traceroute KPIs. Approved screenshots govern layout, not sample values or version strings.
- Build/test supported projects, report PASS/FAIL/NOT TESTED precisely, preserve existing edits, and do not commit or push without user instruction.
