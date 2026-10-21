using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public sealed record AssetImportRow(int Row, Asset? Asset, string Action, string Error);
public static class InventoryWorkbook
{
    public static readonly string[] Headers = ["AssetId", "Hostname", "Address", "Vendor", "Model", "Serial", "Site", "Location", "Role", "Contract", "MaStart", "MaEnd"];
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static string Col(int x) { string s = ""; do { s = (char)('A' + x % 26) + s; x = x / 26 - 1; } while (x >= 0); return s; }
    private static XElement Row(int number, IEnumerable<string> values) => new(Ns + "row", new XAttribute("r", number), values.Select((v, i) => new XElement(Ns + "c", new XAttribute("r", Col(i) + number), new XAttribute("t", "inlineStr"), new XElement(Ns + "is", new XElement(Ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), v)))));
    public static Task WriteAsync(string path, IReadOnlyList<Asset> assets, CancellationToken token)
    {
        foreach (var a in assets) EngineeringRules.Validate(a);
        return WriteTableAsync(path, "NetStucked Inventory schema 1", Headers, assets.Select(a => new[] { a.Id.ToString(), a.Hostname, a.Address, a.Vendor, a.Model, a.Serial, a.Site, a.Location, a.Role, a.Contract, a.MaStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", a.MaEnd?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "" }).ToArray(), token);
    }
    public static async Task WriteTableAsync(string path, string schema, IReadOnlyList<string> headers, IReadOnlyList<string[]> values, CancellationToken token)
    {
        if (values.Count > 10000 || headers.Count is < 1 or > 40 || values.Any(row => row.Length != headers.Count || row.Any(s => s.Length > 20000)) || values.Sum(row => row.Sum(s => (long)s.Length)) > 4 * 1024 * 1024) throw new ArgumentException("Report table exceeds row/column/text limits. Export a smaller set of records.");
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await Task.Run(() =>
            {
                using var zip = ZipFile.Open(temp, ZipArchiveMode.Create);
                void Entry(string name, string text)
                {
                    if (Encoding.UTF8.GetByteCount(text) > (schema == "NetStucked Inventory schema 1" ? 8 : 32) * 1024 * 1024) throw new ArgumentException("Workbook part exceeds its bounded import/report limit. Export fewer records; existing destination retained.");
                    using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); w.Write(text);
                }
                Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Entry("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Inventory\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Entry("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                var rows = new List<XElement> { Row(1, [schema]), Row(2, headers) };
                int n = 2;
                foreach (var row in values) { token.ThrowIfCancellationRequested(); rows.Add(Row(++n, row)); }
                Entry("xl/worksheets/sheet1.xml", new XDocument(new XElement(Ns + "worksheet", new XElement(Ns + "sheetData", rows))).ToString());
            }, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static async Task<AssetImportRow[]> ReadAsync(string path, IReadOnlyList<Asset> existing, CancellationToken token) => await Task.Run(() =>
    {
        if (new FileInfo(path).Length > 10 * 1024 * 1024) throw new IOException("Workbook exceeds 10 MiB.");
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > 128 || zip.Entries.Select(e => e.FullName).Distinct().Count() != zip.Entries.Count || zip.Entries.Sum(e => e.Length) > 32 * 1024 * 1024 || zip.Entries.Any(e => e.Length > 8 * 1024 * 1024 || e.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || e.FullName.Contains("externalLinks", StringComparison.OrdinalIgnoreCase))) throw new IOException("Workbook exceeds limits or contains duplicate parts/macros/external links.");
        XDocument Xml(string name)
        {
            var entry = zip.GetEntry(name) ?? throw new IOException("Template worksheet is missing.");
            using var reader = XmlReader.Create(entry.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1024 * 1024 });
            return XDocument.Load(reader);
        }
        string[] shared = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Xml("xl/sharedStrings.xml").Descendants(Ns + "si").Select(s => string.Concat(s.Descendants(Ns + "t").Select(t => t.Value))).ToArray();
        bool date1904 = (string?)Xml("xl/workbook.xml").Root?.Element(Ns + "workbookPr")?.Attribute("date1904") is "1" or "true";
        var rows = Xml("xl/worksheets/sheet1.xml").Descendants(Ns + "row").ToArray();
        if (rows.Length is < 2 or > 10002) throw new IOException("Use the versioned template (maximum 10,000 asset rows).");
        string[] Cells(XElement row)
        {
            string[] values = new string[Headers.Length]; Array.Fill(values, "");
            var seenCells = new HashSet<int>();
            foreach (var c in row.Elements(Ns + "c"))
            {
                if (c.Element(Ns + "f") is not null) throw new IOException("Formulas are not allowed; paste values into the template.");
                string reference = (string?)c.Attribute("r") ?? ""; int index = 0;
                foreach (char ch in reference.TakeWhile(char.IsAsciiLetterUpper)) index = checked(index * 26 + ch - 'A' + 1);
                if (index < 1 || !seenCells.Add(index)) throw new IOException("Invalid or duplicate cell reference.");
                if (index > Headers.Length) { if (c.Descendants(Ns + "t").Any(x => x.Value.Length > 0) || c.Element(Ns + "v") is { Value.Length: > 0 }) throw new IOException("Unexpected nonempty column outside template headers."); continue; }
                string value = (string?)c.Element(Ns + "v") ?? "";
                values[index - 1] = (string?)c.Attribute("t") switch { "s" => int.TryParse(value, out int id) && id >= 0 && id < shared.Length ? shared[id] : throw new IOException("Invalid shared string index."), "inlineStr" => string.Concat(c.Descendants(Ns + "t").Select(x => x.Value)), _ => value };
                if (index is 11 or 12 && ((string?)c.Attribute("t") is null or "n") && value.Length > 0 && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial))
                {
                    if (!double.IsFinite(serial) || serial != Math.Floor(serial) || serial < (date1904 ? 0 : 61) || serial > 2900000) throw new IOException("Invalid Excel date serial.");
                    values[index - 1] = (date1904 ? new DateTime(1904, 1, 1).AddDays(serial) : DateTime.FromOADate(serial)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
            }
            return values;
        }
        if (Cells(rows[0])[0] != "NetStucked Inventory schema 1" || !Cells(rows[1]).SequenceEqual(Headers)) throw new IOException("Unsupported schema/headers. Export a fresh template; keep the first two rows.");
        var result = new List<AssetImportRow>(); var identities = new Dictionary<int, Guid>(); var serials = new Dictionary<int, string>();
        for (int i = 2; i < rows.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var v = Cells(rows[i]); if (v.All(string.IsNullOrWhiteSpace)) continue;
                if (v[5].Length > 0) serials[i + 1] = v[5];
                Guid id = v[0].Length == 0 ? Guid.NewGuid() : Guid.Parse(v[0]); identities[i + 1] = id;
                DateOnly? Date(string s) => s.Length == 0 ? null : DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : throw new ArgumentException("Dates must be yyyy-MM-dd text or whole-day Excel date cells.");
                var asset = new Asset(id, v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], Date(v[10]), Date(v[11]), DateTimeOffset.UtcNow); EngineeringRules.Validate(asset);
                if (asset.Serial.Length > 0 && existing.Any(a => a.Id != id && a.Serial.Equals(asset.Serial, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Serial conflicts with another asset; reconcile stable identity first.");
                var old = existing.FirstOrDefault(a => a.Id == id);
                result.Add(new(i + 1, asset, old is null ? "Add" : "Update human fields (explicit selection)", ""));
            }
            catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or IOException) { result.Add(new(i + 1, null, "Rejected", e.Message)); }
        }
        // Reject every member of an ambiguous identity group, including the first row.
        // Otherwise Select valid silently applies a first-wins update from a conflicting workbook.
        var duplicateIds = identities.GroupBy(x => x.Value).Where(g => g.Count() > 1).SelectMany(g => g.Select(x => x.Key)).ToHashSet();
        var duplicateSerials = serials.GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).SelectMany(g => g.Select(x => x.Key)).ToHashSet();
        return result.Select(row =>
        {
            string error = row.Error;
            if (duplicateIds.Contains(row.Row)) error = (error.Length > 0 ? error + " " : "") + "Duplicate AssetId in workbook; all matching rows rejected.";
            if (duplicateSerials.Contains(row.Row)) error = (error.Length > 0 ? error + " " : "") + "Serial conflicts with another workbook row; all matching rows rejected.";
            return error.Length > 0 ? row with { Asset = null, Action = "Rejected", Error = error } : row;
        }).ToArray();
    }, token).ConfigureAwait(false);
}
