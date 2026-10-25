using GedcomSharp.Values;
using ModelAddress = GedcomSharp.Model.Address;

namespace GedcomSharp.GedcomX;

/// <summary>
/// Converts between GEDCOM datasets and GEDCOM X documents. The models differ (GEDCOM X has relationships rather
/// than families, and references rather than pointers), so this covers the genealogical core — people, names,
/// facts, couples, parent-child links, sources, repositories, notes and media — not every GEDCOM structure.
/// </summary>
public static class GedcomXConverter
{
    private const string PARENT_CHILD_TYPE_PREFIX = "http://gedcomx.org/";

    private static readonly Dictionary<string, string> gedcomTagsByFactType = GedcomXTypes.FactTypesByGedcomTag
        .GroupBy(pair => pair.Value, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);

    public static GedcomXDocument ToGedcomX(GedcomFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var document = new GedcomXDocument { Lang = file.Header.Language is { } language ? Conversion.GedcomLanguages.ToTag(language) : null };

        foreach (var individual in file.Individuals)
        {
            document.Persons ??= [];
            document.Persons.Add(ToPerson(individual));
        }

        foreach (var family in file.Families)
        {
            AddFamilyRelationships(document, family);
        }

        foreach (var source in file.Sources)
        {
            document.SourceDescriptions ??= [];
            document.SourceDescriptions.Add(ToSourceDescription(source));
        }

        foreach (var multimedia in file.Multimedia)
        {
            document.SourceDescriptions ??= [];
            document.SourceDescriptions.Add(ToMediaDescription(multimedia));
        }

        foreach (var repository in file.Repositories)
        {
            document.Agents ??= [];
            document.Agents.Add(ToAgent(repository.Xref, repository.Name, repository.Contact));
        }

        foreach (var submitter in file.Submitters)
        {
            document.Agents ??= [];
            document.Agents.Add(ToAgent(submitter.Xref, submitter.Name, submitter.Contact));
        }

        return document;
    }

    /// <summary>Builds a GEDCOM dataset of <paramref name="version"/> from a GEDCOM X document.</summary>
    public static GedcomFile FromGedcomX(GedcomXDocument document, GedcomVersion version = GedcomVersion.V70)
    {
        ArgumentNullException.ThrowIfNull(document);
        var file = GedcomFile.Create(version);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);

        string Xref(string? id, string prefix)
        {
            if (id is not null && ids.TryGetValue(id, out string? existing))
            {
                return existing;
            }

            string candidate = id is null ? string.Empty : new string(id.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
            string xref = candidate.Length is > 0 and <= 20 && !file.Contains(candidate) ? candidate : file.CreateXref(prefix);
            if (id is not null)
            {
                ids[id] = xref;
            }

            return xref;
        }

        string? Reference(string? resource) => resource is { Length: > 1 } && resource[0] == '#' ? resource[1..] : resource;

        foreach (var agent in document.Agents ?? [])
        {
            bool isRepository = document.SourceDescriptions?.Any(source => Reference(source.Repository?.Resource) == agent.Id) == true;
            var contact = isRepository
                ? file.AddRepository(agent.Names?.FirstOrDefault()?.Value ?? "Unknown", Xref(agent.Id, "R")).Contact
                : file.AddSubmitter(agent.Names?.FirstOrDefault()?.Value ?? "Unknown", Xref(agent.Id, "U")).Contact;
            FromAgentContact(agent, contact);
        }

        foreach (var source in document.SourceDescriptions ?? [])
        {
            bool isMedia = source.MediaType is { } mediaType && !mediaType.StartsWith("text/", StringComparison.Ordinal) && source.About is not null;
            if (isMedia)
            {
                var multimedia = file.AddMultimedia(xref: Xref(source.Id, "O"));
                multimedia.AddFile(source.About!, version.IsSeven ? source.MediaType : MediaTypes.ToLegacyFormat(source.MediaType), source.Titles?.FirstOrDefault()?.Value);
                continue;
            }

            var record = file.AddSource(source.Titles?.FirstOrDefault()?.Value, Xref(source.Id, "S"));
            if (source.Citations?.FirstOrDefault()?.Value is string citation)
            {
                record.Publication = citation;
            }

            if (Reference(source.Repository?.Resource) is string repositoryId && ids.TryGetValue(repositoryId, out string? repositoryXref))
            {
                record.AddRepositoryCitation(repositoryXref);
            }

            AddNotes(record, source.Notes);
        }

        foreach (var person in document.Persons ?? [])
        {
            var individual = file.AddIndividual(Xref(person.Id, "I"));
            individual.Sex = person.Gender?.Type switch
            {
                GedcomXTypes.Gender.MALE => GedcomSex.Male,
                GedcomXTypes.Gender.FEMALE => GedcomSex.Female,
                GedcomXTypes.Gender.INTERSEX => GedcomSex.Intersex,
                GedcomXTypes.Gender.UNKNOWN => GedcomSex.Unknown,
                _ => GedcomSex.NotRecorded
            };

            foreach (var name in person.Names ?? [])
            {
                AddName(individual, name, version);
            }

            if (person.Private == true)
            {
                individual.Restriction = version.IsSeven ? "PRIVACY" : "privacy";
            }

            foreach (var fact in person.Facts ?? [])
            {
                AddFact(individual.Structure, fact, family: false);
            }

            AddNotes(individual, person.Notes);
            AddCitations(individual, person.Sources, Reference, ids);
            foreach (var media in person.Media ?? [])
            {
                if (Reference(media.Description) is string mediaId && ids.TryGetValue(mediaId, out string? mediaXref))
                {
                    individual.AddMediaLink(mediaXref);
                }
            }
        }

        AddFamilies(document, file, ids, Reference);
        return file;
    }

    private static Person ToPerson(IndividualRecord individual)
    {
        var person = new Person
        {
            Id = individual.Xref,
            Private = individual.IsPrivate ? true : null,
            Gender = individual.Sex switch
            {
                GedcomSex.Male => new Gender(GedcomXTypes.Gender.MALE),
                GedcomSex.Female => new Gender(GedcomXTypes.Gender.FEMALE),
                GedcomSex.Intersex => new Gender(GedcomXTypes.Gender.INTERSEX),
                GedcomSex.Unknown => new Gender(GedcomXTypes.Gender.UNKNOWN),
                _ => null
            }
        };

        foreach (var name in individual.Names)
        {
            person.Names ??= [];
            person.Names.Add(ToName(name));
        }

        foreach (var gedcomEvent in individual.Events)
        {
            person.Facts ??= [];
            person.Facts.Add(ToFact(gedcomEvent));
        }

        person.Notes = ToNotes(individual.Notes);
        person.Sources = ToSourceReferences(individual.SourceCitations);
        person.Media = individual.MediaLinks
            .Where(link => link.IsReference)
            .Select(link => new SourceReference { Description = "#" + link.MultimediaXref })
            .ToList() is { Count: > 0 } media ? media : null;
        foreach (string uid in individual.Uids)
        {
            person.Identifiers ??= [];
            person.Identifiers.Add(new Identifier(uid));
        }

        return person;
    }

    private static Name ToName(PersonalName name)
    {
        var form = new NameForm { FullText = name.DisplayName };
        void Part(string type, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                form.Parts ??= [];
                form.Parts.Add(new NamePart { Type = type, Value = value });
            }
        }

        Part(GedcomXTypes.NamePart.PREFIX, name.Prefix);
        Part(GedcomXTypes.NamePart.GIVEN, name.Given);
        Part(GedcomXTypes.NamePart.SURNAME, string.IsNullOrEmpty(name.SurnamePrefix) ? name.Surname : $"{name.SurnamePrefix} {name.Surname}");
        Part(GedcomXTypes.NamePart.SUFFIX, name.Suffix);

        string? type = name.Type?.ToUpperInvariant() switch
        {
            "BIRTH" => GedcomXTypes.NameType.BIRTH_NAME,
            "MARRIED" => GedcomXTypes.NameType.MARRIED_NAME,
            "AKA" => GedcomXTypes.NameType.ALSO_KNOWN_AS,
            "MAIDEN" => GedcomXTypes.NameType.BIRTH_NAME,
            _ => null
        };

        return new Name { Type = type, NameForms = [form], Notes = ToNotes(name.Notes), Sources = ToSourceReferences(name.SourceCitations) };
    }

    private static Fact ToFact(GedcomEvent gedcomEvent)
    {
        string type = GedcomXTypes.FactTypesByGedcomTag.TryGetValue(gedcomEvent.Tag, out string? known)
            ? known
            : "data:," + Uri.EscapeDataString(gedcomEvent.Type ?? gedcomEvent.Tag);

        var date = gedcomEvent.Date;
        return new Fact
        {
            Type = type,
            Date = date.IsEmpty ? null : new Date { Original = gedcomEvent.DateText ?? date.ToString(), Formal = ToFormalDate(date)?.ToString() },
            Place = gedcomEvent.PlaceName is { } place ? new PlaceReference { Original = place } : null,
            Value = gedcomEvent.Value is { Length: > 0 } value && value != "Y" ? value : null,
            Notes = ToNotes(gedcomEvent.Notes),
            Sources = ToSourceReferences(gedcomEvent.SourceCitations)
        };
    }

    /// <summary>The GEDCOM X formal form of a Gregorian GEDCOM date; null for other calendars and phrases.</summary>
    public static GedcomXDate? ToFormalDate(GedcomDate date)
    {
        GedcomXSimpleDate? Simple(GedcomDatePart? part)
        {
            if (part is null || part.Calendar != GedcomCalendar.Gregorian)
            {
                return null;
            }

            int year = part.DualYear ?? part.Year;
            return new GedcomXSimpleDate(part.IsBce ? 1 - year : year, part.Month, part.Month is null ? null : part.Day);
        }

        var start = Simple(date.Start);
        var end = Simple(date.End);
        return date.Kind switch
        {
            GedcomDateKind.Simple or GedcomDateKind.Interpreted when start is not null => GedcomXDate.Simple(start),
            GedcomDateKind.About or GedcomDateKind.Calculated or GedcomDateKind.Estimated when start is not null => GedcomXDate.Simple(start, approximate: true),
            GedcomDateKind.Before when start is not null => GedcomXDate.Range(null, start),
            GedcomDateKind.After or GedcomDateKind.From when start is not null => GedcomXDate.Range(start, null),
            GedcomDateKind.To when end is not null => GedcomXDate.Range(null, end),
            GedcomDateKind.Between when start is not null && end is not null => GedcomXDate.Range(start, end, approximate: true),
            GedcomDateKind.FromTo when start is not null && end is not null => GedcomXDate.Range(start, end),
            _ => null
        };
    }

    /// <summary>The GEDCOM date for a GEDCOM X formal date.</summary>
    public static GedcomDate FromFormalDate(GedcomXDate date)
    {
        static GedcomDatePart? Part(GedcomXSimpleDate? simple) => simple is null
            ? null
            : new GedcomDatePart(simple.Year <= 0 ? 1 - simple.Year : simple.Year, simple.Month, simple.Month is null ? null : simple.Day) { IsBce = simple.Year <= 0 };

        var start = Part(date.Start);
        var end = Part(date.End);
        if (!date.IsRange)
        {
            return start is null ? GedcomDate.Empty : GedcomDate.Create(date.IsApproximate ? GedcomDateKind.About : GedcomDateKind.Simple, start);
        }

        return (start, end) switch
        {
            (not null, not null) when date.IsApproximate => GedcomDate.Between(start, end),
            (not null, not null) => GedcomDate.Period(start, end),
            (null, not null) => GedcomDate.Create(GedcomDateKind.Before, end),
            (not null, null) => GedcomDate.Create(GedcomDateKind.After, start),
            _ => GedcomDate.Empty
        };
    }

    private static void AddFamilyRelationships(GedcomXDocument document, FamilyRecord family)
    {
        string? husband = family.HusbandXref;
        string? wife = family.WifeXref;
        document.Relationships ??= [];
        if (husband is not null && wife is not null)
        {
            document.Relationships.Add(new Relationship
            {
                Id = family.Xref,
                Type = GedcomXTypes.Relationship.COUPLE,
                Person1 = ResourceReference.ToLocal(husband),
                Person2 = ResourceReference.ToLocal(wife),
                Facts = family.Events.Select(ToFact).ToList() is { Count: > 0 } facts ? facts : null,
                Notes = ToNotes(family.Notes),
                Sources = ToSourceReferences(family.SourceCitations)
            });
        }

        foreach (var child in family.Children)
        {
            string? pedigree = child.ChildToFamilyLinks.FirstOrDefault(link => link.FamilyXref == family.Xref)?.Pedigree;
            string? lineage = pedigree switch
            {
                "BIRTH" => "BiologicalParent",
                "ADOPTED" => "AdoptiveParent",
                "FOSTER" => "FosterParent",
                _ => null
            };

            foreach (string? parent in (string?[])[husband, wife])
            {
                if (parent is null)
                {
                    continue;
                }

                document.Relationships.Add(new Relationship
                {
                    Type = GedcomXTypes.Relationship.PARENT_CHILD,
                    Person1 = ResourceReference.ToLocal(parent),
                    Person2 = ResourceReference.ToLocal(child.Xref!),
                    Facts = lineage is null ? null : [new Fact { Type = PARENT_CHILD_TYPE_PREFIX + lineage }]
                });
            }
        }
    }

    private static SourceDescription ToSourceDescription(SourceRecord source)
    {
        string citation = string.Join(", ", new[] { source.Author, source.Title, source.Publication }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return new SourceDescription
        {
            Id = source.Xref,
            Titles = source.Title is { } title ? [new TextValue(title)] : null,
            Citations = [new SourceCitation { Value = citation.Length > 0 ? citation : source.Xref }],
            Repository = source.RepositoryCitations.FirstOrDefault()?.RepositoryXref is { } repository ? ResourceReference.ToLocal(repository) : null,
            Notes = ToNotes(source.Notes)
        };
    }

    private static SourceDescription ToMediaDescription(MultimediaRecord multimedia)
    {
        var file = multimedia.PrimaryFile;
        return new SourceDescription
        {
            Id = multimedia.Xref,
            ResourceType = GedcomXTypes.ResourceType.DIGITAL_ARTIFACT,
            About = file?.Path,
            MediaType = file?.MediaType,
            Titles = multimedia.Title is { } title ? [new TextValue(title)] : null,
            Citations = [new SourceCitation { Value = multimedia.Title ?? file?.Path ?? multimedia.Xref }]
        };
    }

    private static Agent ToAgent(string? id, string? name, ContactDetails contact)
    {
        var agent = new Agent { Id = id, Names = name is null ? null : [new TextValue(name)] };
        if (contact.Address is ModelAddress address)
        {
            agent.Addresses =
            [
                new Address
                {
                    Value = address.Lines,
                    Street = address.Line1,
                    Street2 = address.Line2,
                    Street3 = address.Line3,
                    City = address.City,
                    StateOrProvince = address.State,
                    PostalCode = address.PostalCode,
                    Country = address.Country
                }
            ];
        }

        agent.Emails = contact.Emails.Select(email => new ResourceReference("mailto:" + email)).ToList() is { Count: > 0 } emails ? emails : null;
        agent.Phones = contact.Phones.Select(phone => new ResourceReference("tel:" + phone)).ToList() is { Count: > 0 } phones ? phones : null;
        agent.Homepage = contact.Websites.FirstOrDefault() is { } site ? new ResourceReference(site) : null;
        return agent;
    }

    private static void FromAgentContact(Agent agent, ContactDetails contact)
    {
        if (agent.Addresses?.FirstOrDefault() is { } address)
        {
            var target = contact.GetOrAddAddress();
            target.Lines = address.Value;
            target.Line1 = address.Street;
            target.Line2 = address.Street2;
            target.Line3 = address.Street3;
            target.City = address.City;
            target.State = address.StateOrProvince;
            target.PostalCode = address.PostalCode;
            target.Country = address.Country;
        }

        foreach (var email in agent.Emails ?? [])
        {
            if (email.Resource is { } resource)
            {
                contact.AddEmail(resource.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? resource[7..] : resource);
            }
        }

        foreach (var phone in agent.Phones ?? [])
        {
            if (phone.Resource is { } resource)
            {
                contact.AddPhone(resource.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ? resource[4..] : resource);
            }
        }

        if (agent.Homepage?.Resource is { } homepage)
        {
            contact.AddWebsite(homepage);
        }
    }

    private static List<Note>? ToNotes(IEnumerable<NoteLink> notes)
    {
        var list = notes.Select(note => note.Text).OfType<string>().Select(text => new Note { Text = text }).ToList();
        return list.Count == 0 ? null : list;
    }

    private static List<SourceReference>? ToSourceReferences(IEnumerable<Model.SourceCitation> citations)
    {
        var list = citations
            .Where(citation => citation.IsReference)
            .Select(citation => new SourceReference
            {
                Description = "#" + citation.SourceXref,
                Qualifiers = citation.Page is { } page ? [new Qualifier("http://gedcomsharp.org/Page", page)] : null
            })
            .ToList();
        return list.Count == 0 ? null : list;
    }

    private static void AddName(IndividualRecord individual, Name name, GedcomVersion version)
    {
        var form = name.NameForms?.FirstOrDefault();
        string? Part(string type) => form?.Parts?.FirstOrDefault(part => part.Type == type)?.Value;
        string? given = Part(GedcomXTypes.NamePart.GIVEN);
        string? surname = Part(GedcomXTypes.NamePart.SURNAME);
        var personalName = given is null && surname is null
            ? individual.AddName(form?.FullText ?? string.Empty)
            : individual.AddName(given, surname);
        personalName.Prefix = Part(GedcomXTypes.NamePart.PREFIX);
        personalName.Suffix = Part(GedcomXTypes.NamePart.SUFFIX);
        string? type = name.Type switch
        {
            GedcomXTypes.NameType.BIRTH_NAME => "BIRTH",
            GedcomXTypes.NameType.MARRIED_NAME => "MARRIED",
            GedcomXTypes.NameType.ALSO_KNOWN_AS or GedcomXTypes.NameType.NICKNAME => "AKA",
            _ => null
        };

        if (type is not null)
        {
            personalName.Type = version.IsSeven ? type : type.ToLowerInvariant();
        }
    }

    private static void AddFact(GedcomStructure owner, Fact fact, bool family)
    {
        string? tag = fact.Type is { } type && gedcomTagsByFactType.TryGetValue(type, out string? known) ? known : null;
        var structure = owner.AddChild(tag ?? (family ? GedcomTags.EVEN : GedcomTags.FACT));
        if (tag is null)
        {
            string typeName = fact.Type is null ? "Fact" : Uri.UnescapeDataString(fact.Type.StartsWith("data:,", StringComparison.Ordinal) ? fact.Type[6..] : fact.Type[(fact.Type.LastIndexOf('/') + 1)..]);
            structure.AddChild(GedcomTags.TYPE, typeName);
        }

        if (fact.Value is { Length: > 0 } value)
        {
            structure.Value = value;
        }
        else if (tag is not null && !GedcomTags.IndividualAttributes.Contains(tag))
        {
            structure.Value = "Y";
        }

        var gedcomEvent = new GedcomEvent(structure);
        if (fact.Date is { } date)
        {
            var parsed = GedcomXDate.Parse(date.Formal) is { } formal ? FromFormalDate(formal) : GedcomDate.Parse(date.Original);
            if (parsed.Kind == GedcomDateKind.Unparsed && date.Original is not null)
            {
                parsed = GedcomDate.FromPhrase(date.Original);
            }

            gedcomEvent.Date = parsed;
        }

        if (fact.Place?.Original is { } place)
        {
            gedcomEvent.PlaceName = place;
        }

        AddNotes(gedcomEvent, fact.Notes);
    }

    private static void AddNotes(GedcomCitableView target, List<Note>? notes)
    {
        foreach (var note in notes ?? [])
        {
            if (note.Text is { Length: > 0 } text)
            {
                target.AddNote(note.Subject is { Length: > 0 } subject ? $"{subject}\n{text}" : text);
            }
        }
    }

    private static void AddCitations(GedcomCitableView target, List<SourceReference>? sources, Func<string?, string?> reference, Dictionary<string, string> ids)
    {
        foreach (var source in sources ?? [])
        {
            if (reference(source.Description) is string id && ids.TryGetValue(id, out string? xref))
            {
                target.AddSourceCitation(xref, source.Qualifiers?.FirstOrDefault(qualifier => qualifier.Name?.EndsWith("/Page", StringComparison.Ordinal) == true)?.Value);
            }
        }
    }

    /// <summary>GEDCOM X couples and parent-child links become FAM records: one per couple, plus one per other set of parents.</summary>
    private static void AddFamilies(GedcomXDocument document, GedcomFile file, Dictionary<string, string> ids, Func<string?, string?> reference)
    {
        string? PersonXref(ResourceReference? person) => reference(person?.Resource) is string id && ids.TryGetValue(id, out string? xref) ? xref : null;

        var families = new Dictionary<(string?, string?), FamilyRecord>();
        FamilyRecord GetFamily(string? first, string? second)
        {
            var partners = new[] { first, second }.Where(xref => xref is not null).Order(StringComparer.Ordinal).ToArray();
            var key = (partners.ElementAtOrDefault(0), partners.ElementAtOrDefault(1));
            if (families.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var husband = first is null ? null : file.Find<IndividualRecord>(first);
            var wife = second is null ? null : file.Find<IndividualRecord>(second);
            if (husband?.Sex == GedcomSex.Female || wife?.Sex == GedcomSex.Male)
            {
                (husband, wife) = (wife, husband);
            }

            var family = file.AddFamily(husband, wife);
            families[key] = family;
            return family;
        }

        foreach (var couple in (document.Relationships ?? []).Where(relationship => relationship.Type == GedcomXTypes.Relationship.COUPLE))
        {
            var family = GetFamily(PersonXref(couple.Person1), PersonXref(couple.Person2));
            foreach (var fact in couple.Facts ?? [])
            {
                AddFact(family.Structure, fact, family: true);
            }

            AddNotes(family, couple.Notes);
        }

        var parentsByChild = (document.Relationships ?? [])
            .Where(relationship => relationship.Type == GedcomXTypes.Relationship.PARENT_CHILD)
            .Select(relationship => (Parent: PersonXref(relationship.Person1), Child: PersonXref(relationship.Person2), Relationship: relationship))
            .Where(link => link.Parent is not null && link.Child is not null)
            .GroupBy(link => link.Child!, StringComparer.Ordinal);

        foreach (var links in parentsByChild)
        {
            var parents = links.Select(link => link.Parent).Distinct().ToList();
            var child = file.Find<IndividualRecord>(links.Key);
            if (child is null)
            {
                continue;
            }

            string? lineage = links.SelectMany(link => link.Relationship.Facts ?? []).Select(fact => fact.Type).FirstOrDefault(type => type is not null);
            string? pedigree = lineage?[(lineage.LastIndexOf('/') + 1)..] switch
            {
                "BiologicalParent" => "BIRTH",
                "AdoptiveParent" => "ADOPTED",
                "FosterParent" => "FOSTER",
                _ => null
            };

            for (int i = 0; i < parents.Count; i += 2)
            {
                var family = GetFamily(parents[i], i + 1 < parents.Count ? parents[i + 1] : null);
                file.AddChildToFamily(family, child, pedigree);
            }
        }
    }
}
