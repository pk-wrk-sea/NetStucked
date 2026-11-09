# 0.13.0 release verification — 2026-11-09

Human explicitly authorized release after the local review implementation. Version 0.13.0; [scope](FEATURES_0.13.0.md), [implementation QA](QA_CVE_IPS_2026-11-09.md). Evidence is accumulated under `artifacts/github-release/0.13.0/` in fresh directories. Prior assets/tags and installed profile/process remain unchanged. No Setup execution or firewall/device configuration.

## Required checks

Release build, all unit tests, exact current-menu/CVE WPF tests, native startup/retired Monitoring/instance restoration, owned Collector/SSH/Netmiko tests, frozen Portable/Installer bytes, GitHub Windows source/PR/main/installer CI, public downloads and baseline/current updater/helper staging are recorded from completed results in the publication report. Initial state: verification in progress; no release success is inferred from a version bump.

The workflow retains historical checks for older versions while 0.13.0 validates CVE Checker | IPS and rejects retired routes. Dormant legacy Core/data contracts remain covered by their unit/regression fixtures. Native 0.13.0 checks require **zero** connections from saved legacy monitors and unchanged monitor-file bytes, plus profile-instance restoration.

## Separate limits

Threat Vault API/permissions/subscription with a real key and real SmartConsole export completeness: NOT TESTED. Physical DPI/mixed monitors, accessibility, real vendor/AAA, large production/long-soak capacity and clean-machine Setup/upgrade/recovery/uninstall: NOT TESTED. Test fixtures never establish deployed IPS enforcement, applicability or exploit outcomes. Installer is expected unsigned; trust/hash/size checks and explicit acknowledgement remain mandatory.
