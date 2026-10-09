# Future ideas and unresolved product choices

This is a backlog, not implementation authorization. [FEATURE_REGISTRY](FEATURE_REGISTRY.md) retains detailed functional requirements; [milestones](MILESTONE_INDEX.md) carry actionable prerequisites and tests.

## Approval-dependent pre-stable candidates

Network Info with Wi-Fi inside it; dedicated DNS/HTTP/TLS and health profiles; local monitoring with hybrid contracts; SQLite history/reports; an operational Dashboard. Network Info/Dashboard/Diagnostics/History UI references are pending; reported Wi-Fi and Monitoring mockups are not located in the tracked repository. Do not invent their appearance.

## Post-stable candidates

A01–A09 cover SSH/Telnet/serial/SFTP terminal, Cisco-only Collector, MA Inventory/Excel templates, evidence-aware device parsers, one-hop Topology, Cisco CVE/advisory synchronization, engineer-reviewed Upgrade Planner, central/shared monitoring and advanced Enterprise Wi-Fi. Exact 1.x version and final UI are unassigned. Collector scheduling stays excluded from its selected scope. Analyze existing collected data first; Refresh & Analyze is a separately confirmed collection action. Fictional planning names such as CSW-HQ, DSW-01/02, FW-HQ, RTR-HQ and Wi-Fi profiles PTT-Wireless/PTTOR/Spirit/Test01 are design examples, never production device facts or preconfigured probe targets.

Candidate advisory sources to evaluate later: Cisco PSIRT/OpenVuln, Software Checker/official advisories, NVD, CISA KEV and vendor release documentation. API access/licensing, availability, rate limits, applicability mapping and fixed-release evidence must be verified at implementation time; no connection or dataset is claimed here.

## Commercial research — On Hold

B01–B05 retain Freemium Free/Professional/Team/Enterprise tiers, per-user licensing, proposed two activations, signed offline entitlement cache, proposed 30-day offline policy, local-first behavior, license API/customer portal, annual prepaid/renewal/recovery, dynamic PromptPay QR, payment gateway, verified webhook and automatic activation. All require fresh explicit authorization; no provider/price/policy is chosen. Subscription/Billing would belong in future Account/License settings, not current navigation. Do not add payment packages, services or entitlement enforcement now.

## Decisions required before implementation

- Approve UI assets and first functional slice for each new page; locate reported mockups/brand vectors.
- Define authorized target bounds, local-versus-central execution and source-adapter guarantees.
- Define SQLite migrations/retention and old-version compatibility before persistent data ships.
- Define SSH host trust, credential protection, raw-config protection and parser evidence schema.
- Define topology confidence, advisory applicability and engineer approval before upgrade recommendations.
- Obtain a Windows 11/clean Windows test environment and an authorized remote lab before closing release acceptance.
