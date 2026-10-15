# Utility revision — 2026-10-15

Version **0.7.0** release preparation on `codex/tools-rework`, based on `f643f3fe043e8f8bc6eaa12b35ede4ba92ec3127`. The subsequent human clarification requests an actual update release. Prior v0.6.0 assets/source remain immutable. Publication and exact-package evidence will be recorded in the release report; the development verification below predates the version bump.

## Human approval

The human requested a mockup before implementation, then approved the complete menu/tool revision on 2026-10-15. The final clarification specifies Subnet Cal's single text field, automatic calculation and inline incomplete/error messages. URL results must appear in the app through APIs after provider accounts/keys are prepared. After requesting GitHub publication, the human asked why version 0.7.0 was unavailable for update. This clarifies authorization to merge and publish the actual 0.7.0 packages/tag and verify public updater eligibility, preserving older published assets.

## Implemented desktop scope

- Removed the Diagnostics navigation item, old DNS Test and HTTP/HTTPS Test surfaces and commands, and the complete desktop Network Info/Wi-Fi Manager including profile/security dialogs, startup registrations, transitions and shutdown hooks. Development does not change WLAN services, profiles, connections or credential entries belonging to users.
- Port Test is TCP-only: no protocol selector, standard UDP template, UDP status filters, UDP-only No response metric or UDP guidance. Legacy stored protocol selection is normalized to TCP. The application does not register a generic UDP probe.
- Subnet Cal calculates IPv4 network/CIDR, mask, wildcard, broadcast, usable range and counts immediately on every text change. It accepts `10.10.10.10/23`, `10.10.10.10 /23`, `10.10.10.10 / 23`, and `10.10.10.10 255.255.255.0`. The supplied punctuation-separated form `10.10.10.10. 255.255.255.0` is also accepted, as is a terminal sentence period after the prefix. No CIDR dropdown or Calculate button.
- `Input incomplete` is separate from `Syntax error`; incomplete/invalid input clears previous results. Masks must be contiguous. /0 counts use 64-bit arithmetic without expansion; /31 is explicitly point-to-point and /32 represents one address. IPv6 is outside this approved IPv4 slice.
- DNS Resolver uses the existing bounded, validated DNS transport with system/custom resolver selection, A/AAAA/CNAME/MX/TXT/PTR/SRV and newly supported NS/SOA parsing. Its explicit Resolve action performs a single bounded lookup batch; copy, column settings, CSV and cancellation remain available. Generic UDP Port Test removal does not remove UDP/TCP DNS transport.
- MAC Checker accepts colon, dash, Cisco dotted and plain EUI-48 formats, deduplicates normalized inputs, looks up actual vendors through fixed HTTPS MACVendors.com, exports measured provider results and awaits Stop/shutdown. A serial queue limits requests to one per second, cache retains at most 512 successful results for one day, HTTP 429 applies provider cooldown, and response bodies are bounded to 4 KiB. Locally administered/multicast/broadcast/zero addresses do not fabricate vendor identities or send an API request.
- URL Checker has the approved in-app provider-results layout and truthful unconfigured status. **Live VirusTotal/Palo Alto API integration is pending**, requiring an account API key and a confirmed supported Palo Alto URL-category endpoint/access. Public Test A Site is not treated as a documented anonymous API. No key is collected/stored, no URL is submitted and no verdict is fabricated by this local UI.

Core/Infrastructure legacy adapters, DTOs and their low-level regression tests are retained for existing settings and compatibility; their retired desktop features are not registered or reachable. Existing schema-1 settings—including old Wi-Fi metadata—are retained rather than deleted. No Windows WLAN mutation is performed by the new app. Published release-history text remains historical.

## Verification and evidence

Evidence directory: `artifacts/tools-rework/` (Git-ignored). Source/view tests use a fresh build tree, preserving the previously launched build and release artifacts.

- Release solution build: PASS, zero warnings/errors (`build-final.log`).
- Final unit/adapter suite: PASS, **315 passed / 0 failed / 0 skipped**, `tests-final.log` and `tests-final/`; includes subnet boundary/format/state/contiguous-mask cases, NS/SOA answers and MAC format/cache/error/cancellation/provider-cooldown cases. Existing low-level retired-adapter regression coverage remains in the suite; it does not mean those desktop features still ship.
- Actual WPF/native checks: PASS, **221 assertions**, `windows-verified.log` and `windows-verified/`; new-menu presence/old-menu absence, property-change input calculation, no CIDR selector/Calculate action, clearing stale results, invalid mask message, new pages in Light/Dark, actual owned DNS answers, TCP-only UI, existing Ping/Trace/TCP lifecycle/updates and no data-binding errors.
- First WPF attempt is preserved under `windows-qa/`: its broad assertion counted internal DataGrid buttons as Calculate actions. The assertion was narrowed to actual Calculate actions and the final checks passed. Images are actual WPF renders, not physical monitor/DPI/manual acceptance.
- Actual MAC API request: PASS (`mac-online/mac-api-result.json`). Public example `FC-A1-3E-2A-1C-33` from the provider documentation returned `Samsung Electronics Co.,Ltd`. No machine/user-inventory MAC was collected for this check. Provider data is manufacturer registration, not device identity proof.
- Actual executable startup/graceful close: `native-verified.log`, isolated QA data, exit 0. This framework-dependent development build uses the local .NET SDK/runtime; no self-contained release package or installer was verified in this task.
- Human/physical monitor DPI/accessibility, remote DNS/network environments, clean-machine installer/upgrade/recovery and long soak: NOT TESTED in this task. URL provider integration is explicitly pending; it is not marked implemented or passed.

## Run the local build

From the repository root:

```powershell
. .\artifacts\dev-setup\Enter-DevEnvironment.ps1
& .\artifacts\tools-rework\build\bin\NetStucked.Desktop\release\NetStucked.exe
```

Regular launches use the normal per-user preferences; automated QA uses isolated data. The build directory must remain together.

## Remaining acceptance

- [x] Approved local tool/menu scope implemented, with URL access pending explicitly separated.
- [x] Single-field subnet parsing, automatic updates and inline validation tested.
- [x] Current source build, unit/adapter, WPF and startup checks recorded.
- [ ] Human acceptance of the actual new screens.
- [ ] VirusTotal account/key and supported Palo Alto API provisioned, then integration implemented/tested.
- [ ] Clean Windows installer/recovery and physical DPI/accessibility acceptance.

This slice does not close the full M02/M07/M08 milestones. The later human clarification authorizes the 0.7.0 release; publication does not imply completion of the outstanding provider/manual gates.
