# Authorized defect follow-up — 2026-10-09

The human reported defects in 0.4.0 and authorized these follow-up changes, including TCP payload transmission in the Packet Size clarification. These revisions supersede the affected 0.4.0 checkbox, empty-UDP and TCP-connect-only behavior. Original scope/PNG files and published releases remain preserved.

- Remove the redundant NetStucked/page-tab labels from the caption. Retain the existing drag area and minimize/maximize/close controls.
- Give every text input a theme-aware caret, including editable dropdowns, popup fields and the editable Traceroute Description cell. Session titles and containers explicitly follow the theme text color.
- Allocate the full Save button width plus an eight-pixel gap before Probe Settings in Live Ping and Port Test. Clip selected template content to its own dropdown bounds.
- Place the single shared Hop Description button directly before All status in the Traceroute Results toolbar. Keep the shared command and mappings across all sessions.
- Display stable Traceroute lifecycle state such as Running; omit the rapidly changing probe outcome after it. Errors remain visible and actual outcomes remain in results/events.
- Port Test always accepts multiple Host/IP/CIDR targets and runs continuously after Start. Remove both mode checkboxes, retain Pause/Stop, and ignore earlier false mode preferences. The Core single-pass API remains available to tests; the application exposes continuous behavior.
- Accept inclusive comma-separated port ranges, such as `22,443,1000-1005`. Deduplicate numbers, reject reversed/out-of-range/malformed ranges and enforce the existing 64 unique-port and 16,384-check limits before a probe starts. Saved port templates and the editor share the same parsing.
- Add Packet Size to Port Probe Settings: **0–1400 payload bytes, default 32**. Both TCP and UDP send zero-filled data. TCP completes a real connection then sends the entire payload, handling partial sends under the same timeout/cancellation deadline. Size zero tests TCP connection only or sends an empty UDP datagram. The setting describes payload bytes; the operating system controls TCP segmentation and IP/TCP/UDP headers.
- TCP timing remains the measured connection duration. Successful send does not assert an application-level response. A send failure after connection is labeled explicitly; it does not claim that the port was refused. UDP measures an actual received response; silence remains inconclusive. Actual sent-byte counts appear in result details and CSV.

Large-scope acknowledgement, host/port/check/concurrency/rate limits, diagnostic shutdown, settings compatibility and verified explicit selected-build installation/recovery remain in force. No machine installation is part of this task. GitHub packaging follows the existing session publication instruction and uses a new 0.4.1 tag/assets; it does not replace 0.4.0.
