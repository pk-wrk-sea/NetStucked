# Test plan

Automated Core tests cover token/description parsing, errors, IPv4 CIDR boundaries /24 /31 /32, deduplication and caps; statistics, cancellations, bounded retention and UTF-8 CSV escaping; 254-host concurrency/no overlap, pause/resume/stop/restart and adapter failures; trace TTL progression, intermediate/success/unreachable/timeout, cycles, jitter, missing routes and ECMP alternates. Windows presentation tests cover commands/selection and actual WPF loading where supported. Live adapter smoke probes loopback only unless explicitly configured.

Manual acceptance (record PASS/FAIL/NOT TESTED in QA_REPORT):

- Live Ping loopback, permitted remote endpoint, nonexistent DNS, mixed invalid input. Expand an authorized /24 to 254 unique rows; run bounded probes for 10 minutes. Sorting/filtering/selected history remain correct.
- Rapid Start/Stop/Start and Pause/Resume; minimize and navigate while active; close during timeout. No outstanding clients remain after Stop.
- Trace loopback, permitted gateway/remote destination, filtered route and repeated cycles; inspect No final reply, log/filter, genuine alternate responses. Do not synthesize production route events.
- Save/load text with Thai descriptions, cancel dialogs, inaccessible paths, CSV escaping and overwrite confirmation. Persist columns and restore after restart.
- Compare both actual WPF pages against reference at 1536x1024 and 1280x800, DPI 100/125/150%; keyboard/focus, screen-reader names and horizontal/vertical scrolling.
- Publish self-contained; compile Inno; clean Windows 10 and 11 install/launch/uninstall; upgrade using stable AppId; user settings retained. Signed production distribution is a separate release decision.
