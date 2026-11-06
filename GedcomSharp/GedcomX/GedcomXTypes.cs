namespace GedcomSharp.GedcomX;

/// <summary>The GEDCOM X media types and the URIs of the types the specifications define.</summary>
public static class GedcomXTypes
{
    /// <summary>
    /// The JSON media type constant.
    /// </summary>
    public const string JSON_MEDIA_TYPE = "application/x-gedcomx-v1+json";
    /// <summary>
    /// The XML media type constant.
    /// </summary>
    public const string XML_MEDIA_TYPE = "application/x-gedcomx-v1+xml";
    /// <summary>
    /// The XML namespace constant.
    /// </summary>
    public const string XML_NAMESPACE = "http://gedcomx.org/v1/";
    /// <summary>
    /// The file conforms to constant.
    /// </summary>
    public const string FILE_CONFORMS_TO = "http://gedcomx.org/file/v1";

    private const string BASE = "http://gedcomx.org/";

    /// <summary>
    /// Provides gender.
    /// </summary>
    public static class Gender
    {
        /// <summary>
        /// The male constant.
        /// </summary>
        public const string MALE = BASE + "Male";
        /// <summary>
        /// The female constant.
        /// </summary>
        public const string FEMALE = BASE + "Female";
        /// <summary>
        /// The unknown constant.
        /// </summary>
        public const string UNKNOWN = BASE + "Unknown";
        /// <summary>
        /// The intersex constant.
        /// </summary>
        public const string INTERSEX = BASE + "Intersex";
    }

    /// <summary>
    /// Provides relationship.
    /// </summary>
    public static class Relationship
    {
        /// <summary>
        /// The couple constant.
        /// </summary>
        public const string COUPLE = BASE + "Couple";
        /// <summary>
        /// The parent child constant.
        /// </summary>
        public const string PARENT_CHILD = BASE + "ParentChild";
        /// <summary>
        /// The enslaved by constant.
        /// </summary>
        public const string ENSLAVED_BY = BASE + "EnslavedBy";
        /// <summary>
        /// The godparent constant.
        /// </summary>
        public const string GODPARENT = BASE + "Godparent";
    }

    /// <summary>
    /// Provides name type.
    /// </summary>
    public static class NameType
    {
        /// <summary>
        /// The birth name constant.
        /// </summary>
        public const string BIRTH_NAME = BASE + "BirthName";
        /// <summary>
        /// The married name constant.
        /// </summary>
        public const string MARRIED_NAME = BASE + "MarriedName";
        /// <summary>
        /// The also known as constant.
        /// </summary>
        public const string ALSO_KNOWN_AS = BASE + "AlsoKnownAs";
        /// <summary>
        /// The nickname constant.
        /// </summary>
        public const string NICKNAME = BASE + "Nickname";
        /// <summary>
        /// The adoptive name constant.
        /// </summary>
        public const string ADOPTIVE_NAME = BASE + "AdoptiveName";
        /// <summary>
        /// The formal name constant.
        /// </summary>
        public const string FORMAL_NAME = BASE + "FormalName";
        /// <summary>
        /// The religious name constant.
        /// </summary>
        public const string RELIGIOUS_NAME = BASE + "ReligiousName";
    }

    /// <summary>
    /// Provides name part.
    /// </summary>
    public static class NamePart
    {
        /// <summary>
        /// The prefix constant.
        /// </summary>
        public const string PREFIX = BASE + "Prefix";
        /// <summary>
        /// The suffix constant.
        /// </summary>
        public const string SUFFIX = BASE + "Suffix";
        /// <summary>
        /// The given constant.
        /// </summary>
        public const string GIVEN = BASE + "Given";
        /// <summary>
        /// The surname constant.
        /// </summary>
        public const string SURNAME = BASE + "Surname";
    }

    /// <summary>
    /// Provides confidence.
    /// </summary>
    public static class Confidence
    {
        /// <summary>
        /// The high constant.
        /// </summary>
        public const string HIGH = BASE + "High";
        /// <summary>
        /// The medium constant.
        /// </summary>
        public const string MEDIUM = BASE + "Medium";
        /// <summary>
        /// The low constant.
        /// </summary>
        public const string LOW = BASE + "Low";
    }

    /// <summary>
    /// Provides identifier.
    /// </summary>
    public static class Identifier
    {
        /// <summary>
        /// The primary constant.
        /// </summary>
        public const string PRIMARY = BASE + "Primary";
        /// <summary>
        /// The authority constant.
        /// </summary>
        public const string AUTHORITY = BASE + "Authority";
        /// <summary>
        /// The deprecated constant.
        /// </summary>
        public const string DEPRECATED = BASE + "Deprecated";
    }

    /// <summary>
    /// Provides resource type.
    /// </summary>
    public static class ResourceType
    {
        /// <summary>
        /// The collection constant.
        /// </summary>
        public const string COLLECTION = BASE + "Collection";
        /// <summary>
        /// The physical artifact constant.
        /// </summary>
        public const string PHYSICAL_ARTIFACT = BASE + "PhysicalArtifact";
        /// <summary>
        /// The digital artifact constant.
        /// </summary>
        public const string DIGITAL_ARTIFACT = BASE + "DigitalArtifact";
        /// <summary>
        /// The record constant.
        /// </summary>
        public const string RECORD = BASE + "Record";
    }

    /// <summary>
    /// Provides event role.
    /// </summary>
    public static class EventRole
    {
        /// <summary>
        /// The principal constant.
        /// </summary>
        public const string PRINCIPAL = BASE + "Principal";
        /// <summary>
        /// The participant constant.
        /// </summary>
        public const string PARTICIPANT = BASE + "Participant";
        /// <summary>
        /// The official constant.
        /// </summary>
        public const string OFFICIAL = BASE + "Official";
        /// <summary>
        /// The witness constant.
        /// </summary>
        public const string WITNESS = BASE + "Witness";
    }

    /// <summary>
    /// Fact types (<c>http://gedcomx.org/Birth</c>, ...) for the GEDCOM events and attributes that have one.
    /// Used by <see cref="GedcomXConverter"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> FactTypesByGedcomTag { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [GedcomTags.ADOP] = BASE + "Adoption",
        [GedcomTags.BAPM] = BASE + "Baptism",
        [GedcomTags.BARM] = BASE + "BarMitzvah",
        [GedcomTags.BASM] = BASE + "BatMitzvah",
        [GedcomTags.BIRT] = BASE + "Birth",
        [GedcomTags.BLES] = BASE + "Blessing",
        [GedcomTags.BURI] = BASE + "Burial",
        [GedcomTags.CAST] = BASE + "Caste",
        [GedcomTags.CENS] = BASE + "Census",
        [GedcomTags.CHR] = BASE + "Christening",
        [GedcomTags.CHRA] = BASE + "AdultChristening",
        [GedcomTags.CONF] = BASE + "Confirmation",
        [GedcomTags.CREM] = BASE + "Cremation",
        [GedcomTags.DEAT] = BASE + "Death",
        [GedcomTags.DSCR] = BASE + "PhysicalDescription",
        [GedcomTags.EDUC] = BASE + "Education",
        [GedcomTags.EMIG] = BASE + "Emigration",
        [GedcomTags.FCOM] = BASE + "FirstCommunion",
        [GedcomTags.GRAD] = BASE + "Graduation",
        [GedcomTags.IDNO] = BASE + "NationalId",
        [GedcomTags.IMMI] = BASE + "Immigration",
        [GedcomTags.NATI] = BASE + "Nationality",
        [GedcomTags.NATU] = BASE + "Naturalization",
        [GedcomTags.NCHI] = BASE + "NumberOfChildren",
        [GedcomTags.NMR] = BASE + "NumberOfMarriages",
        [GedcomTags.OCCU] = BASE + "Occupation",
        [GedcomTags.ORDN] = BASE + "Ordination",
        [GedcomTags.PROB] = BASE + "Probate",
        [GedcomTags.PROP] = BASE + "Property",
        [GedcomTags.RELI] = BASE + "Religion",
        [GedcomTags.RESI] = BASE + "Residence",
        [GedcomTags.RETI] = BASE + "Retirement",
        [GedcomTags.WILL] = BASE + "Will",
        [GedcomTags.ANUL] = BASE + "Annulment",
        [GedcomTags.DIV] = BASE + "Divorce",
        [GedcomTags.DIVF] = BASE + "DivorceFiling",
        [GedcomTags.ENGA] = BASE + "Engagement",
        [GedcomTags.MARB] = BASE + "MarriageBanns",
        [GedcomTags.MARC] = BASE + "MarriageContract",
        [GedcomTags.MARL] = BASE + "MarriageLicense",
        [GedcomTags.MARR] = BASE + "Marriage",
        [GedcomTags.MARS] = BASE + "MarriageSettlement",
        [GedcomTags.TITL] = BASE + "Title",
        [GedcomTags.SSN] = BASE + "NationalId"
    };
}
