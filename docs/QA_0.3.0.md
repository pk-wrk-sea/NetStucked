# NetStucked 0.3.0 verification — 2026-10-09

Host: Windows 10 Pro 22H2 x64 (19045), .NET SDK 10.0.401 / runtime 10.0.12. This is an uncommitted local development build based on `976422b18d676145aa14a41a673a6c9cd4b973d8`. No commit, push or GitHub publication occurred for 0.3.0. Published 0.2.x source/tags/packages remain unchanged.

## Results

| Check | Result | Actual evidence and limit |
|---|---|---|
| Solution Release build | PASS | All five projects, zero warnings/errors |
| Automated tests | PASS | 150 passed, 0 failed, 0 skipped; `final/test-results/tests.trx` |
| Download/integrity adapters | PASS | Fixed origin, redirects, exact filename/version, metadata, truncated/oversized/wrong-hash payload, server failure, cancellation before/after bytes written; no executable fixture is run |
| Backup/recovery adapters | PASS | Exact BOM/Unicode bytes, matching version only, tamper detection, unsupported schema, retention of 20 verified backups and preservation of unrelated files |
| Installer orchestration adapters | PASS | Parent exit before verification/backup/install, failure exits, simulated UAC cancellation, restart-required 3010, installed-version mismatch, invalid paths and parent timeout |
| Selected-build WPF commands | PASS | Install/Reinstall/Recover selection, missing digest disables execution, duplicate/cancel guard, failed helper preparation unlocks/discards and permits retry; backend is explicitly a test-only recorder |
| Actual diagnostic drain before handoff | PASS | Real Ping/TCP and two independent loopback trace sessions stop, active requests reach zero and preferences save before the recorder's helper handoff |
| Actual GitHub installer download | PASS | Public 0.2.1 asset ID 623733922, 46,848,661 bytes; anonymous HTTPS redirect/stream/hash/size checked; Setup not executed |
| Windows installer trust/product/version | PASS | Real PE metadata identifies NetStucked/0.2.1; WinVerifyTrust returns unsigned `0x800B0100`; rejected without acknowledgement, accepted with acknowledgement, Internet ZoneId=3 retained |
| Real isolated helper startup | PASS | Exact executable/DLL copies start a real helper process, acknowledgement identifies its own PID, helper waits for the real parent to exit; test stops its own helper while parent stays alive and unsigned execution is disallowed |
| Real WPF render/bindings | PASS | Exact final application DLLs rendered at 1536x1024, 1280x800 and existing scaled diagnostic views; zero binding errors; final Updates images inspected |
| Concurrent polling regression | PASS | 60.2-second real loopback soak: Ping 254 targets, two trace sessions, TCP 32 endpoints; no same-address/TTL or TCP overlap and Stop drains requests |
| Self-contained publish/start/close | PASS | Full win-x64 private runtime; actual WPF HWND found and graceful close exits 0 |
| Inno Setup compiler | PASS | Official 6.7.3 installer hash matched GitHub metadata, valid Pyrsys B.V. Authenticode signature; compiler installed in per-user tools cache; local installer compiled successfully |
| Package verification | PASS | Portable ZIP decompressed/hash/size checked against all 411 payload files; installer ProductVersion 0.3.0 / ProductName NetStucked; installer is unsigned |
| Full helper → Setup → installed-app restart | NOT TESTED | Real helper acknowledgement is tested; no NetStucked installer was executed and no Program Files replacement occurred |
| Clean install/upgrade/downgrade/uninstall | NOT TESTED | No machine installer/UAC/SmartScreen interaction, partial-install repair, reboot or actual installed-version settings recovery was exercised |
| Windows 11 / remote networking / human acceptance | NOT TESTED | No Windows 11, authorized remote multi-hop/TCP, manual GUI/DPI/accessibility or human visual acceptance run |

Native download SHA-256: `346d0a26fb0620ad8da4d86a9411f8df22ece91a594586059139538f08791ca7`. Records: `artifacts/selected-build/0.3.0/final/native-installer-verification/actual-installer-verification.json` and its transcript. The exact final app/runtime were copied into `final/qa-runtime`; the harness verifies the three loaded application DLL hashes against `final/publish` before the WPF run. Fixtures stay in tests; all displayed diagnostic measurements come from network APIs.

## Measured simultaneous polling

The 60.2-second soak completed 61,325 Ping sends/replies, 242 first-trace cycles and 241 second-trace cycles. Maximum combined concurrent ICMP requests was 18, with zero same-address/TTL overlap. All 32 TCP endpoints connected successfully and no sockets remained active after Stop. Navigation: 12 samples, mean 5.6 ms, maximum 29.9 ms. Maximum observed Dispatcher heartbeat gap: 108.1 ms. These are this machine's loopback observations, not promises for remote networks or long-run performance.

Evidence: `final/windows-qa.log`, `final/windows-qa/soak-results.txt`, `tcp-soak-results.json`, `engine-diagnostics.json` and actual WPF PNGs. One earlier new QA condition timed out because it read inactive-page presentation counters without refreshing them; the harness was corrected to read current results, then the complete final run passed. An initial unit test exposed path equality allowing data/install directory collision; preparation now rejects it and the regression passes.

## Commands executed

Using `C:\Users\PK\.codex\cache\netstucked-tools\dotnet\dotnet.exe`:

```powershell
dotnet build NetStucked.sln -c Release --no-restore
dotnet test tests/NetStucked.Tests/NetStucked.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/selected-build/0.3.0/final/test-results
./scripts/Publish.ps1 -Dotnet <SDK executable> -PublishDirectory artifacts/selected-build/0.3.0/final/publish -CompileInstaller -Iscc <per-user Inno 6.7.3 ISCC.exe>
dotnet artifacts/selected-build/0.3.0/final/qa-runtime/NetStucked.WindowsQa.dll artifacts/selected-build/0.3.0/final/windows-qa 60 artifacts/selected-build/0.3.0/final/publish
dotnet artifacts/selected-build/0.3.0/final/qa-runtime/NetStucked.WindowsQa.dll --verify-installer artifacts/selected-build/0.3.0/final/native-installer-verification
./scripts/SmokePublished.ps1 -PublishDirectory artifacts/selected-build/0.3.0/final/publish
git -c core.whitespace=cr-at-eol diff --check
```

`cr-at-eol` recognizes the preserved CRLF files under this repository's `* -text` attribute; it changes no repository/global setting. The compiler setup, signature and hash were verified from [the official download](https://jrsoftware.org/isdl.php) and [verification instructions](https://jrsoftware.org/isdl-verify.php). Only the compiler tool was installed, under `C:\Users\PK\.codex\cache\netstucked-tools\inno-6.7.3\compiler`; this is distinct from testing installation of NetStucked.

## Runnable local packages

Keep the complete `artifacts/selected-build/0.3.0/final/publish` directory together and run `NetStucked.exe`, or extract the portable ZIP. Existing unsigned builds require explicit acknowledgement for in-app installation. Windows may still request permission. Recovery to 0.2.x returns to its older manual update menu; preserve this 0.3.0 portable package to return manually. On-demand checking is the only check mode. Incomplete crash jobs/logs remain available for investigation; there is no transactional restoration of Program Files after partial Setup failure.

| Package | Bytes | SHA-256 |
|---|---:|---|
| `NetStucked-0.3.0-win-x64-portable.zip` | 65,727,489 | `841ac3b5890b5deb37fd006aea091ac5cdf0641c0b06114c165f74e5467c9c0b` |
| `NetStucked-0.3.0-win-x64-setup.exe` | 46,880,759 | `f3556f251f9e0b6c5d42e94aff4787f6e5813ff0871b9f359a03e52660e60f36` |

Both are under `artifacts/selected-build/0.3.0/final`, with `.sha256` files and a payload/source manifest. Compiler configuration uses the unchanged stable AppId. Installer compilation alone does not prove installation or upgrade/recovery success.

Canonical hashes still match: scope `0f722da815555532a330e1ae9c5121778aa184cc740c94da82cb7e65d3003776`, Ping reference `45f941795fa7347a824976b79f73cd61f029778f7afecc246df302e181d55772`, Trace reference `e8f8f3ad08d9d9ca844bc2f2f3326910df74583afe52dbc4730c19085f2cb2f1`.
