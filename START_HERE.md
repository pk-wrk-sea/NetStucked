# NetStucked v0.1.0 — Codex Implementation Pack

## Scope is locked

Build a Windows installer and a REAL C#/.NET 10 WPF application for network engineers with **only two functional pages**: multi-target Live Ping and continuous ICMP Traceroute. The two reference screenshots in `design/references/` are explicitly approved. Do **not** redesign them.

Other navigation entries, if shown, are placeholders or minimal foundations. Dashboard design remains TBD. No network-health monitoring of the local PC, SNMP, auto-update implementation, cloud backend, authentication, LAN discovery page, or packet capture.

## Use on Windows

1. Copy the contents of this pack into the ROOT of your new or existing `NetStucked` Git repository. Keep `docs/APPROVED_UI_SCOPE.md` and both PNG references intact. If files already exist, reconcile, don't overwrite user code.
2. In Codex set **GPT-6.1 Sol / Medium** for task 1 (or **GPT-6 Astra / High** for one-model workflow). Open `codex/PROMPT_01_BOOTSTRAP.md` and paste its entire contents into Codex. Let it create and verify the solution, `AGENTS.md`, repo-local Skill and docs.
3. When task 1 is complete, use **GPT-6 Astra / High**. Paste `codex/PROMPT_02_BUILD_FEATURES.md`. This implements live probes and final WPF pages. You may split this task further into Live Ping and Traceroute without altering the scope.
4. After implementation, run `codex/PROMPT_03_QA_RELEASE.md` (GPT-6 Astra / High). Validate UX against the approved references, performance, test results and Installer.
5. Test with a supported Windows 10/11 x64 machine, .NET 10 SDK, and Inno Setup. Install a Release installer, run/stop both probes, and uninstall.

## Model recommendation

- GPT-6 Astra / High: best default for precision and complex C#/WPF/network code.
- GPT-6.1 Sol / Medium: cost-conscious for bootstrap/docs.
- GPT-6 Astra / Extra High: use only for serious WPF Dispatcher/cancellation/concurrency issues requiring deeper investigation.
- If those models are unavailable in Codex, choose the strongest currently accessible coding model and High effort.

## Versioning and GitHub

Version `0.1.0`. Generate one authoritative version source and stable Inno Setup AppId. Document SemVer (`MAJOR.MINOR.PATCH`), Git tags and future GitHub Releases updater/recovery. **Do not implement online auto-update/rollback in 0.1.0**. Implement an updater *before shipping 1.0.0* if users should update from within 1.0.0.

## Definition of Done

All functionality must be driven by real network results: no fabricated metric values or local-network "healthy" assertions. If Windows GUI/Installer verification is not possible in the current execution environment, report it as NOT TESTED.
