using NetStucked.Infrastructure;
using Xunit;
namespace NetStucked.Tests;
public sealed class ProtectedSecretTests
{
    [Fact] public void RealWindowsToolVaultRoundTripsAndSeparatesDataProfiles()
    {
        var a=new WindowsSecretVault(new UserSettingsStore(Path.Combine(Path.GetTempPath(),"NetStucked-owned-vault-"+Guid.NewGuid())));
        var b=new WindowsSecretVault(new UserSettingsStore(Path.Combine(Path.GetTempPath(),"NetStucked-owned-vault-"+Guid.NewGuid())));
        const string reference="owned-qa-entry"; const string secret="Owned QA temporary secret";
        try {Assert.Null(a.Read(reference));a.Write(reference,secret);Assert.Equal(secret,a.Read(reference));Assert.Null(b.Read(reference));a.Delete(reference);Assert.Null(a.Read(reference));Assert.Throws<ArgumentException>(()=>a.Write("../unsafe",secret));}
        finally {a.Delete(reference);}
    }
}
