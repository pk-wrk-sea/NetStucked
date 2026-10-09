---
name: netstucked-development
description: Implement, debug, review and test NetStucked's approved WPF UI, ICMP Ping, recurring Traceroute, TCP Port Test, manual release checks, Windows installer and versioned releases.
---

# NetStucked development

Use for implementation or maintenance in this repository.

1. Read root `AGENTS.md`, `docs/APPROVED_UI_SCOPE.md` and the two PNGs in `design/references/`. Preserve those canonical files. Inspect existing changes before editing.
2. Apply the user-authorized 2026-10-08 UI revision in `docs/UI_REVISION_2026-10-08.md` and 2026-10-09 TCP Port Test/manual Updates extension in `docs/FEATURES_0.2.0.md`. Keep the remaining visual hierarchy. Dashboard and Network Info remain placeholders. Screenshot telemetry is illustrative and must never become initial data.
3. For UI/lifecycle changes read [WPF/MVVM](references/wpf-mvvm.md); for network/statistics changes read [ICMP semantics](references/icmp-ping-traceroute.md); for release/GUI review read [visual acceptance](references/visual-acceptance.md).
4. Implement focused changes, verify Core with deterministic adapters and Windows with real loopback ICMP. Await cancellation cleanup before reuse; do not grant network authority beyond explicitly chosen targets.
5. Restore, build, test and self-contained publish with .NET 10. Compile `installer/NetStucked.iss` when available. Keep docs and `docs/QA_REPORT.md` aligned with measured results, including untested GUI/install scenarios.

Version lives in `Directory.Build.props`. `scripts/Publish.ps1` derives Inno version from that file; keep the installer AppId stable. 0.2.0 supports manual public GitHub release checks and manual recovery guidance; automated download, installation and rollback remain roadmap only. Test TCP with owned loopback listeners and never invent timing for refused, timed-out or unresolved endpoints.
