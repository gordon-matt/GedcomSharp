using System.Xml;
using System.Xml.Linq;

namespace GedcomSharp.GedcomX;

/// <summary>Maps GEDCOM X documents to and from the XML format (namespace <c>http://gedcomx.org/v1/</c>).</summary>
internal static class GedcomXXml
{
    private static readonly XNamespace gx = GedcomXTypes.XML_NAMESPACE;
    private static readonly XName langName = XNamespace.Xml + "lang";

    private static readonly string[] conclusionElements = ["attribution", "source", "analysis", "note"];
    private static readonly string[] conclusionAttributes = ["id", "lang", "confidence"];
    private static readonly string[] subjectElements = [.. conclusionElements, "evidence", "media", "identifier"];
    private static readonly string[] subjectAttributes = [.. conclusionAttributes, "extracted"];

    public static XDocument Write(GedcomXDocument document)
    {
        var root = new XElement(gx + "gedcomx",
            Attr("id", document.Id),
            Lang(document.Lang),
            Attr("description", document.Description),
            Single("attribution", document.Attribution, WriteAttribution),
            Many("person", document.Persons, WritePerson),
            Many("relationship", document.Relationships, WriteRelationship),
            Many("sourceDescription", document.SourceDescriptions, WriteSourceDescription),
            Many("agent", document.Agents, WriteAgent),
            Many("event", document.Events, WriteEvent),
            Many("document", document.Documents, WriteDocument),
            Many("place", document.Places, WritePlaceDescription),
            Many("group", document.Groups, WriteGroup),
            Extensions(document));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    public static GedcomXDocument Read(XDocument xml)
    {
        var root = xml.Root ?? throw new XmlException("The XML document is empty.");
        if (root.Name != gx + "gedcomx")
        {
            throw new XmlException($"The root element is {root.Name}, not {gx + "gedcomx"}.");
        }

        var document = new GedcomXDocument
        {
            Id = A(root, "id"),
            Lang = (string?)root.Attribute(langName),
            Description = A(root, "description"),
            Attribution = One(root, "attribution", ReadAttribution),
            Persons = ListOf(root, "person", ReadPerson),
            Relationships = ListOf(root, "relationship", ReadRelationship),
            SourceDescriptions = ListOf(root, "sourceDescription", ReadSourceDescription),
            Agents = ListOf(root, "agent", ReadAgent),
            Events = ListOf(root, "event", ReadEvent),
            Documents = ListOf(root, "document", ReadDocument),
            Places = ListOf(root, "place", ReadPlaceDescription),
            Groups = ListOf(root, "group", ReadGroup)
        };

        Capture(document, root, ["attribution", "person", "relationship", "sourceDescription", "agent", "event", "document", "place", "group"], ["id", "lang", "description"]);
        return document;
    }

    private static XElement WritePerson(string name, Person person) => new(gx + name,
        WriteSubjectAttributes(person),
        Attr("private", person.Private),
        WriteSubjectElements(person),
        Single("gender", person.Gender, WriteGender),
        Many("name", person.Names, WriteName),
        Many("fact", person.Facts, WriteFact),
        Extensions(person));

    private static Person ReadPerson(XElement element)
    {
        var person = ReadSubject(element, new Person());
        person.Private = Bool(element, "private");
        person.Gender = One(element, "gender", ReadGender);
        person.Names = ListOf(element, "name", ReadName);
        person.Facts = ListOf(element, "fact", ReadFact);
        Capture(person, element, [.. subjectElements, "gender", "name", "fact"], [.. subjectAttributes, "private"]);
        return person;
    }

    private static XElement WriteRelationship(string name, Relationship relationship) => new(gx + name,
        WriteSubjectAttributes(relationship),
        Attr("type", relationship.Type),
        WriteSubjectElements(relationship),
        Single("person1", relationship.Person1, WriteResource),
        Single("person2", relationship.Person2, WriteResource),
        Many("fact", relationship.Facts, WriteFact),
        Extensions(relationship));

    private static Relationship ReadRelationship(XElement element)
    {
        var relationship = ReadSubject(element, new Relationship());
        relationship.Type = A(element, "type");
        relationship.Person1 = One(element, "person1", ReadResource);
        relationship.Person2 = One(element, "person2", ReadResource);
        relationship.Facts = ListOf(element, "fact", ReadFact);
        Capture(relationship, element, [.. subjectElements, "person1", "person2", "fact"], [.. subjectAttributes, "type"]);
        return relationship;
    }

    private static XElement WriteSourceDescription(string name, SourceDescription source) => new(gx + name,
        Attr("id", source.Id),
        Attr("resourceType", source.ResourceType),
        Attr("mediaType", source.MediaType),
        Attr("about", source.About),
        Attr("sortKey", source.SortKey),
        Many("citation", source.Citations, WriteCitation),
        Single("mediator", source.Mediator, WriteResource),
        Single("publisher", source.Publisher, WriteResource),
        Many("author", source.Authors, WriteResource),
        Many("source", source.Sources, WriteSourceReference),
        Single("analysis", source.Analysis, WriteResource),
        Single("componentOf", source.ComponentOf, WriteSourceReference),
        Many("title", source.Titles, WriteTextValue),
        Many("note", source.Notes, WriteNote),
        Single("attribution", source.Attribution, WriteAttribution),
        Many("rights", source.Rights, WriteResource),
        Many("coverage", source.Coverage, WriteCoverage),
        Many("description", source.Descriptions, WriteTextValue),
        Many("identifier", source.Identifiers, WriteIdentifier),
        Timestamp("created", source.Created),
        Timestamp("modified", source.Modified),
        Timestamp("published", source.Published),
        Single("repository", source.Repository, WriteResource),
        Extensions(source));

    private static SourceDescription ReadSourceDescription(XElement element)
    {
        var source = new SourceDescription
        {
            Id = A(element, "id"),
            ResourceType = A(element, "resourceType"),
            MediaType = A(element, "mediaType"),
            About = A(element, "about"),
            SortKey = A(element, "sortKey"),
            Citations = ListOf(element, "citation", ReadCitation),
            Mediator = One(element, "mediator", ReadResource),
            Publisher = One(element, "publisher", ReadResource),
            Authors = ListOf(element, "author", ReadResource),
            Sources = ListOf(element, "source", ReadSourceReference),
            Analysis = One(element, "analysis", ReadResource),
            ComponentOf = One(element, "componentOf", ReadSourceReference),
            Titles = ListOf(element, "title", ReadTextValue),
            Notes = ListOf(element, "note", ReadNote),
            Attribution = One(element, "attribution", ReadAttribution),
            Rights = ListOf(element, "rights", ReadResource),
            Coverage = ListOf(element, "coverage", ReadCoverage),
            Descriptions = ListOf(element, "description", ReadTextValue),
            Identifiers = ListOf(element, "identifier", ReadIdentifier),
            Created = ReadTimestamp(element, "created"),
            Modified = ReadTimestamp(element, "modified"),
            Published = ReadTimestamp(element, "published"),
            Repository = One(element, "repository", ReadResource)
        };

        Capture(source, element,
            ["citation", "mediator", "publisher", "author", "source", "analysis", "componentOf", "title", "note", "attribution", "rights", "coverage", "description", "identifier", "created", "modified", "published", "repository"],
            ["id", "resourceType", "mediaType", "about", "sortKey"]);
        return source;
    }

    private static XElement WriteAgent(string name, Agent agent) => new(gx + name,
        Attr("id", agent.Id),
        Many("identifier", agent.Identifiers, WriteIdentifier),
        Many("name", agent.Names, WriteTextValue),
        Single("homepage", agent.Homepage, WriteResource),
        Single("openid", agent.Openid, WriteResource),
        Many("account", agent.Accounts, WriteAccount),
        Many("email", agent.Emails, WriteResource),
        Many("phone", agent.Phones, WriteResource),
        Many("address", agent.Addresses, WriteAddress),
        Single("person", agent.Person, WriteResource),
        Extensions(agent));

    private static Agent ReadAgent(XElement element)
    {
        var agent = new Agent
        {
            Id = A(element, "id"),
            Identifiers = ListOf(element, "identifier", ReadIdentifier),
            Names = ListOf(element, "name", ReadTextValue),
            Homepage = One(element, "homepage", ReadResource),
            Openid = One(element, "openid", ReadResource),
            Accounts = ListOf(element, "account", ReadAccount),
            Emails = ListOf(element, "email", ReadResource),
            Phones = ListOf(element, "phone", ReadResource),
            Addresses = ListOf(element, "address", ReadAddress),
            Person = One(element, "person", ReadResource)
        };

        Capture(agent, element, ["identifier", "name", "homepage", "openid", "account", "email", "phone", "address", "person"], ["id"]);
        return agent;
    }

    private static XElement WriteEvent(string name, Event value) => new(gx + name,
        WriteSubjectAttributes(value),
        Attr("type", value.Type),
        WriteSubjectElements(value),
        Single("date", value.Date, WriteDate),
        Single("place", value.Place, WritePlaceReference),
        Many("role", value.Roles, WriteEventRole),
        Extensions(value));

    private static Event ReadEvent(XElement element)
    {
        var value = ReadSubject(element, new Event());
        value.Type = A(element, "type");
        value.Date = One(element, "date", ReadDate);
        value.Place = One(element, "place", ReadPlaceReference);
        value.Roles = ListOf(element, "role", ReadEventRole);
        Capture(value, element, [.. subjectElements, "date", "place", "role"], [.. subjectAttributes, "type"]);
        return value;
    }

    private static XElement WriteEventRole(string name, EventRole role) => new(gx + name,
        WriteConclusionAttributes(role),
        Attr("type", role.Type),
        WriteConclusionElements(role),
        Single("person", role.Person, WriteResource),
        Text("details", role.Details),
        Extensions(role));

    private static EventRole ReadEventRole(XElement element)
    {
        var role = ReadConclusion(element, new EventRole());
        role.Type = A(element, "type");
        role.Person = One(element, "person", ReadResource);
        role.Details = ChildText(element, "details");
        Capture(role, element, [.. conclusionElements, "person", "details"], [.. conclusionAttributes, "type"]);
        return role;
    }

    private static XElement WriteDocument(string name, Document document) => new(gx + name,
        WriteConclusionAttributes(document),
        Attr("type", document.Type),
        Attr("extracted", document.Extracted),
        Attr("textType", document.TextType),
        WriteConclusionElements(document),
        Text("text", document.Text),
        Extensions(document));

    private static Document ReadDocument(XElement element)
    {
        var document = ReadConclusion(element, new Document());
        document.Type = A(element, "type");
        document.Extracted = Bool(element, "extracted");
        document.TextType = A(element, "textType");
        document.Text = ChildText(element, "text");
        Capture(document, element, [.. conclusionElements, "text"], [.. conclusionAttributes, "type", "extracted", "textType"]);
        return document;
    }

    private static XElement WritePlaceDescription(string name, PlaceDescription place) => new(gx + name,
        WriteSubjectAttributes(place),
        Attr("type", place.Type),
        WriteSubjectElements(place),
        Many("name", place.Names, WriteTextValue),
        Single("place", place.Place, WriteResource),
        Single("jurisdiction", place.Jurisdiction, WriteResource),
        Text("latitude", place.Latitude is double latitude ? XmlConvert.ToString(latitude) : null),
        Text("longitude", place.Longitude is double longitude ? XmlConvert.ToString(longitude) : null),
        Single("temporalDescription", place.TemporalDescription, WriteDate),
        Single("spatialDescription", place.SpatialDescription, WriteResource),
        Extensions(place));

    private static PlaceDescription ReadPlaceDescription(XElement element)
    {
        var place = ReadSubject(element, new PlaceDescription());
        place.Type = A(element, "type");
        place.Names = ListOf(element, "name", ReadTextValue);
        place.Place = One(element, "place", ReadResource);
        place.Jurisdiction = One(element, "jurisdiction", ReadResource);
        place.Latitude = ChildText(element, "latitude") is string latitude ? XmlConvert.ToDouble(latitude) : null;
        place.Longitude = ChildText(element, "longitude") is string longitude ? XmlConvert.ToDouble(longitude) : null;
        place.TemporalDescription = One(element, "temporalDescription", ReadDate);
        place.SpatialDescription = One(element, "spatialDescription", ReadResource);
        Capture(place, element, [.. subjectElements, "name", "place", "jurisdiction", "latitude", "longitude", "temporalDescription", "spatialDescription"], [.. subjectAttributes, "type"]);
        return place;
    }

    private static XElement WriteGroup(string name, Group group) => new(gx + name,
        WriteSubjectAttributes(group),
        WriteSubjectElements(group),
        Many("name", group.Names, WriteTextValue),
        Single("date", group.Date, WriteDate),
        Single("place", group.Place, WritePlaceReference),
        Many("role", group.Roles, WriteGroupRole),
        Extensions(group));

    private static Group ReadGroup(XElement element)
    {
        var group = ReadSubject(element, new Group());
        group.Names = ListOf(element, "name", ReadTextValue);
        group.Date = One(element, "date", ReadDate);
        group.Place = One(element, "place", ReadPlaceReference);
        group.Roles = ListOf(element, "role", ReadGroupRole);
        Capture(group, element, [.. subjectElements, "name", "date", "place", "role"], subjectAttributes);
        return group;
    }

    private static XElement WriteGroupRole(string name, GroupRole role) => new(gx + name,
        WriteConclusionAttributes(role),
        Attr("type", role.Type),
        WriteConclusionElements(role),
        Single("person", role.Person, WriteResource),
        Single("date", role.Date, WriteDate),
        Text("details", role.Details),
        Extensions(role));

    private static GroupRole ReadGroupRole(XElement element)
    {
        var role = ReadConclusion(element, new GroupRole());
        role.Type = A(element, "type");
        role.Person = One(element, "person", ReadResource);
        role.Date = One(element, "date", ReadDate);
        role.Details = ChildText(element, "details");
        Capture(role, element, [.. conclusionElements, "person", "date", "details"], [.. conclusionAttributes, "type"]);
        return role;
    }

    private static XElement WriteGender(string name, Gender gender) => new(gx + name,
        WriteConclusionAttributes(gender),
        Attr("type", gender.Type),
        WriteConclusionElements(gender),
        Extensions(gender));

    private static Gender ReadGender(XElement element)
    {
        var gender = ReadConclusion(element, new Gender());
        gender.Type = A(element, "type");
        Capture(gender, element, conclusionElements, [.. conclusionAttributes, "type"]);
        return gender;
    }

    private static XElement WriteName(string name, Name value) => new(gx + name,
        WriteConclusionAttributes(value),
        Attr("type", value.Type),
        WriteConclusionElements(value),
        Single("date", value.Date, WriteDate),
        Many("nameForm", value.NameForms, WriteNameForm),
        Extensions(value));

    private static Name ReadName(XElement element)
    {
        var value = ReadConclusion(element, new Name());
        value.Type = A(element, "type");
        value.Date = One(element, "date", ReadDate);
        value.NameForms = ListOf(element, "nameForm", ReadNameForm);
        Capture(value, element, [.. conclusionElements, "date", "nameForm"], [.. conclusionAttributes, "type"]);
        return value;
    }

    private static XElement WriteNameForm(string name, NameForm form) => new(gx + name,
        Attr("id", form.Id),
        Lang(form.Lang),
        Text("fullText", form.FullText),
        Many("part", form.Parts, WriteNamePart),
        Extensions(form));

    private static NameForm ReadNameForm(XElement element)
    {
        var form = new NameForm
        {
            Id = A(element, "id"),
            Lang = (string?)element.Attribute(langName),
            FullText = ChildText(element, "fullText"),
            Parts = ListOf(element, "part", ReadNamePart)
        };

        Capture(form, element, ["fullText", "part"], ["id", "lang"]);
        return form;
    }

    private static XElement WriteNamePart(string name, NamePart part) => new(gx + name,
        Attr("id", part.Id),
        Attr("type", part.Type),
        Attr("value", part.Value),
        Many("qualifier", part.Qualifiers, WriteQualifier),
        Extensions(part));

    private static NamePart ReadNamePart(XElement element)
    {
        var part = new NamePart
        {
            Id = A(element, "id"),
            Type = A(element, "type"),
            Value = A(element, "value"),
            Qualifiers = ListOf(element, "qualifier", ReadQualifier)
        };

        Capture(part, element, ["qualifier"], ["id", "type", "value"]);
        return part;
    }

    private static XElement WriteFact(string name, Fact fact) => new(gx + name,
        WriteConclusionAttributes(fact),
        Attr("type", fact.Type),
        WriteConclusionElements(fact),
        Single("date", fact.Date, WriteDate),
        Single("place", fact.Place, WritePlaceReference),
        Text("value", fact.Value),
        Many("qualifier", fact.Qualifiers, WriteQualifier),
        Extensions(fact));

    private static Fact ReadFact(XElement element)
    {
        var fact = ReadConclusion(element, new Fact());
        fact.Type = A(element, "type");
        fact.Date = One(element, "date", ReadDate);
        fact.Place = One(element, "place", ReadPlaceReference);
        fact.Value = ChildText(element, "value");
        fact.Qualifiers = ListOf(element, "qualifier", ReadQualifier);
        Capture(fact, element, [.. conclusionElements, "date", "place", "value", "qualifier"], [.. conclusionAttributes, "type"]);
        return fact;
    }

    private static XElement WriteDate(string name, Date date) => new(gx + name,
        Attr("id", date.Id),
        Text("original", date.Original),
        Text("formal", date.Formal),
        Many("normalized", date.Normalized, WriteTextValue),
        Extensions(date));

    private static Date ReadDate(XElement element)
    {
        var date = new Date
        {
            Id = A(element, "id"),
            Original = ChildText(element, "original"),
            Formal = ChildText(element, "formal"),
            Normalized = ListOf(element, "normalized", ReadTextValue)
        };

        Capture(date, element, ["original", "formal", "normalized"], ["id"]);
        return date;
    }

    private static XElement WritePlaceReference(string name, PlaceReference place) => new(gx + name,
        Attr("id", place.Id),
        Attr("description", place.Description),
        Text("original", place.Original),
        Extensions(place));

    private static PlaceReference ReadPlaceReference(XElement element)
    {
        var place = new PlaceReference
        {
            Id = A(element, "id"),
            Description = A(element, "description"),
            Original = ChildText(element, "original")
        };

        Capture(place, element, ["original"], ["id", "description"]);
        return place;
    }

    private static XElement WriteAttribution(string name, Attribution attribution) => new(gx + name,
        Attr("id", attribution.Id),
        Single("contributor", attribution.Contributor, WriteResource),
        Timestamp("modified", attribution.Modified),
        Text("changeMessage", attribution.ChangeMessage),
        Single("creator", attribution.Creator, WriteResource),
        Timestamp("created", attribution.Created),
        Extensions(attribution));

    private static Attribution ReadAttribution(XElement element)
    {
        var attribution = new Attribution
        {
            Id = A(element, "id"),
            Contributor = One(element, "contributor", ReadResource),
            Modified = ReadTimestamp(element, "modified"),
            ChangeMessage = ChildText(element, "changeMessage"),
            Creator = One(element, "creator", ReadResource),
            Created = ReadTimestamp(element, "created")
        };

        Capture(attribution, element, ["contributor", "modified", "changeMessage", "creator", "created"], ["id"]);
        return attribution;
    }

    private static XElement WriteNote(string name, Note note) => new(gx + name,
        Attr("id", note.Id),
        Lang(note.Lang),
        Text("subject", note.Subject),
        Text("text", note.Text),
        Single("attribution", note.Attribution, WriteAttribution),
        Extensions(note));

    private static Note ReadNote(XElement element)
    {
        var note = new Note
        {
            Id = A(element, "id"),
            Lang = (string?)element.Attribute(langName),
            Subject = ChildText(element, "subject"),
            Text = ChildText(element, "text"),
            Attribution = One(element, "attribution", ReadAttribution)
        };

        Capture(note, element, ["subject", "text", "attribution"], ["id", "lang"]);
        return note;
    }

    private static XElement WriteSourceReference(string name, SourceReference reference) => new(gx + name,
        Attr("id", reference.Id),
        Attr("description", reference.Description),
        Attr("descriptionId", reference.DescriptionId),
        Single("attribution", reference.Attribution, WriteAttribution),
        Many("qualifier", reference.Qualifiers, WriteQualifier),
        Extensions(reference));

    private static SourceReference ReadSourceReference(XElement element)
    {
        var reference = new SourceReference
        {
            Id = A(element, "id"),
            Description = A(element, "description"),
            DescriptionId = A(element, "descriptionId"),
            Attribution = One(element, "attribution", ReadAttribution),
            Qualifiers = ListOf(element, "qualifier", ReadQualifier)
        };

        Capture(reference, element, ["attribution", "qualifier"], ["id", "description", "descriptionId"]);
        return reference;
    }

    private static XElement WriteEvidence(string name, EvidenceReference evidence) => new(gx + name,
        Attr("id", evidence.Id),
        Attr("resource", evidence.Resource),
        Single("attribution", evidence.Attribution, WriteAttribution),
        Extensions(evidence));

    private static EvidenceReference ReadEvidence(XElement element)
    {
        var evidence = new EvidenceReference
        {
            Id = A(element, "id"),
            Resource = A(element, "resource"),
            Attribution = One(element, "attribution", ReadAttribution)
        };

        Capture(evidence, element, ["attribution"], ["id", "resource"]);
        return evidence;
    }

    private static XElement WriteCitation(string name, SourceCitation citation) => new(gx + name,
        Attr("id", citation.Id),
        Lang(citation.Lang),
        Text("value", citation.Value),
        Extensions(citation));

    private static SourceCitation ReadCitation(XElement element)
    {
        var citation = new SourceCitation
        {
            Id = A(element, "id"),
            Lang = (string?)element.Attribute(langName),
            Value = ChildText(element, "value")
        };

        Capture(citation, element, ["value"], ["id", "lang"]);
        return citation;
    }

    private static XElement WriteAccount(string name, OnlineAccount account) => new(gx + name,
        Attr("id", account.Id),
        Single("serviceHomepage", account.ServiceHomepage, WriteResource),
        Text("accountName", account.AccountName),
        Extensions(account));

    private static OnlineAccount ReadAccount(XElement element)
    {
        var account = new OnlineAccount
        {
            Id = A(element, "id"),
            ServiceHomepage = One(element, "serviceHomepage", ReadResource),
            AccountName = ChildText(element, "accountName")
        };

        Capture(account, element, ["serviceHomepage", "accountName"], ["id"]);
        return account;
    }

    private static readonly string[] addressElements = ["value", "city", "country", "postalCode", "stateOrProvince", "street", "street2", "street3", "street4", "street5", "street6"];

    private static XElement WriteAddress(string name, Address address) => new(gx + name,
        Attr("id", address.Id),
        Text("value", address.Value),
        Text("city", address.City),
        Text("country", address.Country),
        Text("postalCode", address.PostalCode),
        Text("stateOrProvince", address.StateOrProvince),
        Text("street", address.Street),
        Text("street2", address.Street2),
        Text("street3", address.Street3),
        Text("street4", address.Street4),
        Text("street5", address.Street5),
        Text("street6", address.Street6),
        Extensions(address));

    private static Address ReadAddress(XElement element)
    {
        var address = new Address
        {
            Id = A(element, "id"),
            Value = ChildText(element, "value"),
            City = ChildText(element, "city"),
            Country = ChildText(element, "country"),
            PostalCode = ChildText(element, "postalCode"),
            StateOrProvince = ChildText(element, "stateOrProvince"),
            Street = ChildText(element, "street"),
            Street2 = ChildText(element, "street2"),
            Street3 = ChildText(element, "street3"),
            Street4 = ChildText(element, "street4"),
            Street5 = ChildText(element, "street5"),
            Street6 = ChildText(element, "street6")
        };

        Capture(address, element, addressElements, ["id"]);
        return address;
    }

    private static XElement WriteCoverage(string name, Coverage coverage) => new(gx + name,
        Attr("id", coverage.Id),
        Single("spatial", coverage.Spatial, WritePlaceReference),
        Single("temporal", coverage.Temporal, WriteDate),
        Extensions(coverage));

    private static Coverage ReadCoverage(XElement element)
    {
        var coverage = new Coverage
        {
            Id = A(element, "id"),
            Spatial = One(element, "spatial", ReadPlaceReference),
            Temporal = One(element, "temporal", ReadDate)
        };

        Capture(coverage, element, ["spatial", "temporal"], ["id"]);
        return coverage;
    }

    private static XElement WriteResource(string name, ResourceReference reference) => new(gx + name, Attr("resource", reference.Resource));

    private static ResourceReference ReadResource(XElement element) => new() { Resource = A(element, "resource") };

    private static XElement WriteIdentifier(string name, Identifier identifier) => new(gx + name, Attr("type", identifier.Type), identifier.Value);

    private static Identifier ReadIdentifier(XElement element) => new(element.Value, A(element, "type"));

    private static XElement WriteQualifier(string name, Qualifier qualifier) => new(gx + name, Attr("name", qualifier.Name), qualifier.Value);

    private static Qualifier ReadQualifier(XElement element) => new(A(element, "name") ?? string.Empty, element.Value.Length == 0 ? null : element.Value);

    private static XElement WriteTextValue(string name, TextValue value) => new(gx + name, Lang(value.Lang), value.Value);

    private static TextValue ReadTextValue(XElement element) => new(element.Value, (string?)element.Attribute(langName));

    private static object?[] WriteConclusionAttributes(Conclusion conclusion) =>
        [Attr("id", conclusion.Id), Lang(conclusion.Lang), Attr("confidence", conclusion.Confidence)];

    private static object?[] WriteConclusionElements(Conclusion conclusion) =>
    [
        Single("attribution", conclusion.Attribution, WriteAttribution),
        Many("source", conclusion.Sources, WriteSourceReference),
        Single("analysis", conclusion.Analysis, WriteResource),
        Many("note", conclusion.Notes, WriteNote)
    ];

    private static object?[] WriteSubjectAttributes(Subject subject) =>
        [.. WriteConclusionAttributes(subject), Attr("extracted", subject.Extracted)];

    private static object?[] WriteSubjectElements(Subject subject) =>
    [
        .. WriteConclusionElements(subject),
        Many("evidence", subject.Evidence, WriteEvidence),
        Many("media", subject.Media, WriteSourceReference),
        Many("identifier", subject.Identifiers, WriteIdentifier)
    ];

    private static T ReadConclusion<T>(XElement element, T conclusion) where T : Conclusion
    {
        conclusion.Id = A(element, "id");
        conclusion.Lang = (string?)element.Attribute(langName);
        conclusion.Confidence = A(element, "confidence");
        conclusion.Attribution = One(element, "attribution", ReadAttribution);
        conclusion.Sources = ListOf(element, "source", ReadSourceReference);
        conclusion.Analysis = One(element, "analysis", ReadResource);
        conclusion.Notes = ListOf(element, "note", ReadNote);
        return conclusion;
    }

    private static T ReadSubject<T>(XElement element, T subject) where T : Subject
    {
        ReadConclusion(element, subject);
        subject.Extracted = Bool(element, "extracted");
        subject.Evidence = ListOf(element, "evidence", ReadEvidence);
        subject.Media = ListOf(element, "media", ReadSourceReference);
        subject.Identifiers = ListOf(element, "identifier", ReadIdentifier);
        return subject;
    }

    private static XAttribute? Attr(string name, string? value) => value is null ? null : new XAttribute(name, value);

    private static XAttribute? Attr(string name, bool? value) => value is bool flag ? new XAttribute(name, flag ? "true" : "false") : null;

    private static XAttribute? Lang(string? lang) => lang is null ? null : new XAttribute(langName, lang);

    private static XElement? Text(string name, string? value) => value is null ? null : new XElement(gx + name, value);

    private static XElement? Timestamp(string name, long? milliseconds) => milliseconds is long value
        ? new XElement(gx + name, XmlConvert.ToString(DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime, XmlDateTimeSerializationMode.Utc))
        : null;

    private static long? ReadTimestamp(XElement element, string name) => ChildText(element, name) is string text
        ? new DateTimeOffset(XmlConvert.ToDateTime(text, XmlDateTimeSerializationMode.Utc)).ToUnixTimeMilliseconds()
        : null;

    private static XElement? Single<T>(string name, T? value, Func<string, T, XElement> write) where T : class =>
        value is null ? null : write(name, value);

    private static IEnumerable<XElement> Many<T>(string name, List<T>? values, Func<string, T, XElement> write) =>
        values?.Select(value => write(name, value)) ?? [];

    private static IEnumerable<XObject> Extensions(ExtensibleData data) =>
        data.XmlExtensions?.Select(extension => extension switch
        {
            XElement element => (XObject)new XElement(element),
            XAttribute attribute => new XAttribute(attribute),
            _ => extension
        }) ?? [];

    private static string? A(XElement element, string name) => (string?)element.Attribute(name);

    private static bool? Bool(XElement element, string name) => element.Attribute(name) is { } attribute ? XmlConvert.ToBoolean(attribute.Value) : null;

    private static string? ChildText(XElement element, string name) => element.Element(gx + name)?.Value;

    private static T? One<T>(XElement element, string name, Func<XElement, T> read) where T : class =>
        element.Element(gx + name) is { } child ? read(child) : null;

    private static List<T>? ListOf<T>(XElement element, string name, Func<XElement, T> read)
    {
        var items = element.Elements(gx + name).Select(read).ToList();
        return items.Count == 0 ? null : items;
    }

    /// <summary>Keeps elements and attributes the mapping does not know, such as vendor extensions.</summary>
    private static void Capture(ExtensibleData data, XElement element, IReadOnlyCollection<string> knownElements, IReadOnlyCollection<string> knownAttributes)
    {
        var extensions = new List<XObject>();
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || attribute.Name == langName)
            {
                continue;
            }

            if (attribute.Name.Namespace != XNamespace.None || !knownAttributes.Contains(attribute.Name.LocalName))
            {
                extensions.Add(new XAttribute(attribute));
            }
        }

        foreach (var child in element.Elements())
        {
            if (child.Name.Namespace != gx || !knownElements.Contains(child.Name.LocalName))
            {
                extensions.Add(new XElement(child));
            }
        }

        data.XmlExtensions = extensions.Count == 0 ? null : extensions;
    }
}
