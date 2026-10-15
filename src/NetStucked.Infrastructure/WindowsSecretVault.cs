using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NetStucked.Core;

namespace NetStucked.Infrastructure;
public sealed class WindowsSecretVault(UserSettingsStore store) : ISecretVault
{
    private string Target(string reference)
    {
        if (reference.Length is < 1 or > 80 || reference.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("Invalid credential reference.");
        string scope = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(store.DirectoryPath).ToUpperInvariant())))[..24];
        return "NetStucked/Tools/" + scope + "/" + reference;
    }
    public string? Read(string reference)
    {
        if (!CredRead(Target(reference), 1, 0, out var pointer)) { int error = Marshal.GetLastWin32Error(); if (error == 1168) return null; throw new Win32Exception(error, "Windows could not read the protected credential."); }
        byte[] bytes = [];
        try { var value = Marshal.PtrToStructure<Credential>(pointer); if (value.BlobSize is 0 or > 2560) throw new InvalidDataException("Unsupported protected credential."); bytes = new byte[value.BlobSize]; Marshal.Copy(value.Blob, bytes, 0, bytes.Length); return new UTF8Encoding(false, true).GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); CredFree(pointer); }
    }
    public void Write(string reference, string secret)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(secret); IntPtr memory = IntPtr.Zero;
        try { if (bytes.Length is < 1 or > 2560) throw new ArgumentException("Credential exceeds Windows storage limits."); memory = Marshal.AllocHGlobal(bytes.Length); Marshal.Copy(bytes, 0, memory, bytes.Length); var value = new Credential { Type = 1, TargetName = Target(reference), BlobSize = (uint)bytes.Length, Blob = memory, Persist = 2, UserName = "NetStucked" }; if (!CredWrite(ref value, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not save the protected credential."); }
        finally { CryptographicOperations.ZeroMemory(bytes); if (memory != IntPtr.Zero) { Marshal.Copy(bytes, 0, memory, bytes.Length); Marshal.FreeHGlobal(memory); } }
    }
    public void Delete(string reference) { if (!CredDelete(Target(reference), 1, 0) && Marshal.GetLastWin32Error() != 1168) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not remove the protected credential."); }
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
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr pointer);
}
