# Approved 0.8.0 scope — 2026-10-15

The human explicitly approves implementation without a mockup after reviewing the plan and answering six questions. The existing WPF theme/layout language is retained. This supersedes M04's missing-mockup boundary for the named local Monitoring slice; other pending modules stay pending.

- Rename Subnet Calculator; add optional IPv6 DNS server inclusion below active-adapter selection (default off; AAAA remains independent), wrapped/resizable answers.
- MAC/ARP pasted text is processed only by Check. Recognize supported MAC formats amidst words/headers; preserve input order and same-line strict IP mapping. No automatic result parsing or provider request on paste. Cache vendor requests while preserving duplicate mapping rows.
- Personal-account VirusTotal lookup/configuration with Windows-protected key storage, quota/error/unknown states; the human will prepare their own key. Palo Alto uses the actual Test A Site website displayed inside the app, not an undocumented API scrape.
- Local Monitoring: approximately 50 explicitly configured devices initially, ICMP and optional TCP secondary, SNMP v1/v2c/v3 with explicit Test before use, selectable interface/bandwidth/system observations, editable intervals/thresholds, read-only network operations. ICMP 10s, SNMP 30s, three failures/two successes by default.
- Persist Start/Stop intent and bounded incidents; previously started monitors resume on application startup. Maintenance suppresses local alerts. Device reachability, SNMP errors and selected-link health remain distinct. Sleep/offline periods are observation gaps, not fabricated downtime. X hides to System Tray, Restore/Exit are explicit, update/exit drain jobs. Windows notification and optional sound are local only.
- Ctrl+mouse wheel zooms all result regions, with bounded scaling and reset support; regular scrolling, input controls and sidebar remain usable.

Version 0.8.0 and GitHub update publication follow the agreed plan and standing publication instruction. Preserve all older tags/assets and canonical references. QA must distinguish deterministic, owned-loopback, WPF/native, live provider and actual Cisco/installed-machine acceptance. No real Cisco targets/credentials or VirusTotal key have been supplied; those live checks remain untested until provisioned. No Setup execution on the working machine.
