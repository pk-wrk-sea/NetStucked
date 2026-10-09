using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed class WindowsWifiCredentialVault : IWifiCredentialVault
{
    public static string Target(Guid adapter, string profile) => "NetStucked/WLAN/" + adapter.ToString("N") + "/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profile)));
    public EnterpriseCredentials? Read(Guid adapter, string profile)
    {
        if (!CredRead(Target(adapter, profile), 1, 0, out var pointer)) { int error = Marshal.GetLastWin32Error(); if (error == 1168) return null; throw new Win32Exception(error, "Windows Credential Manager could not read this profile credential."); }
        byte[] bytes = [];
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.BlobSize is 0 or > 2560) throw new InvalidOperationException("Unsupported stored credential.");
            bytes = new byte[credential.BlobSize]; Marshal.Copy(credential.Blob, bytes, 0, bytes.Length);
            var secret = JsonSerializer.Deserialize<Secret>(bytes) ?? throw new InvalidOperationException("Unsupported stored credential.");
            if (credential.UserName is null || credential.UserName.Length is < 1 or > 256 || secret.Password is null || secret.Password.Length is < 1 or > 256 || secret.Domain is null || secret.Domain.Length > 256) throw new InvalidOperationException("Invalid Windows-stored profile credential.");
            return new(credential.UserName, secret.Password, secret.Domain, true);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); CredFree(pointer); }
    }
    public void Write(Guid adapter, string profile, EnterpriseCredentials input)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Secret(input.Password, input.Domain));
        IntPtr memory = IntPtr.Zero;
        try
        {
            if (bytes.Length > 2560) throw new ArgumentException("Credential exceeds Windows storage limit.");
            memory = Marshal.AllocHGlobal(bytes.Length); Marshal.Copy(bytes, 0, memory, bytes.Length);
            var credential = new Credential { Type = 1, TargetName = Target(adapter, profile), BlobSize = (uint)bytes.Length, Blob = memory, Persist = 2, UserName = input.Username };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows Credential Manager could not store this profile credential.");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); if (memory != IntPtr.Zero) { Marshal.Copy(bytes, 0, memory, bytes.Length); Marshal.FreeHGlobal(memory); } }
    }
    public void Delete(Guid adapter, string profile)
    {
        if (!CredDelete(Target(adapter, profile), 1, 0) && Marshal.GetLastWin32Error() != 1168) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows Credential Manager could not remove this credential.");
    }
    private sealed record Secret(string Password, string Domain) { public override string ToString() => "Protected credential payload"; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Credential
    {
        public uint Flags, Type; [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        public IntPtr Comment; public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize; public IntPtr Blob; public uint Persist, AttributeCount;
        public IntPtr Attributes, TargetAlias; [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr buffer);
}
