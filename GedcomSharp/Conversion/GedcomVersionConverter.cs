namespace GedcomSharp.Conversion;

/// <summary>
/// Converts a dataset between GEDCOM versions, following the FamilySearch 5.5.1-to-7.0 conversion notes and
/// the reverse where 7.0 has no 5.x equivalent. Anything that cannot be represented in the target is kept as
/// an extension tag (<c>_PHRASE</c>, <c>_UID</c>, ...) rather than dropped, and reported as an issue.
/// </summary>
public static class GedcomVersionConverter
{
    /// <summary>Tags 7.0 added that have no 5.x equivalent; written to 5.x with a leading underscore.</summary>
    private static readonly HashSet<string> sevenOnlyTags =
    [
        GedcomTags.PHRASE, GedcomTags.CREA, GedcomTags.SDATE, GedcomTags.NO, GedcomTags.INIL, GedcomTags.CROP,
        GedcomTags.MIME, GedcomTags.EXID, "TOP", "LEFT", "HEIGHT", "WIDTH", "INIL"
    ];

    /// <summary>Converts a copy of <paramref name="file"/>; the original is not changed.</summary>
    public static GedcomFile Convert(GedcomFile file, GedcomVersion target)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (target == GedcomVersion.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "Choose a GEDCOM version.");
        }

        var copy = file.Clone();
        var issues = new GedcomIssueCollector();
        var source = file.Version == GedcomVersion.Unknown ? GedcomVersion.V551 : file.Version;

        if (source != target)
        {
            var context = new ConversionContext(copy, source, target, issues);
            if (source.IsLegacy && target.IsSeven)
            {
                LegacyToSeven(context);
            }
            else if (source.IsSeven && target.IsLegacy)
            {
                SevenToLegacy(context);
                LegacyToLegacy(context with { Source = GedcomVersion.V551 });
            }
            else
            {
                LegacyToLegacy(context);
            }
        }

        copy.Version = target;
        UpdateVersionDeclaration(copy.HeaderStructure, target);
        NormalizeXrefs(copy, target, issues);
        if (target == GedcomVersion.V555)
        {
            EnsureSubmitter(copy, issues);
        }

        copy.Issues = issues.ToList();
        return copy;
    }

    private sealed record ConversionContext(GedcomFile File, GedcomVersion Source, GedcomVersion Target, GedcomIssueCollector Issues)
    {
        /// <summary>A snapshot of every structure, so the passes can rename, move and remove while iterating.</summary>
        public List<GedcomStructure> AllStructures() =>
        [
            .. File.HeaderStructure.DescendantsAndSelf(),
            .. File.Records.SelectMany(record => record.DescendantsAndSelf())
        ];

        public void Info(string message, GedcomStructure? structure = null) =>
            Issues.Info(GedcomIssueCodes.CONVERSION, message, structure is { LineNumber: > 0 } ? structure.LineNumber : null);

        public void Warning(string message, GedcomStructure? structure = null) =>
            Issues.Warning(GedcomIssueCodes.CONVERSION, message, structure is { LineNumber: > 0 } ? structure.LineNumber : null);
    }

    private static void LegacyToSeven(ConversionContext context)
    {
        var file = context.File;
        var header = file.HeaderStructure;
        header.RemoveChildren(GedcomTags.FILE);
        header.RemoveChildren(GedcomTags.CHAR);
        header.RemoveChildren(GedcomTags.SUBN);
        header.Child(GedcomTags.GEDC)?.RemoveChildren(GedcomTags.FORM);

        foreach (var submission in file.Records.Where(record => record.Tag == GedcomTags.SUBN).ToList())
        {
            file.Records.Remove(submission);
            context.Warning("The SUBN record was removed; GEDCOM 7 has no submission record.", submission);
        }

        foreach (var record in file.Records.Where(record => record.Tag == GedcomTags.NOTE).ToList())
        {
            record.Tag = GedcomTags.SNOTE;
        }

        // Embedded objects and sources become records first so the later passes see them in their final place.
        foreach (var structure in context.AllStructures())
        {
            if (structure.Parent is null)
            {
                if (structure.Tag == GedcomTags.OBJE)
                {
                    RestructureObject(structure, context);
                }

                continue;
            }

            if (structure.Tag == GedcomTags.OBJE && !structure.HasPointer)
            {
                ExtractEmbeddedObject(structure, context);
            }
            else if (structure.Tag == GedcomTags.SOUR && !structure.HasPointer && structure.Root.Tag != GedcomTags.HEAD)
            {
                ExtractEmbeddedSource(structure, context);
            }
        }

        foreach (var structure in context.AllStructures())
        {
            if (structure.Parent is null && structure.Tag != GedcomTags.HEAD && !ReferenceEquals(structure.Owner, file))
            {
                continue;
            }

            ConvertStructureToSeven(structure, context);
        }

        VoidMissingPointers(context);
    }

    private static void ConvertStructureToSeven(GedcomStructure structure, ConversionContext context)
    {
        string? parentTag = structure.Parent?.Tag;
        switch (structure.Tag)
        {
            case GedcomTags.NOTE when structure.HasPointer:
                structure.Tag = GedcomTags.SNOTE;
                break;

            case GedcomTags.DATE:
                ConvertDateToSeven(structure, context);
                break;

            case GedcomTags.AGE:
                ConvertAgeToSeven(structure, context);
                break;

            case GedcomTags.PEDI when structure.Value is { } pedigree:
                SetEnumeration(structure, GedcomEnumerations.ToPedigree(pedigree, out bool pedigreeExact), pedigreeExact ? null : pedigree);
                break;

            case GedcomTags.TYPE when parentTag == GedcomTags.NAME && structure.Value is { } nameType:
                SetEnumeration(structure, GedcomEnumerations.ToNameType(nameType, out bool nameTypeExact), nameTypeExact ? null : nameType);
                break;

            case GedcomTags.MEDI or "TYPE" or "TYP" when parentTag == GedcomTags.FORM && structure.Value is { } medium:
                structure.Tag = GedcomTags.MEDI;
                SetEnumeration(structure, GedcomEnumerations.ToMedium(medium, out bool mediumExact), mediumExact ? null : medium);
                break;

            case GedcomTags.RESN when structure.Value is { } restriction:
                structure.Value = string.Join(", ", restriction.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Select(part => part.ToUpperInvariant()));
                break;

            case GedcomTags.STAT when structure.Value is { } status:
                string upper = status.Trim().ToUpperInvariant();
                structure.Value = upper switch
                {
                    "PRE-1970" => "PRE_1970",
                    "DNS/CAN" => "DNS_CAN",
                    _ => upper
                };
                break;

            case GedcomTags.SEX when structure.Value is { } sex:
                structure.Value = GedcomSexExtensions.ParseSex(sex).ToCode();
                break;

            case GedcomTags.ASSO:
                ConvertAssociationToSeven(structure);
                break;

            case GedcomTags.ROLE when parentTag == GedcomTags.EVEN && structure.Value is { } citationRole:
                string trimmedRole = citationRole.Trim();
                bool descriptor = trimmedRole.StartsWith('(') && trimmedRole.EndsWith(')');
                string roleText = descriptor ? trimmedRole[1..^1].Trim() : trimmedRole;
                string mappedRole = GedcomEnumerations.ToRole(roleText, out bool roleExact);
                SetEnumeration(structure, mappedRole, roleExact ? null : roleText);
                break;

            case GedcomTags.RIN:
                structure.Tag = GedcomTags.Extensions.RIN;
                break;

            case GedcomTags.AFN or GedcomTags.RFN:
                string type = structure.Tag == GedcomTags.AFN ? ExternalId.AFN_TYPE : ExternalId.RFN_TYPE;
                structure.Tag = GedcomTags.EXID;
                structure.SetChildValue(GedcomTags.TYPE, type);
                break;

            case GedcomTags.Extensions.UID:
                structure.Tag = GedcomTags.UID;
                break;

            case GedcomTags.EMAI or GedcomTags.Extensions.EMAIL:
                structure.Tag = GedcomTags.EMAIL;
                break;

            case GedcomTags.Extensions.FAX:
                structure.Tag = GedcomTags.FAX;
                break;

            case GedcomTags.Extensions.WWW:
                structure.Tag = GedcomTags.WWW;
                break;

            case "_MAP" when parentTag == GedcomTags.PLAC:
                structure.Tag = GedcomTags.MAP;
                break;

            case GedcomTags.LANG when structure.Value is { } language:
                if (GedcomLanguages.ToTag(language) is string tag)
                {
                    structure.Value = tag;
                }
                else
                {
                    structure.Value = "und";
                    structure.AddChild("_LANG", language);
                    context.Info($"Language '{language}' has no BCP 47 tag; it was written as 'und' with the name in _LANG.", structure);
                }

                break;

            case GedcomTags.FONE or GedcomTags.ROMN:
                bool romanized = structure.Tag == GedcomTags.ROMN;
                structure.Tag = GedcomTags.TRAN;
                if (structure.Child(GedcomTags.TYPE) is { } method)
                {
                    method.Tag = "_TYPE";
                }

                if (structure.Child(GedcomTags.LANG) is null)
                {
                    structure.AddChild(GedcomTags.LANG, romanized ? "und-Latn" : "und-fonipa");
                }

                break;

            case "ANCI" or "DESI" or GedcomTags.BLOB:
                context.Info($"{structure.Tag} is not part of GEDCOM 7 and was kept as _{structure.Tag}.", structure);
                structure.Tag = "_" + structure.Tag;
                break;

            case GedcomTags.FILE when parentTag == GedcomTags.OBJE && structure.Value is { Length: > 0 } path:
                structure.Value = MediaTypes.ToUrl(path);
                ConvertFileFormatToSeven(structure, context);
                break;
        }
    }

    private static void ConvertDateToSeven(GedcomStructure structure, ConversionContext context)
    {
        if (structure.Value is not { Length: > 0 } text)
        {
            return;
        }

        var date = GedcomDate.Parse(text);
        var (value, phrase) = date.FormatWithPhrase(GedcomVersion.V70);
        if (date.Kind == GedcomDateKind.Unparsed)
        {
            context.Info($"Date '{text}' is not a valid GEDCOM date; it was kept as a PHRASE.", structure);
        }

        bool exactRequired = structure.Parent?.Tag is GedcomTags.CHAN or GedcomTags.CREA || structure.Parent?.Parent?.Tag == GedcomTags.HEAD;
        if (exactRequired && date.Kind != GedcomDateKind.Simple)
        {
            context.Warning($"{structure.Path} must be an exact date in GEDCOM 7 but is '{text}'.", structure);
        }

        structure.Value = value.Length == 0 ? null : value;
        if (phrase is not null)
        {
            structure.SetChildValue(GedcomTags.PHRASE, phrase);
        }
    }

    private static void ConvertAgeToSeven(GedcomStructure structure, ConversionContext context)
    {
        if (structure.Value is not { Length: > 0 } text)
        {
            return;
        }

        var age = GedcomAge.Parse(text);
        if (age is null)
        {
            structure.Value = null;
            structure.SetChildValue(GedcomTags.PHRASE, text);
            context.Info($"Age '{text}' is not a valid GEDCOM age; it was kept as a PHRASE.", structure);
            return;
        }

        structure.Value = age.Format(GedcomVersion.V70);
        if (age.Keyword is GedcomAgeKeyword keyword)
        {
            structure.SetChildValue(GedcomTags.PHRASE, keyword switch
            {
                GedcomAgeKeyword.Child => "Child",
                GedcomAgeKeyword.Infant => "Infant",
                _ => "Stillborn"
            });
        }
    }

    private static void ConvertAssociationToSeven(GedcomStructure structure)
    {
        structure.RemoveChildren(GedcomTags.TYPE);
        var relation = structure.Child(GedcomTags.RELA);
        if (relation is null)
        {
            if (structure.Child(GedcomTags.ROLE) is null)
            {
                structure.AddChild(GedcomTags.ROLE, "OTHER");
            }

            return;
        }

        string text = relation.Value ?? string.Empty;
        string role = GedcomEnumerations.ToRole(text, out bool exact);
        relation.Tag = GedcomTags.ROLE;
        relation.Value = role;
        if (!exact && text.Length > 0)
        {
            relation.SetChildValue(GedcomTags.PHRASE, text);
        }
    }

    private static void ConvertFileFormatToSeven(GedcomStructure file, ConversionContext context)
    {
        var form = file.Child(GedcomTags.FORM);
        string? mediaType = MediaTypes.FromFormat(form?.Value) ?? MediaTypes.FromPath(file.Value);
        if (mediaType is null)
        {
            mediaType = "application/octet-stream";
            context.Info($"The format of '{file.Value}' is unknown; FORM was set to {mediaType}.", file);
        }

        form ??= file.AddChild(GedcomTags.FORM);
        form.Value = mediaType;
    }

    /// <summary>
    /// Moves 5.5-style FORM (and TITL) beside FILE to under FILE. 5.5.1 keeps TITL beside FILE in embedded
    /// objects, so the title only moves for records and for 7.0.
    /// </summary>
    private static void RestructureObject(GedcomStructure obje, ConversionContext context)
    {
        bool moveTitle = obje.Parent is null || context.Target.IsSeven;
        var form = obje.Child(GedcomTags.FORM);
        var title = obje.Child(GedcomTags.TITL);
        var blob = obje.Child(GedcomTags.BLOB);
        var file = obje.Child(GedcomTags.FILE);

        if (file is null && (form is not null || blob is not null))
        {
            file = new GedcomStructure(GedcomTags.FILE, blob is null ? string.Empty : null);
            obje.Children.Insert(0, file);
            if (blob is not null)
            {
                context.Warning("Embedded BLOB image data cannot be carried into this version; the BLOB was kept as _BLOB with an empty FILE.", blob);
            }
        }

        if (file is null)
        {
            return;
        }

        if (form is not null && file.Child(GedcomTags.FORM) is null)
        {
            obje.Children.Remove(form);
            file.Children.Add(form);
        }

        if (title is not null && file.Child(GedcomTags.TITL) is null && moveTitle)
        {
            obje.Children.Remove(title);
            file.Children.Add(title);
        }
    }

    private static void ExtractEmbeddedObject(GedcomStructure obje, ConversionContext context)
    {
        RestructureObject(obje, context);
        var file = context.File;
        var record = new GedcomStructure(GedcomTags.OBJE) { Xref = file.CreateXref("O"), LineNumber = obje.LineNumber };
        foreach (var child in obje.Children.ToList())
        {
            // The preferred-photo flag and crop describe this use of the object, not the object itself.
            if (child.Tag is GedcomTags.Extensions.PRIM or GedcomTags.Extensions.PRIMARY or GedcomTags.CROP)
            {
                continue;
            }

            obje.Children.Remove(child);
            record.Children.Add(child);
        }

        file.Records.Add(record);
        obje.Pointer = record.Xref;
        context.Info($"An embedded OBJE was moved to the new record @{record.Xref}@.", obje);
    }

    private static void ExtractEmbeddedSource(GedcomStructure citation, ConversionContext context)
    {
        var file = context.File;
        var record = new GedcomStructure(GedcomTags.SOUR) { Xref = file.CreateXref("S"), LineNumber = citation.LineNumber };
        if (citation.Value is { Length: > 0 } description)
        {
            record.AddChild(GedcomTags.TITL, description);
        }

        foreach (var text in citation.ChildrenWithTag(GedcomTags.TEXT).ToList())
        {
            citation.Children.Remove(text);
            record.Children.Add(text);
        }

        file.Records.Add(record);
        citation.Pointer = record.Xref;
        context.Info($"An embedded source citation was moved to the new SOUR record @{record.Xref}@.", citation);
    }

    private static void SetEnumeration(GedcomStructure structure, string value, string? phrase)
    {
        structure.Value = value;
        if (phrase is not null)
        {
            structure.SetChildValue(GedcomTags.PHRASE, phrase);
        }
    }

    /// <summary>7.0 requires every pointer to resolve; pointers to missing records become @VOID@.</summary>
    private static void VoidMissingPointers(ConversionContext context)
    {
        foreach (var structure in context.AllStructures())
        {
            if (structure.Pointer is string pointer && !structure.IsVoidPointer && !context.File.Contains(pointer))
            {
                structure.Pointer = GedcomStructure.VOID_POINTER;
                context.Warning($"{structure.Path} pointed to the missing record @{pointer}@; it now points to @VOID@.", structure);
            }
        }
    }

    private static void SevenToLegacy(ConversionContext context)
    {
        var file = context.File;
        var target = context.Target;
        file.HeaderStructure.RemoveChildren(GedcomTags.SCHMA);

        foreach (var record in file.Records.Where(record => record.Tag == GedcomTags.SNOTE).ToList())
        {
            record.Tag = GedcomTags.NOTE;
        }

        foreach (var structure in context.AllStructures())
        {
            if (structure.IsVoidPointer)
            {
                if (structure.Parent is not null)
                {
                    context.Info($"{structure.Path} @VOID@ was removed; 5.x has no void pointer.", structure);
                    structure.Remove();
                }

                continue;
            }

            ConvertStructureToLegacy(structure, context, target);
        }
    }

    private static void ConvertStructureToLegacy(GedcomStructure structure, ConversionContext context, GedcomVersion target)
    {
        string? parentTag = structure.Parent?.Tag;
        switch (structure.Tag)
        {
            case GedcomTags.SNOTE:
                structure.Tag = GedcomTags.NOTE;
                return;

            case GedcomTags.DATE:
                ConvertDateToLegacy(structure, context);
                return;

            case GedcomTags.AGE when structure.Value is { Length: > 0 } ageText:
                if (structure.Child(GedcomTags.PHRASE) is { Value: { } agePhrase } phraseChild)
                {
                    var keyword = agePhrase.Trim().ToUpperInvariant();
                    if (keyword is "CHILD" or "INFANT" or "STILLBORN")
                    {
                        structure.Value = keyword;
                        structure.Children.Remove(phraseChild);
                        return;
                    }
                }

                structure.Value = GedcomAge.Parse(ageText)?.Format(target) ?? ageText;
                return;

            case GedcomTags.ROLE when parentTag is GedcomTags.ASSO:
                string role = structure.Value ?? "OTHER";
                string relation = role == "OTHER" && structure.ChildValue(GedcomTags.PHRASE) is { } rolePhrase
                    ? rolePhrase
                    : GedcomEnumerations.ToRelation(role);
                structure.Tag = GedcomTags.RELA;
                structure.Value = relation;
                structure.RemoveChildren(GedcomTags.PHRASE);
                return;

            case GedcomTags.ROLE when parentTag == GedcomTags.EVEN && structure.Parent?.Parent?.Tag == GedcomTags.SOUR:
                // 5.5.1 citation roles are CHIL, HUSB, WIFE, MOTH, FATH, SPOU or a "(descriptor)".
                string citationRole = structure.Value ?? "OTHER";
                if (citationRole is not ("CHIL" or "HUSB" or "WIFE" or "MOTH" or "FATH" or "SPOU"))
                {
                    string descriptor = citationRole == "OTHER" && structure.ChildValue(GedcomTags.PHRASE) is { } citationPhrase
                        ? citationPhrase
                        : GedcomEnumerations.ToRelation(citationRole);
                    structure.Value = $"({descriptor})";
                }

                structure.RemoveChildren(GedcomTags.PHRASE);
                return;

            case GedcomTags.ASSO when parentTag is not (GedcomTags.INDI or null):
                structure.Tag = "_ASSO";
                context.Info($"ASSO under {parentTag} was kept as _ASSO; 5.x only has ASSO on individuals.", structure);
                return;

            case GedcomTags.TYPE when parentTag == GedcomTags.NAME && structure.Value is { } nameType:
                structure.Value = nameType == "OTHER" && structure.ChildValue(GedcomTags.PHRASE) is { } typePhrase
                    ? typePhrase
                    : nameType.ToLowerInvariant();
                structure.RemoveChildren(GedcomTags.PHRASE);
                return;

            case GedcomTags.PEDI or GedcomTags.MEDI when structure.Value is { } enumeration:
                structure.Value = enumeration.ToLowerInvariant();
                if (structure.Tag == GedcomTags.MEDI)
                {
                    structure.Tag = "TYPE";
                }

                if (structure.Child(GedcomTags.PHRASE) is { } enumerationPhrase)
                {
                    enumerationPhrase.Tag = GedcomTags.Extensions.PHRASE;
                }

                return;

            case GedcomTags.STAT when parentTag == GedcomTags.FAMC && structure.Value is { } linkStatus:
                structure.Value = linkStatus.ToLowerInvariant();
                return;

            case GedcomTags.STAT when structure.Value is { } ordinanceStatus:
                structure.Value = ordinanceStatus switch
                {
                    "PRE_1970" => "PRE-1970",
                    "DNS_CAN" => "DNS/CAN",
                    _ => ordinanceStatus
                };
                return;

            case GedcomTags.RESN when structure.Value is { } restriction:
                string[] parts = restriction.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
                string kept = parts.FirstOrDefault(part => part.Equals("PRIVACY", StringComparison.OrdinalIgnoreCase))
                    ?? parts.FirstOrDefault(part => part.Equals("CONFIDENTIAL", StringComparison.OrdinalIgnoreCase))
                    ?? parts.FirstOrDefault()
                    ?? restriction;
                if (parts.Length > 1)
                {
                    context.Info($"RESN '{restriction}' has several values; 5.x allows one, so '{kept.ToLowerInvariant()}' was kept.", structure);
                }

                structure.Value = kept.ToLowerInvariant();
                return;

            case GedcomTags.SEX when structure.Value == "X" && target != GedcomVersion.V555:
                structure.Value = "U";
                context.Info("SEX X (intersex) does not exist before GEDCOM 5.5.5; it was written as U.", structure);
                return;

            case GedcomTags.UID:
                structure.Tag = GedcomTags.Extensions.UID;
                return;

            case GedcomTags.Extensions.RIN:
                structure.Tag = GedcomTags.RIN;
                return;

            case GedcomTags.EXID:
                string? type = structure.ChildValue(GedcomTags.TYPE);
                if (type == ExternalId.AFN_TYPE || type == ExternalId.RFN_TYPE)
                {
                    structure.Tag = type == ExternalId.AFN_TYPE ? GedcomTags.AFN : GedcomTags.RFN;
                    structure.RemoveChildren(GedcomTags.TYPE);
                }
                else
                {
                    structure.Tag = "_EXID";
                }

                return;

            case GedcomTags.TRAN:
                string language = structure.ChildValue(GedcomTags.LANG) ?? string.Empty;
                if (parentTag is GedcomTags.NAME or GedcomTags.PLAC && language.Contains("-Latn", StringComparison.OrdinalIgnoreCase))
                {
                    structure.Tag = GedcomTags.ROMN;
                    structure.RemoveChildren(GedcomTags.LANG);
                    structure.SetChildValue(GedcomTags.TYPE, structure.ChildValue("_TYPE") ?? language);
                    structure.RemoveChildren("_TYPE");
                }
                else if (parentTag is GedcomTags.NAME or GedcomTags.PLAC && language.Contains("fonipa", StringComparison.OrdinalIgnoreCase))
                {
                    structure.Tag = GedcomTags.FONE;
                    structure.RemoveChildren(GedcomTags.LANG);
                    structure.SetChildValue(GedcomTags.TYPE, structure.ChildValue("_TYPE") ?? "IPA");
                    structure.RemoveChildren("_TYPE");
                }
                else
                {
                    structure.Tag = "_TRAN";
                }

                return;

            case GedcomTags.LANG when structure.Value is { } languageTag:
                structure.Value = GedcomLanguages.ToName(languageTag);
                return;

            case GedcomTags.FILE when parentTag == GedcomTags.OBJE && structure.Value is { } url:
                structure.Value = MediaTypes.ToPath(url);
                if (structure.Child(GedcomTags.FORM) is { Value: { } form } formStructure)
                {
                    formStructure.Value = MediaTypes.ToLegacyFormat(form);
                }

                return;

            case GedcomTags.TITL when parentTag == GedcomTags.OBJE && structure.Parent!.HasPointer:
                structure.Tag = "_TITL";
                return;
        }

        if (sevenOnlyTags.Contains(structure.Tag))
        {
            structure.Tag = "_" + structure.Tag;
        }
    }

    private static void ConvertDateToLegacy(GedcomStructure structure, ConversionContext context)
    {
        var phraseChild = structure.Child(GedcomTags.PHRASE);
        var date = GedcomDate.Parse(structure.Value, phraseChild?.Value);
        if (phraseChild is not null)
        {
            structure.Children.Remove(phraseChild);
        }

        string value = date.Format(GedcomVersion.V551);
        structure.Value = value.Length == 0 ? null : value;
        if (date.Phrase is not null && date.Kind is not (GedcomDateKind.Interpreted or GedcomDateKind.Phrase))
        {
            structure.AddChild(GedcomTags.Extensions.PHRASE, date.Phrase);
            context.Info($"The phrase '{date.Phrase}' on a {date.Kind} date has no 5.x form; it was kept as _PHRASE.", structure);
        }
    }

    /// <summary>Adjustments between 5.5, 5.5.1 and 5.5.5.</summary>
    private static void LegacyToLegacy(ConversionContext context)
    {
        var target = context.Target;
        foreach (var structure in context.AllStructures())
        {
            string? parentTag = structure.Parent?.Tag;
            if (target == GedcomVersion.V55)
            {
                switch (structure.Tag)
                {
                    case GedcomTags.EMAIL or GedcomTags.FAX or GedcomTags.WWW:
                        structure.Tag = "_" + structure.Tag;
                        break;
                    case GedcomTags.MAP when parentTag == GedcomTags.PLAC:
                        structure.Tag = "_MAP";
                        break;
                    case GedcomTags.SEX when structure.Value == "X":
                        structure.Value = "U";
                        break;
                    case GedcomTags.OBJE when !structure.HasPointer:
                        FlattenObject(structure);
                        break;
                }

                continue;
            }

            switch (structure.Tag)
            {
                case GedcomTags.Extensions.EMAIL or GedcomTags.EMAI:
                    structure.Tag = GedcomTags.EMAIL;
                    break;
                case GedcomTags.Extensions.FAX:
                    structure.Tag = GedcomTags.FAX;
                    break;
                case GedcomTags.Extensions.WWW:
                    structure.Tag = GedcomTags.WWW;
                    break;
                case "_MAP" when parentTag == GedcomTags.PLAC:
                    structure.Tag = GedcomTags.MAP;
                    break;
                case GedcomTags.SEX when structure.Value == "X" && target != GedcomVersion.V555:
                    structure.Value = "U";
                    context.Info("SEX X (intersex) does not exist before GEDCOM 5.5.5; it was written as U.", structure);
                    break;
                case GedcomTags.OBJE when !structure.HasPointer && context.Source == GedcomVersion.V55:
                    RestructureObject(structure, context);
                    if (structure.Child(GedcomTags.BLOB) is { } blob)
                    {
                        blob.Tag = "_BLOB";
                    }

                    break;
                case GedcomTags.TYPE when parentTag == GedcomTags.ASSO && context.Source == GedcomVersion.V55:
                    structure.Remove();
                    break;
            }
        }
    }

    /// <summary>5.5 multimedia: one FILE with FORM and TITL beside it rather than under it.</summary>
    private static void FlattenObject(GedcomStructure obje)
    {
        var file = obje.Child(GedcomTags.FILE);
        if (file is null)
        {
            return;
        }

        foreach (string tag in (string[])[GedcomTags.FORM, GedcomTags.TITL])
        {
            if (file.Child(tag) is { } child && obje.Child(tag) is null)
            {
                file.Children.Remove(child);
                child.RemoveChildren(GedcomTags.TYPE);
                child.RemoveChildren(GedcomTags.MEDI);
                obje.Children.Add(child);
            }
        }
    }

    /// <summary>Sets HEAD.GEDC.VERS, and for 5.x HEAD.GEDC.FORM (with FORM.VERS for 5.5.5).</summary>
    internal static void UpdateVersionDeclaration(GedcomStructure header, GedcomVersion version)
    {
        var gedc = header.Child(GedcomTags.GEDC);
        if (gedc is null)
        {
            gedc = new GedcomStructure(GedcomTags.GEDC);
            int sourceIndex = header.Children.ToList().FindIndex(child => child.Tag == GedcomTags.SOUR);
            header.Children.Insert(sourceIndex < 0 ? 0 : sourceIndex + 1, gedc);
        }

        gedc.SetChildValue(GedcomTags.VERS, version.ToVersionString());
        if (version.IsSeven)
        {
            gedc.RemoveChildren(GedcomTags.FORM);
            return;
        }

        var form = gedc.Child(GedcomTags.FORM) ?? gedc.AddChild(GedcomTags.FORM);
        form.Value = "LINEAGE-LINKED";
        form.SetChildValue(GedcomTags.VERS, version == GedcomVersion.V555 ? "5.5.5" : null);
    }

    /// <summary>Renames xrefs the target does not allow (7.0: <c>[A-Z0-9_]</c>; 5.5.5: letters and digits), updating every pointer.</summary>
    internal static void NormalizeXrefs(GedcomFile file, GedcomVersion target, GedcomIssueCollector issues)
    {
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(file.Records.Select(record => record.Xref).OfType<string>(), StringComparer.Ordinal);
        foreach (var record in file.Records)
        {
            if (record.Xref is not string xref || Parsing.GedcomPostProcessor.IsValidXref(xref, target) || renames.ContainsKey(xref))
            {
                continue;
            }

            string candidate = new(xref.ToUpperInvariant().Where(c => char.IsAsciiLetterOrDigit(c) || (c == '_' && target.IsSeven)).ToArray());
            if (candidate.Length == 0 || candidate == GedcomStructure.VOID_POINTER || candidate.Length > 20)
            {
                candidate = GedcomFile.DefaultPrefix(record.Tag);
            }

            string unique = candidate;
            for (int n = 1; used.Contains(unique); n++)
            {
                unique = candidate + n.ToString(CultureInfo.InvariantCulture);
            }

            used.Add(unique);
            renames[xref] = unique;
        }

        if (renames.Count == 0)
        {
            return;
        }

        foreach (var record in file.Records)
        {
            if (record.Xref is string xref && renames.TryGetValue(xref, out string? renamed))
            {
                record.Xref = renamed;
            }
        }

        foreach (var structure in file.HeaderStructure.Descendants().Concat(file.Records.SelectMany(record => record.Descendants())))
        {
            if (structure.Pointer is string pointer && renames.TryGetValue(pointer, out string? renamed))
            {
                structure.Pointer = renamed;
            }
        }

        issues.Info(GedcomIssueCodes.INVALID_XREF, $"{renames.Count} cross-reference id(s) were renamed to suit GEDCOM {target.ToVersionString()}, e.g. @{renames.First().Key}@ to @{renames.First().Value}@.");
    }

    /// <summary>5.5.5 requires HEAD.SUBM pointing to a submitter record.</summary>
    internal static void EnsureSubmitter(GedcomFile file, GedcomIssueCollector issues)
    {
        var header = file.HeaderStructure;
        if (header.Child(GedcomTags.SUBM)?.Pointer is string pointer && file.Find(pointer) is { Tag: GedcomTags.SUBM })
        {
            return;
        }

        var submitter = file.Submitters.FirstOrDefault() ?? file.AddSubmitter("Unknown");
        header.SetChildPointer(GedcomTags.SUBM, submitter.Xref);
        issues.Info(GedcomIssueCodes.CONVERSION, $"GEDCOM 5.5.5 requires a submitter; HEAD.SUBM now points to @{submitter.Xref}@.");
    }
}
