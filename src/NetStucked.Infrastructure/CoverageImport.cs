using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NetStucked.Core;

namespace NetStucked.Infrastructure;

/// <summary>Bounded, value-only CSV/OpenXML import. No Office automation, macros or formula evaluation.</summary>
public static class CoverageImport
{
    private const int MaxRows = 10100, MaxColumns = 256, MaxText = 32767;
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static Task<CoverageFile> ReadAsync(string path, CancellationToken token) => Task.Run(async () =>
    {
        string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".xlsx" or ".csv")) throw new ArgumentException("Select an .xlsx or .csv file.");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (file.Length > 20 * 1024 * 1024) throw new IOException("Input exceeds 20 MiB. Split the report.");
        using var memory = new MemoryStream(); await file.CopyToAsync(memory, token); byte[] bytes = memory.ToArray();
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var sheets = ext == ".csv" ? ImmutableArray.Create(new CoverageSheet("CSV", ReadCsv(Decode(bytes), token))) : ReadXlsx(bytes, token);
        return new CoverageFile(System.IO.Path.GetFullPath(path), hash, DateTimeOffset.UtcNow, sheets);
    }, token);
    private static string Decode(byte[] bytes)
    {
        using var r = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false, true), true); return r.ReadToEnd();
    }
    public static ImmutableArray<CoverageSourceRow> ReadCsv(string text, CancellationToken token)
    {
        if (text.Length > 20 * 1024 * 1024) throw new IOException("CSV text exceeds its bounded limit.");
        char delimiter = Delimiter(text); var rows = ImmutableArray.CreateBuilder<CoverageSourceRow>(); var cells = new List<string>(); var field = new StringBuilder();
        bool quoted = false, closed = false; int number = 1;
        void Cell() { if (cells.Count >= MaxColumns || field.Length > MaxText) throw new IOException("CSV exceeds 256 columns or Excel's 32,767-character cell limit."); cells.Add(field.ToString()); field.Clear(); closed = false; }
        void Row() { Cell(); rows.Add(new(number++, cells.ToImmutableArray())); cells.Clear(); if (rows.Count > MaxRows) throw new IOException("Input exceeds 10,100 records."); }
        for (int i = 0; i < text.Length; i++)
        {
            if ((i & 4095) == 0) token.ThrowIfCancellationRequested(); char c = text[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else { quoted = false; closed = true; } }
                else field.Append(c);
            }
            else if (c == delimiter) Cell();
            else if (c is '\r' or '\n') { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; Row(); }
            else if (c == '"' && field.Length == 0 && !closed) quoted = true;
            else if (closed || c == '"') throw new IOException("Malformed CSV quoting. Export a standard quoted CSV.");
            else field.Append(c);
            if (field.Length > MaxText) throw new IOException("CSV cell exceeds Excel's 32,767-character limit.");
        }
        if (quoted) throw new IOException("Unclosed CSV quoted field.");
        if (field.Length > 0 || cells.Count > 0 || closed) Row();
        return rows.ToImmutable();
    }
    private static char Delimiter(string text)
    {
        var counts = new Dictionary<char, int> { [','] = 0, [';'] = 0, ['\t'] = 0 }; bool quoted = false;
        foreach (char c in text.Take(32768)) { if (c == '"') quoted = !quoted; if (!quoted && c is '\r' or '\n') break; if (!quoted && counts.ContainsKey(c)) counts[c]++; }
        return counts.OrderByDescending(x => x.Value).First().Key;
    }
    private static ImmutableArray<CoverageSheet> ReadXlsx(byte[] bytes, CancellationToken token)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        if (zip.Entries.Count > 512 || zip.Entries.Sum(e => e.Length) > 64 * 1024 * 1024 || zip.Entries.Any(e => e.Length > 32 * 1024 * 1024) || zip.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != zip.Entries.Count)
            throw new IOException("Workbook exceeds safe part/expanded-size limits or has duplicate parts.");
        if (zip.Entries.Any(e => e.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || e.FullName.Contains("externalLinks", StringComparison.OrdinalIgnoreCase))) throw new IOException("Macros and external-link workbooks are not accepted. Export values only.");
        XDocument Xml(string name)
        {
            token.ThrowIfCancellationRequested(); var entry = zip.GetEntry(name) ?? throw new IOException("Workbook part is missing.");
            using var stream = entry.Open(); using var reader = XmlReader.Create(stream, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 }); return XDocument.Load(reader);
        }
        string[] shared = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Xml("xl/sharedStrings.xml").Descendants(Ns + "si").Select(s => string.Concat(s.Descendants(Ns + "t").Select(t => t.Value))).ToArray();
        XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships", docRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var links = Xml("xl/_rels/workbook.xml.rels").Descendants(rel + "Relationship").Where(e => (string?)e.Attribute("TargetMode") != "External").ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
        var sheets = ImmutableArray.CreateBuilder<CoverageSheet>();
        var metadata = Xml("xl/workbook.xml").Descendants(Ns + "sheet").ToArray();
        if (metadata.Length is < 1 or > 32) throw new IOException("Workbook must have 1–32 worksheets.");
        int total = 0;
        foreach (var sheet in metadata)
        {
            string id = (string?)sheet.Attribute(docRel + "id") ?? "";
            if (!links.TryGetValue(id, out string? target)) throw new IOException("Invalid worksheet relationship.");
            string part = new Uri(new Uri("https://workbook.invalid/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
            if (!part.StartsWith("xl/worksheets/", StringComparison.Ordinal)) throw new IOException("Unsupported worksheet relationship.");
            var rows = ImmutableArray.CreateBuilder<CoverageSourceRow>(); int lastRow = 0;
            foreach (var row in Xml(part).Descendants(Ns + "row"))
            {
                token.ThrowIfCancellationRequested(); int number = (int?)row.Attribute("r") ?? lastRow + 1;
                if (number <= lastRow || number > 1048576 || ++total > MaxRows) throw new IOException("Invalid row order or workbook exceeds 10,100 records across sheets."); lastRow = number;
                var values = new SortedDictionary<int, string>();
                foreach (var cell in row.Elements(Ns + "c"))
                {
                    string reference = (string?)cell.Attribute("r") ?? ""; int index = 0;
                    foreach (char ch in reference.TakeWhile(char.IsAsciiLetterUpper)) index = checked(index * 26 + ch - 'A' + 1);
                    if (index < 1 || index > MaxColumns || values.ContainsKey(index)) throw new IOException("Invalid/duplicate cell or workbook exceeds 256 columns.");
                    string value = (string?)cell.Element(Ns + "v") ?? "";
                    // Preserve formula text as inert text, never evaluate or follow links. Export is inlineStr.
                    values[index] = cell.Element(Ns + "f") is { } formula ? "=" + formula.Value : (string?)cell.Attribute("t") switch
                    {
                        "s" => int.TryParse(value, out int n) && n >= 0 && n < shared.Length ? shared[n] : throw new IOException("Invalid shared string index."),
                        "inlineStr" => string.Concat(cell.Descendants(Ns + "t").Select(x => x.Value)), _ => value
                    };
                    if (values[index].Length > MaxText) throw new IOException("Workbook cell exceeds Excel's text limit.");
                }
                int width = values.Where(x => x.Value.Length > 0).Select(x => x.Key).DefaultIfEmpty(0).Max();
                rows.Add(new(number, Enumerable.Range(1, width).Select(i => values.GetValueOrDefault(i, "")).ToImmutableArray()));
            }
            sheets.Add(new((string?)sheet.Attribute("name") ?? "Sheet", rows.ToImmutable()));
        }
        return sheets.ToImmutable();
    }
}
