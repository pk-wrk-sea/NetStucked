param([string]$Destination = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$lockPath = Join-Path $PSScriptRoot 'collector-runtime.lock.json'
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
$cache = Join-Path $repoRoot 'artifacts\collector-runtime\downloads'
if (-not $Destination) { $Destination = Join-Path $repoRoot 'artifacts\collector-runtime\payload' }
$Destination = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force -Path $cache, $Destination | Out-Null
$lockHash = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash
$stampPath = Join-Path $Destination 'runtime-files.json'
if (Test-Path -LiteralPath $stampPath) {
    $stamp = Get-Content -LiteralPath $stampPath -Raw | ConvertFrom-Json
    if ($stamp.lockHash -ne $lockHash) { throw 'Collector runtime lock changed; choose a fresh runtime destination.' }
    if (@(Get-ChildItem -LiteralPath $Destination -Recurse -File).Count -ne $stamp.files.Count + 1) { throw 'Collector runtime contains unexpected files; choose a fresh runtime destination.' }
    foreach ($f in $stamp.files) {
        $path = [IO.Path]::GetFullPath((Join-Path $Destination $f.path))
        if (-not $path.StartsWith($Destination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid runtime manifest path.' }
        if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $f.sha256) { throw "Collector runtime integrity failed: $($f.path)" }
    }
    Write-Output "Verified bundled Collector runtime: $($stamp.files.Count) files."
    return
}
if (@(Get-ChildItem -LiteralPath $Destination -Force).Count -ne 0) { throw 'Unverified partial runtime directory exists; choose a fresh runtime destination.' }
foreach ($item in @($lock.python) + @($lock.wheels)) {
    $archive = Join-Path $cache $item.filename
    if (-not (Test-Path -LiteralPath $archive)) { Invoke-WebRequest -Uri $item.url -OutFile $archive }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $item.sha256) { throw "Official package SHA256 mismatch: $($item.filename)" }
    $target = if ($item.filename.EndsWith('.whl')) { Join-Path $Destination 'Lib\site-packages' } else { $Destination }
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $target, $true)
}
# Explicit isolated paths: no user site, PYTHONPATH, startup .pth hooks or global Python dependency.
[IO.File]::WriteAllText((Join-Path $Destination 'python313._pth'), "python313.zip`n.`nLib/site-packages`n", [Text.UTF8Encoding]::new($false))
$signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $Destination 'python.exe')
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Python Software Foundation') { throw 'Python runtime publisher verification failed.' }
$files = @(Get-ChildItem -LiteralPath $Destination -Recurse -File | Where-Object FullName -ne $stampPath | ForEach-Object { @{ path = [IO.Path]::GetRelativePath($Destination, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
@{ lockHash = $lockHash; python = $lock.python.version; files = $files } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stampPath -Encoding utf8
Write-Output "Prepared verified Python $($lock.python.version) / Netmiko runtime ($($files.Count) files)."
