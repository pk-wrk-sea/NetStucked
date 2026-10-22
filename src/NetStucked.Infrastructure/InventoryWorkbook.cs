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
    public static readonly string[] LegacyHeaders = ["AssetId", "Hostname", "Address", "Vendor", "Model", "Serial", "Site", "Location", "Role", "Contract", "MaStart", "MaEnd"];
    public static readonly string[] Headers = ["No", "Company", "Location", "Province", "Building", "Floor", "Room", "Rack", "Hostname", "IP Address", "Brand", "Partnumber", "SerialNumber", "Device Type", "MA Type", "SLA Type", "Remark", "Remark2", "LDoS", "AssetId", "Site", "Contract", "MaStart", "MaEnd"];
    private static string DateText(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
    private static string[] Values(Asset a) => [a.Number, a.Company, a.Location, a.Province, a.Building, a.Floor, a.Room, a.Rack, a.Hostname, a.Address, a.Vendor, a.Model, a.Serial, a.Role, a.MaType, a.SlaType, a.Remark, a.Remark2, DateText(a.Ldos), a.Id.ToString(), a.Site, a.Contract, DateText(a.MaStart), DateText(a.MaEnd)];
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static string Col(int x) { string s = ""; do { s = (char)('A' + x % 26) + s; x = x / 26 - 1; } while (x >= 0); return s; }
    private static XElement Row(int number, IEnumerable<string> values) => new(Ns + "row", new XAttribute("r", number), values.Select((v, i) => new XElement(Ns + "c", new XAttribute("r", Col(i) + number), new XAttribute("t", "inlineStr"), new XElement(Ns + "is", new XElement(Ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), v)))));
    public static Task WriteAsync(string path, IReadOnlyList<Asset> assets, CancellationToken token)
    {
        foreach (var a in assets) EngineeringRules.Validate(a);
        return WriteTableAsync(path, "NetStucked Inventory schema 2", Headers, assets.Select(Values).ToArray(), token);
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
                    if (schema == "NetStucked Inventory schema 2" && name == "[Content_Types].xml") text = text.Replace("</Types>", "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>", StringComparison.Ordinal);
                    if (schema == "NetStucked Inventory schema 2" && name == "xl/_rels/workbook.xml.rels") text = text.Replace("</Relationships>", "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>", StringComparison.Ordinal);
                    if (Encoding.UTF8.GetByteCount(text) > (schema.StartsWith("NetStucked Inventory schema ", StringComparison.Ordinal) ? 8 : 32) * 1024 * 1024) throw new ArgumentException("Workbook part exceeds its bounded import/report limit. Export fewer records; existing destination retained.");
                    using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); w.Write(text);
                }
                Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Entry("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Inventory\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Entry("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
                var rows = new List<XElement> { Row(1, [schema]), Row(2, headers) };
                int n = 2;
                foreach (var row in values) { token.ThrowIfCancellationRequested(); rows.Add(Row(++n, row)); }
                var worksheet = new XElement(Ns + "worksheet");
                if (schema == "NetStucked Inventory schema 2")
                {
                    Entry("xl/styles.xml", "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFC6EFD8\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
                    foreach (var cell in rows[1].Elements()) cell.SetAttributeValue("s", "1");
                    rows[0].SetAttributeValue("hidden", "1");
                    worksheet.Add(new XElement(Ns + "sheetViews", new XElement(Ns + "sheetView", new XAttribute("workbookViewId", "0"), new XElement(Ns + "pane", new XAttribute("ySplit", "2"), new XAttribute("topLeftCell", "A3"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))));
                    worksheet.Add(new XElement(Ns + "cols", headers.Select((_, i) => new XElement(Ns + "col", new XAttribute("min", i + 1), new XAttribute("max", i + 1), new XAttribute("width", i == 8 ? 36 : i is 16 or 17 ? 32 : 18), new XAttribute("customWidth", "1"), i >= 19 ? new XAttribute("hidden", "1") : null))));
                }
                worksheet.Add(new XElement(Ns + "sheetData", rows));
                if (schema == "NetStucked Inventory schema 2") worksheet.Add(new XElement(Ns + "autoFilter", new XAttribute("ref", $"A2:S{Math.Max(3, n)}")));
                Entry("xl/worksheets/sheet1.xml", new XDocument(worksheet).ToString());
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
        if (rows.Length is < 1 or > 10002) throw new IOException("Use a header row and at most 10,000 asset rows.");
        string[] Cells(XElement row)
        {
            string[] values = new string[40]; Array.Fill(values, "");
            var seenCells = new HashSet<int>();
            foreach (var c in row.Elements(Ns + "c"))
            {
                if (c.Element(Ns + "f") is not null) throw new IOException("Formulas are not allowed; paste values into the template.");
                string reference = (string?)c.Attribute("r") ?? ""; int index = 0;
                foreach (char ch in reference.TakeWhile(char.IsAsciiLetterUpper)) index = checked(index * 26 + ch - 'A' + 1);
                if (index < 1 || !seenCells.Add(index)) throw new IOException("Invalid or duplicate cell reference.");
                if (index > values.Length) { if (c.Descendants(Ns + "t").Any(x => x.Value.Length > 0) || c.Element(Ns + "v") is { Value.Length: > 0 }) throw new IOException("Unexpected nonempty column beyond the 40-column limit."); continue; }
                string value = (string?)c.Element(Ns + "v") ?? "";
                values[index - 1] = (string?)c.Attribute("t") switch { "s" => int.TryParse(value, out int id) && id >= 0 && id < shared.Length ? shared[id] : throw new IOException("Invalid shared string index."), "inlineStr" => string.Concat(c.Descendants(Ns + "t").Select(x => x.Value)), _ => value };
            }
            return values;
        }
        string marker = Cells(rows[0])[0];
        bool versioned = marker.StartsWith("NetStucked Inventory schema ", StringComparison.Ordinal);
        if (versioned && marker is not ("NetStucked Inventory schema 1" or "NetStucked Inventory schema 2")) throw new IOException("Unsupported inventory schema.");
        int headerRow = versioned ? 1 : 0;
        if (rows.Length <= headerRow) throw new IOException("Inventory headers are missing.");
        static string Key(string s) => string.Concat(s.Where(char.IsLetterOrDigit)).ToLowerInvariant() switch
        { "address" => "ipaddress", "vendor" => "brand", "model" => "partnumber", "serial" => "serialnumber", "role" => "devicetype", var k => k };
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        var header = Cells(rows[headerRow]);
        var known = Headers.Concat(LegacyHeaders).Select(Key).ToHashSet();
        for (int c = 0; c < header.Length; c++)
        {
            if (string.IsNullOrWhiteSpace(header[c])) continue;
            string k = Key(header[c]);
            if (!known.Contains(k) || !columns.TryAdd(k, c)) throw new IOException("Unknown or duplicate inventory header: " + header[c]);
        }
        if (columns.Count == 0) throw new IOException("No recognized inventory headers. Export a fresh template.");
        DateOnly? Date(string s)
        {
            s = s.Trim(); if (s.Length == 0) return null;
            if (DateOnly.TryParseExact(s, ["yyyy-MM-dd", "dd-MMM-yyyy", "d-MMM-yyyy", "d/M/yyyy", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial) && double.IsFinite(serial) && serial == Math.Floor(serial) && serial >= (date1904 ? 0 : 61) && serial <= 2900000)
                return DateOnly.FromDateTime(date1904 ? new DateTime(1904, 1, 1).AddDays(serial) : DateTime.FromOADate(serial));
            throw new ArgumentException("Invalid Excel date or date text. Use yyyy-MM-dd, dd-MMM-yyyy, day/month/year or whole-day Excel date cells; empty is allowed.");
        }
        var result = new List<AssetImportRow>(); var identities = new Dictionary<int, Guid>(); var serials = new Dictionary<int, string>();
        for (int i = headerRow + 1; i < rows.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var v = Cells(rows[i]); if (v.All(string.IsNullOrWhiteSpace)) continue;
                if (v.Where((value, index) => !string.IsNullOrWhiteSpace(value) && !columns.Values.Contains(index)).Any()) throw new ArgumentException("Nonempty data has no recognized column header.");
                var previous = new Dictionary<string, string>();
                string Field(string name) => columns.TryGetValue(Key(name), out int col) ? v[col].Trim() : previous.GetValueOrDefault(Key(name), "");
                if (Field("SerialNumber").Length > 0) serials[i + 1] = Field("SerialNumber");
                bool matchSerial = Field("AssetId").Length == 0 && Field("SerialNumber").Length > 0;
                var serialMatches = matchSerial ? existing.Where(a => a.Serial.Equals(Field("SerialNumber"), StringComparison.OrdinalIgnoreCase)).ToArray() : [];
                if (serialMatches.Length > 1) throw new ArgumentException("Serial matches multiple existing assets; retain the exported AssetId to identify the row.");
                Guid id = Field("AssetId").Length == 0 ? serialMatches.FirstOrDefault()?.Id ?? Guid.NewGuid() : Guid.Parse(Field("AssetId")); identities[i + 1] = id;
                var old = existing.FirstOrDefault(a => a.Id == id);
                if (old is not null) previous = Headers.Zip(Values(old)).ToDictionary(pair => Key(pair.First), pair => pair.Second);
                var asset = new Asset(id, Field("Hostname"), Field("IP Address"), Field("Brand"), Field("Partnumber"), Field("SerialNumber"), Field("Site"), Field("Location"), Field("Device Type"), Field("Contract"), Date(Field("MaStart")), Date(Field("MaEnd")), DateTimeOffset.UtcNow)
                { Number = Field("No"), Company = Field("Company"), Province = Field("Province"), Building = Field("Building"), Floor = Field("Floor"), Room = Field("Room"), Rack = Field("Rack"), MaType = Field("MA Type"), SlaType = Field("SLA Type"), Remark = Field("Remark"), Remark2 = Field("Remark2"), Ldos = Date(Field("LDoS")) };
                EngineeringRules.Validate(asset);
                if (asset.Serial.Length > 0 && existing.Any(a => a.Id != id && a.Serial.Equals(asset.Serial, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Serial conflicts with another asset; reconcile stable identity first.");
                result.Add(new(i + 1, asset, old is null ? "Add" : serialMatches.Length == 1 ? "Update by unique Serial (review)" : "Update human fields (explicit selection)", ""));
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
