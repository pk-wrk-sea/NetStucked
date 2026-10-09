# 0.6.0 — DNS / HTTP diagnostics and grouped navigation

Human authorization 2026-10-09: fix Dark version labels and taskbar branding, group sidebar menus using the supplied outline-icon reference, then implement DNS Test and HTTP / HTTPS Test and release 0.6.0. The earlier standing instruction authorizes review/testing and GitHub publication after each completed feature/defect task. This supersedes the pending M02 UI/implementation gate for these two tools and their navigation only.

Wi-Fi remains unchanged. Broader LAN/VPN/routes/ARP and durable History/Reports were explicitly declined; they are excluded from this release and navigation. Individual diagnostic result export remains available.

## Behavior

- Workspace: Dashboard, Live Ping, Traceroute, Diagnostics (Port Test / DNS Test / HTTP / HTTPS Test), Network Info (existing Wi-Fi).
- Network Operations: Monitoring, Terminal, Config Collector, Topology Explorer. Asset & Security: MA Inventory, CVE & Upgrade Planner. Unimplemented pages display **Pending - Coming Soon** with no invented telemetry. Settings and Updates remain at the bottom under Data & Application. Supplied Lucide-style outline icons use licensed embedded Lucide 0.468.0 SVGs rendered as WPF geometry; no runtime web dependency.
- Version History headers explicitly follow the shared TextBrush. A stable application identity and both native window icon sizes use the supplied NetStucked icon; shortcuts specify the same application identity and EXE icon. Existing Windows-pinned shortcuts may retain their own cache until re-pinned; no user pins/cache are changed by the application.
- DNS Test: up to 128 names, four resolver IPs and selected A/AAAA/CNAME/MX/TXT/PTR/SRV types, capped at 512 combinations per run. PTR converts literal IPv4/IPv6 addresses to reverse names. Default resolver addresses are read from active adapters (up to four); queries are sent directly to those addresses, not Windows NRPT/DoH/search-suffix policy. Custom IP:port / [IPv6]:port is supported. Actual response query identity/type/class is verified. UDP truncation falls back to TCP under one deadline; response records, sections, TTL, resolver, status and measured completion time are displayed. Negative DNS responses are separate from transport errors/timeouts. At most 128 records / 65,535 wire bytes per response.
- HTTP / HTTPS Test: up to 64 explicit HTTP(S) URLs; GET or HEAD, expected status 100–599, optional follow redirects (maximum five; loops and HTTPS downgrade blocked), total deadline 250–60,000 ms. Direct connections bypass system proxies. Only response headers are inspected; no response bodies or cookies/authentication are consumed/sent. Actual status, time through final response headers (including redirects), remote IP, redirect chain, negotiated TLS/cipher, certificate validity/chain result and bounded details are shown. Normal certificate validation stays enabled; invalid certificates fail. No response means no invented completion time.
- Both tools run bounded cancellable work at selected concurrency 1/4/8/16, survive navigation, support copyable cells/headers, sortable/resizable/configurable columns, CSV, smooth collapsible/resizable input/details panels, and await cancellation during shutdown/update preparation. Inputs and results are transient; column preferences use existing schema-1 settings. URL userinfo is rejected; query strings are omitted from displayed/exported URLs and sensitive response headers are excluded.

## Human acceptance checklist

- [ ] In Dark theme, Version History labels including v0.5.0 and current v0.6.0 are readable.
- [ ] Running app shows the NetStucked logo in Taskbar and Alt-Tab; new shortcuts show it.
- [ ] Sidebar groups/icons match the reference; History/Reports are absent; pending menus show Coming Soon.
- [ ] Wi-Fi screen and existing Ping/Trace/Port behavior remain usable.
- [ ] DNS: select multiple record types/resolvers, verify answers/TTL/sections/timing; test PTR, NXDOMAIN, NODATA and timeout.
- [ ] HTTP: test GET/HEAD, expected status, redirects on/off, error and timeout.
- [ ] HTTPS: inspect valid certificate/TLS information and verify invalid certificates fail.
- [ ] Select/copy result cells and headers; configure columns, resize/collapse panels and export CSV.
- [ ] Navigate while tests run; Stop and close/update preparation complete without leaving active requests.

Build/unit/owned-loopback/native-render results are recorded separately in QA_0.6.0.md. Human acceptance, remote enterprise DNS/proxy/TLS matrices, full installation/update/uninstall and Windows 11 tests must not be implied by local checks.

Primary implementation references: [DNS wire format](https://www.rfc-editor.org/rfc/rfc1035.html), [.NET TLS authentication](https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream?view=net-10.0), [HTTP TLS options](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.ssloptions?view=net-10.0).
