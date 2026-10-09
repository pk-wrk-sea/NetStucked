# Product roadmap

Snapshot: 2026-10-09. Published **0.4.0** is current; **1.0.0** remains the first stable target. The brief's 0.1–0.9 assignments were proposals based on an earlier state. Preserve milestone IDs but do not reuse versions already shipped with different features.

## Actual release sequence

| Version | Actual delivery |
|---|---|
| 0.1.0 | Foundation, Live Ping, IPv4 ICMP Trace, templates/settings/CSV/installer foundation and approved UI revisions; retained in subsequent releases |
| 0.2.0 / 0.2.1 test | TCP Port Test/manual GitHub Updates; releases/downloads retired by explicit instruction; source tags/history and verified backups retained |
| 0.3.0 | Selected-build Install/Reinstall/Recover baseline |
| 0.3.1 TEST BUILD | Metadata/docs test build for the 0.3.0 update/recovery path; normal SemVer, not GitHub Latest |
| 0.4.0 | Supplied branding/themes/panels, shared hop descriptions/five sessions, TCP/UDP host × port scans; GitHub Latest |

No claim is made that the brief's Network Info or DNS/HTTP milestone shipped in 0.2.0/0.3.0.

## Pre-stable candidates

| ID | Brief version → reconciled candidate | Scope and present status |
|---|---|---|
| M00 | 0.1.0 → retained in 0.4.0 | Implemented/released functionality; baseline Windows/remote/manual acceptance open; next approved work |
| M01 | 0.2.0 → **0.5.0 proposed** | Network Info; Wi-Fi separately approval-dependent; no feature implementation |
| M02 | 0.3.0 → **0.6.0 proposed** | Dedicated DNS/HTTP/TLS; reuse shipped TCP/UDP; approval pending |
| M03 | 0.4.0 → **0.7.0 proposed** | Target health checks/network profiles; approval pending |
| M04 | 0.5.0 → **0.8.0 proposed** | Local monitoring/incidents/alerts/tray, hybrid-ready; UI approval pending |
| M05 | 0.6.0 → **0.9.0 proposed** | SQLite durable history/reports; schema/recovery/UI approval pending |
| M06 | 0.7.0 → **0.10.0 proposed** | Dashboard/notifications/operational refinement; approval pending |
| M07 | 0.8.0 → delivered in **0.3.0+**, acceptance on 0.4.0 | Update client already exists; actual installation/recovery compatibility still open; corrections could use an explicitly authorized 0.4.x patch |
| M08 | 0.9.0 → **0.11.0 proposed** | Security, release signing, clean Windows matrix, accessibility/performance hardening |
| M09 | **1.0.0 target** | Stable release only after chosen committed scope is accepted or explicitly deferred |

These are candidate allocations, not dates, commitments, approved UI or permission to bump metadata. SemVer supports 0.10.0 and 0.11.0; numeric comparison is already implemented. No 0.4.1 build is created by this task.

Execution order starts with M00/M07 outstanding acceptance. Next new-feature candidate is M01 after design approval. M02 and the storage contracts for M05 may proceed after approval independently of some Network Info work; M04 needs engines plus a tested incident/retention policy. M06 consumes actual diagnostic/history/monitoring data; it must not invent dashboard measurements. M08/M09 are release gates. See [dependencies](FEATURE_DEPENDENCIES.md).

## Later candidates and deferred commercial work

A01 SSH terminal; A02 Cisco Collector; A03 MA Inventory; A04 Device Intelligence; A05 one-hop Topology; A06 Cisco CVE; A07 Upgrade Planner; A08 central/team collector; A09 advanced Wi-Fi/EAP. All are proposed after 1.0.0, with exact 1.x version unassigned. Shared SSH transport and raw-evidence contracts precede Collector parsing; the UI terminal is not a Collector prerequisite. Minimal stable asset identity is required before Collector-to-inventory reconciliation.

B01 entitlements; B02 per-user/offline licensing; B03 license service/portal; B04 QR payments/activation; B05 Team/Enterprise licensing. All are **On Hold**, unscheduled and need fresh explicit authorization; no provider, price, device/offline policy is final.

Every candidate has a concrete [milestone document](MILESTONE_INDEX.md). Unapproved pages are BLOCKED for UI implementation unless a functional-only prototype is explicitly authorized.
