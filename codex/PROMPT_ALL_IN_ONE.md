# OPTIONAL: One-shot Codex prompt for a single long-running task

You are implementing NetStucked v0.1.0, a Windows 10/11 x64 C# .NET 10 WPF desktop app, for Network Engineers checking OTHER devices/networks rather than local PC health.

Read FIRST:
- `docs/APPROVED_UI_SCOPE.md`
- `design/references/LivePing_APPROVED.png`
- `design/references/Traceroute_APPROVED.png`

These screenshots are FINAL approved. Do not redesign their layouts, create large card layouts, add KPI cards to Traceroute, insert tips/global search/duplicate action buttons, or invent network values.

Execute the complete contents of `codex/PROMPT_01_BOOTSTRAP.md`, then `codex/PROMPT_02_BUILD_FEATURES.md`, in order, on the SAME repository. Finally execute the release checks in `codex/PROMPT_03_QA_RELEASE.md`. Work autonomously, implement actual files, and run builds/tests available in the environment. Preserve and use the canonical approved scope and screenshots. Do not implement any other features, including GitHub Update/Recovery. Report verified vs untested items clearly.

Use GPT-6 Astra + High effort; only switch to Extra High if genuinely necessary for hard concurrency/Windows integration debugging. When the environment cannot run Windows WPF, report this accurately rather than pretending GUI tests were done.
