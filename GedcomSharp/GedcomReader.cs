using System.IO.Compression;
using GedcomSharp.Gedzip;
using GedcomSharp.Parsing;
using GedcomSharp.Text;

namespace GedcomSharp;

/// <summary>
/// Reads any GEDCOM version into a <see cref="GedcomFile"/>. Reading is lenient: problems that real-world files
/// have (wrong CHAR, bad nesting, missing TRLR, ...) are repaired where the intent is clear and listed in
/// <see cref="GedcomFile.Issues"/>. The result is only a failure when the input is not GEDCOM at all, or when
/// <see cref="GedcomReaderOptions.Strict"/> is set and an error-level issue was found.
/// </summary>
public sealed class GedcomReader(GedcomReaderOptions? options = null) : IGedcomReader
{
    private readonly GedcomReaderOptions settings = options ?? GedcomReaderOptions.Default;

    public async Task<Result<GedcomFile>> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return Result<GedcomFile>.NotFound($"File '{path}' was not found.");
        }

        try
        {
            byte[] data = await File.ReadAllBytesAsync(path, cancellationToken);
            return Read(data, Path.GetFileName(path));
        }
        catch (IOException ex)
        {
            return Result<GedcomFile>.Error($"File '{path}' could not be read: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<GedcomFile>.Error($"File '{path}' could not be read: {ex.Message}");
        }
    }

    public async Task<Result<GedcomFile>> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] data = await ReadAllBytesAsync(stream, cancellationToken);
        return Read(data, (stream as FileStream)?.Name is string name ? Path.GetFileName(name) : null);
    }

    public Result<GedcomFile> Read(byte[] data, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
        {
            return Invalid(GedcomIssueCodes.EMPTY_FILE, "The file is empty.");
        }

        if (GedzipPackage.IsZip(data))
        {
            var package = OpenGedzip(new MemoryStream(data, writable: false));
            if (!package.IsSuccess)
            {
                return package.Map(_ => (GedcomFile)null!);
            }

            using var opened = package.Value;
            return Result.Success(opened.File);
        }

        try
        {
            var issues = new GedcomIssueCollector(settings.MaxIssuesPerKind);
            var detection = settings.Encoding is { } forced
                ? EncodingDetector.Forced(data, forced)
                : EncodingDetector.Detect(data, issues);
            string text = detection.Encoding.GetString(detection.Bytes, detection.PreambleLength, detection.Bytes.Length - detection.PreambleLength);
            return ParseText(text, detection.Kind, detection.Encoding.WebName, detection.HasByteOrderMark, detection.DeclaredCharacterSet, fileName, issues);
        }
        catch (Exception ex) when (ex is ArgumentException or DecoderFallbackException or InvalidOperationException)
        {
            return Result<GedcomFile>.Error($"The data could not be read as GEDCOM: {ex.Message}");
        }
    }

    public Result<GedcomFile> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return Invalid(GedcomIssueCodes.EMPTY_FILE, "The text is empty.");
        }

        var issues = new GedcomIssueCollector(settings.MaxIssuesPerKind);
        bool hasBom = text[0] == '\uFEFF';
        return ParseText(hasBom ? text[1..] : text, GedcomCharacterEncoding.Utf8, "utf-16", hasBom, null, null, issues);
    }

    public async Task<Result<GedzipPackage>> ReadGedzipAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return Result<GedzipPackage>.NotFound($"File '{path}' was not found.");
        }

        await using var stream = File.OpenRead(path);
        return await ReadGedzipAsync(stream, cancellationToken);
    }

    public async Task<Result<GedzipPackage>> ReadGedzipAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] data = await ReadAllBytesAsync(stream, cancellationToken);
        if (!GedzipPackage.IsZip(data))
        {
            return Result<GedzipPackage>.Invalid(new ValidationError(string.Empty, "The data is not a ZIP archive.", GedcomIssueCodes.NOT_GEDCOM, ValidationSeverity.Error));
        }

        return OpenGedzip(new MemoryStream(data, writable: false));
    }

    private Result<GedzipPackage> OpenGedzip(MemoryStream stream)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException ex)
        {
            stream.Dispose();
            return Result<GedzipPackage>.Invalid(new ValidationError(string.Empty, $"The ZIP archive is damaged: {ex.Message}", GedcomIssueCodes.NOT_GEDCOM, ValidationSeverity.Error));
        }

        var dataset = GedzipPackage.FindDatasetEntry(archive);
        if (dataset is null)
        {
            archive.Dispose();
            return Result<GedzipPackage>.Invalid(new ValidationError(string.Empty, "The archive has no gedcom.ged (or other .ged) dataset.", GedcomIssueCodes.NOT_GEDCOM, ValidationSeverity.Error));
        }

        byte[] data;
        using (var entryStream = dataset.Open())
        using (var buffer = new MemoryStream())
        {
            entryStream.CopyTo(buffer);
            data = buffer.ToArray();
        }

        if (GedzipPackage.IsZip(data))
        {
            archive.Dispose();
            return Result<GedzipPackage>.Invalid(new ValidationError(dataset.FullName, "The dataset entry is itself a ZIP archive.", GedcomIssueCodes.NOT_GEDCOM, ValidationSeverity.Error));
        }

        var file = Read(data, dataset.FullName);
        if (!file.IsSuccess)
        {
            archive.Dispose();
            return file.Map(_ => (GedzipPackage)null!);
        }

        return Result.Success(new GedzipPackage(archive, file.Value, dataset.FullName));
    }

    private Result<GedcomFile> ParseText(
        string text,
        GedcomCharacterEncoding encoding,
        string encodingName,
        bool hasByteOrderMark,
        string? declaredCharacterSet,
        string? fileName,
        GedcomIssueCollector issues)
    {
        string start = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (start.StartsWith("0 HEADER", StringComparison.Ordinal))
        {
            return Invalid(GedcomIssueCodes.FTW_TEXT, "This is a Family Tree Maker 'FTW TEXT' file, not GEDCOM. Export it from Family Tree Maker as GEDCOM instead.");
        }

        var tokenized = GedcomLineTokenizer.Tokenize(text, settings, issues);
        var lines = tokenized.Lines;
        if (lines.Count == 0)
        {
            return Invalid(GedcomIssueCodes.EMPTY_FILE, "The file has no GEDCOM lines.");
        }

        if (!lines.Any(line => line.Level == 0 && GedcomLineTokenizer.IsValidTag(line.Tag)))
        {
            return Invalid(GedcomIssueCodes.NOT_GEDCOM, "The data is not GEDCOM: no line starts with level 0.");
        }

        var firstLine = lines.FirstOrDefault(line => line.HasLevel);
        if (firstLine.Tag != GedcomTags.HEAD)
        {
            issues.Warning(GedcomIssueCodes.MISSING_HEADER, "The file does not start with HEAD.", firstLine.LineNumber);
        }

        var build = GedcomTreeBuilder.Build(lines, settings, issues);
        var header = build.Header;
        if (header is null)
        {
            issues.Warning(GedcomIssueCodes.MISSING_HEADER, "The file has no HEAD record; an empty one was created.");
            header = new GedcomStructure(GedcomTags.HEAD);
        }

        var version = GedcomVersionDetector.Detect(header, build.Records, encoding, issues);
        GedcomPostProcessor.Unescape(build.Records.Prepend(header), version, issues);

        if (version.IsSeven)
        {
            int concLines = lines.Count(line => line.Tag == GedcomTags.CONC);
            if (concLines > 0)
            {
                issues.Warning(GedcomIssueCodes.CONC_IN_VERSION_7, $"{concLines} CONC line(s) were joined; GEDCOM 7 does not allow CONC.");
            }

            if (encoding != GedcomCharacterEncoding.Utf8)
            {
                issues.Warning(GedcomIssueCodes.ENCODING, $"GEDCOM 7 files must be UTF-8 but this one was read as {encodingName}.");
            }
        }

        if (!build.HasTrailer)
        {
            issues.Warning(GedcomIssueCodes.MISSING_TRAILER, "The file does not end with TRLR; it may be truncated.");
        }

        GedcomPostProcessor.CheckReferences(header, build.Records, version, settings.CheckPointers, issues);

        var file = new GedcomFile(version, header);
        file.Records.AddRange(build.Records);
        file.Issues = issues.ToList();
        file.Source = new GedcomSourceInfo(
            fileName,
            encoding,
            encodingName,
            hasByteOrderMark,
            tokenized.LineTerminator,
            declaredCharacterSet ?? header.ChildValue(GedcomTags.CHAR),
            header.Find(GedcomTags.GEDC, GedcomTags.VERS)?.Value,
            lines.Count);

        if (settings.Strict && issues.HasErrors)
        {
            var errors = file.Issues
                .Where(issue => issue.Severity == GedcomIssueSeverity.Error)
                .Select(issue => new ValidationError(
                    issue.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    issue.Message,
                    issue.Code,
                    ValidationSeverity.Error));
            return Result<GedcomFile>.Invalid(errors);
        }

        return Result.Success(file);
    }

    private static Result<GedcomFile> Invalid(string code, string message) =>
        Result<GedcomFile>.Invalid(new ValidationError(string.Empty, message, code, ValidationSeverity.Error));

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream memory && memory.Position == 0)
        {
            return memory.ToArray();
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
