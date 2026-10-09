# Authorized UI and diagnostic extension — 2026-10-09

The human reviewed the interactive mockup and authorized implementation with the final Traceroute and popup refinements. These changes supersede older light-only, fixed-panel, eight-session and TCP-only input constraints. The original approved scope and PNG bytes remain canonical historical references.

- Use the supplied logo unchanged as the brand source; display its mark in the application/sidebar and executable icon, a compact wordmark in the sidebar/settings, and full artwork in installer branding.
- Light, Dark and Windows-following System themes apply to application controls, popup editors, column chooser, menus and validation. Native file dialogs and Windows trust/elevation prompts remain platform-managed.
- The main sidebar, Ping/Port address panel, Trace event panel and Ping/Port histories have accessible toggle icons and a short smooth transition. Address/event panels resize horizontally; histories resize vertically.
- Ping's editor is titled `IP Addresses & Templates`; input guides and semantic status colors apply throughout.
- Hop Description is a multiline textbox: `10.10.10.1 CSW-HQ`, one mapping per line, applied to all trace sessions. Manual mappings take priority over RIPEstat public WAN prefix/ASN organization metadata. Private/reserved IPs never go to that API. Cache size and outstanding work are bounded; unavailable metadata does not change probe results.
- One main trace session cannot be removed. Added sessions appear before the adjacent Add new session card; no sequence title is required. A new session must contain a valid Host/IP before another can be added. Maximum five sessions including the main session.
- Port Test uses separate Host/IP/CIDR input, TCP/UDP selection and comma-separated ports. A checkbox selects continuous polling; unchecked means one pass. Multiple IP Scan is an explicit checkbox.
- Generic UDP sends an empty datagram and records Responded, Closed (network-stack port-unreachable), No response, Unreachable or Error. Silence is inconclusive. Only actual responses have response timing.
- Common-port templates based on the [IANA service-name and port-number registry](https://www.iana.org/assignments/service-names-port-numbers/service-names-port-numbers.xhtml) can be edited, saved and restored to built-in values. These templates do not assert that a service is actually running on a port.
- Port scope: maximum 1,024 expanded hosts (supports /22 usable hosts), 64 selected ports and 16,384 endpoint checks. More than 4,096 checks requires an explicit checkbox acknowledgement for the current target/port/protocol scope. Concurrency stays 32 with 128 send admissions/second. No probe starts from merely editing, loading, expanding or previewing a scope.
- Dashboard and Network Info remain placeholders. Selected-build installation/recovery retains its existing explicit-click, integrity/trust and shutdown requirements. Machine installation is outside this task.

The later human instruction on 2026-10-09, “เอาขึ้น github”, authorizes committing/pushing this extension and publishing the verified 0.4.0 packages on GitHub. Earlier releases/tags/assets remain immutable. Publication rebuilds committed source and checks the exact frozen package before release.
