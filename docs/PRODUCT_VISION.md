# NetStucked product vision

Planning snapshot: 2026-10-09. The published baseline verified in this audit is **0.4.0**; current source has the separately authorized **0.4.1** defect follow-up. Both are development versions; local metadata does not prove publication. The initial 0.1.0 target is historical and the first stable target remains **1.0.0**. Version proposals do not authorize implementation/publication.

NetStucked is an All-in-One Network Engineering Toolkit for network engineers, NOC teams, infrastructure engineers and IT support. Its purpose is diagnosing remote IP addresses, hostnames, authorized subnets, routes and services. Local adapter information is supporting context, not a PC health dashboard.

The product is Windows 10/11 x64, C#, WPF, .NET 10, MVVM, dependency injection and cancellable asynchronous services. Windows 10 is the tested host; Windows 11 acceptance is pending. Installation uses Inno Setup and a self-contained private .NET runtime. SQLite is the proposed store for future durable history/inventory; current preferences use schema-1 JSON and diagnostic histories are bounded in memory.

The visual foundation is compact Minimal Light with blue `#2563EB`, thin borders, dense tables and status words beside colors. The later approved 0.4.0 extension adds Light/Dark/System themes, supplied branding and collapsible/resizable panels. Preserve the two original approved PNGs and apply only recorded later authorizations.

Product priorities are accurate observable results, independent responsive probing, explicit network scope, evidence traceability and settings-preserving release/recovery. An ICMP timeout is not proof of a service outage; UDP silence is inconclusive; metadata is not a measurement.

See [implementation status](IMPLEMENTATION_STATUS.md), [feature registry](FEATURE_REGISTRY.md), [milestones](MILESTONE_INDEX.md), [approval register](UI_APPROVAL_REGISTER.md), [architecture](ARCHITECTURE.md) and [next task](CODEX_NEXT_TASK.md). Future mockups and commercial ideas remain separate from approved work.
