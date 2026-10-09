using System.Text;
using System.Xml;
using System.Xml.Linq;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

/// <summary>Only Windows stores WLAN keys. No plaintext retrieval flag is ever used.</summary>
public static class WifiProfileXml
{
    private static readonly XNamespace Wlan = "http://www.microsoft.com/networking/WLAN/profile/v1";
    public static XDocument Parse(string xml)
    {
        if (xml.Length > 1_000_000) throw new ArgumentException("WLAN profile exceeds 1 MB.");
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000 });
        var doc = XDocument.Load(reader);
        if (doc.Root?.Name != Wlan + "WLANProfile") throw new ArgumentException("Expected a Windows WLAN profile XML document.");
        return doc;
    }
    public static WifiProfile Describe(string xml, uint flags, int priority)
    {
        var root = Parse(xml).Root!;
        string Value(string name) => root.Descendants(Wlan + name).FirstOrDefault()?.Value ?? "";
        string name = root.Element(Wlan + "name")?.Value ?? "";
        if (name.Length is < 1 or > 255) throw new ArgumentException("Invalid WLAN profile name.");
        var ssid = root.Element(Wlan + "SSIDConfig")?.Element(Wlan + "SSID");
        string hex = ssid?.Element(Wlan + "hex")?.Value.ToUpperInvariant() ?? Convert.ToHexString(Encoding.UTF8.GetBytes(ssid?.Element(Wlan + "name")?.Value ?? ""));
        if (hex.Length is < 2 or > 64 || hex.Length % 2 != 0 || !hex.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid SSID bytes.");
        string ssidName = ssid?.Element(Wlan + "name")?.Value ?? Encoding.UTF8.GetString(Convert.FromHexString(hex));
        string auth = Value("authentication");
        string security = auth switch { "WPA2PSK" => "WPA2-Personal", "WPA3SAE" => "WPA3-Personal", "WPA2" => "WPA2-Enterprise", "WPA3ENT" or "WPA3ENT192" => "WPA3-Enterprise", "open" => "Open", _ => auth };
        var types = root.Descendants().Where(e => e.Name.LocalName == "Type").Select(e => e.Value).ToHashSet();
        string eap = types.Contains("25") ? (types.Contains("26") ? "PEAP / MSCHAPv2" : "PEAP (Windows-managed)") : types.Contains("13") ? "EAP-TLS (certificate)" : Value("useOneX") == "true" ? "802.1X (Windows-managed)" : "—";
        return new(name, ssidName, hex, security, eap, Value("connectionMode") == "auto", (flags & 1) != 0, priority, xml) { PerUser = (flags & 2) != 0 };
    }
    public static string Personal(WifiProfileInput input)
    {
        byte[] ssid = Encoding.UTF8.GetBytes(input.Ssid);
        if (input.Name.Length is < 1 or > 255 || ssid.Length is < 1 or > 32 || input.Name.Contains('\0') || input.Ssid.Contains('\0')) throw new ArgumentException("Profile name 1–255 characters; SSID 1–32 UTF-8 bytes.");
        bool open = input.Security == "Open";
        if (input.Security is not ("Open" or "WPA2-Personal" or "WPA3-Personal")) throw new ArgumentException("Import IT-provisioned XML for Enterprise profiles.");
        bool raw = input.Password.Length == 64 && input.Password.All(Uri.IsHexDigit) && input.Security == "WPA2-Personal";
        if (!open && !raw && (input.Password.Length is < 8 or > 63 || input.Password.Any(c => c < 32 || c > 126))) throw new ArgumentException("Use an 8–63 ASCII character passphrase (WPA2 also supports a 64-digit hexadecimal key).");
        var security = new XElement(Wlan + "security", new XElement(Wlan + "authEncryption", new XElement(Wlan + "authentication", open ? "open" : input.Security == "WPA3-Personal" ? "WPA3SAE" : "WPA2PSK"), new XElement(Wlan + "encryption", open ? "none" : "AES"), new XElement(Wlan + "useOneX", "false")));
        if (!open) security.Add(new XElement(Wlan + "sharedKey", new XElement(Wlan + "keyType", raw ? "networkKey" : "passPhrase"), new XElement(Wlan + "protected", "false"), new XElement(Wlan + "keyMaterial", input.Password)));
        return new XDocument(new XElement(Wlan + "WLANProfile", new XElement(Wlan + "name", input.Name), new XElement(Wlan + "SSIDConfig", new XElement(Wlan + "SSID", new XElement(Wlan + "hex", Convert.ToHexString(ssid)), new XElement(Wlan + "name", input.Ssid))), new XElement(Wlan + "connectionType", "ESS"), new XElement(Wlan + "connectionMode", input.AutoConnect ? "auto" : "manual"), new XElement(Wlan + "MSM", security))).ToString();
    }
    public static string ExportSafe(string xml)
    {
        var doc = Parse(xml);
        // EAP configuration contains trust policy; user credentials are never portable.
        var secretNames = new HashSet<string>(["sharedKey", "EapHostUserCredentials", "EapUserData", "Password", "Username", "UserCert", "keyMaterial", "RoutingIdentity"], StringComparer.OrdinalIgnoreCase);
        doc.Descendants().Where(e => secretNames.Contains(e.Name.LocalName)).ToList().ForEach(e => e.Remove());
        return doc.ToString();
    }
    public static string SetAutoConnect(string xml, bool auto)
    {
        var doc = Parse(xml); var mode = doc.Root!.Element(Wlan + "connectionMode");
        if (mode is null) throw new ArgumentException("WLAN connection mode is missing.");
        mode.Value = auto ? "auto" : "manual"; return doc.ToString();
    }
    public static string ReplacePersonalKey(string xml, string password)
    {
        var profile = Describe(xml, 0, 1);
        if (!profile.Security.EndsWith("Personal", StringComparison.Ordinal)) throw new ArgumentException("This profile does not use a Personal key.");
        var key = Parse(Personal(new(profile.Name, "validation", profile.Security, profile.AutoConnect, "", password))).Descendants(Wlan + "sharedKey").Single();
        var document = Parse(xml); var security = document.Root!.Element(Wlan + "MSM")?.Element(Wlan + "security") ?? throw new ArgumentException("Missing WLAN security configuration.");
        var existing = security.Element(Wlan + "sharedKey"); if (existing is null) security.Add(key); else existing.ReplaceWith(key);
        return document.ToString();
    }
    public static string PeapCredentials(EnterpriseCredentials credentials)
    {
        XNamespace host = "http://www.microsoft.com/provisioning/EapHostUserCredentials", common = "http://www.microsoft.com/provisioning/EapCommon", baseUser = "http://www.microsoft.com/provisioning/BaseEapUserPropertiesV1", peap = "http://www.microsoft.com/provisioning/MsPeapUserPropertiesV1", chap = "http://www.microsoft.com/provisioning/MsChapV2UserPropertiesV1";
        if (credentials.Username.Length is < 1 or > 256 || credentials.Password.Length is < 1 or > 256 || credentials.Domain.Length > 256) throw new ArgumentException("Invalid Enterprise credential lengths.");
        return new XDocument(new XElement(host + "EapHostUserCredentials", new XElement(host + "EapMethod", new XElement(common + "Type", 25), new XElement(common + "AuthorId", 0)),
            new XElement(host + "Credentials", new XElement(baseUser + "Eap", new XElement(baseUser + "Type", 25), new XElement(peap + "EapType", new XElement(peap + "RoutingIdentity", credentials.Username), new XElement(baseUser + "Eap", new XElement(baseUser + "Type", 26), new XElement(chap + "EapType", new XElement(chap + "Username", credentials.Username), new XElement(chap + "Password", credentials.Password), new XElement(chap + "LogonDomain", credentials.Domain)))))))).ToString();
    }
}
