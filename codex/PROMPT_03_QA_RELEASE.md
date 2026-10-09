# CODEX TASK 03 — Independent verification and release hardening of v0.1.0

Perform a focused read-only review first of the current NetStucked repository, then apply only necessary fixes. Read `AGENTS.md`, `.agents/skills/netstucked-development/SKILL.md`, `docs/APPROVED_UI_SCOPE.md` and visually examine BOTH PNGs in `design/references/`.

## Scope

Check that only Live Ping and Traceroute are functional, and that their WPF UI closely matches the APPROVED images. Do NOT add future Dashboard, DNS, network scans, SNMP, GitHub Update/Recovery or other feature pages.

## Visual review against PNGs

- Live Ping: full-height LEFT target editor; Load List/Save List/gear TOP; Start/Pause/Stop only at BOTTOM; six very compact summary cards top of right; Realtime Results top right; selected-target Ping History beneath; compact tool icons; no Clear/global search/tips; tiny scrollbars.
- Traceroute: NO KPI cards; compact target bar, hidden Advanced Settings; Start/Pause/Stop; large route grid left; full-height right Event Log; compact toolbar and scrollbars.
- Text remains readable at 100% and 125% DPI, no overlap at 1280x800 and 1536x1024 (use horizontal scrolling rather than illegible cells), keyboard navigation, accessible icon controls.
- Screenshots contain fictitious examples only; app must not initialize to the screenshot's fake counts or status.

## Network correctness

Check parser/CIDR math and `/24` -> exactly 254 usable hosts, /31 /32, per-host concurrency, global rate controls, no duplicates or runaway tasks. Check Pause/Resume/Stop semantics in all modes. Check statistics denominators, unknown vs down, sorting/filtering under updates, per-host history selection, memory ceilings.

Check Traceroute TTL sequence, TtlExpired/Success behavior, timeout rows, repeated route cycle scheduling, route change event validity, jitter definition, no ungrounded ISP/hostname labels, event filter and CSV.

## Automated verification

Run unit tests and build on Windows; add tests for uncovered critical error paths. If Windows GUI is accessible, launch app and manually test localhost and authorized network endpoints. If Inno Setup available, compile and install in a clean environment, launch and uninstall. Document exact output and all untested items truthfully. Record executable/publish/installer sizes if produced; do not invent them.

## Release security/future maintenance

Stable installer AppId, data outside Program Files, proper disposal/cleanup, safe file handling, no hardcoded GitHub tokens, SemVer 0.1.0. GitHub update/rollback remains future until explicitly authorized.

## Final report

Produce an acceptance checklist with PASS/FAIL/NOT TESTED, files changed, commands run, measured performance where tested, screenshots comparison issues, actual installer artifact path, known limitations, and release readiness. Do not make claims unsupported by actual verification.
