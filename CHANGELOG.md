# Changelog

## 0.12.1 — 2026-10-22

- Fix Inventory lag after import with deferred selectable cell editors, batched snapshots, cached search/evidence projections and delayed live search.
- Index bulk identity matching; retain original timestamps/audit for unchanged Excel roundtrips and preserve real before/after edits.
- Fix the Brand editor binding; retain existing data/schema, explicit import preview/selection and stable component identities.
- See docs/QA_0.12.1.md for measured performance, correctness and remaining manual acceptance.

## 0.12.0 — 2026-10-22

- Use bundled Netmiko for explicit parallel Collector jobs across IOS, IOS-XE, IOS XR, NX-OS and Huawei VRP, including per-device profile overrides.
- Confirm/save SSH keys before credentials; save usernames only, show passwords on demand and name protected captures by observed address/hostname. Guard Huawei confirmation behavior and await worker cancellation.
- Rename Inventory and support the supplied component XLSX template with nullable fields, reviewed stable-ID/unique-serial updates, retained legacy imports and independent rows sharing IPs.
- Replace both theme dropdowns with Dark-mode switches and remove the Terminal desktop feature/emulator assets.
- See docs/QA_0.12.0.md for exact checks and physical vendor/AAA/Setup limitations.

## 0.11.2 - 2026-10-21

- Fix Config Collector compatibility with strong Cisco AES-CTR/SHA-2 ETM combinations that the earlier SSH provider could not negotiate. Retain host-key pin checks before authentication, memory-only credentials and authentication-rejection-only fallback.
- Use SSH console CR command termination, preserve Telnet NVT CRLF, and separate authentication/connection and shell setup deadlines.
- Bound stream buffers and own connection cancellation/cleanup; report safe stage-specific SSH errors instead of one generic negotiation/session error.
- Published as GitHub Latest after exact-payload/CI/public-download and actual 0.11.1 updater discovery/staging checks. See docs/QA_0.11.2.md and docs/GITHUB_RELEASE_0.11.2_REPORT_2026-10-21.md for evidence and actual-device acceptance limits.

## 0.11.1 - 2026-10-21

- Clear the obsolete maximum-tab warning after Terminal tabs are closed, retaining the live SSH session and unrelated error messages.
- Preserve unsaved Inventory/CVE edits during filtering and workspace refresh; reject every conflicting duplicate Excel ID/serial row.
- Retry bounded atomic engineering-data replacement during transient file contention; retain the original revision on cancellation/permanent failure and always release the store gate.
- Scope Cisco neighbor parsing to captured CDP/LLDP commands, prevent incomplete neighbor leakage, and keep contradictory endpoint addresses under review. Clear configuration differences on device changes. Explicitly reanalyze legacy captures with verified original hashes, retaining observation history.
- Stop treating ordinary running-config banner words as live Collector authentication/confirmation prompts; genuine waiting prompts still stop without replay.
- Bound CVE response body reads, retain NVD metadata when optional CISA lookup times out, and refresh expired assessments/plans before approval or export.
- See docs/QA_0.11.1.md for actual program captures, owned SSH integration and remaining acceptance limits.

## 0.11.0 - 2026-10-20

- Complete current navigation with an actual-state Dashboard, stable-ID MA Inventory and versioned Excel preview/import/export with before/after audit.
- Analyze hash-verified Collector captures into versioned Cisco facts; inspect one-hop CDP/LLDP links, freshness, source lines and bounded capture comparisons. No discovery is triggered.
- Fetch NVD/CISA metadata by explicit CVE ID; record source/configuration-aware engineer review and invalidate it when evidence changes. Plan device changes with explicit engineer approval, before/after observations and Excel/CSV/HTML reports. No device upgrades execute.
- Keep separate protected atomic engineering records/previous snapshot; retain existing preferences/monitoring/Terminal formats. All new result regions use resizable tables and Ctrl+wheel zoom.
- See docs/QA_0.11.0.md for actual verification and remaining human/device gates.

## 0.10.1 - 2026-10-19

- Replace Terminal fingerprint entry with first-use Accept Once / Accept & Save / Cancel, saved-host recognition and explicit changed-key confirmation before authentication.
- Add a nonsecret per-user host-key database and a themed viewer/removal window. Preserve legacy profile fields without allowing them to bypass the new trust flow.
- Pause the SSH connection timer during host-key review; cancellation closes the prompt and drains the connection. See docs/FEATURES_0.10.1.md for QA and open gates.

## 0.10.0 - 2026-10-19

- Add real offline xterm.js Terminal in the existing Light/Dark/System theme, with five independent SSH/Telnet tabs and bounded input, output and scrollback.
- Verify SHA-256 host keys; support password and encrypted OpenSSH private keys without automatic reconnect or credential fallback. Save nonsecret profiles only.
- Add keyboard/control input, guarded multiline paste, Copy/Paste, resize, Ctrl+wheel zoom and awaited tab/Exit/update cleanup. Block remote OSC52 clipboard writes and external browser resources/navigation.
- Owned SSH/Telnet/emulator and existing-app regressions are recorded in docs/QA_0.10.0.md; real devices, physical DPI/accessibility and Windows clipboard/manual acceptance remain open.

## 0.9.0 — 2026-10-19

- Replace the Config Collector placeholder with explicit-start multiline SSH/Telnet collection, memory-only AUTH profiles, verified host keys and bounded authentication fallback.
- Freeze run options; execute read-only commands after actual device prompts; stream protected TXT/JSON evidence with hashes, bounded previews, error CSV and cancel/drain.
- Use the existing Light/Dark/System theme, adjustable panels/columns and shared result zoom. Owned SSH/Telnet and WPF verification does not establish real Cisco/vendor acceptance. See docs/FEATURES_0.9.0.md and docs/QA_0.9.0.md.

## 0.8.0 — 2026-10-15

- Add local Monitoring for 50 configured devices with ICMP/TCP and read-only SNMP v1/v2c/v3, Test SNMP, selected interfaces/bandwidth/errors/system/custom OIDs, explicit Start/Stop auto-resume, maintenance, bounded incidents and local notification cooldown.
- X hides to Tray; explicit Exit/update drains collection. Restore an existing profile instance on relaunch and mark sleep/network observation gaps separately.
- Rename Subnet Calculator; optional active-adapter IPv6 DNS servers and wrapped/resizable answers; process pasted MAC/ARP text and IP mapping only after Check.
- Add Windows-protected personal VirusTotal key and bounded existing-report API lookup; embed Palo Alto Test A Site with its actual provider verification controls.
- Ctrl+mouse wheel zooms result tables/text; improve bounded atomic replacement retry and WPF async disposal. Scope/QA/remaining gates: docs/FEATURES_0.8.0.md and docs/QA_0.8.0.md.

## 0.7.0 — 2026-10-15

- Replace retired desktop DNS/HTTP/Wi-Fi screens and the Diagnostics entry with Subnet Cal, DNS Resolver, MAC Checker and the URL Checker provider layout; Port Test is TCP-only.
- Calculate IPv4 CIDR/dotted masks on each edit in one field; distinguish incomplete and invalid input without retaining stale subnet results.
- Add NS/SOA DNS lookup, bounded actual MACVendors.com lookup/cache/queue/cooldown, and explicit pending URL API configuration without invented results.
- Verification, approval and remaining provider/manual gates: `docs/TOOLS_REVISION_2026-10-15.md`. Published as GitHub Latest; exact package/CI/public updater evidence: `docs/GITHUB_RELEASE_0.7.0_REPORT_2026-10-15.md`.

## 0.6.0 — 2026-10-09

- Added DNS Test with selected records/resolvers, validated UDP/TCP responses, bounded queries and measured results.
- Added HTTP/HTTPS Test with GET/HEAD, expected status, optional redirects, header timing and TLS/certificate details; normal certificate validation remains enforced.
- Grouped navigation with licensed outline icons and Coming Soon pages; retained Wi-Fi and removed History/Reports from navigation as requested.
- Fixed Dark version labels and native Taskbar/Alt-Tab icon/shortcut identity. New tools drain during Stop/shutdown/update.
- Verification and remaining human/remote/install limits: docs/QA_0.6.0.md.


## 0.5.0 — 2026-10-09

- Replace Network Info's placeholder with the approved compact saved-WLAN-profile table and current Light/Dark theme; add adapter selection, explicit scan/connect/disconnect, nonsecret descriptions/auto-connect, confirmed deletion and safe IT-profile import/secret-free export.
- Use Windows WLAN/EAP/certificate authentication, Personal key editing and supported PEAP user credentials; optional additional secrets stay in Windows Credential Manager. No plaintext preferences/logs/exports or fabricated Wi-Fi profiles.
- Add configured source-aware Gateway ICMP IPv4/IPv6, adapter-bound DNS/TCP/HTTP checks, cancellation/readiness, network-transition notices and bounded connection/test history with measured CSV results. HTTP retains TLS validation and never follows redirects/submits portal credentials.
- Integrate with all 0.4.2 diagnostics fixes, including selectable Wi-Fi tables and awaited Wi-Fi shutdown before update/recovery. Real Wi-Fi authentication still requires hardware acceptance; see `docs/QA_0.5.0.md`.

## Unreleased — milestone documentation (2026-10-09)

- Consolidate product vision, feature/navigation and approval/decision records, dependencies/security and an evidence-based implementation audit, separating published packages from working follow-ups.
- Prepare M00–M09, post-stable A01–A09 and On Hold B01–B05 specifications and the next approved acceptance task; reconcile proposed versions without changing application metadata.
- Synchronize repository instructions/skill and roadmap links. This planning task adds no product behavior, GitHub milestone, commit or release; existing concurrent defect changes are preserved.

## 0.4.2 — 2026-10-09

- Pace Live Ping at up to 128 native sends/s and reserve separate aggregate Traceroute slots/rate, preventing Ping from occupying trace capacity. Preview effective large-scope intervals.
- Explain Warn and use the last 20 completed attempts for recent-loss health while retaining accurate cumulative counters.
- Add actual Reply IP Address to Ping results and Host / Reply IP Address to history and CSV.
- Enable selectable/copyable table cells and headers, bounded whole-table copy/text views, and preserve editable descriptions, sorting and Port Test column preferences.
- Add disabled-by-default per-session direct TCP checks on responding hop IPs; show actual successful ports with independent bounded workers/cache and cancellation.

## 0.4.1 — 2026-10-09

- Remove redundant caption labels; fix dark input carets, session text and address-template Save/Probe Settings spacing.
- Move shared Hop Description before the Traceroute Results filter; show stable lifecycle state without changing per-probe outcome text.
- Make Port Test continuous and multiple-target by default with both checkboxes removed; support inclusive, deduplicated port ranges in inputs/templates while retaining scope limits.
- Add bounded Packet Size settings and actual TCP/UDP zero-filled payload transmission, with real sent-byte details, partial-send handling, cancellation and explicit send-failure outcomes. TCP timing measures the connection; UDP silence remains inconclusive.

## 0.4.0 — 2026-10-09

- Apply the supplied NetStucked brand, Light/Dark/System themes, themed application dialogs, input guides, status colors and smooth collapsible/resizable menu, addresses, history and event panels.
- Share multiline IP-to-description mappings across Traceroute sessions; look up public WAN organization/ASN through RIPEstat with bounded cached requests and local mapping priority.
- Keep one undeletable main trace session, require a valid destination before adding another new session, and limit the workspace to five sessions.
- Separate port targets, TCP/UDP and port numbers; add single-pass/continuous selection, bounded Host/IP/CIDR × port scans, editable saved/restorable common-port templates and explicit acknowledgement above 4,096 checks. UDP silence remains inconclusive.
- Preserve previous update/recovery behavior and all canonical approved reference files.

## 0.3.0 — 2026-10-09

- Added user-triggered installation/recovery of a selected published build, with actual release notes, a dynamic Install/Reinstall/Recover button, download progress and preparation cancellation.
- Verify fixed-project HTTPS installer metadata, hash/size, product/version and Windows trust. Explicit acknowledgement is required for unsigned builds; invalid signatures are blocked and Windows prompts remain enabled.
- Drain diagnostic sessions, save preferences, stage an isolated application/runtime helper, keep bounded verified versioned settings backups, run Inno Setup and restart. Recovery restores a matching settings backup when available; compatible schema-1 preferences otherwise remain in place.
- Keep release-page/manual fallback and on-demand checking. Published 0.2.x packages are immutable; their own update menus remain manual after downgrading to them.

## 0.2.0 — 2026-10-09

- Added TCP Port Test for explicit IPv4/IPv6/hostname endpoints, with independent bounded polling, actual connect time/outcomes, pause/resume/stop, local named templates, selected-endpoint history and CSV export.
- Added Updates & Recovery: user-triggered GitHub Releases checks, real executable version, release notes, recorded version history, manual download links and rollback guidance. No automatic check, download, installation or rollback.
- Moved Settings and Updates to the bottom sidebar application group; added Port Test activity indication and cached navigation.
- Preserved Live Ping and Traceroute behavior and the canonical approved references; extended real TCP, release parsing and Windows integration tests.

## 0.1.0

- Establish .NET 10 WPF/MVVM solution, shared compact theme and approved two-page layout.
- Add bounded, cancellable ICMP Live Ping and continuous IPv4 TTL traceroute, real statistics, target lists, history, event log and CSV export.
- Add persisted column preferences, settings, deterministic network-adapter tests, self-contained publishing and stable Inno Setup installer identity.
- Keep Dashboard/Network Info neutral placeholders; updates and recovery remain roadmap only.
- Fix Traceroute refresh during Description editing, immediate Description CSV export, final-hop probe counts, horizontal scrollbar direction and nullable settings validation; add regression coverage (2026-10-08 bug audit).
- Improve Ping cadence with monotonic deadlines, bounded worker queues, packet-rate limits and separate cached/backed-off DNS; add parallel TTL discovery, adaptive continuous polling, asynchronous reverse DNS, incremental UI updates and bounded timing diagnostics (2026-10-08 performance work).

- Apply the user-requested compact settings, named address templates, destination history, independent traceroute sessions, result timestamps, stable history scrolling, cached views, activity indicators, Fit Columns/resize guide/tooltips and monitor-aware framed window (2026-10-08 UI revision). Replace adaptive cycle batches with independent per-hop continuous polling and important-only events.

Validation and known limitations are recorded separately in `docs/QA_REPORT.md`.
