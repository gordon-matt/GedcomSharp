using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace GedcomSharp.GedcomX;

/// <summary>Base for GEDCOM X data types. Members this library does not model are kept so a document round-trips.</summary>
public abstract class ExtensibleData
{
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
    public string? Lang { get; set; }

    /// <summary>A reference to the SourceDescription that describes this document.</summary>
    public string? Description { get; set; }

    public Attribution? Attribution { get; set; }

    public List<Person>? Persons { get; set; }

    public List<Relationship>? Relationships { get; set; }

    public List<SourceDescription>? SourceDescriptions { get; set; }

    public List<Agent>? Agents { get; set; }

    public List<Event>? Events { get; set; }

    public List<Document>? Documents { get; set; }

    public List<PlaceDescription>? Places { get; set; }

    public List<Group>? Groups { get; set; }

    public Person? FindPerson(string? reference) => Find(Persons, reference);

    public SourceDescription? FindSourceDescription(string? reference) => Find(SourceDescriptions, reference);

    public PlaceDescription? FindPlace(string? reference) => Find(Places, reference);

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

public sealed class ResourceReference
{
    public ResourceReference()
    {
    }

    public ResourceReference(string resource) => Resource = resource;

    public string? Resource { get; set; }

    /// <summary>A local reference to an item with <paramref name="id"/> in the same document.</summary>
    public static ResourceReference ToLocal(string id) => new("#" + id);
}

public sealed class Identifier
{
    public Identifier()
    {
    }

    public Identifier(string value, string? type = null)
    {
        Value = value;
        Type = type;
    }

    /// <summary>The identifier type URI (e.g. <c>http://gedcomx.org/Primary</c>), or null for an untyped identifier.</summary>
    public string? Type { get; set; }

    public string? Value { get; set; }
}

public sealed class Attribution : ExtensibleData
{
    public ResourceReference? Contributor { get; set; }

    /// <summary>Milliseconds since the Unix epoch.</summary>
    public long? Modified { get; set; }

    public string? ChangeMessage { get; set; }

    public ResourceReference? Creator { get; set; }

    /// <summary>Milliseconds since the Unix epoch.</summary>
    public long? Created { get; set; }
}

public sealed class Note : ExtensibleData
{
    public string? Lang { get; set; }

    public string? Subject { get; set; }

    public string? Text { get; set; }

    public Attribution? Attribution { get; set; }
}

public sealed class TextValue
{
    public TextValue()
    {
    }

    public TextValue(string value, string? lang = null)
    {
        Value = value;
        Lang = lang;
    }

    public string? Lang { get; set; }

    public string? Value { get; set; }
}

public sealed class SourceCitation : ExtensibleData
{
    public string? Lang { get; set; }

    public string? Value { get; set; }
}

public sealed class Qualifier
{
    public Qualifier()
    {
    }

    public Qualifier(string name, string? value = null)
    {
        Name = name;
        Value = value;
    }

    public string? Name { get; set; }

    public string? Value { get; set; }
}

public sealed class SourceReference : ExtensibleData
{
    /// <summary>A reference to the SourceDescription, e.g. <c>#S1</c>.</summary>
    public string? Description { get; set; }

    public string? DescriptionId { get; set; }

    public Attribution? Attribution { get; set; }

    public List<Qualifier>? Qualifiers { get; set; }
}

public sealed class EvidenceReference : ExtensibleData
{
    public string? Resource { get; set; }

    public Attribution? Attribution { get; set; }
}

public sealed class OnlineAccount : ExtensibleData
{
    public ResourceReference? ServiceHomepage { get; set; }

    public string? AccountName { get; set; }
}

public sealed class Address : ExtensibleData
{
    public string? Value { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? PostalCode { get; set; }

    public string? StateOrProvince { get; set; }

    public string? Street { get; set; }

    public string? Street2 { get; set; }

    public string? Street3 { get; set; }

    public string? Street4 { get; set; }

    public string? Street5 { get; set; }

    public string? Street6 { get; set; }
}

public sealed class Coverage : ExtensibleData
{
    public PlaceReference? Spatial { get; set; }

    public Date? Temporal { get; set; }
}

/// <summary>A date: the original text and its formal GEDCOM X form (see <see cref="GedcomXDate"/>).</summary>
public sealed class Date : ExtensibleData
{
    public string? Original { get; set; }

    public string? Formal { get; set; }

    public List<TextValue>? Normalized { get; set; }
}

public sealed class PlaceReference : ExtensibleData
{
    public string? Original { get; set; }

    /// <summary>A reference to a PlaceDescription, e.g. <c>#PL1</c>.</summary>
    public string? Description { get; set; }
}

public abstract class Conclusion : ExtensibleData
{
    public string? Lang { get; set; }

    public List<SourceReference>? Sources { get; set; }

    public ResourceReference? Analysis { get; set; }

    public List<Note>? Notes { get; set; }

    /// <summary>A confidence level URI, e.g. <c>http://gedcomx.org/High</c>.</summary>
    public string? Confidence { get; set; }

    public Attribution? Attribution { get; set; }
}

public abstract class Subject : Conclusion
{
    public bool? Extracted { get; set; }

    public List<EvidenceReference>? Evidence { get; set; }

    public List<SourceReference>? Media { get; set; }

    [JsonConverter(typeof(IdentifierListJsonConverter))]
    public List<Identifier>? Identifiers { get; set; }
}

public sealed class Gender : Conclusion
{
    public Gender()
    {
    }

    public Gender(string type) => Type = type;

    public string? Type { get; set; }
}

public sealed class NamePart : ExtensibleData
{
    public string? Type { get; set; }

    public string? Value { get; set; }

    public List<Qualifier>? Qualifiers { get; set; }
}

public sealed class NameForm : ExtensibleData
{
    public string? Lang { get; set; }

    public string? FullText { get; set; }

    public List<NamePart>? Parts { get; set; }
}

public sealed class Name : Conclusion
{
    public string? Type { get; set; }

    public List<NameForm>? NameForms { get; set; }

    public Date? Date { get; set; }
}

public sealed class Fact : Conclusion
{
    public string? Type { get; set; }

    public Date? Date { get; set; }

    public PlaceReference? Place { get; set; }

    public string? Value { get; set; }

    public List<Qualifier>? Qualifiers { get; set; }
}

public sealed class Person : Subject
{
    public bool? Private { get; set; }

    public Gender? Gender { get; set; }

    public List<Name>? Names { get; set; }

    public List<Fact>? Facts { get; set; }
}

public sealed class Relationship : Subject
{
    public string? Type { get; set; }

    public ResourceReference? Person1 { get; set; }

    public ResourceReference? Person2 { get; set; }

    public List<Fact>? Facts { get; set; }
}

public sealed class SourceDescription : ExtensibleData
{
    public string? ResourceType { get; set; }

    public List<SourceCitation>? Citations { get; set; }

    public string? MediaType { get; set; }

    public string? About { get; set; }

    public ResourceReference? Mediator { get; set; }

    public ResourceReference? Publisher { get; set; }

    public List<ResourceReference>? Authors { get; set; }

    public List<SourceReference>? Sources { get; set; }

    public ResourceReference? Analysis { get; set; }

    public SourceReference? ComponentOf { get; set; }

    public List<TextValue>? Titles { get; set; }

    public List<Note>? Notes { get; set; }

    public Attribution? Attribution { get; set; }

    public List<ResourceReference>? Rights { get; set; }

    public List<Coverage>? Coverage { get; set; }

    public List<TextValue>? Descriptions { get; set; }

    [JsonConverter(typeof(IdentifierListJsonConverter))]
    public List<Identifier>? Identifiers { get; set; }

    public long? Created { get; set; }

    public long? Modified { get; set; }

    public long? Published { get; set; }

    public ResourceReference? Repository { get; set; }

    public string? SortKey { get; set; }
}

public sealed class Agent : ExtensibleData
{
    [JsonConverter(typeof(IdentifierListJsonConverter))]
    public List<Identifier>? Identifiers { get; set; }

    public List<TextValue>? Names { get; set; }

    public ResourceReference? Homepage { get; set; }

    public ResourceReference? Openid { get; set; }

    public List<OnlineAccount>? Accounts { get; set; }

    public List<ResourceReference>? Emails { get; set; }

    public List<ResourceReference>? Phones { get; set; }

    public List<Address>? Addresses { get; set; }

    public ResourceReference? Person { get; set; }
}

public sealed class EventRole : Conclusion
{
    public ResourceReference? Person { get; set; }

    public string? Type { get; set; }

    public string? Details { get; set; }
}

public sealed class Event : Subject
{
    public string? Type { get; set; }

    public Date? Date { get; set; }

    public PlaceReference? Place { get; set; }

    public List<EventRole>? Roles { get; set; }
}

public sealed class Document : Conclusion
{
    public string? Type { get; set; }

    public bool? Extracted { get; set; }

    public string? TextType { get; set; }

    public string? Text { get; set; }
}

public sealed class PlaceDescription : Subject
{
    public List<TextValue>? Names { get; set; }

    public string? Type { get; set; }

    public ResourceReference? Place { get; set; }

    public ResourceReference? Jurisdiction { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public Date? TemporalDescription { get; set; }

    public ResourceReference? SpatialDescription { get; set; }
}

public sealed class GroupRole : Conclusion
{
    public ResourceReference? Person { get; set; }

    public string? Type { get; set; }

    public Date? Date { get; set; }

    public string? Details { get; set; }
}

public sealed class Group : Subject
{
    public List<TextValue>? Names { get; set; }

    public Date? Date { get; set; }

    public PlaceReference? Place { get; set; }

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
