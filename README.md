<img src="https://github.com/gordon-matt/GedcomSharp/blob/master/_Misc/Logo.png" alt="Logo" width="350" />

Read and write genealogy data in every GEDCOM format: 5.5, 5.5.1, 5.5.5 and 7.0, GEDZIP packages (`.gdz`), and GEDCOM X as JSON, XML or `.gedx` files.

Every line of a file is kept, including vendor extensions this library knows nothing about. A file you read and write again holds the same data. Typed records such as `IndividualRecord` and `FamilyRecord` are views over that structure tree, so you can use the typed API for common work and edit the tree directly for anything else.

## Install

```xml
<ProjectReference Include="..\GedcomSharp\GedcomSharp.csproj" />
```

Register the reader, writer and GEDCOM X serializer. All three are stateless singletons.

```csharp
builder.Services.AddGedcomSharp(
    reader => reader.Strict = false,
    writer => writer.SourceSystem = "MyApp");
```

You can also construct them yourself: `new GedcomReader()`, `new GedcomWriter(options)`, `new GedcomXSerializer()`.

## Read

Every method returns an [Ardalis.Result](https://github.com/ardalis/Result). Check `IsSuccess` before using `Value`.

```csharp
var result = await reader.ReadAsync("family.ged");
if (!result.IsSuccess)
{
    // Invalid: empty file, not GEDCOM, or (in strict mode) unreadable lines. NotFound: no such file.
    return;
}

GedcomFile file = result.Value;
foreach (var person in file.Individuals)
{
    Console.WriteLine($"{person.DisplayName}, born {person.Birth?.Date} in {person.Birth?.PlaceName}");
}
```

The reader works out the version and the character encoding itself. It handles:

- byte order marks, UTF-8, UTF-16, ANSEL, ASCII, Windows code pages, and files whose `CHAR` line is wrong (a Cyrillic file labelled ANSEL, for example);
- 5.5 files that are really 5.5.1, using the detection rules from the 5.5.5 specification;
- `CONC` and `CONT` lines, `@@` escapes, wrapped lines with no level number, UTF-8 characters split across `CONC` lines, and lowercase tags.

Anything it had to repair is listed in `file.Issues`, with a code, a severity and a line number. `file.Source` records how the file was stored: its encoding, byte order mark, line terminator and declared version.

`GedcomReaderOptions.Strict` makes errors fail the read. `Encoding` skips encoding detection.

## Work with the data

```csharp
var file = GedcomFile.Create(GedcomVersion.V70);
var john = file.AddIndividual();
john.AddName("John", "Smith");
john.Sex = GedcomSex.Male;
john.AddEvent(GedcomTags.BIRT, GedcomDate.Parse("ABT 1850"), "London, England");

var mary = file.AddIndividual();
mary.AddName("Mary", "Jones");
var family = file.AddFamily(john, mary, file.AddIndividual());
family.AddEvent(GedcomTags.MARR, GedcomDate.Exact(1872, 6, 3));

var parents = john.Children.First().Parents;
```

`AddFamily` and `AddChildToFamily` write both sides of each link (`FAMS`/`FAMC` and `HUSB`/`WIFE`/`CHIL`). `Remove(record)` also removes the pointers to that record.

For anything the typed API doesn't cover, use the structure tree. `record.Structure` is the `INDI` line. `Child`, `Find("BIRT", "DATE")`, `AddChild` and `SetChildValue` navigate and edit it, and `file.Find(xref)` returns any record.

### Dates

`GedcomDate` covers every GEDCOM date form, in the Gregorian, Julian, Hebrew and French Republican calendars:

- simple dates and `ABT`, `CAL`, `EST`;
- `BEF`, `AFT`, `BET ... AND ...`;
- `FROM ... TO ...`;
- `INT` with a phrase, and phrases on their own;
- dual years (`1699/00`) and BCE dates.

The parser also accepts the loose forms people type, such as `circa 1900`, `1900?`, `17.05.1900`, `May 17, 1900`, `1900-1910` and month names in several languages. It sets `WasNormalized` when it had to interpret one.

`ToDateOnly()`, `CompareTo` and the min and max Julian day numbers work across all four calendars, so dates sort by meaning:

```csharp
GedcomDate.Parse("@#DHEBREW@ 1 TSH 5785").ToDateOnly(); // 2024-10-03
```

`GedcomAge` reads ages such as `> 35y 6m`, `CHILD` and `2 yrs 3 mos`.

## Write

```csharp
await writer.WriteAsync(file, "family.ged");

var asSeven = new GedcomWriter(new GedcomWriterOptions { Version = GedcomVersion.V70 });
byte[] bytes = asSeven.WriteToBytes(file).Value;
```

The writer follows the rules of the target version:

- **Line length:** 5.x lines are split with `CONC` at 255 bytes of the target encoding, and never in the middle of a character.
- **Escapes:** 5.x doubles every `@`; 7.0 doubles only a leading one.
- **Encodings:** 7.0 is UTF-8 only, and 5.5.5 is UTF-8 or UTF-16.
- **Byte order marks:** written where the specification asks for one.
- **Header:** `GEDC`, `CHAR` and `SOUR` are updated to match the output. Turn `UpdateHeader` off to write the header exactly as it is.

If no encoding is set, the writer keeps the encoding the file was read in, as long as the version is unchanged. Otherwise it uses UTF-8, or ANSEL for 5.5. Characters the chosen encoding cannot hold are reported in the summary's `Issues`.

## Convert between versions

Setting `GedcomWriterOptions.Version` converts a copy before writing. To convert without writing, call `file.ConvertTo(version)`. The original is never changed. `Issues` on the copy says what changed and anything that could not be carried over.

**5.x to 7.0:**

- `NOTE` records become `SNOTE`.
- Dates and ages are rewritten in 7.0 syntax, and the original wording is kept as a `PHRASE`.
- Enumerations (`PEDI`, `NAME.TYPE`, `MEDI`, `RESN`, `STAT`) are mapped to their 7.0 values.
- `FONE` and `ROMN` become `TRAN`.
- Language names become BCP 47 tags.
- File paths become URLs, with a media type.
- Embedded `OBJE` and `SOUR` become records.
- `RIN`, `AFN` and `RFN` become `EXID`, and `_UID` becomes `UID`.
- Broken pointers become `@VOID@`.

**7.0 to 5.x** reverses these changes. 7.0-only data with no 5.x equivalent is kept under an underscore tag rather than dropped.

Between the 5.x versions, the email, fax and website tags and the multimedia structure are converted. 5.5.5 also gets the submitter it requires and short alphanumeric xrefs.

## GEDZIP

```csharp
using var stream = File.Create("family.gdz");
await writer.WriteGedzipAsync(file, stream, [GedzipFileEntry.FromPath("media/john.jpg", @"C:\Photos\john.jpg")]);

var package = (await reader.ReadGedzipAsync("family.gdz")).Value;
using (package)
{
    byte[]? photo = await package.ReadFileAsync("media/john.jpg");
    IReadOnlyList<string> missing = package.MissingFiles;
}
```

`ReadAsync` also accepts a `.gdz` file and returns only the dataset. When reading a package, a `FILE` reference is matched to its entry exactly, then case-insensitively, then by its longest matching path tail. The tail match handles older packages that stored absolute Windows paths.

## GEDCOM X

`GedcomXSerializer` reads and writes the GEDCOM X model, following the gedcomx.org specifications, in three forms:

- JSON;
- XML;
- `.gedx` files, which are ZIP archives with a manifest.

Properties this library doesn't model are kept, so they survive a round-trip.

```csharp
var document = GedcomXConverter.ToGedcomX(file);
string json = serializer.ToJson(document);

var back = GedcomXConverter.FromGedcomX(serializer.FromJson(json).Value, GedcomVersion.V551);
```

The converter maps the genealogical core in both directions:

- people, names, gender and facts;
- couples and parent-child relationships, which become families;
- sources, repositories, submitters, notes and media.

GEDCOM X has no family record, so families are rebuilt from couple and parent-child relationships. Data outside that core does not survive the conversion.

## Tests

`dotnet test` runs the unit tests. To also round-trip and convert a set of real files, point `GEDCOMSHARP_CORPUS` at one or more folders of `.ged` files, separated by `;`.

## Support

If you find this project useful, consider supporting its development.

<p align="center">
  <a href="https://buymeacoffee.com/vnmatt">
    <img src="https://github.com/gordon-matt/MyResume/blob/gh-pages/assets/images/BuyMeACoffee_Logo.png"
         alt="Buy Me a Coffee"
         height="40">
  </a>&nbsp;&nbsp;&nbsp;
  <a href="https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=gordon_matt%40live%2ecom&lc=AU&currency_code=AUD&bn=PP%2dDonationsBF%3abtn_donateCC_LG%2egif%3aNonHosted">
    <img src="https://github.com/gordon-matt/MyResume/blob/gh-pages/assets/images/PayPal_Logo.png"
         alt="Donate with PayPal"
         height="40">
  </a>
</p>

<p align="center">
  Prefer using your phone? Scan the QR code:
</p>

<p align="center">
  <a href="https://buymeacoffee.com/vnmatt">
    <img src="https://github.com/gordon-matt/MyResume/blob/gh-pages/assets/images/BuyMeACoffee_QR.png"
         alt="Buy Me a Coffee QR Code"
         width="180">
  </a>
</p>

<img src="https://komarev.com/ghpvc/?username=gordon-matt&label=GitHub%20Hits%20Since%202025-06-01%3A%20&color=ff0000&style=flat" alt="gordon-matt" />
