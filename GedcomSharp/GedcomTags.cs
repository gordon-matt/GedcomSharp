namespace GedcomSharp;

/// <summary>Standard tags from GEDCOM 5.5, 5.5.1, 5.5.5 and 7.0, plus the extension tags this library reads.</summary>
public static class GedcomTags
{
    public const string ABBR = "ABBR";
    public const string ADDR = "ADDR";
    public const string ADOP = "ADOP";
    public const string ADR1 = "ADR1";
    public const string ADR2 = "ADR2";
    public const string ADR3 = "ADR3";
    public const string AFN = "AFN";
    public const string AGE = "AGE";
    public const string AGNC = "AGNC";
    public const string ALIA = "ALIA";
    public const string ANCI = "ANCI";
    public const string ANUL = "ANUL";
    public const string ASSO = "ASSO";
    public const string AUTH = "AUTH";
    public const string BAPL = "BAPL";
    public const string BAPM = "BAPM";
    public const string BARM = "BARM";
    public const string BASM = "BASM";
    public const string BIRT = "BIRT";
    public const string BLES = "BLES";
    public const string BLOB = "BLOB";
    public const string BURI = "BURI";
    public const string CALN = "CALN";
    public const string CAST = "CAST";
    public const string CAUS = "CAUS";
    public const string CENS = "CENS";
    public const string CHAN = "CHAN";
    public const string CHAR = "CHAR";
    public const string CHIL = "CHIL";
    public const string CHR = "CHR";
    public const string CHRA = "CHRA";
    public const string CITY = "CITY";
    public const string CONC = "CONC";
    public const string CONF = "CONF";
    public const string CONL = "CONL";
    public const string CONT = "CONT";
    public const string COPR = "COPR";
    public const string CORP = "CORP";
    public const string CREA = "CREA";
    public const string CREM = "CREM";
    public const string CROP = "CROP";
    public const string CTRY = "CTRY";
    public const string DATA = "DATA";
    public const string DATE = "DATE";
    public const string DEAT = "DEAT";
    public const string DESC = "DESC";
    public const string DESI = "DESI";
    public const string DEST = "DEST";
    public const string DIV = "DIV";
    public const string DIVF = "DIVF";
    public const string DSCR = "DSCR";
    public const string EDUC = "EDUC";
    public const string EMAI = "EMAI";
    public const string EMAIL = "EMAIL";
    public const string EMIG = "EMIG";
    public const string ENDL = "ENDL";
    public const string ENGA = "ENGA";
    public const string EVEN = "EVEN";
    public const string EXID = "EXID";
    public const string FACT = "FACT";
    public const string FAM = "FAM";
    public const string FAMC = "FAMC";
    public const string FAMF = "FAMF";
    public const string FAMS = "FAMS";
    public const string FAX = "FAX";
    public const string FCOM = "FCOM";
    public const string FILE = "FILE";
    public const string FONE = "FONE";
    public const string FORM = "FORM";
    public const string GEDC = "GEDC";
    public const string GIVN = "GIVN";
    public const string GRAD = "GRAD";
    public const string HEAD = "HEAD";
    public const string HEIGHT = "HEIGHT";
    public const string HUSB = "HUSB";
    public const string IDNO = "IDNO";
    public const string IMMI = "IMMI";
    public const string INDI = "INDI";
    public const string INIL = "INIL";
    public const string LANG = "LANG";
    public const string LATI = "LATI";
    public const string LEFT = "LEFT";
    public const string LONG = "LONG";
    public const string MAP = "MAP";
    public const string MARB = "MARB";
    public const string MARC = "MARC";
    public const string MARL = "MARL";
    public const string MARR = "MARR";
    public const string MARS = "MARS";
    public const string MEDI = "MEDI";
    public const string MIME = "MIME";
    public const string NAME = "NAME";
    public const string NATI = "NATI";
    public const string NATU = "NATU";
    public const string NCHI = "NCHI";
    public const string NICK = "NICK";
    public const string NMR = "NMR";
    public const string NO = "NO";
    public const string NOTE = "NOTE";
    public const string NPFX = "NPFX";
    public const string NSFX = "NSFX";
    public const string OBJE = "OBJE";
    public const string OCCU = "OCCU";
    public const string ORDI = "ORDI";
    public const string ORDN = "ORDN";
    public const string PAGE = "PAGE";
    public const string PEDI = "PEDI";
    public const string PHON = "PHON";
    public const string PHRASE = "PHRASE";
    public const string PLAC = "PLAC";
    public const string POST = "POST";
    public const string PROB = "PROB";
    public const string PROP = "PROP";
    public const string PUBL = "PUBL";
    public const string QUAY = "QUAY";
    public const string REFN = "REFN";
    public const string RELA = "RELA";
    public const string RELI = "RELI";
    public const string REPO = "REPO";
    public const string RESI = "RESI";
    public const string RESN = "RESN";
    public const string RETI = "RETI";
    public const string RFN = "RFN";
    public const string RIN = "RIN";
    public const string ROLE = "ROLE";
    public const string ROMN = "ROMN";
    public const string SCHMA = "SCHMA";
    public const string SDATE = "SDATE";
    public const string SEX = "SEX";
    public const string SLGC = "SLGC";
    public const string SLGS = "SLGS";
    public const string SNOTE = "SNOTE";
    public const string SOUR = "SOUR";
    public const string SPFX = "SPFX";
    public const string SSN = "SSN";
    public const string STAE = "STAE";
    public const string STAT = "STAT";
    public const string SUBM = "SUBM";
    public const string SUBN = "SUBN";
    public const string SURN = "SURN";
    public const string TAG = "TAG";
    public const string TEMP = "TEMP";
    public const string TEXT = "TEXT";
    public const string TIME = "TIME";
    public const string TITL = "TITL";
    public const string TOP = "TOP";
    public const string TRAN = "TRAN";
    public const string TRLR = "TRLR";
    public const string TYPE = "TYPE";
    public const string UID = "UID";
    public const string VERS = "VERS";
    public const string WIDTH = "WIDTH";
    public const string WIFE = "WIFE";
    public const string WILL = "WILL";
    public const string WWW = "WWW";

    /// <summary>Common extension tags that this library reads as aliases or flags.</summary>
    public static class Extensions
    {
        public const string PRIM = "_PRIM";
        public const string PRIMARY = "_PRIMARY";
        public const string UID = "_UID";
        public const string MARNM = "_MARNM";
        public const string EMAIL = "_EMAIL";
        public const string URL = "_URL";
        public const string WWW = "_WWW";
        public const string FAX = "_FAX";
        public const string RIN = "_RIN";
        public const string PHRASE = "_PHRASE";
        public const string DATE = "_DATE";
        public const string TEXT = "_TEXT";
    }

    /// <summary>Individual events, from GEDCOM 5.5.1 and 7.0.</summary>
    public static readonly IReadOnlySet<string> IndividualEvents = new HashSet<string>(StringComparer.Ordinal)
    {
        ADOP, BAPM, BARM, BASM, BIRT, BLES, BURI, CENS, CHR, CHRA, CONF, CREM, DEAT, EMIG, FCOM, GRAD, IMMI,
        NATU, ORDN, PROB, RETI, WILL, EVEN
    };

    /// <summary>Individual attributes, from GEDCOM 5.5.1 and 7.0. RESI is listed here as well as being event-like.</summary>
    public static readonly IReadOnlySet<string> IndividualAttributes = new HashSet<string>(StringComparer.Ordinal)
    {
        CAST, DSCR, EDUC, IDNO, NATI, NCHI, NMR, OCCU, PROP, RELI, RESI, SSN, TITL, FACT
    };

    public static readonly IReadOnlySet<string> FamilyEvents = new HashSet<string>(StringComparer.Ordinal)
    {
        ANUL, CENS, DIV, DIVF, ENGA, MARB, MARC, MARR, MARL, MARS, RESI, EVEN, NCHI, FACT
    };

    /// <summary>LDS ordinances: individual (BAPL, CONL, ENDL, INIL, SLGC) and family (SLGS).</summary>
    public static readonly IReadOnlySet<string> LdsOrdinances = new HashSet<string>(StringComparer.Ordinal)
    {
        BAPL, CONL, ENDL, INIL, SLGC, SLGS
    };
}
