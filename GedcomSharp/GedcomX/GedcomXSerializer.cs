using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace GedcomSharp.GedcomX;

/// <summary>Reads and writes GEDCOM X documents as JSON or XML, and GEDCOM X files (<c>.gedx</c> archives).</summary>
public interface IGedcomXSerializer
{
    string ToJson(GedcomXDocument document, bool indented = true);

    Result<GedcomXDocument> FromJson(string json);

    string ToXml(GedcomXDocument document);

    Result<GedcomXDocument> FromXml(string xml);

    /// <summary>Reads a JSON or XML document, telling them apart by the first non-blank character.</summary>
    Task<Result<GedcomXDocument>> ReadAsync(Stream stream, CancellationToken cancellationToken = default);

    Task WriteJsonAsync(GedcomXDocument document, Stream stream, CancellationToken cancellationToken = default);

    Task WriteXmlAsync(GedcomXDocument document, Stream stream, CancellationToken cancellationToken = default);

    Task<Result<GedcomXFile>> ReadFileAsync(Stream stream, CancellationToken cancellationToken = default);

    Task<Result> WriteFileAsync(GedcomXFile file, Stream stream, CancellationToken cancellationToken = default);
}

/// <summary>A GEDCOM X file: a ZIP archive with a manifest, one or more GEDCOM X documents and other resources such as images.</summary>
public sealed class GedcomXFile
{
    public const string MANIFEST_ENTRY_NAME = "META-INF/MANIFEST.MF";

    /// <summary>GEDCOM X documents by entry name, e.g. <c>tree.json</c>.</summary>
    public Dictionary<string, GedcomXDocument> Documents { get; } = new(StringComparer.Ordinal);

    /// <summary>Other entries by name (images, PDFs, ...).</summary>
    public Dictionary<string, byte[]> Resources { get; } = new(StringComparer.Ordinal);

    /// <summary>Manifest attributes per entry (Content-Type, X-DC-modified, ...); the key "" holds the main section.</summary>
    public Dictionary<string, Dictionary<string, string>> Manifest { get; } = new(StringComparer.Ordinal);

    /// <summary>The first document, which for most files is the only one.</summary>
    public GedcomXDocument? MainDocument => Documents.Values.FirstOrDefault();
}

public sealed class GedcomXSerializer : IGedcomXSerializer
{
    private static readonly JsonSerializerOptions jsonOptions = CreateJsonOptions(indented: true);
    private static readonly JsonSerializerOptions compactJsonOptions = CreateJsonOptions(indented: false);

    public string ToJson(GedcomXDocument document, bool indented = true) =>
        JsonSerializer.Serialize(document, indented ? jsonOptions : compactJsonOptions);

    public Result<GedcomXDocument> FromJson(string json)
    {
        try
        {
            var document = JsonSerializer.Deserialize<GedcomXDocument>(json, jsonOptions);
            return document is null ? Invalid("The JSON document is empty.") : Result.Success(document);
        }
        catch (JsonException ex)
        {
            return Invalid($"The JSON is not a valid GEDCOM X document: {ex.Message}");
        }
    }

    public string ToXml(GedcomXDocument document)
    {
        var xml = GedcomXXml.Write(document);
        using var writer = new Utf8StringWriter();
        xml.Save(writer);
        return writer.ToString();
    }

    public Result<GedcomXDocument> FromXml(string xml)
    {
        try
        {
            return Result.Success(GedcomXXml.Read(XDocument.Parse(xml)));
        }
        catch (Exception ex) when (ex is XmlException or FormatException)
        {
            return Invalid($"The XML is not a valid GEDCOM X document: {ex.Message}");
        }
    }

    public async Task<Result<GedcomXDocument>> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string text = await reader.ReadToEndAsync(cancellationToken);
        string trimmed = text.TrimStart();
        return trimmed.StartsWith('<') ? FromXml(text) : FromJson(text);
    }

    public async Task WriteJsonAsync(GedcomXDocument document, Stream stream, CancellationToken cancellationToken = default) =>
        await JsonSerializer.SerializeAsync(stream, document, jsonOptions, cancellationToken);

    public async Task WriteXmlAsync(GedcomXDocument document, Stream stream, CancellationToken cancellationToken = default)
    {
        var settings = new XmlWriterSettings { Async = true, Indent = true, Encoding = new UTF8Encoding(false) };
        await using var writer = XmlWriter.Create(stream, settings);
        await GedcomXXml.Write(document).SaveAsync(writer, cancellationToken);
    }

    public async Task<Result<GedcomXFile>> ReadFileAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
            var file = new GedcomXFile();

            if (archive.GetEntry(GedcomXFile.MANIFEST_ENTRY_NAME) is { } manifestEntry)
            {
                using var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8);
                foreach (var (name, attributes) in ParseManifest(await reader.ReadToEndAsync(cancellationToken)))
                {
                    file.Manifest[name] = attributes;
                }
            }

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var data = new MemoryStream();
                await using (var entryStream = entry.Open())
                {
                    await entryStream.CopyToAsync(data, cancellationToken);
                }

                string? contentType = file.Manifest.GetValueOrDefault(entry.FullName)?.GetValueOrDefault("Content-Type");
                bool isJson = contentType == GedcomXTypes.JSON_MEDIA_TYPE || (contentType is null && entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
                bool isXml = contentType == GedcomXTypes.XML_MEDIA_TYPE || (contentType is null && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
                if (!isJson && !isXml)
                {
                    file.Resources[entry.FullName] = data.ToArray();
                    continue;
                }

                string text = Encoding.UTF8.GetString(data.ToArray()).TrimStart('\uFEFF');
                var document = isJson ? FromJson(text) : FromXml(text);
                if (!document.IsSuccess)
                {
                    return document.Map(_ => (GedcomXFile)null!);
                }

                file.Documents[entry.FullName] = document.Value;
            }

            return file.Documents.Count == 0
                ? Result<GedcomXFile>.Invalid(new ValidationError(string.Empty, "The archive has no GEDCOM X documents.", "NotGedcomX", ValidationSeverity.Error))
                : Result.Success(file);
        }
        catch (InvalidDataException ex)
        {
            return Result<GedcomXFile>.Invalid(new ValidationError(string.Empty, $"The archive is damaged: {ex.Message}", "NotGedcomX", ValidationSeverity.Error));
        }
    }

    public async Task<Result> WriteFileAsync(GedcomXFile file, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(stream);
        if (file.Documents.Count == 0)
        {
            return Result.Invalid(new ValidationError(string.Empty, "A GEDCOM X file needs at least one document.", "NotGedcomX", ValidationSeverity.Error));
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        var manifest = new StringBuilder();
        var main = file.Manifest.GetValueOrDefault(string.Empty) ?? [];
        AppendManifestAttribute(manifest, "Manifest-Version", main.GetValueOrDefault("Manifest-Version") ?? "1.0");
        AppendManifestAttribute(manifest, "X-DC-conformsTo", GedcomXTypes.FILE_CONFORMS_TO);
        foreach (var (key, value) in main.Where(pair => pair.Key is not ("Manifest-Version" or "X-DC-conformsTo")))
        {
            AppendManifestAttribute(manifest, key, value);
        }

        manifest.Append("\r\n");

        foreach (var (name, document) in file.Documents)
        {
            bool xml = name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            await using (var entryStream = entry.Open())
            {
                if (xml)
                {
                    await WriteXmlAsync(document, entryStream, cancellationToken);
                }
                else
                {
                    await WriteJsonAsync(document, entryStream, cancellationToken);
                }
            }

            AppendManifestSection(manifest, name, xml ? GedcomXTypes.XML_MEDIA_TYPE : GedcomXTypes.JSON_MEDIA_TYPE, file.Manifest.GetValueOrDefault(name));
        }

        foreach (var (name, data) in file.Resources)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            await using (var entryStream = entry.Open())
            {
                await entryStream.WriteAsync(data, cancellationToken);
            }

            AppendManifestSection(manifest, name, MediaTypes.FromPath(name), file.Manifest.GetValueOrDefault(name));
        }

        var manifestEntry = archive.CreateEntry(GedcomXFile.MANIFEST_ENTRY_NAME, CompressionLevel.Optimal);
        await using (var manifestStream = manifestEntry.Open())
        {
            await manifestStream.WriteAsync(Encoding.UTF8.GetBytes(manifest.ToString()), cancellationToken);
        }

        return Result.Success();
    }

    private static void AppendManifestSection(StringBuilder manifest, string name, string? contentType, Dictionary<string, string>? attributes)
    {
        AppendManifestAttribute(manifest, "Name", name);
        string? type = attributes?.GetValueOrDefault("Content-Type") ?? contentType;
        if (type is not null)
        {
            AppendManifestAttribute(manifest, "Content-Type", type);
        }

        foreach (var (key, value) in attributes?.Where(pair => pair.Key is not ("Name" or "Content-Type")) ?? [])
        {
            AppendManifestAttribute(manifest, key, value);
        }

        manifest.Append("\r\n");
    }

    /// <summary>JAR manifest lines are limited to 72 bytes; longer values continue on lines starting with a space.</summary>
    private static void AppendManifestAttribute(StringBuilder manifest, string name, string value)
    {
        string line = $"{name}: {value}";
        const int MAX_BYTES = 72;
        int start = 0;
        bool first = true;
        while (start < line.Length)
        {
            int budget = first ? MAX_BYTES : MAX_BYTES - 1;
            int length = 0;
            int bytes = 0;
            while (start + length < line.Length)
            {
                int next = Encoding.UTF8.GetByteCount(line.AsSpan(start + length, char.IsHighSurrogate(line[start + length]) ? 2 : 1));
                if (bytes + next > budget)
                {
                    break;
                }

                bytes += next;
                length += char.IsHighSurrogate(line[start + length]) ? 2 : 1;
            }

            manifest.Append(first ? string.Empty : " ").Append(line, start, length).Append("\r\n");
            start += length;
            first = false;
        }
    }

    internal static IEnumerable<(string Name, Dictionary<string, string> Attributes)> ParseManifest(string text)
    {
        var lines = new List<string>();
        foreach (string raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (raw.StartsWith(' ') && lines.Count > 0 && lines[^1].Length > 0)
            {
                lines[^1] += raw[1..];
            }
            else
            {
                lines.Add(raw);
            }
        }

        var section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool isMain = true;
        foreach (string line in lines.Append(string.Empty))
        {
            if (line.Length == 0)
            {
                if (section.Count > 0)
                {
                    yield return (isMain ? string.Empty : section.GetValueOrDefault("Name") ?? string.Empty, section);
                    section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    isMain = false;
                }

                continue;
            }

            int colon = line.IndexOf(':');
            if (colon > 0)
            {
                section[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
        }
    }

    private static JsonSerializerOptions CreateJsonOptions(bool indented) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = indented,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private static Result<GedcomXDocument> Invalid(string message) =>
        Result<GedcomXDocument>.Invalid(new ValidationError(string.Empty, message, "NotGedcomX", ValidationSeverity.Error));

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
