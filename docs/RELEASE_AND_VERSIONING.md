# Release and versioning register

Current integration (later human authorization, 2026-10-09): **0.5.0** combines the approved Network Info Wi-Fi slice with published **0.4.2** diagnostics. Version bump/publication were explicitly authorized; **0.5.0 is now GitHub Latest**, with 242 tests, exact WPF/native/soak/package/public checks and both Windows CI workflows PASS. [Publication report](GITHUB_RELEASE_0.5.0_REPORT_2026-10-09.md). See [0.5.0 scope](FEATURES_0.5.0.md) and [combined verification](QA_0.5.0.md); earlier audit/preview version and release statements below are historical snapshots. Wi-Fi hardware authentication and broader M01 routes/neighbors remain open.

`Directory.Build.props` is authoritative and now says **0.4.1** for a separately authorized concurrent defect follow-up. The latest public release verified during this audit's release read is **0.4.0**. Local metadata/notes do not establish 0.4.1 publication. Assembly/file/informational metadata, generated manifest and Inno version derive from the central version. Use numeric SemVer and `vX.Y.Z` tags. Proposed versions do not change metadata; documentation-only work uses an Unreleased entry.

## Public release state

- [0.4.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.4.0): Latest; tag `eb5bb0f880d1f9dc2ab4173c935e53cd92d0b22a`; exact installer/portable hashes, source and Windows CI in [publication report](GITHUB_RELEASE_0.4_REPORT_2026-10-09.md).
- [0.3.0](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.3.0): selected-build update/recovery baseline.
- [0.3.1 TEST BUILD](https://github.com/pk-wrk-sea/NetStucked/releases/tag/v0.3.1): version/docs-only test package; stable SemVer with explicit test label, not Latest. Current checker excludes prereleases.
- 0.2.x releases/downloads and six old Actions artifacts: retired under explicit user instruction after verified local backups. Keep source tags/history. Do not recreate or rewrite them as part of planning.

The update catalog begins at 0.3.0. Published tags/assets are immutable except the specific already-executed 0.2.x retirement authorization. Publication is not evidence of stable acceptance. Installers remain unsigned; machine install/upgrade/recovery/uninstall and Windows 11 acceptance are NOT TESTED.

The 0.4.1 source follow-up is in [FEATURES_0.4.1](FEATURES_0.4.1.md)/[QA_0.4.1](QA_0.4.1.md). This milestone task independently built/tested its captured 103-file snapshot (196 PASS) but performs no public packaging/mutation. A subsequent 0.4.1 tag/asset/CI report must be checked before upgrading this record to Released.

At the final read-only API check of this audit, **0.4.1 is Draft**, while **0.4.0 remains Latest**. Draft assets are preparation, not public update eligibility. The concurrent release task may publish later; its actual publication report supersedes this timestamped snapshot.

## Packaging and release gates

Use [RELEASE.md](RELEASE.md) for the operational package recipe, [VERSIONING.md](VERSIONING.md) for version rules and [M07](milestones/M07_UPDATES_RECOVERY.md)/[M08](milestones/M08_RELEASE_HARDENING.md)/[M09](milestones/M09_STABLE_RELEASE.md) for remaining acceptance gates. Do not duplicate or overwrite frozen artifacts. Keep stable Inno AppId `{82CA4B18-541C-4D04-9A09-E676AB21F504}`, x64 full runtime payload and per-user data outside Program Files.

Restore/build/test, publish into a fresh isolated folder, verify exact payload and native executable, compile Inno, and record hashes/trust/source commit. For an authorized GitHub release, create an immutable source/tag and draft, attach the tested portable ZIP/hash/manifest, run the Windows installer workflow against those bytes, verify uploaded/downloaded assets, then publish only within explicit release authority. No commit/push/tag/release permission is granted by this planning task.

Publisher Authenticode signing/reputation is a remaining production gate and must use an authorized certificate/signing service; no borrowed private keys, fabricated signatures or embedded credentials. SQLite or any future settings-schema change requires migration and real downgrade tests before declaring recovery compatible. Preserve failed job/backup evidence; installer failure is not transactional recovery.
