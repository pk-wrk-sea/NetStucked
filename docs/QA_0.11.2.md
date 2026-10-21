# 0.11.2 - Collector incident verification

Date: 2026-10-21, Asia/Bangkok. [Scope and security contract](FEATURES_0.11.2.md). **Published as GitHub Latest** after exact-package/CI/public-download and real old/current updater verification; [immutable publication evidence](GITHUB_RELEASE_0.11.2_REPORT_2026-10-21.md). Actual-device resolution is not claimed.

## Observed incident

At incident inspection the installed executable was 0.11.1+314c2f3da937232e5f9441c0863e9390016af8e7. Installed Core/Infrastructure/Desktop/Tmds DLL hashes matched the frozen 0.11.1 payload. Read-only inspection confirmed the reported generic Detail. No Start, credentials or commands were submitted to the working device during development.

Do not infer that Collector authentication succeeded from the supplied Cisco event: the two events have distinct tty values, and the successful event's AES256-CTR is absent from the installed provider's supported cipher list. The whole server offer and connection correlation are unknown. The generic Detail alone also covers channel, PTY and shell rejection.

## Released-binary reproductions

Artifact root: artifacts/collector-incident-20261021/transport-owned/results.

| Owned fixture | Exact 0.11.1 result | Server evidence |
| --- | --- | --- |
| AES256-CTR + SHA512-ETM only | Same reported generic error; raw provider reports no common cipher | Zero authentication/channel/PTY/shell requests |
| AES-GCM control | Connected | Authentication + shell succeeds |
| Reject channel / PTY / shell | Same generic error for all three | Distinct requested/denied setup stages |
| Authentication 3s + shell 3s, timeout 5s | Timeout due shared setup deadline | Distinct from generic incident message |

Three production DLLs used in the probes are hash-verified against frozen 0.11.1. Probe exceptions are from owned fixtures only; no user credentials, actual-device raw config or provider-controlled errors are added to production logs.

Separate strict-CR console regression: 1 control PASS, 1 released Collector FAIL (post-auth command timeout). Evidence artifacts/collector-cisco-investigation/strict-cr. This is a second interoperability defect, not the observed pre-return transport error. Former fixtures accepted bare LF and therefore missed it.

Provider research artifacts: collector-incident-20261021/sshnet-research. Owned stalls confirm that provider ConnectAsync cancellation alone does not abort synchronous KEX/password waits promptly; public captured Session disposal fixes password-stage waits only. The production adapter must own the transport sockets, drain cancellation and bound buffers, rather than assume token support or ShellStream initial capacity is a memory limit.

An intermediate CreateShell/custom-stream adapter compiled and completed two actual WPF CTR/ETM collections (16 CR commands, zero LF/blanks) but crashed during the existing large-output wire suite with an unhandled SDK Shell input-worker NullReferenceException. Evidence: collector-cisco-fix-0112/wpf-ctr and collector-incident-20261021/sshnet-research/provider-existing-wire.log. This candidate is not publication-ready. A successful small collection/build does not close the lifecycle gate; the final adapter must pass the full suite after this failure is addressed. Native Computer Use capture of this intermediate QA window failed to target its pixels; retained Light/Dark PNGs are actual WPF control renders, not verified native captures.

## Corrected adapter verification

The final adapter removes the SDK Shell input worker. It subscribes to public Session events before creating ShellStream, binds the actual confirmed channel, bounds the application queue, rejects combined pre-ready output above 64 KiB and synchronously drains the SDK duplicate buffer. Separate UTF-8 decoders, serialized owned writes and awaited socket/SDK disposal preserve large captures and cancellation. The SDK initial buffer size is not treated as a memory limit.

| Check | Actual result | Evidence under artifacts/ |
| --- | --- | --- |
| Final solution Release build | PASS, 0 warnings/errors | collector-cisco-release-0112/build-final.log |
| Complete unit/integration suite | 512 PASS, 0 failed/skipped | collector-cisco-release-0112/unit-quiet.log; test-results/quiet-unit.trx |
| Cisco-compatible real SSH matrix | 60 PASS, 23 owned scenarios | collector-incident-20261021/transport-owned/guarded-matrix-final.log and guarded-matrix-final/result.json |
| Independent existing SSH wire/banner | 12 + 5 PASS; >8 MiB, 8192 complete lines, stderr, Thai, redaction and cancellation | collector-incident-20261021/sshnet-research/final-guarded-review/ |
| Actual KEX/no-dial policy | 2 PASS; strong-only wire offers, confirmed stalled KEX cancellation/EOF and pre-cancel no dial | same final-guarded-review; hash-gate.json confirms source/runtime unchanged |
| Direct exact-source relay policy | 6 PASS; wrong nonce/host/port/malformed/unsupported requests have zero outbound; authorized forwarding/EOF joins | collector-incident-20261021/sshnet-relay-probe/results-2/result.json |
| Current production output/guard tests | 14 PASS; combined startup cap, reject-before-enqueue, duplicate drain serialization, queue backpressure, split/malformed UTF-8 and cancellation | collector-cisco-investigation/guard-tests/result.json and collector-guard-streams.trx |
| Fragmented console decoder | 4 PASS | python -m unittest discover -s tests/fixtures -p test_console_input.py |
| Actual WPF Collector + production SSH/capture | 14 PASS; both devices complete using CTR/SHA512-ETM, 2 logins/16 CR-only commands, zero LF/blanks | collector-cisco-fix-0112/wpf-ctr-guard.log and wpf-ctr-guard/owned-ssh-audit.json |
| Existing Terminal WPF/WebView/SSH | 50 PASS; provider/trust workflow retained | collector-cisco-fix-0112/terminal.log |
| General WPF regression | 249 PASS; zero binding errors and awaited shutdown | collector-cisco-fix-0112/wpf-regression.log |

The SSH matrix includes GCM control, channel/PTY/shell denial, early close, changed key on rekey, separate 3s authentication + 3s shell budgets under a 5s per-stage timeout, pin rejection before even none-auth, exactly one actual-rejection AUTH2, unsupported/partial MFA, password change, none-auth bypass rejection, six canceled KEX/auth/channel/PTY/shell waits with peer EOF, explicit initial-flood rejection and saturated post-shell output disposal. An additional write after awaited disposal cancels without restarting SDK work. No working-network probes or real credentials are used.

One complete 512-test attempt during concurrent build/SSH probe activity failed only the pre-existing ICMP admission timing assertion (511 passed). The subsequent quiet complete run passes all 512. No unrelated pacing code or timing threshold is changed. The earlier SDK-worker crash candidate is retained above; the guarded design's fresh independent wire/banner/policy runs exit cleanly with unchanged source/runtime hashes.

Light/Dark PNGs are actual WPF control renders. Computer Use was stopped by the human with Esc; no successful native screenshot is claimed for this patch. Exact native executable/monitoring, frozen package and public release/updater checks are recorded in the publication report below. Raw TCP-connect/DNS stalls on a real network are NOT TESTED; owned connected-socket KEX cancellation and relay startup/no-dial policies are verified.

## Publication checks

PASS: all four source/PR/main/exact-installer CI runs, six-asset draft verification, 498 exact payload entries, anonymous public ZIP/Setup downloads, actual 0.11.1 discovery of Install v0.11.2 (8 assertions), current Reinstall/Recovery (9) and old production updater staging/trust/native-helper preparation (11). No Setup was executed. [Exact hashes, source identities and evidence](GITHUB_RELEASE_0.11.2_REPORT_2026-10-21.md).

- Exact reported CTR/SHA512-ETM real SSH WPF collection and GCM control; actual protected JSON/TXT capture and command framing.
- Host mismatch before any authentication, one rejected AUTH1 then bounded AUTH2, unsupported/partial/MFA/password-change no fallback; no credentials/nonce in errors/state/files.
- Stage-specific channel/PTY/shell rejection; fresh post-auth shell deadline.
- Cancel during owned KEX/none/password/channel/PTY/shell/output stalls, relay startup/no-dial policies; no late credentials, task/socket/listener drain and no replay. Real-network TCP/DNS stalls remain separate.
- Wrong nonce/destination cannot open an outbound connection; listener only loopback, buffers bounded with backpressure, large stdout/stderr/UTF-8 redaction preserved.
- Full supported build/regression, unchanged Terminal/Telnet, actual WPF captures/native executable checks, frozen portable/installer CI and public-download/updater path.

Actual-device resolution, Cisco platform/AAA/command corpus, physical DPI/accessibility, clean-machine Setup/recovery and human acceptance remain NOT TESTED. Existing published assets remain immutable.
