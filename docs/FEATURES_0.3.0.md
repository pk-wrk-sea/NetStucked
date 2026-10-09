# NetStucked 0.3.0 — selected-build installation and recovery

The human request of 2026-10-09 authorizes one action to install or recover the selected build. This supersedes the manual-only execution restriction in the 0.2.0 documents. Public GitHub checks and installation still start only when the user clicks; there is no background checking or unattended installation. The later publication request starts the public build catalog at 0.3.0 and authorizes removing 0.2.x release/build downloads after local verified backups. Original 0.1.0 scope/reference images, old local artifact bytes and Git source history/tags remain unchanged.

## Use

1. Open Updates & Recovery and click **Check for Updates**.
2. Choose a published build. The action becomes **Install**, **Reinstall**, or **Recover**, relative to the running version. Its actual release notes, download size and installation location are shown.
3. Current project installers are unsigned. Explicitly acknowledge hash-verified unsigned project installers if you choose to use them. This checkbox is off on every launch; it cannot authorize a damaged or untrusted signature.
4. Click the primary action. Download progress and cancellation are available during preparation. NetStucked verifies the installer, stops all diagnostic sessions, saves preferences, hands off to an isolated helper, closes, runs Setup and restarts the selected version. Windows may still ask for UAC/SmartScreen approval; the application does not dismiss those prompts.

Portable launches install to the registered installation directory, or `%ProgramFiles%\NetStucked` when none exists. Recovery is an in-place installation using the stable Inno AppId; a portable folder itself is not overwritten. Install/extract 0.3.0 once to begin the new update path, then select 0.3.1 TEST BUILD for Upgrade and 0.3.0 for Recovery. Both use the same diagnostic behavior and schema-1 preferences. Legacy pre-0.3.0 source/local packages retain their older menu; they are excluded from the new public release catalog by retiring those GitHub releases.

## Execution and integrity

Only stable SemVer installer assets in `pk-wrk-sea/NetStucked` are eligible. Asset metadata must include its GitHub ID, exact `NetStucked-{version}-win-x64-setup.exe` name, positive size up to 256 MiB, complete SHA-256 digest and expected HTTPS release URL. Prereleases, drafts and incomplete assets cannot execute. Redirects are restricted to the same project URL or GitHub's known HTTPS asset hosts. Release pages remain available for manual installation.

Downloads are asynchronous, anonymous, bounded to ten minutes and streamed to a non-executable `.partial` file. The final name is created only after size/hash verification. Internet Mark of the Web is retained. Windows checks the installer signature and PE product/version. A valid signature is accepted; an unsigned file requires explicit acknowledgement; other trust failures are rejected. SHA-256 validates bytes against GitHub metadata, not the publisher's identity. Signing/reputation are separate concerns.

The helper rechecks live release metadata, local hash, Windows trust and installer version. It validates fixed job/cache/install paths and rejects junctions/symbolic links. Setup receives only fixed Inno arguments, with `/SILENT`, `/NORESTART`, no forced process closing and a private log. The original user's token launches the helper/restarted app; only Setup requests elevation. No shell scripts, GitHub credentials, arbitrary URLs or downloaded application binaries are executed.

NetStucked awaits ongoing commands, Ping/TCP requests and every traceroute session before saving preferences. The helper must acknowledge startup before the original window closes, then waits for that exact parent process to exit. An existing installation process/mutex, timeout or other target-installation app instance prevents concurrent replacement. Once Setup starts, it is awaited without cancellation or forced termination. Restart-required exit 3010 is reported explicitly and does not claim completion or restart the app.

## Settings and failure recovery

Immediately before running Setup, the helper backs up exact compatible schema-1 `settings.json` bytes with version/date/hash metadata under `%LOCALAPPDATA%\NetStucked\updates\backups`. Recovery restores the newest verified backup matching the selected older version. Without one, compatible current settings are retained. Unsupported or damaged preferences block preparation rather than being overwritten. At most twenty verified backups are retained; unrelated files are not recursively removed.

Jobs, installer logs and `last-result.json` live under `%LOCALAPPDATA%\NetStucked\updates`. Normal completed-job retention keeps the two latest prior jobs plus the new job; unfinished crash jobs remain available for investigation. Failed/cancelled preparation discards its own job. Installer failure preserves the settings backup and records the actual result; restarting the previous source requires its executable and application DLL bytes to remain unchanged. Backup restoration, installed-version validation or restart failure cannot report successful recovery. A partial installer failure may require manual repair from the preserved log/package; there is no transactional restoration of Program Files.

## Verification

See [0.3.0 QA](QA_0.3.0.md) for actual tests and remaining limits. Installer download/verification, orchestration adapters and real network-drain tests are distinct from executing a machine upgrade/downgrade. No automatic background updater or new diagnostic page was added.
