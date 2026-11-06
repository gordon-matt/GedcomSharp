using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace GedcomSharp.GedcomX;

/// <summary>Base for GEDCOM X data types. Members this library does not model are kept so a document round-trips.</summary>
public abstract class ExtensibleData
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>Unknown JSON members, written back unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>Unknown XML elements and attributes, written back unchanged.</summary>
    [JsonIgnore]
    public List<XObject>? XmlExtensions { get; set; }
}

/// <summary>The root of a GEDCOM X document.</summary>
public sealed class GedcomXDocument : ExtensibleData
{
    /// <summary>
    /// Gets or sets the lang.
    /// </summary>
    public string? Lang { get; set; }

    /// <summary>A reference to the SourceDescription that describes this document.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the attribution.
    /// </summary>
    public Attribution? Attribution { get; set; }

    /// <summary>
    /// Gets or sets the persons.
    /// </summary>
    public List<Person>? Persons { get; set; }

    /// <summary>
    /// Gets or sets the relationships.
    /// </summary>
    public List<Relationship>? Relationships { get; set; }

    /// <summary>
    /// Gets or sets the source descriptions.
    /// </summary>
    public List<SourceDescription>? SourceDescriptions { get; set; }

    /// <summary>
    /// Gets or sets the agents.
    /// </summary>
    public List<Agent>? Agents { get; set; }

    /// <summary>
    /// Gets or sets the events.
    /// </summary>
    public List<Event>? Events { get; set; }

    /// <summary>
    /// Gets or sets the documents.
    /// </summary>
    public List<Document>? Documents { get; set; }

    /// <summary>
    /// Gets or sets the places.
    /// </summary>
    public List<PlaceDescription>? Places { get; set; }

    /// <summary>
    /// Gets or sets the groups.
    /// </summary>
    public List<Group>? Groups { get; set; }

    /// <summary>
    /// Finds the person.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The person.</returns>
    public Person? FindPerson(string? reference) => Find(Persons, reference);

    /// <summary>
    /// Finds the source description.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The source description.</returns>
    public SourceDescription? FindSourceDescription(string? reference) => Find(SourceDescriptions, reference);

    /// <summary>
    /// Finds the place.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The place.</returns>
    public PlaceDescription? FindPlace(string? reference) => Find(Places, reference);

    /// <summary>
    /// Finds the agent.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The agent.</returns>
    public Agent? FindAgent(string? reference) => Find(Agents, reference);

    /// <summary>Resolves a local reference (<c>#P1</c> or <c>P1</c>) to an item with that id.</summary>
    private static T? Find<T>(List<T>? items, string? reference) where T : ExtensibleData
    {
        if (items is null || string.IsNullOrEmpty(reference))
        {
            return null;
        }

        string id = reference.StartsWith('#') ? reference[1..] : reference;
        return items.FirstOrDefault(item => item.Id == id);
    }
}

/// <summary>
/// Represents the resource reference.
/// </summary>
public sealed class ResourceReference
{
    /// <summary>
    /// Initializes a new instance of the ResourceReference class.
    /// </summary>
    public ResourceReference()
    {
    }

    /// <summary>
    /// Initializes a new instance of the ResourceReference class.
    /// </summary>
    /// <param name="resource">The resource.</param>
    public ResourceReference(string resource) => Resource = resource;

    /// <summary>
    /// Gets or sets the resource.
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>A local reference to an item with <paramref name="id"/> in the same document.</summary>
    public static ResourceReference ToLocal(string id) => new("#" + id);
}

/// <summary>
/// Represents the identifier.
/// </summary>
public sealed class Identifier
{
    /// <summary>
    /// Initializes a new instance of the Identifier class.
    /// </summary>
    public Identifier()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Identifier class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The type.</param>
    public Identifier(string value, string? type = null)
    {
        Value = value;
        Type = type;
    }

    /// <summary>The identifier type URI (e.g. <c>http://gedcomx.org/Primary</c>), or null for an untyped identifier.</summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Represents the attribution.
/// </summary>
public sealed class Attribution : ExtensibleData
{
    /// <summary>
    /// Gets or sets the contributor.
    /// </summary>
    public ResourceReference? Contributor { get; set; }

    /// <summary>Milliseconds since the Unix epoch.</summary>
    public long? Modified { get; set; }

    /// <summary>
    /// Gets or sets the change message.
    /// </summary>
    public string? ChangeMessage { get; set; }

    /// <summary>
    /// Gets or sets the creator.
    /// </summary>
    public ResourceReference? Creator { get; set; }

    /// <summary>Milliseconds since the Unix epoch.</summary>
    public long? Created { get; set; }
}

/// <summary>
/// Represents the note.
/// </summary>
public sealed class Note : ExtensibleData
{
    /// <summary>
    /// Gets or sets the lang.
    /// </summary>
    public string? Lang { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the attribution.
    /// </summary>
    public Attribution? Attribution { get; set; }
}

/// <summary>
/// Represents the text value.
/// </summary>
public sealed class TextValue
{
    /// <summary>
    /// Initializes a new instance of the TextValue class.
    /// </summary>
    public TextValue()
    {
    }

    /// <summary>
    /// Initializes a new instance of the TextValue class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="lang">The lang.</param>
    public TextValue(string value, string? lang = null)
    {
        Value = value;
        Lang = lang;
    }

    /// <summary>
    /// Gets or sets the lang.
    /// </summary>
    public string? Lang { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Represents the source citation.
/// </summary>
public sealed class SourceCitation : ExtensibleData
{
    /// <summary>
    /// Gets or sets the lang.
    /// </summary>
    public string? Lang { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Represents the qualifier.
/// </summary>
public sealed class Qualifier
{
    /// <summary>
    /// Initializes a new instance of the Qualifier class.
    /// </summary>
    public Qualifier()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Qualifier class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    public Qualifier(string name, string? value = null)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Represents the source reference.
/// </summary>
public sealed class SourceReference : ExtensibleData
{
    /// <summary>A reference to the SourceDescription, e.g. <c>#S1</c>.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the description identifier.
    /// </summary>
    public string? DescriptionId { get; set; }

    /// <summary>
    /// Gets or sets the attribution.
    /// </summary>
    public Attribution? Attribution { get; set; }

    /// <summary>
    /// Gets or sets the qualifiers.
    /// </summary>
    public List<Qualifier>? Qualifiers { get; set; }
}

/// <summary>
/// Represents the evidence reference.
/// </summary>
public sealed class EvidenceReference : ExtensibleData
{
    /// <summary>
    /// Gets or sets the resource.
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// Gets or sets the attribution.
    /// </summary>
    public Attribution? Attribution { get; set; }
}

/// <summary>
/// Represents the online account.
/// </summary>
public sealed class OnlineAccount : ExtensibleData
{
    /// <summary>
    /// Gets or sets the service homepage.
    /// </summary>
    public ResourceReference? ServiceHomepage { get; set; }

    /// <summary>
    /// Gets or sets the account name.
    /// </summary>
    public string? AccountName { get; set; }
}

/// <summary>
/// Represents the address.
/// </summary>
public sealed class Address : ExtensibleData
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Gets or sets the country.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets the postal code.
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the state or province.
    /// </summary>
    public string? StateOrProvince { get; set; }

    /// <summary>
    /// Gets or sets the street.
    /// </summary>
    public string? Street { get; set; }

    /// <summary>
    /// Gets or sets the street2.
    /// </summary>
    public string? Street2 { get; set; }

    /// <summary>
    /// Gets or sets the street3.
    /// </summary>
    public string? Street3 { get; set; }

    /// <summary>
    /// Gets or sets the street4.
    /// </summary>
    public string? Street4 { get; set; }

    /// <summary>
    /// Gets or sets the street5.
    /// </summary>
    public string? Street5 { get; set; }

    /// <summary>
    /// Gets or sets the street6.
    /// </summary>
    public string? Street6 { get; set; }
}

/// <summary>
/// Represents the coverage.
/// </summary>
public sealed class Coverage : ExtensibleData
{
    /// <summary>
    /// Gets or sets the spatial.
    /// </summary>
    public PlaceReference? Spatial { get; set; }

    /// <summary>
    /// Gets or sets the temporal.
    /// </summary>
    public Date? Temporal { get; set; }
}

/// <summary>A date: the original text and its formal GEDCOM X form (see <see cref="GedcomXDate"/>).</summary>
public sealed class Date : ExtensibleData
{
    /// <summary>
    /// Gets or sets the original.
    /// </summary>
    public string? Original { get; set; }

    /// <summary>
    /// Gets or sets the formal.
    /// </summary>
    public string? Formal { get; set; }

    /// <summary>
    /// Gets or sets the normalized.
    /// </summary>
    public List<TextValue>? Normalized { get; set; }
}

/// <summary>
/// Represents the place reference.
/// </summary>
public sealed class PlaceReference : ExtensibleData
{
    /// <summary>
    /// Gets or sets the original.
    /// </summary>
    public string? Original { get; set; }

    /// <summary>A reference to a PlaceDescription, e.g. <c>#PL1</c>.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Represents the conclusion.
/// </summary>
public abstract class Conclusion : ExtensibleData
{
    /// <summary>
    /// Gets or sets the lang.
    /// </summary>
    public string? Lang { get; set; }

    /// <summary>
    /// Gets or sets the sources.
    /// </summary>
    public List<SourceReference>? Sources { get; set; }

    /// <summary>
    /// Gets or sets the analysis.
    /// </summary>
    public ResourceReference? Analysis { get; set; }

    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    public List<Note>? Notes { get; set; }

    /// <summary>A confidence level URI, e.g. <c>http://gedcomx.org/High</c>.</summary>
    public string? Confidence { get; set; }

    /// <summary>
    /// Gets or sets the attribution.
    /// </summary>
    public Attribution? Attribution { get; set; }
}

/// <summary>
/// Represents the subject.
/// </summary>
public abstract class Subject : Conclusion
{
    /// <summary>
    /// Gets or sets a value indicating whether this instance is extracted.
    /// </summary>
    public bool? Extracted { get; set; }

    /// <summary>
    /// Gets or sets the evidence.
    /// </summary>
    public List<EvidenceReference>? Evidence { get; set; }

    /// <summary>
    /// Gets or sets the media.
    /// </summary>
    public List<SourceReference>? Media { get; set; }

    /// <summary>
    /// Gets or sets the identifiers.
    /// </summary>
    [JsonConverter(typeof(IdentifierListJsonConverter))]
    public List<Identifier>? Identifiers { get; set; }
}

/// <summary>
/// Represents the gender.
/// </summary>
public sealed class Gender : Conclusion
{
    /// <summary>
    /// Initializes a new instance of the Gender class.
    /// </summary>
    public Gender()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Gender class.
    /// </summary>
    /// <param name="type">The type.</param>
    public Gender(string type) => Type = type;

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string? Type { get; set; }
}

/// <summary>
/// Represents the name part.
/// </summary>
public sealed class NamePart : ExtensibleData
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the qualifiers.
    /// </summary>
    public List<Qualifier>? Qualifiers { get; set; }
}

/// <summary>
/// Represents the name form.
/// </summary>
public sealed class NameForm : ExtensibleData
{
    /// <summary>
    /// Gets or sets the lang.
    /// </summary>
    public string? Lang { get; set; }

    /// <summary>
    /// Gets or sets the full text.
    /// </summary>
    public string? FullText { get; set; }

    /// <summary>
    /// Gets or sets the parts.
    /// </summary>
    public List<NamePart>? Parts { get; set; }
}

/// <summary>
/// Represents the name.
/// </summary>
public sealed class Name : Conclusion
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the name forms.
    /// </summary>
    public List<NameForm>? NameForms { get; set; }

    /// <summary>
    /// Gets or sets the date.
    /// </summary>
    public Date? Date { get; set; }
}

/// <summary>
/// Represents the fact.
/// </summary>
public sealed class Fact : Conclusion
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the date.
    /// </summary>
    public Date? Date { get; set; }

    /// <summary>
    /// Gets or sets the place.
    /// </summary>
    public PlaceReference? Place { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the qualifiers.
    /// </summary>
    public List<Qualifier>? Qualifiers { get; set; }
}

/// <summary>
/// Represents the person.
/// </summary>
public sealed class Person : Subject
{
    /// <summary>
    /// Gets or sets a value indicating whether this instance is private.
    /// </summary>
    public bool? Private { get; set; }

    /// <summary>
    /// Gets or sets the gender.
    /// </summary>
    public Gender? Gender { get; set; }

    /// <summary>
    /// Gets or sets the names.
    /// </summary>
    public List<Name>? Names { get; set; }

    /// <summary>
    /// Gets or sets the facts.
    /// </summary>
    public List<Fact>? Facts { get; set; }
}

/// <summary>
/// Represents the relationship.
/// </summary>
public sealed class Relationship : Subject
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the person1.
    /// </summary>
    public ResourceReference? Person1 { get; set; }

    /// <summary>
    /// Gets or sets the person2.
    /// </summary>
    public ResourceReference? Person2 { get; set; }

    /// <summary>
    /// Gets or sets the facts.
    /// </summary>
    public List<Fact>? Facts { get; set; }
}

/// <summary>
/// Represents the source description.
/// </summary>
public sealed class SourceDescription : ExtensibleData
{
    /// <summary>
    /// Gets or sets the resource type.
    /// </summary>
    public string? ResourceType { get; set; }

    /// <summary>
    /// Gets or sets the citations.
    /// </summary>
    public List<SourceCitation>? Citations { get; set; }

    /// <summary>
    /// Gets or sets the media type.
    /// </summary>
    public string? MediaType { get; set; }

    /// <summary>
    /// Gets or sets the about.
    /// </summary>
    public string? About { get; set; }

    /// <summary>
    /// Gets or sets the mediator.
    /// </summary>
    public ResourceReference? Mediator { get; set; }

    /// <summary>
    /// Gets or sets the publisher.
    /// </summary>
    public ResourceReference? Publisher { get; set; }

    /// <summary>
    /// Gets or sets the authors.
    /// </summary>
    public List<ResourceReference>? Authors { get; set; }

    /// <summary>
    /// Gets or sets the sources.
    /// </summary>
    public List<SourceReference>? Sources { get; set; }

    /// <summary>
    /// Gets or sets the analysis.
    /// </summary>
    public ResourceReference? Analysis { get; set; }

    /// <summary>
    /// Gets or sets the component of.
    /// </summary>
    public SourceReference? ComponentOf { get; set; }

    /// <summary>
    /// Gets or sets the titles.
    /// </summary>
    public List<TextValue>? Titles { get; set; }

    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    public List<Note>? Notes { get; set; }

    /// <summary>
    /// Gets or sets the attribution.
    /// </summary>
    public Attribution? Attribution { get; set; }

    /// <summary>
    /// Gets or sets the rights.
    /// </summary>
    public List<ResourceReference>? Rights { get; set; }

    /// <summary>
    /// Gets or sets the coverage.
    /// </summary>
    public List<Coverage>? Coverage { get; set; }

    /// <summary>
    /// Gets or sets the descriptions.
    /// </summary>
    public List<TextValue>? Descriptions { get; set; }

    /// <summary>
    /// Gets or sets the identifiers.
    /// </summary>
    [JsonConverter(typeof(IdentifierListJsonConverter))]
    public List<Identifier>? Identifiers { get; set; }

    /// <summary>
    /// Gets or sets the created.
    /// </summary>
    public long? Created { get; set; }

    /// <summary>
    /// Gets or sets the modified.
    /// </summary>
    public long? Modified { get; set; }

    /// <summary>
    /// Gets or sets the published.
    /// </summary>
    public long? Published { get; set; }

    /// <summary>
    /// Gets or sets the repository.
    /// </summary>
    public ResourceReference? Repository { get; set; }

    /// <summary>
    /// Gets or sets the sort key.
    /// </summary>
    public string? SortKey { get; set; }
}

/// <summary>
/// Represents the agent.
/// </summary>
public sealed class Agent : ExtensibleData
{
    /// <summary>
    /// Gets or sets the identifiers.
    /// </summary>
    [JsonConverter(typeof(IdentifierListJsonConverter))]
    public List<Identifier>? Identifiers { get; set; }

    /// <summary>
    /// Gets or sets the names.
    /// </summary>
    public List<TextValue>? Names { get; set; }

    /// <summary>
    /// Gets or sets the homepage.
    /// </summary>
    public ResourceReference? Homepage { get; set; }

    /// <summary>
    /// Gets or sets the openid.
    /// </summary>
    public ResourceReference? Openid { get; set; }

    /// <summary>
    /// Gets or sets the accounts.
    /// </summary>
    public List<OnlineAccount>? Accounts { get; set; }

    /// <summary>
    /// Gets or sets the emails.
    /// </summary>
    public List<ResourceReference>? Emails { get; set; }

    /// <summary>
    /// Gets or sets the phones.
    /// </summary>
    public List<ResourceReference>? Phones { get; set; }

    /// <summary>
    /// Gets or sets the addresses.
    /// </summary>
    public List<Address>? Addresses { get; set; }

    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public ResourceReference? Person { get; set; }
}

/// <summary>
/// Represents the event role.
/// </summary>
public sealed class EventRole : Conclusion
{
    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public ResourceReference? Person { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the details.
    /// </summary>
    public string? Details { get; set; }
}

/// <summary>
/// Represents the event.
/// </summary>
public sealed class Event : Subject
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the date.
    /// </summary>
    public Date? Date { get; set; }

    /// <summary>
    /// Gets or sets the place.
    /// </summary>
    public PlaceReference? Place { get; set; }

    /// <summary>
    /// Gets or sets the roles.
    /// </summary>
    public List<EventRole>? Roles { get; set; }
}

/// <summary>
/// Represents the document.
/// </summary>
public sealed class Document : Conclusion
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is extracted.
    /// </summary>
    public bool? Extracted { get; set; }

    /// <summary>
    /// Gets or sets the text type.
    /// </summary>
    public string? TextType { get; set; }

    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string? Text { get; set; }
}

/// <summary>
/// Represents the place description.
/// </summary>
public sealed class PlaceDescription : Subject
{
    /// <summary>
    /// Gets or sets the names.
    /// </summary>
    public List<TextValue>? Names { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the place.
    /// </summary>
    public ResourceReference? Place { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction.
    /// </summary>
    public ResourceReference? Jurisdiction { get; set; }

    /// <summary>
    /// Gets or sets the latitude.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Gets or sets the temporal description.
    /// </summary>
    public Date? TemporalDescription { get; set; }

    /// <summary>
    /// Gets or sets the spatial description.
    /// </summary>
    public ResourceReference? SpatialDescription { get; set; }
}

/// <summary>
/// Represents the group role.
/// </summary>
public sealed class GroupRole : Conclusion
{
    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public ResourceReference? Person { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the date.
    /// </summary>
    public Date? Date { get; set; }

    /// <summary>
    /// Gets or sets the details.
    /// </summary>
    public string? Details { get; set; }
}

/// <summary>
/// Represents the group.
/// </summary>
public sealed class Group : Subject
{
    /// <summary>
    /// Gets or sets the names.
    /// </summary>
    public List<TextValue>? Names { get; set; }

    /// <summary>
    /// Gets or sets the date.
    /// </summary>
    public Date? Date { get; set; }

    /// <summary>
    /// Gets or sets the place.
    /// </summary>
    public PlaceReference? Place { get; set; }

    /// <summary>
    /// Gets or sets the roles.
    /// </summary>
    public List<GroupRole>? Roles { get; set; }
}

/// <summary>JSON identifiers are an object keyed by type ("$" for untyped) whose values are a string or an array of strings.</summary>
internal sealed class IdentifierListJsonConverter : JsonConverter<List<Identifier>>
{
    private const string UNTYPED = "$";

    public override List<Identifier>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Identifiers must be a JSON object.");
        }

        var identifiers = new List<Identifier>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string key = reader.GetString() ?? UNTYPED;
            string? type = key == UNTYPED ? null : key;
            reader.Read();
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    identifiers.Add(new Identifier(reader.GetString() ?? string.Empty, type));
                }
            }
            else
            {
                identifiers.Add(new Identifier(reader.GetString() ?? string.Empty, type));
            }
        }

        return identifiers;
    }

    public override void Write(Utf8JsonWriter writer, List<Identifier> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var group in value.GroupBy(identifier => identifier.Type ?? UNTYPED))
        {
            writer.WriteStartArray(group.Key);
            foreach (var identifier in group)
            {
                writer.WriteStringValue(identifier.Value);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
