# Security and data policy

## Current application

Only user-selected authorized targets are probed. CIDR/port expansion, admission rate, native requests, histories and metadata caches are bounded. Editing or previewing a scope sends no probes. Current Port Test requires renewed acknowledgement above 4,096 endpoint checks and caps 1,024 hosts, 64 ports and 16,384 checks. See `PortScanPlanner` and [0.4.0 scope](FEATURES_0.4.0.md); do not remove guardrails when adding tools.

Preferences are local schema-1 JSON under `%LOCALAPPDATA%\NetStucked`, with atomic serialized writes and a 2 MB load limit. Histories and event collections are bounded in memory; exports/logs may reveal addresses and descriptions. No credentials are stored. RIPEstat lookups send eligible public hop IPs only; private/reserved addresses are excluded, manual mappings win, and availability metadata never changes probe results.

Updates are user-triggered fixed-project HTTPS actions. Verify asset identity, SHA-256/size, PE product/version and Windows trust. Hash verification does not establish publisher identity. Unsigned installers need explicit acknowledgement; invalid trust fails closed. Preserve Mark of the Web, UAC and SmartScreen. Await network shutdown, stage a private helper and verified compatible settings backup; never kill Setup once started. Current recovery preserves compatible schema-1 JSON or restores a matching versioned backup; it is not transactional restoration of Program Files. Machine acceptance remains pending.

## Proposed persistent modules

For SQLite, define migration/schema compatibility, backup/restore, corruption handling, retention, disk limits and single-writer behavior before implementation. Persist timestamps with offset/UTC semantics, stable IDs and source/session identity. Decide upgrade/downgrade compatibility per schema; block unsupported recovery or restore an independently verified compatible backup. Never assume older applications read newer data.

SSH/password/key and Wi-Fi credentials must use Windows-managed credentials/certificates or an explicitly designed OS-protected store; never plaintext preferences, logs, exports or source control. Host-key mismatch blocks SSH until an engineer explicitly resolves it; bound authentication fallback to avoid account lockout. Telnet requires explicit legacy authorization and visible plaintext-transport context. Respect WLAN Group Policy/EAP restrictions and report inability to establish source-adapter routing.

Collector configuration backups/raw outputs are sensitive even without explicit passwords. Propose restrictive per-user ACLs, redacted logs/default exports, encryption/key handling and retention before collecting production outputs. Store original evidence separately from parsed facts and human inventory. Record raw evidence ID/hash, command, device/OS family, parser version, timestamp and confidence. Imported human fields must not be silently overwritten by observed facts.

Topology links require evidence classifications and freshness; running-config alone does not establish physical adjacency or uplink direction. CVE results require vendor advisory/config applicability evidence; a version string alone cannot establish affected/not-affected or universally safe status. Never upload complete configs to advisory APIs by default. Any automatic advisory-metadata sync belongs only to a future approved CVE milestone, not background application installation.

Commercial licensing/payment processing is On Hold. Future research must address signed entitlements, privacy, verified/idempotent webhooks and provider/legal constraints before activation. Current documentation selects no provider, price or policy.
