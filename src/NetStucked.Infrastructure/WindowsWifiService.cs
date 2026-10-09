using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class WindowsWifiService : IWifiService, IDisposable
{
    private readonly SemaphoreSlim _mutation = new(1, 1);
    public Task<IReadOnlyList<WifiAdapter>> GetAdaptersAsync(CancellationToken token) => Task.Run<IReadOnlyList<WifiAdapter>>(() =>
    {
        token.ThrowIfCancellationRequested(); using var handle = Open();
        Check(Native.WlanEnumInterfaces(handle.Handle, IntPtr.Zero, out var data));
        try { return ReadList<InterfaceInfo>(data, 32).Select(i => new WifiAdapter(i.Id, i.Name, State(i.State))).ToArray(); }
        finally { Native.WlanFreeMemory(data); }
    }, token);
    public Task<WifiSnapshot> ReadAsync(Guid adapter, CancellationToken token) => Task.Run(() => Read(adapter, token), token);
    private static WifiSnapshot Read(Guid adapter, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); using var handle = Open();
        Check(Native.WlanEnumInterfaces(handle.Handle, IntPtr.Zero, out var interfaces));
        InterfaceInfo info;
        try { info = ReadList<InterfaceInfo>(interfaces, 32).SingleOrDefault(i => i.Id == adapter); }
        finally { Native.WlanFreeMemory(interfaces); }
        if (info.Id == Guid.Empty) throw new InvalidOperationException("Wireless adapter is no longer available.");
        Check(Native.WlanGetProfileList(handle.Handle, ref adapter, IntPtr.Zero, out var list));
        var profiles = new List<WifiProfile>(); var notices = new List<string>();
        try
        {
            foreach (var item in ReadList<ProfileInfo>(list, 1024))
            {
                token.ThrowIfCancellationRequested(); uint flags = 0;
                uint code = Native.WlanGetProfile(handle.Handle, ref adapter, item.Name, IntPtr.Zero, out var xml, ref flags, out _);
                if (code != 0) { notices.Add($"Profile '{item.Name}' is not readable: {Error(code)}"); continue; }
                try { profiles.Add(WifiProfileXml.Describe(Marshal.PtrToStringUni(xml)!, flags | item.Flags, profiles.Count + 1)); }
                catch (Exception ex) when (ex is ArgumentException or System.Xml.XmlException) { notices.Add($"Profile '{item.Name}' has unsupported configuration."); }
                finally { Native.WlanFreeMemory(xml); }
            }
        }
        finally { Native.WlanFreeMemory(list); }
        var networks = new List<WifiNetwork>();
        uint available = Native.WlanGetAvailableNetworkList(handle.Handle, ref adapter, 0, IntPtr.Zero, out var availableData);
        if (available != 0) notices.Add("SSID availability: " + Error(available) + (available == 5 ? ". Check Windows Location / Wi-Fi permissions." : ""));
        else
        {
            try { networks.AddRange(ReadList<AvailableNetwork>(availableData, 4096).Select(n => new WifiNetwork(SsidText(n.Ssid), SsidHex(n.Ssid), n.Profile, Auth(n.Auth), checked((int)n.Signal), n.Connectable != 0))); }
            finally { Native.WlanFreeMemory(availableData); }
        }
        string? connected = null, ssid = null; int? signal = null;
        uint query = Native.WlanQueryInterface(handle.Handle, ref adapter, 7, IntPtr.Zero, out _, out var connection, out _);
        if (query == 0)
        {
            try { var attributes = Marshal.PtrToStructure<ConnectionAttributes>(connection); if (attributes.State == 1) { connected = attributes.Profile; ssid = SsidText(attributes.Association.Ssid); signal = checked((int)attributes.Association.Signal); } }
            finally { Native.WlanFreeMemory(connection); }
        }
        else if (info.State == 1) notices.Add("Connection details: " + Error(query));
        var ip = ReadIp(adapter);
        return new(DateTimeOffset.Now, new(adapter, info.Name, State(info.State)), profiles, networks, connected, ssid, signal, ip, notices.Count == 0 ? null : string.Join("\n", notices.Take(5)));
    }
    public static AdapterIpConfiguration ReadIp(Guid id)
    {
        var network = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => Guid.TryParse(n.Id, out var parsed) && parsed == id);
        if (network is null) return AdapterIpConfiguration.Empty;
        var ip = network.GetIPProperties();
        return new(network.Supports(NetworkInterfaceComponent.IPv4) ? ip.GetIPv4Properties().Index : 0,
            network.Supports(NetworkInterfaceComponent.IPv6) ? ip.GetIPv6Properties().Index : 0,
            ip.UnicastAddresses.Select(a => a.Address).ToArray(), ip.GatewayAddresses.Select(a => a.Address).ToArray(), ip.DnsAddresses.ToArray());
    }
    public Task ScanAsync(Guid adapter, CancellationToken token) => Mutate(async () =>
    {
        using var handle = Open();
        var completion = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
        NotificationCallback callback = (ref Notification n, IntPtr _) => { if (n.Source == 8 && n.Adapter == adapter && n.Code is 7 or 8) completion.TrySetResult(n.Code); };
        Check(Native.WlanRegisterNotification(handle.Handle, 8, true, callback, IntPtr.Zero, IntPtr.Zero, out _));
        try
        {
            Check(Native.WlanScan(handle.Handle, ref adapter, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
            if (await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), token).ConfigureAwait(false) == 8) throw new InvalidOperationException("Windows reported that the Wi-Fi scan failed.");
        }
        finally { Native.WlanRegisterNotification(handle.Handle, 0, true, null, IntPtr.Zero, IntPtr.Zero, out _); GC.KeepAlive(callback); }
    }, token);
    public Task ConnectAsync(Guid adapter, string profile, CancellationToken token) => Mutate(async () =>
    {
        if ((await ReadAsync(adapter, token).ConfigureAwait(false)).ConnectedProfile == profile) return; // Already confirmed by Windows; avoid needless network disruption.
        using var handle = Open();
        var failed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NotificationCallback callback = (ref Notification n, IntPtr _) =>
        {
            // Callback only copies fixed native data; never unregister or call UI here.
            if (n.Source != 8 || n.Adapter != adapter || n.Code is not (10 or 11) || n.Data == IntPtr.Zero || n.Size < 564) return;
            if (Marshal.PtrToStringUni(n.Data + 4, 256)?.TrimEnd('\0') != profile) return;
            uint reason = unchecked((uint)Marshal.ReadInt32(n.Data, 560));
            if (n.Code == 10 && reason == 0) completed.TrySetResult(); else failed.TrySetResult(Reason(reason));
        };
        Check(Native.WlanRegisterNotification(handle.Handle, 8, true, callback, IntPtr.Zero, IntPtr.Zero, out _));
        try
        {
            var parameters = new ConnectionParameters { Mode = 0, Profile = profile, BssType = 1 };
            token.ThrowIfCancellationRequested(); Check(Native.WlanConnect(handle.Handle, ref adapter, ref parameters, IntPtr.Zero));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(35));
            try
            {
                while (true)
                {
                    if (failed.Task.IsCompleted) throw new InvalidOperationException("Connection failed: " + await failed.Task.ConfigureAwait(false));
                    var snapshot = await ReadAsync(adapter, timeout.Token).ConfigureAwait(false);
                    if (completed.Task.IsCompletedSuccessfully && snapshot.ConnectedProfile == profile) return;
                    await Task.Delay(250, timeout.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("Windows did not confirm this profile within 35 seconds. Check authentication, certificate and Wi-Fi availability."); }
            finally
            {
                // Cancel a still-pending request only. Do not disconnect an established network on cancellation.
                uint q = Native.WlanQueryInterface(handle.Handle, ref adapter, 6, IntPtr.Zero, out _, out var state, out _);
                bool pending = false;
                if (q == 0) { try { pending = Marshal.ReadInt32(state) is 5 or 6 or 7; } finally { Native.WlanFreeMemory(state); } }
                if (pending)
                {
                    Check(Native.WlanDisconnect(handle.Handle, ref adapter, IntPtr.Zero));
                    // Drain the pending native request before another NetStucked mutation can enter.
                    for (int i = 0; i < 30; i++)
                    {
                        await Task.Delay(100).ConfigureAwait(false);
                        q = Native.WlanQueryInterface(handle.Handle, ref adapter, 6, IntPtr.Zero, out _, out state, out _);
                        if (q != 0) break;
                        int current; try { current = Marshal.ReadInt32(state); } finally { Native.WlanFreeMemory(state); }
                        if (current is not (3 or 5 or 6 or 7)) break;
                    }
                }
            }
        }
        finally { Native.WlanRegisterNotification(handle.Handle, 0, true, null, IntPtr.Zero, IntPtr.Zero, out _); GC.KeepAlive(callback); }
    }, token);
    public Task DisconnectAsync(Guid adapter, CancellationToken token) => Mutate(async () =>
    {
        using var handle = Open(); Check(Native.WlanDisconnect(handle.Handle, ref adapter, IntPtr.Zero));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while ((await ReadAsync(adapter, timeout.Token).ConfigureAwait(false)).ConnectedProfile is not null) await Task.Delay(250, timeout.Token).ConfigureAwait(false);
    }, token);
    public Task SaveProfileAsync(Guid adapter, string xml, bool overwrite, CancellationToken token) => Mutate(async () =>
    {
        var incoming = WifiProfileXml.Describe(xml, 2, 1); uint flags = 2;
        if (overwrite)
        {
            var existing = (await ReadAsync(adapter, token).ConfigureAwait(false)).Profiles.FirstOrDefault(p => p.Name == incoming.Name);
            if (existing is { PolicyManaged: true }) throw new InvalidOperationException("Company policy-managed profiles are read-only.");
            if (existing is not null) flags = existing.PerUser ? 2u : 0u;
        }
        using var handle = Open(); token.ThrowIfCancellationRequested();
        // New profiles are per-user. Editing retains existing scope and security descriptor.
        Check(Native.WlanSetProfile(handle.Handle, ref adapter, flags, xml, null, overwrite, IntPtr.Zero, out var reason), reason);
    }, token);
    public Task DeleteProfileAsync(Guid adapter, string profile, CancellationToken token) => Mutate(() =>
    {
        using var handle = Open(); token.ThrowIfCancellationRequested(); Check(Native.WlanDeleteProfile(handle.Handle, ref adapter, profile, IntPtr.Zero)); return Task.CompletedTask;
    }, token);
    public Task SetEnterpriseCredentialsAsync(Guid adapter, string profile, EnterpriseCredentials credentials, CancellationToken token) => Mutate(async () =>
    {
        var snapshot = await ReadAsync(adapter, token).ConfigureAwait(false);
        var selected = snapshot.Profiles.Single(p => p.Name == profile);
        if (selected.Eap != "PEAP / MSCHAPv2" || selected.PolicyManaged) throw new InvalidOperationException("Credentials are managed by Windows/IT for this EAP method or policy. Only user PEAP-MSCHAPv2 credentials can be entered here.");
        using var handle = Open(); token.ThrowIfCancellationRequested();
        Check(Native.WlanSetProfileEapXmlUserData(handle.Handle, ref adapter, profile, 0, WifiProfileXml.PeapCredentials(credentials), IntPtr.Zero));
    }, token);
    private async Task Mutate(Func<Task> action, CancellationToken token)
    {
        await _mutation.WaitAsync(token).ConfigureAwait(false);
        try { await Task.Run(action, token).ConfigureAwait(false); }
        finally { _mutation.Release(); }
    }
    public void Dispose() => _mutation.Dispose();
    private static WlanHandle Open() { if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows WLAN API is required."); Check(Native.WlanOpenHandle(2, IntPtr.Zero, out _, out var handle)); return new(handle); }
    private sealed class WlanHandle(IntPtr handle) : IDisposable { public IntPtr Handle { get; } = handle; public void Dispose() => Native.WlanCloseHandle(Handle, IntPtr.Zero); }
    private static T[] ReadList<T>(IntPtr pointer, int max) where T : struct
    {
        int count = Marshal.ReadInt32(pointer); if (count < 0 || count > max) throw new InvalidOperationException("Windows WLAN list exceeds the supported limit.");
        return Enumerable.Range(0, count).Select(i => Marshal.PtrToStructure<T>(pointer + 8 + i * Marshal.SizeOf<T>())).ToArray();
    }
    private static string SsidHex(Ssid ssid) => Convert.ToHexString(ssid.Bytes.AsSpan(0, (int)Math.Min(ssid.Length, 32)));
    private static string SsidText(Ssid ssid) => Encoding.UTF8.GetString(ssid.Bytes, 0, (int)Math.Min(ssid.Length, 32));
    private static string State(int value) => value switch { 0 => "Not ready", 1 => "Connected", 2 => "Ad hoc", 3 => "Disconnecting", 4 => "Disconnected", 5 => "Associating", 6 => "Discovering", 7 => "Authenticating", _ => "Unknown" };
    private static string Auth(uint value) => value switch { 1 => "Open", 3 => "WPA-Enterprise", 4 => "WPA-Personal", 5 => "WPA-None", 6 => "WPA2-Enterprise", 7 => "WPA2-Personal", 8 => "WPA3-Enterprise", 9 => "WPA3-Personal", 10 => "OWE", 11 => "WPA3-Enterprise", _ => "Other / Windows-managed" };
    private static string Error(uint code) => code == 1062 ? "Windows WLAN AutoConfig service is stopped. Enable it in Windows when Wi-Fi hardware is available. (Windows 1062)" : new Win32Exception(unchecked((int)code)).Message + $" (Windows {code})";
    private static string Reason(uint code) { var text = new StringBuilder(1024); return Native.WlanReasonCodeToString(code, 1024, text, IntPtr.Zero) == 0 ? text.ToString() : $"WLAN reason {code}"; }
    private static void Check(uint code, uint reason = 0) { if (code != 0) throw new InvalidOperationException(Error(code) + (reason != 0 ? ": " + Reason(reason) : "")); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct InterfaceInfo { public Guid Id; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name; public int State; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProfileInfo { [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct Ssid { public uint Length; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Bytes; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct AvailableNetwork { [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Profile; public Ssid Ssid; public uint BssType, Bssids; public int Connectable; public uint Reason, PhyCount; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] Phys; public int MorePhys; public uint Signal; public int Secure; public uint Auth, Cipher, Flags, Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct Association { public Ssid Ssid; public uint BssType; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] Bssid; public uint Phy, PhyIndex, Signal, RxRate, TxRate; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ConnectionAttributes { public int State, Mode; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Profile; public Association Association; public int Secure, OneX; public uint Auth, Cipher; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ConnectionParameters { public int Mode; [MarshalAs(UnmanagedType.LPWStr)] public string Profile; public IntPtr Ssid, BssidList; public uint BssType, Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct Notification { public uint Source, Code; public Guid Adapter; public uint Size; public IntPtr Data; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void NotificationCallback(ref Notification data, IntPtr context);
    private static class Native
    {
        [DllImport("wlanapi.dll")] internal static extern uint WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr handle);
        [DllImport("wlanapi.dll")] internal static extern uint WlanCloseHandle(IntPtr handle, IntPtr reserved);
        [DllImport("wlanapi.dll")] internal static extern void WlanFreeMemory(IntPtr data);
        [DllImport("wlanapi.dll")] internal static extern uint WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr data);
        [DllImport("wlanapi.dll")] internal static extern uint WlanGetProfileList(IntPtr handle, ref Guid adapter, IntPtr reserved, out IntPtr data);
        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] internal static extern uint WlanGetProfile(IntPtr handle, ref Guid adapter, string name, IntPtr reserved, out IntPtr xml, ref uint flags, out uint access);
        [DllImport("wlanapi.dll")] internal static extern uint WlanGetAvailableNetworkList(IntPtr handle, ref Guid adapter, uint flags, IntPtr reserved, out IntPtr data);
        [DllImport("wlanapi.dll")] internal static extern uint WlanQueryInterface(IntPtr handle, ref Guid adapter, uint opcode, IntPtr reserved, out uint size, out IntPtr data, out uint type);
        [DllImport("wlanapi.dll")] internal static extern uint WlanScan(IntPtr handle, ref Guid adapter, IntPtr ssid, IntPtr ie, IntPtr reserved);
        [DllImport("wlanapi.dll")] internal static extern uint WlanRegisterNotification(IntPtr handle, uint source, [MarshalAs(UnmanagedType.Bool)] bool ignoreDuplicates, NotificationCallback? callback, IntPtr context, IntPtr reserved, out uint previous);
        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] internal static extern uint WlanConnect(IntPtr handle, ref Guid adapter, ref ConnectionParameters parameters, IntPtr reserved);
        [DllImport("wlanapi.dll")] internal static extern uint WlanDisconnect(IntPtr handle, ref Guid adapter, IntPtr reserved);
        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] internal static extern uint WlanSetProfile(IntPtr handle, ref Guid adapter, uint flags, string xml, string? security, [MarshalAs(UnmanagedType.Bool)] bool overwrite, IntPtr reserved, out uint reason);
        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] internal static extern uint WlanDeleteProfile(IntPtr handle, ref Guid adapter, string name, IntPtr reserved);
        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] internal static extern uint WlanSetProfileEapXmlUserData(IntPtr handle, ref Guid adapter, string name, uint flags, string xml, IntPtr reserved);
        [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] internal static extern uint WlanReasonCodeToString(uint code, uint size, StringBuilder text, IntPtr reserved);
    }
}
