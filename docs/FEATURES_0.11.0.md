# 0.11.0 — All current menus, integrated review

The human instruction of 2026-10-20, “ทำต่อให้เสร็จเลย ทุก Menu แล้วจะ review ทีเดียว”, approves implementation of the remaining current navigation pages in the existing theme, followed by one integrated review. It supersedes the Dashboard/Inventory/Topology/CVE UI-pending gates for this implementation. It does not restore removed Wi-Fi/Network Info/DNS Test/HTTP Test menus or activate commercial/payment work.

Scope: actual operational Dashboard; stable-ID MA Inventory with versioned Excel preview/import/export and separate observed facts; offline Cisco evidence analysis and inspectable one-hop topology; source-backed CVE metadata/review and engineer-approved device change plans. Existing tools, Terminal host trust, Collector secret/pin policy, explicit network actions, tray lifecycle and updater integrity remain in force. No network discovery or device upgrade execution. No fake telemetry or automatic vulnerability verdict from version strings.

New local engineering data uses a separate schema-1 atomic JSON document. Existing preferences/monitoring/Terminal formats remain compatible; older versions ignore this new file. Raw transcripts are not copied into that document. No SQLite migration is needed for these current navigation pages. Durable cross-tool history/scheduling, central collectors and Serial/SFTP are separate backlog, not additional current menus.

QA and publication status will be recorded from actual evidence. Implementation completion does not establish real-device, clean-machine, accessibility or human acceptance.

## Current-menu acceptance

- [x] Actual local Dashboard and drill-downs, without extra probes.
- [x] Stable-ID Inventory, separate observed facts, versioned Excel preview/selected atomic import/export and edit audit.
- [x] Hash-verified offline Cisco facts, one-hop graph/link inspector, freshness/conflict states and bounded capture comparison.
- [x] Public advisory metadata, explicit engineer applicability review, stale/change invalidation and approved device plans with before/after comparison and Excel/CSV/HTML export.
- [x] Relevant automated/owned WPF/network/native regressions and immutable portable/installer publication verified; [publication evidence](GITHUB_RELEASE_0.11.0_REPORT_2026-10-20.md).
- [ ] Human integrated review and real-device/provider-account/physical-DPI/accessibility/Setup acceptance.
- [ ] Consistently available anonymous updater/preparation path: first 0.11.0 catalog check passed, later requests/older versions were blocked by GitHub HTTP 403. Direct public downloads passed.
