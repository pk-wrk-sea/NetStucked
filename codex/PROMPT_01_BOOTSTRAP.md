# CODEX TASK 01 — Initialize the real NetStucked v0.1.0 repository

Act as a senior Windows/.NET desktop engineer and repository architect. **Implement actual files and working WPF shell**, not just a proposal.

## Critical source of truth

Read these files in the repository *before* making any changes:

- `docs/APPROVED_UI_SCOPE.md`
- `design/references/LivePing_APPROVED.png`
- `design/references/Traceroute_APPROVED.png`

These two screenshots are **approved**. They supersede all previous ideas. The Dashboard has **no approved design** and must remain a placeholder. Only Live Ping and Traceroute will be functional in the following task. DO NOT redesign approved layouts, add KPI cards to Traceroute, add global search, invent network health data, build advanced infrastructure pages, or implement an updater now.

## Technical decisions

- Product: NetStucked; version **0.1.0**.
- Target: Windows 10/11 x64 (test against actual supported configurations).
- Language: C#; runtime/SDK: .NET 10; frontend: WPF / XAML; architecture: MVVM.
- Libraries: CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging. Add only compatible essential NuGet packages (verify version/availability). Prefer Windows/.NET built-in networking APIs for future functionality.
- Local-first; no server/agent/cloud required. Inno Setup installer, self-contained win-x64, multi-file publish acceptable and preferred over forced single-file.
- Source of truth for version metadata: central MSBuild properties (version, informational version, assembly/file metadata) and Inno Setup release config.
- Prefer focused architecture over speculative generic frameworks. Respect existing files, changes and conventions if this repository already contains code.

## Create / initialize the actual solution

Directory structure (adapt minimally to existing files):

```
NetStucked/
  AGENTS.md
  README.md
  CHANGELOG.md
  .editorconfig
  .gitignore
  Directory.Build.props
  NetStucked.sln
  docs/
    APPROVED_UI_SCOPE.md          # EXISTING: preserve as canonical
    FEATURE_SPEC.md
    ARCHITECTURE.md
    UI_DESIGN.md
    NETWORK_BEHAVIOR.md
    DEVELOPMENT.md
    TEST_PLAN.md
    VERSIONING.md
    ROADMAP.md
    RELEASE.md
  design/references/
    LivePing_APPROVED.png         # EXISTING: preserve
    Traceroute_APPROVED.png       # EXISTING: preserve
  .agents/skills/netstucked-development/
    SKILL.md
    references/
      wpf-mvvm.md
      icmp-ping-traceroute.md
      visual-acceptance.md
  src/
    NetStucked.Desktop/          # net10.0-windows + UseWPF
    NetStucked.Core/             # net10.0; contracts / models / statistics
    NetStucked.Infrastructure/   # net10.0-windows where needed; network/platform
  tests/
    NetStucked.Tests/
  installer/
    NetStucked.iss
  .github/workflows/
    build.yml
```

Make a real Visual Studio-compatible solution; configure references correctly. Use WPF project with `App.xaml`, `MainWindow.xaml`, navigation shell, shared theme dictionaries, Views and ViewModels. `NetStucked.Core` must not depend on WPF types; platform-independent unit tests should run even where WPF GUI cannot.

## Create root AGENTS.md

Keep short and enforceable. Include:

1. Product scope: **only Live Ping and Traceroute functional**, Dashboard TBD, no imaginary metrics. UI references in `design/references/` are mandatory.
2. Layer boundaries: Views XAML; ViewModels for presentation/commands; services for network work; Core models/interfaces free of UI concerns; Infrastructure for API calls; DI at startup.
3. Use MVVM, async/await, cancellation tokens, no `Thread.Sleep`, no synchronous network calls on Dispatcher, correct disposal, bounded collections, virtualized DataGrids, minimal WPF code-behind, no static singleton ping tasks.
4. Real networking only; no `ping.exe`, `tracert.exe`, PowerShell, random generated latency, false IP status claims. ICMP timeout is not definitive host outage.
5. Safety: operate only on approved ranges, limit host expansion and concurrency, no arbitrary automated wide scans. Never store credentials, never blindly run downloaded executables.
6. Documentation/checks: read approved UI scope and Skill on relevant work; build/test where environment supports; distinguish not tested vs passed; preserve user edits; don't commit/push without request.
7. Explicit UX no-go list: no big cards, tutorial tips, duplicate controls, persistent big charts, overly wide scrollbars; Traceroute top KPIs forbidden.

## Create repository skill

Path `.agents/skills/netstucked-development/SKILL.md`, with **valid YAML front matter**:

```
---
name: netstucked-development
description: Implement, debug, review and test NetStucked's approved WPF UI, multi-target ICMP Ping, recurring Traceroute, Windows installer, and versioned releases.
---
```

Keep `SKILL.md` focused (< ~200 lines): when to use it, read approved scope/screenshots + AGENTS, inspect repo, plan, implement, test, verify screenshots on Windows, document actual results. Put deeper guidance in `references/wpf-mvvm.md`, `references/icmp-ping-traceroute.md`, and `references/visual-acceptance.md`. Don't bloat the skill or duplicate whole specs. Use standard repo-local Skill discovery conventions.

## Documentation requirements

Populate all markdown files with specific, useful content (not TODO-only):

- `README.md`: what the app does, two actual in-scope pages, prerequisites and commands, current actual implementation status.
- `FEATURE_SPEC.md`: approved Live Ping, Traceroute, deferred features and priorities.
- `ARCHITECTURE.md`: MVVM layers and dependency arrows, services, cancellation, target expansion, aggregated models.
- `UI_DESIGN.md`: accurately reference approved screenshots and compact/dense controls; minimal scrollbars, keyboard access, DPI scaling.
- `NETWORK_BEHAVIOR.md`: ICMP capabilities/limits, CIDR expansion, concurrency and TTL/routing assumptions; one probe at a time per target.
- `DEVELOPMENT.md`: clean checkout -> .NET SDK -> restore -> build -> run -> test, Windows environment details.
- `TEST_PLAN.md`: unit, integration, UI, 254-target scalability, cancellation, UI validation, installer acceptance.
- `VERSIONING.md`: SemVer MAJOR.MINOR.PATCH, 0.1.0 current, 1.0.0 future stable; 1.0.x patches, 1.x minors; Git tags `vX.Y.Z`; updates after foundation.
- `ROADMAP.md`: 0.1.0 functional Live Ping + Traceroute; Dashboard future; GitHub update/recovery planning prior to releasing 1.0.0; other features deferred.
- `RELEASE.md`: create installer, package, install, uninstall; verify size and versions, explain manual release until updater is developed.
- `CHANGELOG.md`: version 0.1.0 groundwork and actual changes only.

## UI shell in task 01

Implement a **runnable WPF application**, not static HTML and not Electron. Use shared resource dictionaries, sidebar nav, proper page lifetimes, compact modern light cards and data grids. Layout initially uses empty states, no fabricated values.

- Dashboard: simple `Dashboard — Design pending`, no KPIs.
- Live Ping: layout matching reference (left full-height target editor, top-right six small stat cards, top results, bottom history, proper toolbar/buttons) but **disable network buttons until task 02 implements real operations**.
- Traceroute: layout matching approved reference (top Target + Advanced Settings button; Start/Pause/Stop; big results left; event log right; **no KPI cards**). Disable unimplemented actions.
- Network Info: placeholder `Planned`, not active features.
- Settings: minimal preference page or dialog; theme and default probe settings, config location. No update functionality yet.
- `Updates & Recovery` is future: if nav item exists, show `Not available yet` and version only; alternatively document for later. No fake GitHub status.
- No global header search and no decorative Tips panel.

Implement styles for minimal scrollbars, dense tables, small cards, accessible icon controls. The approved PNGs should be viewable by Codex from repo; use them as the primary layout reference.

## Build / installer foundation

- Create an Inno Setup `.iss` file with a stable app GUID (`AppId`), x64 install, uninstall entry, optional desktop shortcut, Start Menu shortcut, proper `Program Files` target, user data under `%LOCALAPPDATA%\NetStucked`, and all files from self-contained publish output. Preserve same AppId for upgrades.
- Do not package the source code or personal assets.
- Create a Windows GitHub Actions workflow: restore, build, unit test. Do not publish releases to GitHub automatically in task 01.
- Add at least one meaningful core unit test (e.g. input validation or percentage calculation), not just `Assert.True(true)`.
- Check correct .NET 10 SDK, package restore and build. If non-Windows, run tests/build for projects supported by environment and report WPF GUI validation not performed.

## Strict completion criteria

Create/modify real files. Run restore/build/tests when supported, repair failures. Show exact commands and actual stdout/summaries. Report what is implemented vs shell-only vs untested. Never claim the final Ping/Traceroute networks work yet: task 02 implements those.

Do not stop after giving a plan. Execute.
