param([switch]$CompileInstaller, [string]$Dotnet = 'dotnet', [string]$Iscc = '', [string]$PublishDirectory = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
[xml]$versionProps = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props')
$appVersion = [string]$versionProps.Project.PropertyGroup.Version
if ($appVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Release version must be MAJOR.MINOR.PATCH.' }
$publishPath = if ($PublishDirectory) {
    $requestedPublishPath = if ([IO.Path]::IsPathRooted($PublishDirectory)) { $PublishDirectory } else { Join-Path $repoRoot $PublishDirectory }
    [IO.Path]::GetFullPath($requestedPublishPath)
} else { Join-Path $repoRoot 'artifacts\publish\win-x64' }
& $Dotnet publish (Join-Path $repoRoot 'src\NetStucked.Desktop\NetStucked.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $publishPath
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
if ($CompileInstaller) {
    if (-not $Iscc) {
        $compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($compiler) { $Iscc = $compiler.Source }
        elseif (Test-Path 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe') { $Iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' }
        else { throw 'Inno Setup 6 ISCC.exe missing. Provide -Iscc.' }
    }
    & $Iscc "/DMyAppVersion=$appVersion" "/DPublishDir=$publishPath" (Join-Path $repoRoot 'installer\NetStucked.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
}
