# 0.12.2 verification โ€” 2026-10-22

Scope: [bounded helper correction and retained Inventory fixes](FEATURES_0.12.2.md). Production profile and installed process remain intact. Isolated profiles and owned fixtures only.

## Confirmed preceding failure

- The real hash-verified 0.12.0 app discovers public 0.12.1 (8 assertions PASS). The real 0.12.1 updater recognizes current Reinstall and older Recovery (9 assertions PASS).
- Both anonymous 0.12.1 downloads return HTTP 200 and match the frozen ZIP/Setup hashes. All six draft assets/digests/sidecars/manifests and 2,738 ZIP entries verify; its exact source, PR and installer workflows pass.
- Actual 0.12.0 public staging downloads and verifies Setup but FAILS during `CopyHelperAsync` with `Application helper payload exceeds the copy limit`. No helper/Setup starts; the owned failed staging job is cleaned up. This is an updater regression introduced by the large 0.12.0 Collector bundle, separate from the Inventory runtime fix. The original installed user process/workspace is untouched.
- Evidence remains in `artifacts/github-release/0.12.1/final-1/stage-from-0120.log` and the associated public-check/download files. Published 0.12.1 assets/tag are preserved and release notes disclose this issue.

## Required checks

Development Release solution build passes with zero warnings/errors; the complete **527-test** suite passes without skips, including the four new helper payload regressions. Remaining exact-package/public results are separate below/in the publication report.

- Automated regressions: over 2,000 excluded Collector files with retained private .NET/runtime assets; exclusion applies only to a root directory, not nested directories or a same-named file; existing 1,000 copied-file bound still rejects oversized required payloads; cancellation still prevents copying.
- Full Release build/unit suite, exact scaled Inventory WPF/Brand/copy/identity/audit/Light/Dark, native startup, frozen installer workflow and downloaded asset/hash/identity validation.
- Actual new-client public staging must acknowledge its bounded helper while the owned QA parent remains alive; stop that helper before parent exit, never run Setup, and retain isolated settings.
- Old-client discovery is rechecked against the final public version. Old-client installation remains BLOCKED by its immutable executing code; do not report server-side publication as a fix to that already installed code.

Final result: Release build and 527 unit tests, exact 29-assertion scaled Inventory/native startup, all four source/PR/main/frozen-installer CI runs, six downloaded asset digests/manifests/2,738 ZIP entries and both anonymous public downloads PASS. Old-client discovery (8), current Reinstall/Recovery (9) and actual current public staging/native helper (11) PASS. No Setup executes. Old-client installation remains BLOCKED by the unchanged old helper; manual Setup is required once. [Immutable publication evidence](GITHUB_RELEASE_0.12.2_REPORT_2026-10-22.md). Physical user input/DPI/accessibility, real vendor/AAA/soak and actual install/recovery/uninstall acceptance remain NOT TESTED.
