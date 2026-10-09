; Version is injected from Directory.Build.props by scripts/Publish.ps1.
#ifndef MyAppVersion
  #error MyAppVersion is required; use scripts/Publish.ps1
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif
[Setup]
AppId={{82CA4B18-541C-4D04-9A09-E676AB21F504}
AppName=NetStucked
AppVersion={#MyAppVersion}
AppPublisher=NetStucked
DefaultDirName={autopf}\NetStucked
DefaultGroupName=NetStucked
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=NetStucked-{#MyAppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\NetStucked.Desktop\Resources\Brand\NetStucked.ico
WizardImageFile=..\src\NetStucked.Desktop\Resources\Brand\WizardLogo.png
WizardSmallImageFile=..\src\NetStucked.Desktop\Resources\Brand\WizardMark.png
WizardImageBackColor=$FCF8F5
WizardSmallImageBackColor=$FCF8F5
UninstallDisplayIcon={app}\NetStucked.exe
CloseApplications=yes
RestartApplications=no
PrivilegesRequired=admin
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\NetStucked"; Filename: "{app}\NetStucked.exe"; IconFilename: "{app}\NetStucked.exe"; AppUserModelID: "NetStucked.Desktop"
Name: "{autodesktop}\NetStucked"; Filename: "{app}\NetStucked.exe"; IconFilename: "{app}\NetStucked.exe"; AppUserModelID: "NetStucked.Desktop"; Tasks: desktopicon
[Run]
Filename: "{app}\NetStucked.exe"; Description: "Launch NetStucked"; Flags: nowait postinstall skipifsilent runasoriginaluser
