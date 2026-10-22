# 0.12.2 — bounded update helper for bundled Collector

Actual public 0.12.0 → 0.12.1 staging exposed `Application helper payload exceeds the copy limit`: the updater copies the installed application, including over 2,000 Python files, into its restart helper, whose existing bound is 1,000 files. Discovery and verified public downloads work; Setup is never reached in that failing path. This necessary compatibility correction retains standing distributable publication intent and all immutable 0.12.1/older assets.

- Retain every 0.12.1 Inventory responsiveness, identity/audit and Brand correction.
- Exclude only the immediate `CollectorRuntime` directory from the restart helper, after validating that entry is not a link. Helper mode branches before desktop/Collector service initialization and needs the private .NET runtime, not Python.
- Retain all current helper limits: 4,096 traversed entries, 1,000 copied files, 1 GiB bytes, path/link checks, cancellation, exact owned helper acknowledgement, hash/size/product/version/Windows trust, unsigned acknowledgement and parent-exit sequencing. The actual application package still includes the entire pinned Collector runtime.
- QA-only scheduling from the post-tag 0.12.1 follow-up keeps bulk and scheduler-sensitive fixtures exclusive, without changing assertions or deadlines.

Already installed 0.12.0/0.12.1 clients retain their old copy algorithm. They can discover/download the update, but need one explicit manual Setup upgrade to acquire the fix. Updating the server cannot repair their executing client code. No Setup is run by development/QA; [verification and limits](QA_0.12.2.md).
