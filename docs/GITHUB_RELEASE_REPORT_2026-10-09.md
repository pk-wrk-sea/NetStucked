# GitHub publication and manual update test — 2026-10-09

Published with explicit human authorization to `pk-wrk-sea/NetStucked`. Application installation and recovery remain manual. Source, tags, portable packages and installers are available on GitHub; no automated updater was added.

## Published versions

| Version | Role | Source commit | Release |
|---|---|---|---|
| 0.2.0 | Regular release; GitHub Latest; `main` stays at this application version | `2d584477f4c3ff3ac4446390c03bc365769a8860` | [v0.2.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.2.0) |
| 0.2.1 | TEST BUILD for manual update/recovery; branch `release-test/0.2.1`; not GitHub Latest | `cf6052a3a6703de1df7ae7e7c851c104a59da799` | [v0.2.1](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.2.1) |

0.2.1 changes version metadata, bundled release history, README and CHANGELOG. Diagnostic behavior and settings schemas are unchanged. It is intentionally a normal SemVer release, explicitly labeled TEST BUILD rather than a GitHub prerelease: the existing 0.2.0 checker excludes prereleases. GitHub's `/releases/latest` was verified to return v0.2.0 after both publications. The application sorts eligible releases by SemVer and therefore offers 0.2.1 to 0.2.0 users.

Annotated version tags remain at these commits. Subsequent `main` changes correct SDK build-metadata verification and record additional QA/documentation; they do not replace release tags or artifacts. Commits/tags and installers are unsigned. The unrelated machine-wide Git signing key was not used.

## Distributed bytes

Each release has a self-contained Windows x64 portable ZIP, an Inno Setup installer, checksum files, `package-manifest.json` and `installer-build.json`.

| File | Bytes | SHA-256 |
|---|---:|---|
| NetStucked-0.2.0-win-x64-portable.zip | 63,615,283 | `b17ee7e1545346839f8eed9a422b40e3ae64b4fb7e193a97ff1564019d9a1771` |
| NetStucked-0.2.0-win-x64-setup.exe | 46,835,743 | `8aa3442b8b6b05cbe861a11366aa956cef8078e87b63572b2a9d26715810d661` |
| NetStucked-0.2.1-win-x64-portable.zip | 63,616,589 | `5ad73f400a56c539428cc6003f5929203b4ab5eb7e5f222f5411a67e9e008625` |
| NetStucked-0.2.1-win-x64-setup.exe | 46,848,661 | `346d0a26fb0620ad8da4d86a9411f8df22ece91a594586059139538f08791ca7` |

The installer workflow downloads the tested ZIP from its draft, verifies its hash and executable version, checks its exact application DLLs, and compiles that same payload using the runner's Inno Setup compiler. No fresh untested publish directory is substituted. Uploaded GitHub asset digests match local package hashes. Both installers were downloaded after compilation and checked against GitHub's digest, the checksum file and `installer-build.json` before publishing. Package manifests describe the portable package validation stage; the separate installer build record and CI run establish the later installer compilation.

## Verification

| Check | Result / evidence |
|---|---|
| Local Release builds | PASS, both versions, zero warnings/errors |
| Local automated tests | PASS, **115 passed, 0 failed/skipped** per version; `artifacts/github-release/0.2.0/test-results/pre-publish.trx` and `artifacts/github-release/0.2.1/final/test-results/tests.trx` |
| GitHub Windows source build/test, frozen package verification, WPF checks, self-contained executable smoke and installer compilation | PASS, [0.2.0 installer run](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37876770501) and [0.2.1 installer run](https://github.com/pk-wrk-sea/NetStucked/actions/runs/37877608110) |
| Exact published application code | PASS, SHA-256 checks of the three loaded application DLLs against each published directory; real loopback ICMP/TCP, DNS, lifecycle/cancellation, UI bindings, templates/history and independent trace sessions |
| Real public release check in 0.2.0 | PASS, discovers v0.2.1, displays actual TEST BUILD notes and Patch update badge, enables the manual installer-page button; recorded release/installer page is the project's v0.2.1 page |
| Real public release check in 0.2.1 | PASS, reports no newer published version, disables the available-install action and records documented v0.2.0 as the recovery version |
| Recovery menu | PASS, real WPF command opens manual instructions in both builds. No installer or rollback process is executed by the QA harness |
| Self-contained executable | PASS, both exact published executables create an actual WPF HWND and close with exit 0 using isolated QA preferences |
| Versioned installer foundation | PASS compilation; stable AppId `{82CA4B18-541C-4D04-9A09-E676AB21F504}` retained; exact package/version/hash recorded |
| Canonical scope/design bytes | PASS, unchanged SHA-256 values below; `.gitattributes` preserves exact bytes |
| Actual installer install/upgrade/uninstall/downgrade and settings restoration | **NOT TESTED**; neither installer was executed by this task |
| Publisher signing/trust, SmartScreen behavior | **NOT TESTED**; packages, commits and tags are unsigned |
| Windows 11 / clean VM / remote multi-hop networking / human multi-monitor and accessibility acceptance | **NOT TESTED**; local native checks used Windows 10 and owned loopback targets |

Live menu checks use an extended test-only Windows harness with expected release/current-version arguments. It loads the original published application DLLs; no production release source is replaced. Test fixtures separately cover error/cancellation behavior. Real API evidence and actual WPF renders are under:

- `artifacts/github-release/live-menu-0.2.0/output/live-release-check.json`, `github-check.txt`, `Updates.png` and `Updates-recovery.png`; transcript `artifacts/github-release/live-menu-0.2.0/windows-qa.log`.
- `artifacts/github-release/live-menu-0.2.1/output/live-release-check.json`, `github-check.txt`, `Updates.png` and `Updates-recovery.png`; transcript `artifacts/github-release/live-menu-0.2.1/windows-qa.log`.
- Downloaded installers/build records and release snapshots: `artifacts/github-release/0.2.0/downloads` and `artifacts/github-release/0.2.1/final/downloads`.

Artifacts are ignored locally. GitHub CI retains installer/test/WPF evidence as run artifacts. The [original 0.2.0 QA report](QA_0.2.0.md) retains the 60.2-second simultaneous real-network soak and its measured performance limits. Those earlier results are not presented as a new soak of 0.2.1.

## Manual user test

1. Run 0.2.0. Open Updates & Recovery and choose Check for Updates; v0.2.1 should appear as a patch update with TEST BUILD notes.
2. Choose Download installer to open the real GitHub release page. Verify the downloaded file's SHA-256. Install manually, or extract the portable ZIP into a separate folder for a version/menu test.
3. Run 0.2.1 and check again: no newer published version is expected. Open Rollback guide; documented previous version is v0.2.0. Use Open published versions to obtain the v0.2.0 package.
4. For an actual machine downgrade, stop diagnostic sessions, close NetStucked and back up `%LOCALAPPDATA%\NetStucked`. Uninstall the current version and install the previous package manually, restoring compatible preferences if required. A settings backup belongs to the user and is never created or restored automatically.

Using separate portable folders verifies version switching without exercising Inno Setup upgrade/uninstall behavior. The 0.1.0 entry is historical application documentation, not proof of a published or previously installed version; this task published only 0.2.0 and 0.2.1.

## Canonical hashes

- `docs/APPROVED_UI_SCOPE.md`: `0f722da815555532a330e1ae9c5121778aa184cc740c94da82cb7e65d3003776`
- `design/references/LivePing_APPROVED.png`: `45f941795fa7347a824976b79f73cd61f029778f7afecc246df302e181d55772`
- `design/references/Traceroute_APPROVED.png`: `e8f8f3ad08d9d9ca844bc2f2f3326910df74583afe52dbc4730c19085f2cb2f1`
