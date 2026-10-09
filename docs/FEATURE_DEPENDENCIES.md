# Feature dependencies

Solid arrows describe required capabilities, not permission or a requirement to finish every preceding UI. All future modules/interfaces below are **proposed**, absent from current production code.

```mermaid
flowchart LR
  PT[Existing ICMP / TCP / UDP engines] --> HC[M03 target health profiles]
  PT --> MON[M04 local monitoring state machine]
  PT --> HIST[M05 durable history]
  NI[M01 Network Info] --> WIFI[F02 adapter-aware Wi-Fi tests]
  MON --> INCIDENT[Incident persistence / alerts]
  STORE[Versioned SQLite migrations and retention] --> HIST
  STORE --> INCIDENT
  HIST --> DASH[M06 dashboard / reports]
  INCIDENT --> DASH
  SSH[Shared verified SSH transport] --> TERM[A01 actual terminal emulator]
  SSH --> COL[A02 Cisco collection and raw evidence]
  ASSET[Stable Asset identity / basic inventory contract] --> COL
  ASSET --> INV[A03 full MA import / reconciliation]
  COL --> PARSE[A04 OS-family parsers]
  PARSE --> INV
  PARSE --> TOPO[A05 evidence-aware one-hop topology]
  PARSE --> CVE[A06 applicability assessment]
  ADV[Official advisory metadata] --> CVE
  CVE --> UPG[A07 engineer-approved upgrade plans]
  INV --> UPG
  MON --> CENTRAL[A08 central collector]
  PACKAGE[Existing Inno / SemVer / integrity foundation] --> UPDATE[M07 selected-build client]
  UPDATE --> REC[Machine-tested compatible recovery]
  REG[Feature registry] -. on hold .-> LIC[B01-B05 licensing research]
```

Current production ownership remains Core contracts/scheduling, Infrastructure network/Windows/persistence, Desktop presentation/DI. Propose narrow interfaces for `IHistoryStore`, `IIncidentStore`, `INetworkContextSource`, `IWlanProfileService`, `ISshTransport`, `IRawEvidenceStore`, `IDeviceParser`, `IAssetStore`, `ITopologyEvidenceSource`, `IAdvisorySource` and `IUpgradePlanStore` only when the corresponding milestone is authorized. They are not implemented now.

Avoid circular dependencies: Collector records stable Asset IDs plus raw observed facts; inventory reconciliation is a separate approval-aware consumer. Parsing consumes raw collection records. Topology and CVE consume versioned fact/evidence contracts; neither invokes the other. Upgrade plans consume reviewed assessments and inventory, never execute device upgrades in this scope. Collection is reusable by Terminal but does not require terminal UI/emulation.

History should accept session/probe identifiers without running network calls on its writer. Monitoring can initially retain bounded incidents, but persistent incident acceptance needs the M05-compatible store contract. Before SQLite is shipped, M07 must gain explicit schema migration/downgrade coverage; current schema-1 JSON compatibility does not prove future database rollback.
