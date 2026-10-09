# Network Info — Wi-Fi Profile Manager

Approved 2026-10-09: [scope](FEATURES_WIFI_2026-10-09.md), `design/references/WiFiProfiles_APPROVED_2026-10-09.png`. The human explicitly replaces individual profile cards with a compact table. Light is the baseline; shared Dark/System themes remain supported. Screenshot SSIDs, addresses and timings are examples, never seeded into production.

## Use

1. Open **Network Info**, select the Wireless Adapter. Existing Windows WLAN profiles load asynchronously. No automatic SSID scans or connection changes occur.
2. **Scan SSIDs** explicitly refreshes Windows' available network list; the Available SSIDs tab shows actual measured signal quality (%) and security. Windows Location permission can restrict this data. NetStucked reports the restriction and does not change settings or policies.
3. Select a saved profile and **Connect**. Windows handles authentication. NetStucked waits for the matching completion notification and actual connection state (up to 35 seconds). A profile already connected is verified without reconnecting. **Disconnect** is an explicit action. Cancel drains application work; completed Windows actions are not undone.
4. **+** adds WPA2-Personal, WPA3-Personal or Open profiles for the current Windows user. **Edit** changes nonsecret description/Windows auto-connect mode. **Set Credentials** replaces a Personal key or supplies supported PEAP-MSCHAPv2 user credentials. Reconnect to apply new authentication credentials. Delete asks for confirmation. Company-policy profiles cannot be edited/deleted through these controls.
5. Import one **IT-approved WLAN XML** file at a time. The security method/server trust configuration is preserved, existing profiles are not overwritten, and importing an auto-connect profile is disclosed in confirmation. Export operates on the selected profile and omits all keys/user credentials; exported Personal configuration requires a password on re-import. Do not distribute an original company XML containing secrets.

The compact profile table supports sorting, search, column resize guides, Fit Columns and saved visibility/order/width. Connection details distinguish the selected profile from the actually connected SSID. Missing measurements display `—`.

## Authentication

| Method | Behavior |
|---|---|
| WPA2/WPA3 Personal | Key supplied transiently to Native WLAN; Windows encrypts/stores it. Never retrieve plaintext keys. |
| PEAP-MSCHAPv2 | Existing/imported IT profile required. Current-user credentials go to `WlanSetProfileEapXmlUserData`. Optional additional app copy lives only in Windows Credential Manager. No changes to server certificate validation. |
| EAP-TLS / other Enterprise | Use Windows/IT-provisioned WLAN/EAP configuration and Certificate Store. Windows selects/authenticates with the configured certificate/private key. NetStucked does not install certificates, export keys, synthesize trust rules or promise support for every EAP/policy. |
| Captive portal | WLAN connection alone does not prove service access. HTTP redirects fail the configured status check and explain possible sign-in. NetStucked does not follow redirects or submit portal credentials; use the normal Windows/browser sign-in flow. |

**Forget app copy** removes only the NetStucked Credential Manager entry. Windows may still retain WLAN/EAP credentials. Remove/provision those through Windows/IT. No password, EAP username or profile XML is written to preferences, history, CSV or application logs. Secrets use masked dialog fields; errors never echo submitted XML/passwords.

## Connect & Test

Create a named **Company test profile** using **+** in the right panel. Set Gateway ICMP, a DNS lookup name, a TCP host/port and/or an HTTP(S) URL/expected status. Empty optional endpoints are skipped, never replaced with sample services. Timeout is 500–10000 ms per check; at most four checks run concurrently. A Wi-Fi profile remembers its selected company test profile.

**Connect & Test** connects the selected profile and waits up to 20 seconds for the IP/gateway/DNS required by the configured checks. **Run Tests** tests the already-connected selected profile. IPv4 APIPA and IPv6 link-local alone do not establish service readiness.

- Gateway ICMP uses the selected adapter's source IPv4/IPv6 address through Windows IP Helper; IPv6 link-local gateways retain the adapter scope. Windows ICMP does not expose the actual egress interface; that limitation is explicit in the result. A source-bound echo reply does not prove physical egress in every multi-adapter/weak-host routing setup.
- DNS sends bounded A/AAAA requests to that adapter's observed DNS servers, binding its source/interface, validates transaction/question/answers and supports TCP fallback for truncated replies. It does not silently use global DNS.
- TCP measures actual connection time. DNS time is separate from TCP connect timing.
- HTTP measures actual time to response headers, uses a direct adapter-bound connection **without a proxy**, preserves normal TLS certificate validation and checks the configured status. It downloads no response body, follows no redirects and submits no credentials. Configure a direct-reachable endpoint for enterprise proxy environments.
- PASS requires an actual successful response. FAIL reports the request/check failure and does not establish a general outage. No completed response means no invented RTT.

A route-related context change (profile/SSID/IP/interface/gateway/DNS) during testing cancels and drains the run; stale results are not published. Meaningful Network Transition events also appear in the main shell while diagnostics run and in Network Info's bounded history. The app does not automatically restart Ping/Traceroute on a Wi-Fi switch.

Connection/history retention: 500 events; last 100 completed test runs with their original adapter, SSID, profile, timestamp and measured result details. Select a Connect & Test history row to reopen recorded results. CSV exports distinguish actual results/times. Windows WLAN profiles remain Windows-owned; local schema-1 JSON stores only nonsecret metadata, test endpoints and bounded history.

## Verification limits

Read [QA_WIFI_2026-10-09](QA_WIFI_2026-10-09.md). Owned-loopback probes, fixture-based WPF behavior and Credential Manager checks do not prove real Wi-Fi authentication. This development machine has WLAN AutoConfig stopped; actual SSID scans, Personal/PEAP/EAP-TLS connection changes, policy/hardware/location permutations and captive-portal sign-in require an authorized Wi-Fi lab. Do not enable services or change the working machine's connection merely to run QA.

## API references

Implementation follows Microsoft's [WlanConnect notification requirement](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanconnect), [WLAN profile storage](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlansetprofile), [EAP user credentials](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlansetprofileeapxmluserdata), [PEAP credential schema](https://learn.microsoft.com/en-us/windows/win32/eaphost/peap-ms-chapv2-user-properties), [Wi-Fi location access restrictions](https://learn.microsoft.com/en-us/windows/win32/nativewifi/changes-to-api-behavior-for-wi-fi-access-and-location), [outgoing interface socket option](https://learn.microsoft.com/en-us/windows/win32/winsock/ipproto-ip-socket-options) and [source-bound ICMPv6](https://learn.microsoft.com/en-us/windows/win32/api/icmpapi/nf-icmpapi-icmp6sendecho2).
