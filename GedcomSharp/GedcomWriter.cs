using System.IO.Compression;
using GedcomSharp.Conversion;
using GedcomSharp.Gedzip;
using GedcomSharp.Text;

namespace GedcomSharp;

/// <summary>
/// Writes datasets in any GEDCOM version. When the target version differs from the file's, a converted copy is
/// written and the file itself is left unchanged. 5.x output is split into CONT/CONC lines that fit the
/// line-length limit; 7.0 output uses CONT only.
/// </summary>
public sealed class GedcomWriter(GedcomWriterOptions? options = null) : IGedcomWriter
{
    private readonly GedcomWriterOptions settings = options ?? GedcomWriterOptions.Default;

    private sealed record PreparedOutput(
        string Text,
        GedcomVersion Version,
        GedcomCharacterEncoding EncodingKind,
        Encoding Encoding,
        bool ByteOrderMark,
        int RecordCount,
        int LineCount,
        GedcomFile Written,
        GedcomIssueCollector Issues);

    public async Task<Result<GedcomWriteSummary>> WriteAsync(GedcomFile file, string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var prepared = Prepare(file);
        if (!prepared.IsSuccess)
        {
            return prepared.Map(_ => (GedcomWriteSummary)null!);
        }

        byte[] data = Encode(prepared.Value);
        try
        {
            await File.WriteAllBytesAsync(path, data, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<GedcomWriteSummary>.Error($"File '{path}' could not be written: {ex.Message}");
        }

        return Result.Success(Summarize(prepared.Value, data.LongLength));
    }

    public async Task<Result<GedcomWriteSummary>> WriteAsync(GedcomFile file, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prepared = Prepare(file);
        if (!prepared.IsSuccess)
        {
            return prepared.Map(_ => (GedcomWriteSummary)null!);
        }

        byte[] data = Encode(prepared.Value);
        await stream.WriteAsync(data, cancellationToken);
        return Result.Success(Summarize(prepared.Value, data.LongLength));
    }

    public Result<byte[]> WriteToBytes(GedcomFile file) => Prepare(file).Map(Encode);

    public Result<string> WriteToString(GedcomFile file) => Prepare(file).Map(prepared => prepared.Text);

    public async Task<Result<GedcomWriteSummary>> WriteGedzipAsync(
        GedcomFile file,
        Stream stream,
        IEnumerable<GedzipFileEntry>? mediaFiles = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prepared = Prepare(file);
        if (!prepared.IsSuccess)
        {
            return prepared.Map(_ => (GedcomWriteSummary)null!);
        }

        var output = prepared.Value;
        byte[] data = Encode(output);
        string datasetName = string.IsNullOrWhiteSpace(settings.GedzipDatasetEntryName) ? GedzipPackage.DATASET_ENTRY_NAME : settings.GedzipDatasetEntryName;
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { datasetName };
        long total = data.LongLength;

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var datasetEntry = archive.CreateEntry(datasetName, CompressionLevel.Optimal);
            await using (var entryStream = datasetEntry.Open())
            {
                await entryStream.WriteAsync(data, cancellationToken);
            }

            foreach (var media in mediaFiles ?? [])
            {
                string name = GedzipPackage.Normalize(media.EntryName);
                if (name.Length == 0 || !written.Add(name))
                {
                    output.Issues.Warning(GedcomIssueCodes.CONVERSION, $"Media entry '{media.EntryName}' was skipped because the name is empty or already used.");
                    continue;
                }

                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                await using var source = await media.OpenAsync(cancellationToken);
                await using var target = entry.Open();
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    total += read;
                }
            }
        }

        foreach (string reference in LocalFileReferences(output.Written))
        {
            if (!written.Contains(GedzipPackage.Normalize(reference)))
            {
                output.Issues.Warning(GedcomIssueCodes.CONVERSION, $"FILE '{reference}' is not in the package.");
            }
        }

        return Result.Success(Summarize(output, total));
    }

    private Result<PreparedOutput> Prepare(GedcomFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var source = file.Version == GedcomVersion.Unknown ? GedcomVersion.V551 : file.Version;
        var target = settings.Version ?? source;
        if (target == GedcomVersion.Unknown)
        {
            return Invalid(GedcomIssueCodes.VERSION, "Choose a GEDCOM version to write.");
        }

        var encodingKind = settings.Encoding
            ?? (target == source && file.Source?.Encoding is { } read && read != GedcomCharacterEncoding.Other && GedcomEncodings.IsAllowed(read, target) ? read : (GedcomCharacterEncoding?)null)
            ?? (target == GedcomVersion.V55 && (target != source || file.Source?.Encoding != GedcomCharacterEncoding.Other) ? GedcomCharacterEncoding.Ansel : GedcomCharacterEncoding.Utf8);
        if (encodingKind == GedcomCharacterEncoding.Other)
        {
            return Invalid(GedcomIssueCodes.ENCODING, "Choose a specific character encoding to write.");
        }

        if (!GedcomEncodings.IsAllowed(encodingKind, target))
        {
            return Invalid(GedcomIssueCodes.ENCODING, $"GEDCOM {target.ToVersionString()} does not allow {GedcomEncodings.GetCharacterSetName(encodingKind)}.");
        }

        var issues = new GedcomIssueCollector();
        GedcomFile work;
        if (target != source)
        {
            work = GedcomVersionConverter.Convert(file, target);
            issues.AddRange(work.Issues);
        }
        else
        {
            work = file.Clone();
            work.Version = target;
            GedcomVersionConverter.NormalizeXrefs(work, target, issues);
            if (target == GedcomVersion.V555)
            {
                GedcomVersionConverter.EnsureSubmitter(work, issues);
            }
        }

        if (settings.UpdateHeader)
        {
            UpdateHeader(work.HeaderStructure, target, encodingKind);
        }

        var encoding = GedcomEncodings.GetEncoding(encodingKind);
        bool utf16 = encodingKind is GedcomCharacterEncoding.Utf16LittleEndian or GedcomCharacterEncoding.Utf16BigEndian;
        var lines = new GedcomLineWriter(settings, target, encoding, utf16);
        lines.Write(work.HeaderStructure, 0);
        foreach (var record in OrderRecords(work, target))
        {
            lines.Write(record, 0);
        }

        lines.WriteTrailer();
        string text = lines.ToString();

        if (encodingKind is GedcomCharacterEncoding.Ansel or GedcomCharacterEncoding.Ascii or GedcomCharacterEncoding.Windows1252)
        {
            ReportUnrepresentable(text, encoding, encodingKind, issues);
        }

        bool bom = encodingKind is GedcomCharacterEncoding.Utf8 or GedcomCharacterEncoding.Utf16LittleEndian or GedcomCharacterEncoding.Utf16BigEndian
            && (settings.WriteByteOrderMark ?? (utf16 || target is GedcomVersion.V555 or GedcomVersion.V70));

        return Result.Success(new PreparedOutput(text, target, encodingKind, encoding, bom, work.Records.Count, lines.LineCount, work, issues));
    }

    private void UpdateHeader(GedcomStructure header, GedcomVersion target, GedcomCharacterEncoding encoding)
    {
        if (settings.SourceSystem is { Length: > 0 } system)
        {
            header.SetChildValue(GedcomTags.SOUR, system);
        }
        else if (header.Child(GedcomTags.SOUR) is null)
        {
            header.Children.Insert(0, new GedcomStructure(GedcomTags.SOUR, "GedcomSharp"));
        }

        var sourceSystem = header.Child(GedcomTags.SOUR)!;
        if (settings.SourceVersion is not null)
        {
            sourceSystem.SetChildValue(GedcomTags.VERS, settings.SourceVersion);
        }

        if (settings.SourceName is not null)
        {
            sourceSystem.SetChildValue(GedcomTags.NAME, settings.SourceName);
        }

        GedcomVersionConverter.UpdateVersionDeclaration(header, target);

        if (target.IsSeven)
        {
            header.RemoveChildren(GedcomTags.CHAR);
        }
        else
        {
            var characterSet = header.Child(GedcomTags.CHAR);
            if (characterSet is null)
            {
                characterSet = new GedcomStructure(GedcomTags.CHAR);
                int gedcIndex = header.Children.IndexOf(header.Child(GedcomTags.GEDC)!);
                header.Children.Insert(gedcIndex + 1, characterSet);
            }

            characterSet.Value = GedcomEncodings.GetCharacterSetName(encoding);
            characterSet.RemoveChildren(GedcomTags.VERS);
        }

        if (settings.HeaderDate is DateTime timestamp)
        {
            var date = header.GetOrAddChild(GedcomTags.DATE);
            date.Value = GedcomDate.FromDateOnly(DateOnly.FromDateTime(timestamp)).Format(target);
            string time = timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            date.SetChildValue(GedcomTags.TIME, target.IsSeven && timestamp.Kind == DateTimeKind.Utc ? time + "Z" : time);
        }
    }

    /// <summary>5.5.5 requires the submitter record straight after the header.</summary>
    private static IEnumerable<GedcomStructure> OrderRecords(GedcomFile file, GedcomVersion target)
    {
        if (target != GedcomVersion.V555)
        {
            return file.Records;
        }

        return file.Records.Where(record => record.Tag == GedcomTags.SUBM)
            .Concat(file.Records.Where(record => record.Tag != GedcomTags.SUBM));
    }

    private static void ReportUnrepresentable(string text, Encoding encoding, GedcomCharacterEncoding kind, GedcomIssueCollector issues)
    {
        string roundTrip = encoding.GetString(encoding.GetBytes(text));
        string expected = text.Normalize(NormalizationForm.FormC);
        if (string.Equals(roundTrip.Normalize(NormalizationForm.FormC), expected, StringComparison.Ordinal))
        {
            return;
        }

        int lost = Math.Max(1, roundTrip.Count(c => c == '?') - expected.Count(c => c == '?'));
        issues.Warning(
            GedcomIssueCodes.UNREPRESENTABLE_CHARACTERS,
            $"About {lost} character(s) cannot be written in {GedcomEncodings.GetCharacterSetName(kind)} and were replaced with '?'. Write UTF-8 to keep them.");
    }

    private static byte[] Encode(PreparedOutput output)
    {
        byte[] preamble = !output.ByteOrderMark
            ? []
            : output.EncodingKind switch
            {
                GedcomCharacterEncoding.Utf16LittleEndian => Encoding.Unicode.GetPreamble(),
                GedcomCharacterEncoding.Utf16BigEndian => Encoding.BigEndianUnicode.GetPreamble(),
                _ => Encoding.UTF8.GetPreamble()
            };

        byte[] body = output.Encoding.GetBytes(output.Text);
        if (preamble.Length == 0)
        {
            return body;
        }

        byte[] data = new byte[preamble.Length + body.Length];
        preamble.CopyTo(data, 0);
        body.CopyTo(data, preamble.Length);
        return data;
    }

    private static GedcomWriteSummary Summarize(PreparedOutput output, long byteCount) =>
        new(output.Version, output.EncodingKind, output.RecordCount, output.LineCount, byteCount, output.Issues.ToList());

    private static IEnumerable<string> LocalFileReferences(GedcomFile file) => file.Records
        .SelectMany(record => record.DescendantsAndSelf())
        .Where(structure => structure.Tag == GedcomTags.FILE && structure.Parent?.Tag == GedcomTags.OBJE && structure.Value is { Length: > 0 })
        .Select(structure => structure.Value!)
        .Where(reference => !reference.Contains("://", StringComparison.Ordinal) || reference.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.Ordinal);

    private static Result<PreparedOutput> Invalid(string code, string message) =>
        Result<PreparedOutput>.Invalid(new ValidationError(string.Empty, message, code, ValidationSeverity.Error));
}
