# Roadmap

## 0.2.0

User-authorized TCP Port Test and manual Updates & Recovery: independent real TCP handshakes, named endpoint templates/history/CSV, manual public GitHub release checks and recorded version notes. Settings/Updates move to the bottom sidebar group. See [the implementation scope](FEATURES_0.2.0.md). No automatic check/download/install/rollback, Dashboard or Network Info implementation.

- 0.1.0: approved Live Ping and continuous ICMP Traceroute, local persistence/export, bounded async execution and Windows installer foundation.
- Future design approval: Dashboard, Network Info and broader preferences. No local-PC health dashboard is assumed.
- Before stable 1.0.0: release signing and clean Windows matrix; if in-app updates are wanted, implement a GitHub Releases client with authenticated publisher/signature verification, SemVer channel policy, staged replacement, backup and recovery from interrupted update. Preserve user settings and stable installer identity. No credentials in the binary.
- IPv6 trace, charts and other network tools require separate scope/validation. SNMP, discovery, topology, TCP/UDP, centralized monitoring and cloud are not part of this implementation.
