# 0.11.1 - Five-menu functional acceptance fixes

Human instruction on 2026-10-21 explicitly requests testing Terminal, Config Collector, Topology Explorer, MA Inventory and CVE from the real program, correcting necessary defects and capturing results. Existing-theme implementation and standing distributable publication authorization remain in force. No working-network discovery/probes or device upgrades are added.

- Terminal: verify the actual bundled xterm/WebView and SSH/Telnet trust/session workflow with owned fixtures; clear obsolete maximum-tab warnings after tab closure; preserve credential and host-trust policy.
- Collector: distinguish captured banner text from an active waiting MFA/access/confirmation prompt, without replay or broad authentication fallback. Verify production SSH/capture classes and import completed real captures into the engineering pages.
- Inventory: preserve dirty fields and stable editing identity across filtering/collection refresh, retain explicit save/import behavior, and reject every conflicting duplicate workbook ID/serial row.
- Topology: version parser changes as cisco-text/2; parse neighbors only inside captured CDP/LLDP commands, reset incomplete records, and review contradictory peer identity. Clear differences when the selected source changes. Explicit confirmed Reanalyze source verifies the original source hash and appends a new observation; original evidence and capture bytes remain intact. Existing reviews become stale through the new evidence identity.
- CVE: bound complete HTTP requests including body reads; optional CISA failure keeps successful NVD metadata with KEV Unknown. Preserve active review/plan fields during refresh. Refresh time-sensitive states while the page is visible, on navigation, and before approval/export. No automatic applicability or safe-release claim.

Existing schema-1 engineering data, preferences and secrets policy remain compatible. Parser revisions append to existing evidence history. Historical milestones and real Cisco/physical-DPI/accessibility/Setup acceptance remain separate. Verification and capture provenance are tracked in [QA_0.11.1.md](QA_0.11.1.md).

Frozen-candidate verification also reproduced transient atomic file replacement failure in the owned OneDrive QA profile. Engineering saves now use the existing bounded atomic-replacement retry for both previous-snapshot and current-document renames, with cancellation and unconditional gate release. ACLs, revision checks and atomic data rules remain unchanged. No permission broadening or user-data repair is performed.
