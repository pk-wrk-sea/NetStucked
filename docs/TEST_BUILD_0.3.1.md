# NetStucked 0.3.1 TEST BUILD

Purpose: exercise the actual selected-build Upgrade/Recovery menu beginning at 0.3.0. Changes from 0.3.0 are central version metadata, bundled release history and explanatory docs only. ICMP/TCP engines, updater execution and schema-1 settings are unchanged. This is normal SemVer 0.3.1, explicitly TEST BUILD and not Latest; prereleases are excluded by the checker.

1. Install or extract 0.3.0 once and open Updates & Recovery.
2. Check GitHub, select 0.3.1 TEST BUILD and review its notes. Current installers are unsigned, so acknowledge that explicitly if you choose to use them.
3. Click Install v0.3.1. The app verifies the installer, drains diagnostics, saves preferences, stages a helper, closes, asks Windows permission and installs/restarts. Windows UAC/SmartScreen may still request confirmation.
4. In 0.3.1, check GitHub and select 0.3.0. Click Recover v0.3.0. A matching verified settings backup is restored when available; otherwise compatible current schema-1 settings remain.
5. Inspect the actual app version and saved templates/preferences after each step. Retain installer logs/backups under `%LOCALAPPDATA%\NetStucked\updates` if a step fails. Avoid running another instance from the installation directory during replacement.

Do not infer successful installation from a menu/button, published package, unit test or installer compilation. The release task tests build/source, actual WPF/network diagnostics, release selection, file integrity/Windows trust and helper acknowledgement. Full machine upgrade/downgrade and interactive UAC/SmartScreen remain NOT TESTED until performed by a human. No automatic background checks/installations occur.
