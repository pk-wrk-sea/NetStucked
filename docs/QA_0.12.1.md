# 0.12.1 Inventory verification — 2026-10-22

## Observed installation and data

The running installed process is `C:\Program Files\NetStucked\NetStucked.exe`, ProductVersion `0.12.0+a082abb58b5f34fae1e75aa4790daf4e4686956d`. It was left running. Read-only production-store validation passes: revision 1, 708 assets, zero evidence/advisories, 708 audit entries, 1,390,414 bytes. Workspace bytes before/after validation are identical. Counts/hash only are reported; no real asset rows/configurations are exported into QA or publication material. Responding/window handle alone are not performance acceptance.

## Reproduction and correction

Actual MainWindow/Inventory/DataGrid and production workbook/store implementations were exercised with isolated synthetic component records (708, then 2,000 additional records = 2,708 total). No working network or production profile was used. The original baseline used the same source as released 0.12.0, before production edits; both baseline and corrected raw metrics/renders are preserved under `artifacts/inventory-performance-20261022/`.

| Operation | Original baseline-2, 708 / 2,708 rows | Corrected fixed-2, 708 / 2,708 rows |
|---|---:|---:|
| Eight immediate search changes including clear | 1,110.6 / 1,495.8 ms | 139.6 / 149.1 ms |
| Twenty viewport jumps and return to top | 29,541.3 / 19,218.5 ms | 675.9 / 731.6 ms |
| Import selected rows, including UI idle | 428.0 / 807.5 ms | 296.4 / 404.2 ms |
| Unchanged roundtrip apply | 620.7 / 885.4 ms | 87.2 / 210.9 ms |
| Search collection notifications | 4,356 / 16,456 | at most 8 per eight queries |
| Asset audit after unchanged roundtrip | 1,416 / 6,124 | 708 / 2,708 |

Both versions virtualize 20 rows/220 realized cells: disabled virtualization was not the cause. Text input-service work in recycled read-only TextBoxes and per-row collection notifications were avoided; passive cells now use TextBlocks and expose the original selectable TextBox on interaction. A short owned-process EventPipe sample is retained, not treated as a precise attribution of all elapsed time. Timings are machine/session measurements, not universal latency guarantees or physical mouse/DPI acceptance.

## Correctness checks

- PASS: Release solution build; 523 unit tests, zero failures/skips. New regressions cover unchanged timestamps/audit, real changed-field before/after history and 2,000-row indexed identity/conflict handling.
- PASS: actual scaled WPF workflow, dirty-editor preservation, one snapshot notification on import, query-bounded notifications, real delayed search binding, correct editor identity, actual Brand binding/save, lazy-cell hover/selectable text/row selection, stable IDs/shared IPs/optional fields, unchanged roundtrip/audit and persisted reload. Light/Dark WPF renders are retained; no binding errors.
- PASS: existing 46-assertion integrated engineering WPF workflow, including audit/evidence/editor preservation, Excel roundtrip, topology/CVE and narrow Light/Dark/zoom checks.
- Historical failed attempts retained: the first stress fixture exceeded the existing 8 MiB workbook-part bound at 5,000 dense rows; fixtures were reduced within that bound rather than weakening it. One mistakenly launched stale 520-test run after a QA build failure had 519 PASS/1 timing failure in the unrelated pacing test; it is not final evidence. Corrected complete 523-test run passes with no production timing threshold changes. QA harness compile errors were corrected before the successful runs.

## Remaining acceptance / release gate

- NOT TESTED: the human's physical mouse/keyboard, Windows text input methods, screen readers, mixed-monitor DPI, sustained production editing and other real hardware/device acceptance. Rendered WPF images and synthetic input events do not close these gates.
- Existing 10 MiB archive / 8 MiB workbook-part / 16 MiB store and record limits remain. Dense 5,000-row export is not claimed to fit these byte limits. Failed bounded exports retain the destination; no limits were increased.
- Exact frozen portable/installer, CI, public updater/download verification and publication are recorded separately after they run. Setup must not execute on this machine; the installed app and real data remain intact.
