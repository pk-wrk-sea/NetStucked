# 0.12.1 — Inventory responsiveness correction

The human report on 2026-10-22 authorizes diagnosing and correcting Inventory lag after import. Standing distributable publication intent is retained. This is a correction to the existing 0.12.0 workflow, with no schema change or real-data rewrite.

- Render passive Inventory cells with lightweight text; initialize the existing read-only selectable textbox only on pointer/keyboard interaction. Preserve table headers, sorting, resizing, copy commands, zoom, virtualization and stable row selection. Other menus retain their existing cell behavior.
- Replace imported/filtered snapshots with one collection reset, reuse projected rows/search text per committed revision and index latest evidence once. Delay live search binding by 180 ms to coalesce typing; preserve unsaved edits and identity when rows are filtered out.
- Index workbook identity/serial lookups and imported IDs. Retain preview, explicit selection/confirmation, duplicate rejection, nullable components and same-IP rows.
- Preserve the original modification timestamp when importing unchanged human fields, so unchanged roundtrips do not consume the bounded audit history. Real edits retain before/after history; old history is never pruned.
- Correct the Brand editor binding to the persisted Vendor field.

No automatic import, migration, real-network probe or user-profile modification is introduced. Published 0.12.0 and older assets remain immutable. Development and release evidence, including limits, is in [QA](QA_0.12.1.md).
