# 0.11.0 integrated menu review and QA

Human authorization: 2026-10-20, finish all current menus and review together. [Scope](FEATURES_0.11.0.md). This is an additive application revision, not a claim that all historical roadmap milestones or machine acceptance are complete.

## Review workflows

- **Dashboard:** actual local diagnostic/session activity, configured/active Monitoring, recent real local incidents, inventory/MA counts and drill-down buttons. No dashboard probes or invented samples.
- **MA Inventory:** New/Save stable AssetId; human model/serial/site/location/role/contract/MA dates; search and resizable columns. Export a real versioned XLSX/template. Import preview validates schema, headers, formulas, duplicate IDs/serials, typed dates and limits. Select valid rows and explicitly Apply; imported updates preserve a before/after edit audit. Attach Collector JSON to a selected asset after reviewing observed identity. Observed version/model/serial and source/hash/time remain separate from human fields. CSV observation and edit-audit exports are available.
- **Config Collector:** existing verified SSH/Telnet policy is retained. The new **Analysis template** selects bounded Cisco read-only version/inventory/interface/route/CDP/LLDP commands; it starts nothing. Confirm platform command support before Start. Attach the completed JSON through Inventory. TXT-only imported captures are marked partial because completion cannot be verified.
- **Topology Explorer:** select captured device, Analyze existing data, select a graph neighbor or link row to inspect endpoints, source line/hash/time and freshness. At most 24 nodes are drawn; the table lists all up to 512 neighbor records. Reciprocal fresh unambiguous matching endpoints can be classified Verified by evidence; this is not an on-site physical cabling acceptance. Conflicting identities remain Needs review. Config comparison verifies the original files still match their saved hashes, then compares a bounded changed region. Source files must remain available.
- **CVE & Upgrade Planner:** enter a CVE ID and explicitly Fetch NVD + KEV. Metadata and Cisco source URLs are cached locally. Inspect product/version/configuration conditions and record an engineer assessment. Version alone never gives Affected/Not Affected. Evidence >30 days or metadata/review >7 days, changed evidence or changed advisory revision invalidates the assessment. Enter target release, official fixed/compatibility source URLs, owner/change/wave/window and approving engineer. Explicit approval requires a current reviewed assessment. Save edits as Draft clears approval. Import newer post-change evidence through Inventory, then Compare; matching the target still requires advisory reassessment. Export Excel/CSV/escaped HTML plans. No device upgrade/config commands are executed.
- Existing Live Ping, Traceroute, TCP Port Test, Subnet Calculator, DNS Resolver, URL Checker, MAC Checker, Monitoring, Terminal, Settings and Updates remain functional. Result grids/text support Ctrl+wheel / Ctrl+0. X continues to hide to tray; explicit Exit/update drains operations.

## Data/security/compatibility

`%LOCALAPPDATA%\NetStucked\engineering\workspace-v1.json` is a separate schema-1 document, bounded to 16 MiB, protected to current user/SYSTEM on Windows. Serialized writes run off Dispatcher with revision checking, validation, temporary replacement and a previous valid snapshot `workspace-v1.previous.json`. Corrupt/unknown data is retained and editing is blocked; restore a verified backup manually with the application closed. No raw transcripts or authentication/API secrets are copied into this store. Exports contain network/asset/change details; choose their destination deliberately.

Existing settings/monitoring/Terminal formats are unchanged. Older app versions ignore the new engineering directory. Existing updater settings backup covers `settings.json`; it does not claim to back up the new engineering workspace or original captures. No SQLite schema migration is introduced. New data remains beside existing settings across application binary replacement; actual Setup/VM upgrade/downgrade remains NOT TESTED.

## Evidence so far

- Release build: PASS, no warnings/errors.
- Unit/integration regression: 462 PASS, zero failed/skipped, `artifacts/all-menus/tests-final-04.log`; includes native Excel date cells, previous metadata revisions and previous valid store snapshot and oversized export preservation.
- Actual WPF current-menu baseline: PASS in `artifacts/all-menus/windows-regression-02.log`; updated the former placeholder assertions and embedded release history for the actual working version.
- Actual new-menu WPF workflow: 42 assertions PASS in `artifacts/all-menus/engineering-ui-final.log`, with owned constructed Cisco/advisory fixtures, real persistence/XLSX, explicit import/review/approval and post-change invalidation. 28 renders cover Light/Dark at 1536×1024 and 1100×700, including every new tab. These are WPF renders, not physical-DPI/manual accessibility evidence.
- Exact old 0.10.1 native binary: start/graceful Exit 0 using an isolated profile containing new engineering data; engineering file SHA-256 is unchanged. `artifacts/all-menus/compatibility-0101.log`. This is binary/data coexistence evidence, not Setup downgrade/rollback acceptance.
- Actual public NVD/CISA adapter: PASS for CVE-2023-20198, metadata identity/description, one Cisco reference and KEV listed; `artifacts/all-menus/public-advisory/advisory.json`. No device data or configuration was sent and no device applicability was asserted.
- Final frozen-payload/native/SSH/CI/public-download evidence: pending publication verification, not implied by the above.

## Sources and access policy checked at implementation

[NVD CVE API 2.0](https://nvd.nist.gov/developers/vulnerabilities), [NVD API guidance](https://nvd.nist.gov/developers/start-here), [CISA KEV catalog](https://www.cisa.gov/known-exploited-vulnerabilities-catalog). The app sends only an explicit CVE ID to NVD and downloads the public KEV catalog; requests are serialized with ≥6 seconds between NVD calls, bounded responses, timeout/cancellation, no redirects or quota bypass. KEV failure is Unknown and does not erase successfully obtained NVD metadata. This product uses the NVD API but is not endorsed or certified by NVD.

[Cisco PSIRT registration](https://developer.cisco.com/docs/psirt/getting-started/) and [authentication](https://developer.cisco.com/docs/psirt/authentication/) require a registered application/client credentials. No Cisco account/token was provided, so this revision does not pretend to call authenticated Software Checker or automatically determine fixed/compatible releases. Official Cisco URLs from NVD and engineer-entered source evidence support the review workflow. VirusTotal still requires the user's protected key; Palo Alto remains the actual embedded Test A Site with provider controls preserved.

## Open acceptance — NOT TESTED

Real Cisco command/parser corpus (IOS/IOS XE/NX-OS/ASA), physical adjacency and platform/feature applicability; engineer validation of actual fixed/compatible targets; personal VirusTotal account and Palo Alto CAPTCHA/session; real SNMP/device capacity; COM/Serial/SFTP, scheduling, cross-tool durable history and central collectors; mixed-monitor/physical DPI, screen reader, High Contrast, long-run behavior and clean Windows installer/recovery. Human review of this integrated revision remains open. These are not silently marked PASS or closed by fixture tests.

