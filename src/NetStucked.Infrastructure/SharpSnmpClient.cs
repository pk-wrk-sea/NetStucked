using System.Globalization;
using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using NetStucked.Core;
namespace NetStucked.Infrastructure;

public sealed class SharpSnmpClient : ISnmpClient, IDisposable
{
    private readonly SemaphoreSlim _admission = new(1);
    private long _nextSend;
    private async Task AdmitAsync(CancellationToken token)
    {
        await _admission.WaitAsync(token);
        try { long wait = _nextSend-Environment.TickCount64; if (wait>0) await Task.Delay(TimeSpan.FromMilliseconds(wait),token); _nextSend=Environment.TickCount64+16; }
        finally { _admission.Release(); }
    }
    public async Task<SnmpSnapshot> ReadAsync(string host, SnmpSettings settings, SnmpSecrets secrets, int timeoutMs, bool discover, CancellationToken token)
    {
        settings.Validate();
        if (timeoutMs is < 500 or > 10000) throw new ArgumentException("Invalid SNMP timeout.");
        if (settings.Version != SnmpVersion.V3 && string.IsNullOrEmpty(secrets.Community)) return Failure("Not configured", "SNMP community is not configured.");
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(token); overall.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, overall.Token); var endpoint = new IPEndPoint(addresses.First(), settings.Port);
            var version = settings.Version switch { SnmpVersion.V1 => VersionCode.V1, SnmpVersion.V2c => VersionCode.V2, _ => VersionCode.V3 };
            IPrivacyProvider? privacy = null; ISnmpMessage? report = null;
            if (version == VersionCode.V3)
            {
                privacy = Privacy(settings, secrets);
                using var discoveryTimeout = CancellationTokenSource.CreateLinkedTokenSource(overall.Token); discoveryTimeout.CancelAfter(timeoutMs);
                await AdmitAsync(discoveryTimeout.Token);
                report = await new Discovery(Messenger.NextMessageId, Messenger.NextRequestId, 65507).GetResponseAsync(endpoint, discoveryTimeout.Token);
            }
            async Task<IList<Variable>> Query(IList<Variable> variables, bool next = false)
            {
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(overall.Token); requestTimeout.CancelAfter(timeoutMs);
                ISnmpMessage request = version == VersionCode.V3
                    ? next ? new GetNextRequestMessage(version, Messenger.NextMessageId, Messenger.NextRequestId, new OctetString(settings.Username), new OctetString(""), variables, privacy!, 65507, report!) : new GetRequestMessage(version, Messenger.NextMessageId, Messenger.NextRequestId, new OctetString(settings.Username), new OctetString(""), variables, privacy!, 65507, report!)
                    : next ? new GetNextRequestMessage(Messenger.NextRequestId, version, new OctetString(secrets.Community), variables) : new GetRequestMessage(Messenger.NextRequestId, version, new OctetString(secrets.Community), variables);
                await AdmitAsync(requestTimeout.Token);
                ISnmpMessage response = await request.GetResponseAsync(endpoint, requestTimeout.Token);
                if (response.TypeCode() == SnmpType.ReportPdu) throw new InvalidDataException("SNMP authentication/engine report received.");
                if (response.Pdu().ErrorStatus.ToInt32() != 0) throw new InvalidDataException("SNMP agent rejected a requested OID.");
                return response.Pdu().Variables;
            }
            var values = new Dictionary<string, ISnmpData>(StringComparer.Ordinal);
            async Task Read(IEnumerable<string> oids)
            {
                foreach (var batch in oids.Distinct().Chunk(16)) foreach (var variable in await Query(batch.Select(o => new Variable(new ObjectIdentifier(o))).ToList())) values[variable.Id.ToString().TrimStart('.')] = variable.Data;
            }
            await Read(settings.SystemInfo ? ["1.3.6.1.2.1.1.1.0", "1.3.6.1.2.1.1.3.0", "1.3.6.1.2.1.1.5.0"] : ["1.3.6.1.2.1.1.3.0"]);
            var indexes = settings.InterfaceIndexes.ToList();
            if (discover && settings.Interfaces)
            {
                indexes.Clear(); const string root = "1.3.6.1.2.1.2.2.1.2"; string current = root;
                for (int count = 0; count < 128; count++)
                {
                    var result = (await Query([new Variable(new ObjectIdentifier(current))], true)).FirstOrDefault(); if (result is null) break;
                    string oid = result.Id.ToString().TrimStart('.');
                    if (!oid.StartsWith(root + ".", StringComparison.Ordinal) || string.CompareOrdinal(oid, current) == 0 || result.Data.TypeCode is SnmpType.EndOfMibView or SnmpType.NoSuchInstance or SnmpType.NoSuchObject) break;
                    if (!int.TryParse(oid[(root.Length + 1)..], out int index) || index < 1 || indexes.Contains(index)) break;
                    indexes.Add(index); values[oid] = result.Data; current = oid;
                }
            }
            var rows = new List<SnmpInterface>();
            if (settings.Interfaces)
            {
                foreach (int index in indexes.Take(discover ? 128 : 32))
                {
                    string Standard(int column) => $"1.3.6.1.2.1.2.2.1.{column}.{index}";
                    string Extended(int column) => $"1.3.6.1.2.1.31.1.1.1.{column}.{index}";
                    var oids = new List<string> { Standard(2), Standard(5), Standard(7), Standard(8), Standard(9) };
                    if (settings.Bandwidth) { oids.Add(Standard(10)); oids.Add(Standard(16)); if (version != VersionCode.V1) oids.AddRange([Extended(6), Extended(10), Extended(15), Extended(19)]); }
                    if (settings.Errors) oids.AddRange([Standard(13), Standard(19), Standard(14), Standard(20)]);
                    await Read(oids);
                    ulong? Number(string oid) => Numeric(values.GetValueOrDefault(oid));
                    bool high = version != VersionCode.V1 && Number(Extended(6)) is not null && Number(Extended(10)) is not null;
                    ulong? speed = Number(Extended(15)) is > 0 ? Number(Extended(15)) * 1_000_000UL : Number(Standard(5));
                    rows.Add(new(index, Text(Standard(2)), (int?)Number(Standard(7)), (int?)Number(Standard(8)), speed, settings.Bandwidth ? Number(high ? Extended(6) : Standard(10)) : null, settings.Bandwidth ? Number(high ? Extended(10) : Standard(16)) : null, settings.Errors ? Number(Standard(14)) : null, settings.Errors ? Number(Standard(20)) : null, settings.Errors ? Number(Standard(13)) : null, settings.Errors ? Number(Standard(19)) : null, Number(Standard(9)), Number(Extended(19)), high));
                }
            }
            await Read(settings.CustomOids);
            return new(DateTimeOffset.UtcNow, "Success", Text("1.3.6.1.2.1.1.5.0"), Text("1.3.6.1.2.1.1.1.0"), Numeric(values.GetValueOrDefault("1.3.6.1.2.1.1.3.0")), rows, settings.CustomOids.ToDictionary(o => o, o => Text(o)), discover && indexes.Count == 128 ? "Interface discovery capped at 128; select at most 32 to poll." : "Read-only SNMP responses");
            string Text(string oid) => values.TryGetValue(oid, out var value) && value.TypeCode is not (SnmpType.NoSuchObject or SnmpType.NoSuchInstance or SnmpType.EndOfMibView) ? value.ToString().Truncate(1024) : "";
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Failure("Timeout", "No valid SNMP response before the bounded deadline; device availability is evaluated separately."); }
        catch (Exception ex) when (ex is SnmpException or System.Net.Sockets.SocketException or InvalidDataException or ArgumentException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
        { return Failure("SNMP error", "Agent/credential/OID response could not be verified. Check version, credentials and device access; no values inferred."); }
        SnmpSnapshot Failure(string status, string details) => new(DateTimeOffset.UtcNow, status, "", "", null, [], new Dictionary<string, string>(), details);
    }
    private static ulong? Numeric(ISnmpData? value) => value switch { Counter64 c => c.ToUInt64(), Counter32 c => c.ToUInt32(), Gauge32 g => g.ToUInt32(), TimeTicks t => t.ToUInt32(), Integer32 i when i.ToInt32() >= 0 => (ulong)i.ToInt32(), _ => null };
    // Legacy algorithms are explicit compatibility choices for older Cisco agents; SHA256/AES128 is the default.
#pragma warning disable CS0618
    public static IPrivacyProvider Privacy(SnmpSettings settings, SnmpSecrets secrets)
    {
        if (settings.Authentication != SnmpAuth.None && secrets.AuthenticationPassword.Length < 8 || settings.Privacy != SnmpPrivacy.None && secrets.PrivacyPassword.Length < 8) throw new ArgumentException("SNMP v3 passwords must be at least 8 characters for selected security levels.");
        IAuthenticationProvider auth = settings.Authentication switch { SnmpAuth.None => DefaultAuthenticationProvider.Instance, SnmpAuth.MD5 => new MD5AuthenticationProvider(new(secrets.AuthenticationPassword)), SnmpAuth.SHA1 => new SHA1AuthenticationProvider(new(secrets.AuthenticationPassword)), SnmpAuth.SHA256 => new SHA256AuthenticationProvider(new(secrets.AuthenticationPassword)), SnmpAuth.SHA384 => new SHA384AuthenticationProvider(new(secrets.AuthenticationPassword)), SnmpAuth.SHA512 => new SHA512AuthenticationProvider(new(secrets.AuthenticationPassword)), _ => throw new ArgumentException("Unsupported SNMP authentication.") };
        return settings.Privacy switch { SnmpPrivacy.None => new DefaultPrivacyProvider(auth), SnmpPrivacy.DES => new DESPrivacyProvider(new(secrets.PrivacyPassword), auth), SnmpPrivacy.AES128 => new AESPrivacyProvider(new(secrets.PrivacyPassword), auth), SnmpPrivacy.AES192 => new AES192PrivacyProvider(new(secrets.PrivacyPassword), auth), SnmpPrivacy.AES256 => new AES256PrivacyProvider(new(secrets.PrivacyPassword), auth), _ => throw new ArgumentException("Unsupported SNMP privacy.") };
    }
#pragma warning restore CS0618
    public void Dispose() => _admission.Dispose();
}
