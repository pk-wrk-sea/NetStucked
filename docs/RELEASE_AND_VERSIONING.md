# Release and versioning register

`Directory.Build.props` is authoritative and currently says **0.4.0**. Assembly/file/informational metadata, generated Windows manifest and Inno version derive from it. Use SemVer numeric ordering and `vX.Y.Z` tags. Proposed milestone versions do not change application metadata. Keep changes in CHANGELOG and bundled release history factual; documentation-only work uses an Unreleased documentation entry, not a fabricated release.

## Public release state

- [0.4.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.4.0): Latest; tag `eb5bb0f880d1f9dc2ab4173c935e53cd92d0b22a`; exact installer/portable hashes, source and Windows CI in [publication report](GITHUB_RELEASE_0.4_REPORT_2026-10-09.md).
- [0.3.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.3.0): selected-build update/recovery baseline.
- [0.3.1 TEST BUILD](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.3.1): version/docs-only test package; stable SemVer with explicit test label, not Latest. Current checker excludes prereleases.
- 0.2.x releases/downloads and six old Actions artifacts: retired under explicit user instruction after verified local backups. Keep source tags/history. Do not recreate or rewrite them as part of planning.

The update catalog begins at 0.3.0. Published tags/assets are immutable except the specific already-executed 0.2.x retirement authorization. Publication is not evidence of stable acceptance. Installers remain unsigned; machine install/upgrade/recovery/uninstall and Windows 11 acceptance are NOT TESTED.

## Packaging and release gates

Use [RELEASE.md](RELEASE.md) for the operational package recipe, [VERSIONING.md](VERSIONING.md) for version rules and [M07](milestones/M07_UPDATES_RECOVERY.md)/[M08](milestones/M08_RELEASE_HARDENING.md)/[M09](milestones/M09_STABLE_RELEASE.md) for remaining acceptance gates. Do not duplicate or overwrite frozen artifacts. Keep stable Inno AppId `{82CA4B18-541C-4D04-9A09-E676AB21F504}`, x64 full runtime payload and per-user data outside Program Files.

Restore/build/test, publish into a fresh isolated folder, verify exact payload and native executable, compile Inno, and record hashes/trust/source commit. For an authorized GitHub release, create an immutable source/tag and draft, attach the tested portable ZIP/hash/manifest, run the Windows installer workflow against those bytes, verify uploaded/downloaded assets, then publish only within explicit release authority. No commit/push/tag/release permission is granted by this planning task.

Publisher Authenticode signing/reputation is a remaining production gate and must use an authorized certificate/signing service; no borrowed private keys, fabricated signatures or embedded credentials. SQLite or any future settings-schema change requires migration and real downgrade tests before declaring recovery compatible. Preserve failed job/backup evidence; installer failure is not transactional recovery.
