# GitHub release and manual upgrade/recovery exercise

Authorized by the user on 2026-10-09: publish the latest 0.2.0 version and create a real test version to exercise Updates & Recovery.

## Versions

- `v0.2.0`: main release, the existing tested TCP Port Test/Updates build. Its original portable package remains frozen and is the installer payload.
- `v0.2.1`: explicitly titled **TEST BUILD — update/recovery exercise**, on a separate release-test branch. It changes version metadata and recorded notes only; there are no new diagnostic features. It is a normal SemVer release so the existing 0.2.0 checker can detect it. GitHub's Latest designation stays on v0.2.0. A GitHub prerelease would be hidden by the current stable-only checker.

The user requested a manual workflow. NetStucked does not download, execute installers, back up settings or perform rollback itself. Release links remain confined to this repository. Test fixtures are not published as network results.

## Build and publication

1. Commit/push the reviewed source and immutable version tag. Prepare a draft GitHub release containing the locally tested portable ZIP, hash and QA/package evidence.
2. Manually dispatch `.github/workflows/release.yml` with the exact tag and SHA-256 of that ZIP. The workflow validates tag/source version and draft state, builds/tests source, verifies the ZIP bytes and executable version, runs Windows QA against its exact application DLLs, then compiles an Inno Setup installer from that same payload.
3. Inspect the successful workflow and verify uploaded asset hashes before publishing the draft. No signing key is available: installers are explicitly unsigned. Compilation does not establish successful installation or rollback.

The workflow runs on the GitHub Windows 2025 image with its existing Inno Setup compiler; it does not install a compiler on this PC. Stable AppId and per-user preferences are preserved. No binaries, credentials or QA user data are committed to Git.

## Try it

1. Run/install **0.2.0**. Open Updates → Check for Updates. Expect **Version 0.2.1 available**, **Patch update**, real test-build release notes and **Download installer** once the installer asset is published.
2. Open its GitHub page, download the 0.2.1 installer and compare SHA-256 with its attached `.sha256`. Stop diagnostics, close NetStucked and back up `%LOCALAPPDATA%\NetStucked` before installing manually.
3. Open 0.2.1 and confirm the Current Version and Version History. Checking again should find no newer published version.
4. Open Version Recovery → Rollback guide → published versions. Close the app, back up preferences, uninstall 0.2.1 and manually install 0.2.0. Restore a compatible preferences backup if needed. Confirm Current Version 0.2.0 and retained templates/column preferences.

Portable alternative: extract the two ZIPs to separate directories and run one version at a time. This exercises version detection and the release/recovery links without claiming installer/registry upgrade testing. Both use the same per-user preferences, so retain a backup before switching.

Human install/uninstall/upgrade/downgrade and signature trust remain separate checks. The exact executed publication results and real API observations are recorded locally under ignored `artifacts/github-release`.
