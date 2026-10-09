# Versioning

Current development version is **0.1.0**. `Directory.Build.props` is authoritative for package, informational, assembly and file metadata. MSBuild generates the Windows manifest from the template using the same assembly version. The publish script derives Inno's version from it. The application displays informational version; screenshot mock versions are not used. Change only the central Version property when incrementing a release.

Use SemVer MAJOR.MINOR.PATCH. 0.x is development; 1.0.0 is a future stable release. Compatible bug fixes increment PATCH (1.0.x), additive compatible functionality increments MINOR (1.x), incompatible public/data behavior increments MAJOR. Tags use `vX.Y.Z`. Preserve stable Inno AppId for future upgrades; never embed a GitHub token.

Manual signed installers and explicit backup/reinstall support rollback now. Design secure update/recovery before releasing 1.0.0 if users should update in-app from 1.0.0. No update client is implemented in 0.1.0.
