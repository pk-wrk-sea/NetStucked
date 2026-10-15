using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using NetStucked.Core;
using NetStucked.Infrastructure;
namespace NetStucked.WindowsQa;
internal static partial class Program
{
    private static async Task<int> CheckNativeMonitoringAsync(string output,string executable)
    {
        Directory.CreateDirectory(output);
        try
        {
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using var stop=new CancellationTokenSource();int accepted=0;
            var server=Task.Run(async()=>{try{while(true){using var client=await listener.AcceptTcpClientAsync(stop.Token);Interlocked.Increment(ref accepted);}}catch(OperationCanceledException)when(stop.IsCancellationRequested){}catch(SocketException)when(stop.IsCancellationRequested){}});
            var settings=new UserSettingsStore(Path.Combine(output,"isolated-user-data"));var store=new MonitorStore(settings);var device=new MonitorDevice {Name="Owned native TCP auto-resume",Host="127.0.0.1",Icmp=false,Tcp=true,TcpPort=((IPEndPoint)listener.LocalEndpoint).Port,IcmpIntervalSeconds=1,Started=true};await store.SaveAsync(new(1,[device],[],DateTimeOffset.UtcNow,Notifications:false),CancellationToken.None);
            var evidence=new List<object>();
            try
            {
                for(int launch=1;launch<=2;launch++)
                {
                    int before=accepted;var start=new ProcessStartInfo(executable){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(executable)};start.Environment["NETSTUCKED_QA_DATA_DIRECTORY"]=settings.DirectoryPath;
                    using var process=Process.Start(start)??throw new InvalidOperationException("Native application did not launch.");_ =process.Handle;
                    try
                    {
                        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(25));
                        while(accepted<before+2){if(process.HasExited)throw new InvalidOperationException("Native application exited before saved monitors resumed.");await Task.Delay(100,deadline.Token);}
                        Require(accepted>=before+2,"Native startup "+launch+" automatically polls the saved explicitly started TCP monitor (owned loopback)");
                        using (var duplicate=Process.Start(start)??throw new InvalidOperationException("Duplicate launch failed")) {await duplicate.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));Require(duplicate.ExitCode==0&&!process.HasExited,"Relaunch restores the existing profile instance without a second collector process");}
                        process.Refresh();await File.WriteAllTextAsync(Path.Combine(output,"launch-"+launch+".json"),JsonSerializer.Serialize(new {process.Id,Executable=executable,Version=FileVersionInfo.GetVersionInfo(executable).ProductVersion,Connections=accepted-before}));
                        if(process.MainWindowHandle==IntPtr.Zero)throw new InvalidOperationException("Owned native window has no close handle.");PostUpdateQaMessage(process.MainWindowHandle,0x10,IntPtr.Zero,IntPtr.Zero);
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));Require(process.ExitCode==0,"Owned native app stops collection and exits cleanly");
                        var persisted=store.Load();Require(persisted.Devices.Single().Started,"Native Exit retains explicitly requested auto-resume intent");evidence.Add(new {Launch=launch,Connections=accepted-before,ExitCode=process.ExitCode,AutoResumeIntentPreserved=true});
                    }
                    finally {if(!process.HasExited){await File.WriteAllTextAsync(Path.Combine(output,"owned-failed-process-"+launch+".json"),JsonSerializer.Serialize(new {process.Id,Executable=executable}));process.Kill();await process.WaitForExitAsync();}}
                }
            }
            finally {stop.Cancel();listener.Stop();await server;}
            await File.WriteAllTextAsync(Path.Combine(output,"native-auto-resume.json"),JsonSerializer.Serialize(new {Source="Actual native application, saved monitor file and owned loopback TCP listener",Evidence=evidence,RealCiscoTested=false},new JsonSerializerOptions {WriteIndented=true}));return 0;
        }
        catch(Exception ex){Console.Error.WriteLine("FAIL "+ex);return 1;}
    }
}
