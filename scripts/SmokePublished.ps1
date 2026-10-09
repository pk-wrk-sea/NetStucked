param([string]$PublishDirectory = '', [int]$ExistingQaProcessId = 0)
$ErrorActionPreference = 'Stop'
if (-not $PublishDirectory) { $PublishDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\publish\win-x64' }
$publishRoot = (Resolve-Path -LiteralPath $PublishDirectory).Path
$publishedExe = Join-Path $publishRoot 'NetStucked.exe'
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class NetStuckedQaWindows {
    public delegate bool EnumCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    public static string[] Describe(uint process) {
        var lines = new List<string>();
        EnumWindows((window, parameter) => { uint owner; GetWindowThreadProcessId(window, out owner);
            if (owner == process) { var title = new StringBuilder(512); var type = new StringBuilder(512);
                GetWindowText(window,title,512); GetClassName(window,type,512);
                lines.Add(window.ToInt64()+" | "+type+" | "+title); } return true; }, IntPtr.Zero);
        return lines.ToArray();
    }
    public static bool Close(uint process) {
        bool found = false;
        EnumWindows((window, parameter) => { uint owner; GetWindowThreadProcessId(window, out owner);
            if (owner == process) { var title = new StringBuilder(512); GetWindowText(window,title,512);
                if (title.ToString() == "NetStucked") { found = PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero); return false; } }
            return true; }, IntPtr.Zero);
        return found;
    }
}
'@
$qaProcess = $null
try {
    if ($ExistingQaProcessId) {
        $qaProcess = Get-Process -Id $ExistingQaProcessId
        if ($qaProcess.Path -ne $publishedExe) { throw 'Requested QA process does not match the exact published executable.' }
    } else {
        $qaDataPath = Join-Path (Split-Path $publishRoot -Parent) ('smoke-user-data-' + [Guid]::NewGuid().ToString('N'))
        $qaProcess = Start-Process -FilePath $publishedExe -WorkingDirectory $publishRoot -WindowStyle Hidden -PassThru -Environment @{ NETSTUCKED_QA_DATA_DIRECTORY = $qaDataPath }
    }
    $null = $qaProcess.Handle # Retain a native process handle so ExitCode remains available after shutdown.
    $windows = @()
    for ($check = 0; $check -lt 50; $check++) {
        $qaProcess.Refresh()
        if ($qaProcess.HasExited) { throw "Published application exited early: $($qaProcess.ExitCode)" }
        $windows = [NetStuckedQaWindows]::Describe([uint32]$qaProcess.Id)
        if ($windows | Where-Object { $_ -like '* | NetStucked' }) { break }
        Start-Sleep -Milliseconds 200
    }
    $windows
    if (-not [NetStuckedQaWindows]::Close([uint32]$qaProcess.Id)) { throw 'Published NetStucked window was not found.' }
    if (-not $qaProcess.WaitForExit(10000)) { throw 'Published app did not close within 10 seconds.' }
    if ($qaProcess.ExitCode -ne 0) { throw "Published app exit code: $($qaProcess.ExitCode)" }
    'PASS self-contained published application starts its actual WPF window and closes gracefully (exit 0).'
} finally {
    # This helper owns the launched QA process. Never enumerate or kill unrelated application processes.
    if ($qaProcess -and -not $qaProcess.HasExited) { $qaProcess.Kill(); $qaProcess.WaitForExit(5000) | Out-Null }
}
