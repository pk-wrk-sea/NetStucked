# 0.12.0 QA

Scope: [approved features](FEATURES_0.12.0.md). Isolated state and owned loopback only. User-installed process/data are not modified.

## Current development evidence

- Release solution build: PASS, zero warnings/errors.
- First inherited regression run: 508 PASS / 4 FAIL. Failures exposed renamed date columns, a previous whole-settings ban on username property names, and new filename collision/storage expectations. Date tests now use the schema-2 columns; Wi-Fi's no-username policy is tested on Wi-Fi metadata while explicit Collector usernames are authorized. Capture collision handling was corrected; JSON failure still retains TXT. Raw failing TRX is retained.
- Pre-cancellation-fix source unit run: 518 PASS / 0 failed or skipped, including legacy schema/JSON, nullable component rows, unique-serial review and missing-column preservation. Frozen-payload checks are recorded below when run.
- Actual bundled Netmiko + production controller/capture: five owned software profiles pass, as do trust-before-auth, saved/changed keys, password-only AUTH2, MFA exclusion, partial capture, cancellation during KEX/command wait and mixed-profile parallel collection. The first parallel counter check exposed a fixture which did not observe disconnected sockets during a deliberate wait; its cleanup was corrected, and the complete rerun passes. No production capacity threshold was relaxed. A subsequent Telnet guard test exposed consecutive login prompts without an echoed newline; the prompt recognizer now resets its tail after each credential send while keeping one username/password send per attempt. The failed raw log is retained; the corrected complete 40-assertion rerun passes, including Telnet, 8,192 Unicode lines, output limits and disconnect-without-replay.
- Actual WPF (14 assertions): Start/Show password/Save usernames, first-use modal key approval before auth, two-device real worker collection, optional component save, import/edit/update without duplicate rows, actual template/export, image columns, Dark switch, Terminal absence and binding checks pass. Light/Dark and narrow-window captures are real WPF renders, not physical DPI/native Computer Use acceptance.
- Complete existing WPF regression: PASS; integrated engineering WPF: 46 assertions PASS. Final exact-payload reruns are separate below.

Development evidence: `artifacts/collector-inventory-20261022/`, including failed/successful TRX, worker audit/captures and WPF images. This document does not turn source or fixture checks into published/frozen evidence.

## Required final package checks

The first source/PR CI pair failed the cancellation status assertion after about three seconds. Audit found a worker-exit race: after cancellation kills the process, EOF or a structured transport failure can arrive instead of an OperationCanceledException. Cancellation now wins over concurrent worker errors and forbids AUTH2, while successful completed results are retained. Two deterministic controller regressions cover transport-error and password-rejection races. The initial immutable `final-1` package and failed CI logs remain preserved; corrected final artifacts use a fresh directory. No cancellation threshold was relaxed.

- [x] Final build, 520 unit tests and exact-payload checks pass.
- [x] Portable/installer manifests, identities, trust status, pinned runtime and licenses verified.
- [x] Source/PR/main/exact-package CI and real public download/updater/staging checks pass.

## Open acceptance

NOT TESTED: physical Cisco IOS/IOS-XE/IOS XR/NX-OS, Huawei firmware/AAA/enable commands, actual device incident resolution, 50-device network capacity/soak, physical/mixed DPI/accessibility and Setup/install/uninstall. No probes are sent to the working network. Full historical A02/A03/stable DoD remains open.

## Published verification

0.12.0 is GitHub Latest. [Immutable source/assets, CI, exact counts and public updater evidence](GITHUB_RELEASE_0.12.0_REPORT_2026-10-22.md). Final artifacts: `artifacts/github-release/0.12.0/final-2/`. Physical/manual/Setup and broad historical acceptance remain open.
