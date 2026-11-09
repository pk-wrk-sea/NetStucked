using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Infrastructure;

namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task<int> CheckNativeRetiredMonitoringAsync(string output, string executable)
    {
        Directory.CreateDirectory(output);
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); using var stop = new CancellationTokenSource(); int accepted = 0;
            var server = Task.Run(async () => { try { while (true) { using var client = await listener.AcceptTcpClientAsync(stop.Token); Interlocked.Increment(ref accepted); } } catch (OperationCanceledException) when (stop.IsCancellationRequested) { } catch (SocketException) when (stop.IsCancellationRequested) { } });
            var settings = new UserSettingsStore(Path.Combine(output, "isolated-legacy-profile")); var store = new MonitorStore(settings);
            var device = new MonitorDevice { Name = "Owned legacy monitor must remain stopped", Host = "127.0.0.1", Icmp = false, Tcp = true, TcpPort = ((IPEndPoint)listener.LocalEndpoint).Port, IcmpIntervalSeconds = 1, Started = true };
            await store.SaveAsync(new(1, [device], [], DateTimeOffset.UtcNow, Notifications: false), default);
            string monitorFile = Path.Combine(settings.DirectoryPath, "monitoring.json"); byte[] original = await File.ReadAllBytesAsync(monitorFile);
            try
            {
                for (int launch = 1; launch <= 2; launch++)
                {
                    var start = new ProcessStartInfo(executable) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetDirectoryName(executable) }; start.Environment["NETSTUCKED_QA_DATA_DIRECTORY"] = settings.DirectoryPath;
                    using var process = Process.Start(start) ?? throw new IOException("Owned application did not launch."); _ = process.Handle;
                    try
                    {
                        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                        while (true) { process.Refresh(); if (process.HasExited) throw new IOException("Owned application exited early."); if (process.MainWindowHandle != IntPtr.Zero) break; await Task.Delay(100, deadline.Token); }
                        await Task.Delay(2200, deadline.Token);
                        Require(Volatile.Read(ref accepted) == 0, $"Native startup {launch} does not resume a saved legacy monitor during the observation window");
                        using (var duplicate = Process.Start(start) ?? throw new IOException("Owned duplicate did not launch.")) { await duplicate.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8)); Require(duplicate.ExitCode == 0 && !process.HasExited, "Native relaunch restores the existing profile instance without starting another collector"); }
                        process.Refresh(); PostUpdateQaMessage(process.MainWindowHandle, 0x10, IntPtr.Zero, IntPtr.Zero); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); Require(process.ExitCode == 0, "Native retired-menu application closes cleanly");
                        byte[] retained = await File.ReadAllBytesAsync(monitorFile); Require(original.SequenceEqual(retained), "Native startup/Exit preserves legacy monitor data byte-for-byte");
                    }
                    finally { if (!process.HasExited) { await File.WriteAllTextAsync(Path.Combine(output, $"owned-failed-process-{launch}.json"), JsonSerializer.Serialize(new { process.Id, Executable = executable })); process.Kill(); await process.WaitForExitAsync(); } }
                }
            }
            finally { stop.Cancel(); listener.Stop(); await server; }
            await File.WriteAllTextAsync(Path.Combine(output, "native-retirement.json"), JsonSerializer.Serialize(new { Source = "Actual native application with saved legacy monitor and owned loopback listener", Version = FileVersionInfo.GetVersionInfo(executable).ProductVersion, Launches = 2, Connections = accepted, LegacyDataSha256 = Convert.ToHexStringLower(SHA256.HashData(original)), LegacyDataUnchanged = true, ProfileInstanceRestored = true, InstallerExecuted = false }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + e); return 1; }
    }
}
