using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

public static class CoverageWorkbook
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private sealed record Sheet(string Name, string[] Headers, IEnumerable<string[]> Rows);
    public static Task WriteAsync(string path, CoverageRun run, CancellationToken token) => Task.Run(() =>
    {
        if (!Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Use an .xlsx destination.");
        if (Path.GetFullPath(path).Equals(Path.GetFullPath(run.Input.Path), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Never overwrite the imported source. Choose a new output filename.");
        if (File.Exists(path)) throw new IOException("Output already exists. Choose a new filename; no existing file was overwritten.");
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var sheets = Build(run).ToArray(); long characters = 0; int totalRows = 0;
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                void Xml(string name, XDocument doc)
                {
                    token.ThrowIfCancellationRequested(); using var stream = zip.CreateEntry(name, CompressionLevel.Fastest).Open(); using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CheckCharacters = true }); doc.Save(writer);
                }
                XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types", rel = "http://schemas.openxmlformats.org/package/2006/relationships", docRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                Xml("[Content_Types].xml", new(new XElement(types + "Types", new XElement(types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(types + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")), new XElement(types + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")), new XElement(types + "Override", new XAttribute("PartName", "/xl/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")), sheets.Select((_, i) => new XElement(types + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))))));
                Xml("_rels/.rels", new(new XElement(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", docRel.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml")))));
                Xml("xl/workbook.xml", new(new XElement(Ns + "workbook", new XAttribute(XNamespace.Xmlns + "r", docRel), new XElement(Ns + "sheets", sheets.Select((s, i) => new XElement(Ns + "sheet", new XAttribute("name", s.Name), new XAttribute("sheetId", i + 1), new XAttribute(docRel + "id", "rId" + (i + 1))))))));
                Xml("xl/_rels/workbook.xml.rels", new(new XElement(rel + "Relationships", sheets.Select((_, i) => new XElement(rel + "Relationship", new XAttribute("Id", "rId" + (i + 1)), new XAttribute("Type", docRel.NamespaceName + "/worksheet"), new XAttribute("Target", $"worksheets/sheet{i + 1}.xml"))), new XElement(rel + "Relationship", new XAttribute("Id", "styles"), new XAttribute("Type", docRel.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml")))));
                Xml("xl/styles.xml", XDocument.Parse("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"2\"><xf fontId=\"0\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf><xf fontId=\"1\" applyFont=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>"));
                for (int i = 0; i < sheets.Length; i++)
                {
                    var sheet = sheets[i]; int rowNumber = 0; var rows = new List<XElement>();
                    foreach (var values in new[] { sheet.Headers }.Concat(sheet.Rows))
                    {
                        token.ThrowIfCancellationRequested(); if (++totalRows > 300000 || values.Length != sheet.Headers.Length) throw new ArgumentException("Export exceeds 300,000 total rows or has inconsistent columns. Split the report.");
                        rowNumber++;
                        rows.Add(new XElement(Ns + "row", new XAttribute("r", rowNumber), values.Select((v, c) =>
                        {
                            characters += v.Length; if (v.Length > 32767 || characters > 32 * 1024 * 1024) throw new ArgumentException("Export text exceeds Excel cell/report limits. No partial workbook was saved.");
                            return new XElement(Ns + "c", new XAttribute("r", Column(c) + rowNumber), new XAttribute("t", "inlineStr"), new XAttribute("s", rowNumber == 1 ? "1" : "0"), new XElement(Ns + "is", new XElement(Ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), Clean(v))));
                        })));
                    }
                    var sheetXml = new XElement(Ns + "worksheet", new XElement(Ns + "sheetViews", new XElement(Ns + "sheetView", new XAttribute("workbookViewId", "0"), new XElement(Ns + "pane", new XAttribute("ySplit", "1"), new XAttribute("topLeftCell", "A2"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))), new XElement(Ns + "cols", sheet.Headers.Select((_, c) => new XElement(Ns + "col", new XAttribute("min", c + 1), new XAttribute("max", c + 1), new XAttribute("width", c == 0 ? 24 : 32), new XAttribute("customWidth", "1")))), new XElement(Ns + "sheetData", rows), new XElement(Ns + "autoFilter", new XAttribute("ref", $"A1:{Column(sheet.Headers.Length - 1)}{rowNumber}")));
                    Xml($"xl/worksheets/sheet{i + 1}.xml", new(sheetXml));
                }
            }
            token.ThrowIfCancellationRequested(); File.Move(temp, path, false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }, token);
    private static string Clean(string value) => XmlConvert.VerifyXmlChars(value);
    private static string Column(int c) { string text = ""; do { text = (char)('A' + c % 26) + text; c = c / 26 - 1; } while (c >= 0); return text; }
    private static string Time(DateTimeOffset? date) => date?.ToString("O", CultureInfo.InvariantCulture) ?? "Unknown";
    private static IEnumerable<Sheet> Build(CoverageRun run)
    {
        var input = run.Input; var results = run.Results.ToDictionary(r => (r.Cve, r.Vendor));
        var cveLinks = input.Associations.ToLookup(a => a.Cve, a => a.Row);
        var rowLinks = input.Associations.ToLookup(a => a.Row, a => a.Cve);
        List<string[]> summary = [
            ["Scope", "Signature/catalog coverage only; no deployed protection, exploit or applicability verdict", ""],
            ["Run ID", run.Id.ToString(), run.Partial ? "PARTIAL — not all jobs completed" : "Complete (Unknown remains unresolved)"],
            ["Source", Path.GetFileName(input.Path), input.Sha256], ["Sheet / header", input.Sheet, input.HeaderRow.ToString(CultureInfo.InvariantCulture)],
            ["Imported", Time(input.ImportedAt), ""], ["Run started / finished", Time(run.StartedAt), Time(run.FinishedAt)],
            ["Source rows / assets / plugins", $"{input.Rows.Length} / {input.AssetCount} / {input.PluginCount}", "Counts from mapped report columns"],
            ["Unique CVE / associations", $"{input.Cves.Length} / {input.Associations.Length}", "CVE counts do not multiply by asset count"],
            ["Completed / total jobs", $"{run.CompletedJobs} / {run.TotalJobs}", "Logical provider-CVE outcomes, not HTTP request count"],
            ["Import issues", input.Issues.Length.ToString(CultureInfo.InvariantCulture), "See Import Issues sheet"] ];
        foreach (var vendor in Enum.GetValues<IpsVendor>())
        {
            foreach (var state in Enum.GetValues<IpsCoverageState>()) summary.Add([CoverageText.Vendor(vendor), CoverageText.State(state), run.Results.Count(r => r.Vendor == vendor && r.State == state).ToString(CultureInfo.InvariantCulture)]);
            foreach (var r in run.Results.Where(r => r.Vendor == vendor).DistinctBy(r => (r.SourceId, r.CheckedAt, r.SourceDate, r.FromCache, r.Stale))) summary.Add([CoverageText.Vendor(vendor) + " source / freshness", $"{r.Source} / {r.Method} / {r.SourceId}", $"Checked {Time(r.CheckedAt)}; source {Time(r.SourceDate)}; cache={r.FromCache}; stale={r.Stale}"]);
        }
        yield return new("Summary", ["Item / vendor", "Value / state", "Detail / count"], summary);
        string[] ResultFields(IpsCoverageResult r) => [r.Status, r.Reason, r.Source, r.Method, r.SourceId, Time(r.CheckedAt), Time(r.SourceDate), r.FromCache.ToString(), r.Stale.ToString(), Time(r.AttemptedAt), r.Failure, string.Join("; ", r.Signatures.Select(s => s.Id.Length > 0 ? s.Id : s.Name)), r.Previous is { } old ? $"Previous {old.Status}; checked {Time(old.CheckedAt)}; source {Time(old.SourceDate)}; stale cached evidence retained in Signatures" : ""];
        string[] resultHeaders = ["Result", "Reason", "Source", "Method", "Source ID", "Checked UTC", "Source date", "From cache", "Stale", "Latest attempt UTC", "Failure", "Signature references", "Previous evidence"];
        yield return new("CVE Details", new[] { "CVE", "Original rows" }.Concat(resultHeaders.Select(h => "PA." + h)).Concat(resultHeaders.Select(h => "CP." + h)).ToArray(), input.Cves.Select(c => new[] { c, string.Join(", ", cveLinks[c]) }.Concat(ResultFields(results[(c, IpsVendor.PaloAlto)])).Concat(ResultFields(results[(c, IpsVendor.CheckPoint)])).ToArray()));
        string Group(IEnumerable<string> cves, IpsVendor vendor)
        {
            var values = cves.Select(c => results[(c, vendor)]).ToArray(); int found = values.Count(r => r.IsIpsFound);
            string partial = found > 0 && found < values.Length ? " · รองรับบางส่วน" : "";
            return $"{found}/{values.Length} Found{partial}; " + string.Join("; ", values.GroupBy(v => v.State).Select(g => $"{CoverageText.State(g.Key)}={g.Count()}"));
        }
        yield return new("Findings", new[] { "IPS.Source sheet", "IPS.Source row" }.Concat(input.Headers.Select(h => "Original." + h)).Concat(["IPS.CVE count", "IPS.Palo Alto summary", "IPS.Check Point summary", "IPS.Run ID"]).ToArray(), input.Rows.Select(row =>
        {
            var cves = rowLinks[row.Number].ToArray();
            return new[] { input.Sheet, row.Number.ToString(CultureInfo.InvariantCulture) }.Concat(Enumerable.Range(0, input.Headers.Length).Select(c => input.Field(row, c))).Concat([cves.Length.ToString(CultureInfo.InvariantCulture), Group(cves, IpsVendor.PaloAlto), Group(cves, IpsVendor.CheckPoint), run.Id.ToString()]).ToArray();
        }));
        yield return new("Signatures", ["CVE", "Vendor", "Evidence kind", "Signature ID (may be empty)", "Name", "Type", "Vendor status", "Release", "Content version", "Limitations / references", "Snapshot action (not enforcement)", "Source", "Checked UTC"], run.Results.SelectMany(r => new[] { r }.Concat(r.Previous is { } p ? [p] : [])).SelectMany(r => r.Signatures.Select(s => new[] { r.Cve, CoverageText.Vendor(r.Vendor), r.Stale ? "Stale / previous evidence" : "Result evidence", s.Id, s.Name, s.Type, s.Status, s.Release, s.ContentVersion, s.Limitations, s.SnapshotAction, r.Source, Time(r.CheckedAt) })));
        yield return new("Row CVE Links", ["Source sheet", "Source row", "CVE", "Run ID"], input.Associations.Select(a => new[] { input.Sheet, a.Row.ToString(CultureInfo.InvariantCulture), a.Cve, run.Id.ToString() }));
        yield return new("Import Issues", ["Source row", "Original value", "Reason"], input.Issues.Select(i => new[] { i.Row.ToString(CultureInfo.InvariantCulture), i.Value, i.Reason }));
    }
}
