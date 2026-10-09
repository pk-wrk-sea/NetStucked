# 0.5.0 — Wi-Fi integration

The later human request on 2026-10-09 explicitly authorizes combining the approved Wi-Fi work with published 0.4.2 and publishing a new version. This supersedes the preview's no-version/publication boundary. Scope follows [approved Wi-Fi slice](FEATURES_WIFI_2026-10-09.md), [usage and limitations](WIFI_PROFILE_MANAGER.md) and its unchanged approved reference with compact-table override.

Network Info now provides Windows WLAN adapters/profiles, explicit scan/connect/disconnect, Personal profile management, safe XML import/export, Windows-managed Enterprise/EAP/certificates and optional Windows Credential Manager storage. Configured Gateway/DNS/TCP/HTTP Connect & Test uses the selected adapter's source context, actual measurements, cancellation, bounded history/CSV and network-transition notices. No automatic switching or fabricated production profiles.

All [0.4.2 diagnostics changes](FEATURES_0.4.2.md) remain: paced Ping with reserved trace capacity, current Warn reasons, actual reply IPs, selectable tables, Port column preferences and optional direct TCP checks on responding hop IPs. Wi-Fi tables use the same selectable text/headers and themed copy actions. Shared update preparation awaits Wi-Fi cancellation/drain before saving settings and starting its helper.

Schema-1 preferences retain new Wi-Fi metadata/test history and diagnostic settings together in 0.5.0. Versioned recovery backs up exact bytes. Older applications may drop unknown Wi-Fi fields when saving; recovering the matching 0.5.0 backup restores them. Windows WLAN profiles and OS-protected credentials live outside JSON. No installer or real Wi-Fi connection changes are authorized implicitly by this development/publication request.

Broader Network Info routes/neighbors, expanded EAP editors/certificate provisioning and Dashboard remain outside this release. Actual hardware/Enterprise authentication needs acceptance on a Wi-Fi-capable authorized machine. [Combined QA](QA_0.5.0.md) separates these limits from automated results.

## Human acceptance checklist

- [ ] Upgrade to 0.5.0; verify Settings/Updates version and retained theme, Ping/Trace/Port templates and settings.
- [ ] Open Network Info; verify real adapter/profile list, no-adapter/unavailable states and compact Light/Dark layout.
- [ ] Scan SSIDs; search, sort, resize/configure profile columns and copy table/header/history text.
- [ ] On a lab SSID, connect/disconnect/cancel; confirm actual SSID, IP, gateway, DNS and signal.
- [ ] Add/edit a lab Personal profile, update its key, import approved XML and export without passwords; verify delete confirmation and policy restrictions.
- [ ] On an IT-approved Enterprise network, verify supported PEAP credentials or Windows-provisioned EAP-TLS; optional remember/forget affects only the app's Credential Manager copy.
- [ ] Configure company Gateway/DNS/TCP/HTTP endpoints; Run Tests / Connect & Test, reopen history and export CSV; check actual results/timing and failure explanations.
- [ ] Change network during tests; verify stale results are cancelled and transition notice appears while diagnostics continue.
- [ ] Repeat /27 and /24 Ping at interval 250 / timeout 500 alongside two trace sessions and Port Test; inspect Warn reasons and UI stability.
- [ ] Verify Ping reply-IP columns, selectable diagnostic tables, Port column preferences and optional hop TCP ports from 0.4.2.
- [ ] Check Updates: Reinstall 0.5.0 / Recover 0.4.2. If testing real recovery, retain the 0.5.0 settings backup before older apps save preferences.
