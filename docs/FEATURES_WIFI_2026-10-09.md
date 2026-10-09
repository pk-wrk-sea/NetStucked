# Approved Network Info extension — 2026-10-09

The human explicitly authorized implementing Wi-Fi Profile Manager inside Network Info using the supplied reference, with a compact profiles table replacing individual Wi-Fi cards. Existing Light/Dark/System resources and navigation remain authoritative. This supersedes the previous placeholder/pending status for this slice only. Canonical Live Ping/Traceroute references remain unchanged.

Scope: Windows WLAN profiles and adapters, explicit scan/connect/disconnect, descriptions, safe profile import/export, Personal profile creation, Windows-managed Enterprise/EAP/certificate authentication, connection/IP/gateway/DNS details, bounded history and network transition events, and configured Gateway/DNS/TCP/HTTP Connect & Test. No sample profiles or manufactured results. No silent Wi-Fi switching, portal authentication, certificate/private-key export, blanket EAP trust changes, or background installation.

Company Enterprise profiles must be provisioned by Windows/IT or imported from approved WLAN XML; credentials can be supplied for supported PEAP-MSCHAPv2 profiles through Windows EAP APIs. TLS credentials/certificates remain managed by Windows. Secrets are excluded from local preferences, logs and exports. Exported Personal profiles require re-entering their key on import.

Implementation authorization does not authorize real connection changes during development, a version bump, commit, push or GitHub release. Real adapter read-only smoke and owned-loopback tests are allowed. Hardware/enterprise/captive portal acceptance must be reported separately.
