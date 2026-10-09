# 0.4.2 diagnostics stability and table usability

Human authorization: 2026-10-09. Investigate Live Ping load at 250 ms interval / 500 ms timeout, protect concurrent Traceroute, allow table text selection/copy, add actual Ping reply addresses/history host, retain Port Test column configuration, and add optional responding-hop TCP checks. Review and publish each completed fix/feature batch to GitHub, with an unchecked human acceptance checklist. Wi-Fi work in the original checkout remains separate and unreleased.

## ICMP load and health

The 0.4.1 desktop forced 64 Ping workers / 2,048 send admissions per second. A /24 requests 1,016 sends/s at 250 ms; /27 requests 120/s. Separate trace engines shared 128 native slots without a reserved class budget. This establishes a load/capacity issue in the application; it does not establish why a particular user's network returned loss. Routers can independently police ICMP, and endpoint/firewall behavior still affects actual replies.

0.4.2 caps desktop Ping at 32 workers and 128 paced native sends/s. Traceroute reserves 32 separate native slots and a separate 128/s aggregate budget across its five sessions. No catch-up bursts; cancellation returns slots. Native timeout and RTT start after admission; queue delay is never presented as ICMP RTT/loss. Core callers retain their own validated scheduler settings, with the production adapter enforcing these aggregate ceilings. Large scopes can run slower than their requested interval. The target preview shows requested rate, the cap and approximate effective interval; 254 targets need about two seconds per host at full capacity, potentially longer under timeout/OS pressure.

Warn explains the current RTT threshold, consecutive missing replies, or loss within the last 20 completed attempts. Twenty successful replies remove a historical-loss warning. Cumulative Sent/Received/Lost/Loss % remain unchanged; cancellation and DNS errors do not count as ICMP loss. Unreachable continues to mean three consecutive missing ICMP responses, not proof of service outage.

## Measured addresses and copying

Live Ping results include Reply IP Address; selected-target history includes Host and Reply IP Address. Values come from the actual ICMP responder, including terminal errors; absent responses display no reply IP. Requested/resolved IPs are not substituted. CSV results include reply addresses.

Cells and column labels use selectable readonly text with a caret in Light/Dark/System. Drag within a cell/label and use native Copy. Right-click a table to copy the selected row/table with visible headers, or open a themed text view for selection across columns and rows. Copy respects visible column order and uses tab-separated text. Text views/copy are bounded to 4,096 rows / about 1 MB; use CSV for complete results. Traceroute Description remains editable by double-click or F2, and column sorting remains available. Port Test's existing column chooser applies checkbox visibility, order and widths and saves those preferences.

## Optional responding-hop TCP checks

Per-session Probe Settings offers a disabled-by-default checkbox and a textbox with `22,23,80,443`. Accept 1–16 unique valid ports, custom values and inclusive ranges. This directly connects to the current responding hop IP; ICMP still discovers the route. It is not TCP TTL traceroute. The TCP open ports column shows only completed successful connections, such as `22,443,8088`; its tooltip records state and actual checked time. Empty results do not establish that every service is absent. CSV carries open ports, check state and timestamp.

No payload is sent by hop TCP checks. Each session uses two independent TCP workers, a queue of 64 IPs and an 8/s budget. Matching hop IPs share a per-session result, cached for 30 seconds; refresh can take longer under a large scope or slow ports. Cache is bounded to 512 IPs. TCP does not hold the next ICMP poll. Pause/Stop/disposal await cancellation; a replaced/missing hop never displays another IP's result. Core one-shot traces finish queued optional checks before ending. Existing Port Test payload behavior is unchanged.

## Human acceptance checklist

- [ ] Run /27 with interval 250 ms / timeout 500 ms alongside Traceroute; inspect actual RTT/loss and any Warn reason.
- [ ] Run /24 with the same settings; confirm rate/effective-interval preview, responsive navigation and continued trace progress.
- [ ] Pause/resume/stop both diagnostics; confirm no continued probes after Stop.
- [ ] Confirm Reply IP Address in Live Ping and Host / Reply IP Address in selected-target history; absent replies must stay blank/dash.
- [ ] Drag-select/copy cells and column headers in Ping results/history, Trace results/events and Port results/history.
- [ ] Right-click and select/copy table text across headers/rows; test Dark theme, sorting and editable hop Description.
- [ ] Hide/reorder/resize Port Test columns, leave/return/restart and confirm preferences remain.
- [ ] Enable optional hop TCP checks with `22,23,80,443` and a known custom listening port; check actual successful port numbers and tooltip time. Disable it in another session and confirm independence.

Original scope, approved screenshots/logo and existing published versions/assets remain immutable. Remote network behavior and full human acceptance require the user's program checks; machine installation is not performed by this change.
