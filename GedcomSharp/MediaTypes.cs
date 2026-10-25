namespace GedcomSharp;

/// <summary>
/// Maps between 7.0 media types (<c>image/jpeg</c>) and the file-extension formats 5.x uses in
/// <c>FORM</c> (<c>jpg</c>), and between 7.0 file URLs and 5.x paths.
/// </summary>
public static class MediaTypes
{
    private static readonly Dictionary<string, string> byExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["jpe"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["bmp"] = "image/bmp",
        ["tif"] = "image/tiff",
        ["tiff"] = "image/tiff",
        ["webp"] = "image/webp",
        ["heic"] = "image/heic",
        ["svg"] = "image/svg+xml",
        ["pcx"] = "image/x-pcx",
        ["psd"] = "image/vnd.adobe.photoshop",
        ["wav"] = "audio/wav",
        ["mp3"] = "audio/mpeg",
        ["m4a"] = "audio/mp4",
        ["ogg"] = "audio/ogg",
        ["flac"] = "audio/flac",
        ["aac"] = "audio/aac",
        ["wma"] = "audio/x-ms-wma",
        ["avi"] = "video/x-msvideo",
        ["mpg"] = "video/mpeg",
        ["mpeg"] = "video/mpeg",
        ["mp4"] = "video/mp4",
        ["m4v"] = "video/mp4",
        ["mov"] = "video/quicktime",
        ["wmv"] = "video/x-ms-wmv",
        ["mkv"] = "video/x-matroska",
        ["webm"] = "video/webm",
        ["pdf"] = "application/pdf",
        ["txt"] = "text/plain",
        ["htm"] = "text/html",
        ["html"] = "text/html",
        ["rtf"] = "application/rtf",
        ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["ole"] = "application/x-ole-storage",
        ["zip"] = "application/zip",
        ["ged"] = "text/vnd.familysearch.gedcom"
    };

    private static readonly Dictionary<string, string> preferredExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/tiff"] = "tif",
        ["video/mpeg"] = "mpg",
        ["video/mp4"] = "mp4",
        ["text/html"] = "htm",
        ["audio/mpeg"] = "mp3"
    };

    /// <summary>The media type for a FORM value, which may already be a media type or a 5.x extension.</summary>
    public static string? FromFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return null;
        }

        string value = format.Trim().TrimStart('.');
        if (value.Contains('/'))
        {
            return value.ToLowerInvariant();
        }

        return byExtension.GetValueOrDefault(value);
    }

    /// <summary>The media type implied by a file path or URL's extension.</summary>
    public static string? FromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string clean = path;
        int query = clean.IndexOfAny(['?', '#']);
        if (query >= 0 && clean.Contains("://", StringComparison.Ordinal))
        {
            clean = clean[..query];
        }

        string extension = Path.GetExtension(clean.Replace('\\', '/')).TrimStart('.');
        return extension.Length == 0 ? null : byExtension.GetValueOrDefault(extension);
    }

    /// <summary>The 5.x FORM value (a lowercase extension) for a media type.</summary>
    public static string? ToLegacyFormat(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return null;
        }

        if (!mediaType.Contains('/'))
        {
            return mediaType.Trim().TrimStart('.').ToLowerInvariant();
        }

        if (preferredExtension.TryGetValue(mediaType, out string? preferred))
        {
            return preferred;
        }

        foreach (var (extension, type) in byExtension)
        {
            if (string.Equals(type, mediaType, StringComparison.OrdinalIgnoreCase))
            {
                return extension;
            }
        }

        string subtype = mediaType[(mediaType.IndexOf('/') + 1)..];
        int plus = subtype.IndexOf('+');
        return (plus > 0 ? subtype[..plus] : subtype).Replace("x-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    /// <summary>
    /// Turns a 5.x file reference into the URL 7.0 requires: backslashes become slashes, absolute paths
    /// become <c>file:///</c> URLs, and characters outside the URL syntax are percent-encoded.
    /// </summary>
    public static string ToUrl(string path)
    {
        string value = path.Trim();
        if (value.Length == 0 || HasScheme(value))
        {
            return value;
        }

        value = value.Replace('\\', '/');
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return "file:" + EscapePath(value);
        }

        if (value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':')
        {
            return "file:///" + value[..2] + EscapePath(value[2..]);
        }

        return value.StartsWith('/') ? "file://" + EscapePath(value) : EscapePath(value);
    }

    /// <summary>Turns a 7.0 file URL back into a 5.x path; other URLs are returned unchanged.</summary>
    public static string ToPath(string url)
    {
        string value = url.Trim();
        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            string rest = value[5..];
            if (rest.StartsWith("///", StringComparison.Ordinal))
            {
                rest = rest[3..];
                string decoded = Uri.UnescapeDataString(rest);
                if (decoded.Length >= 2 && char.IsAsciiLetter(decoded[0]) && decoded[1] == ':')
                {
                    return decoded.Replace('/', '\\');
                }

                return "/" + decoded;
            }

            if (rest.StartsWith("//", StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(rest).Replace('/', '\\');
            }

            return Uri.UnescapeDataString(rest);
        }

        return HasScheme(value) ? value : Uri.UnescapeDataString(value);
    }

    private static bool HasScheme(string value)
    {
        int colon = value.IndexOf(':');
        if (colon <= 1)
        {
            return false;
        }

        for (int i = 0; i < colon; i++)
        {
            char c = value[i];
            if (!(char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.'))
            {
                return false;
            }
        }

        return char.IsAsciiLetter(value[0]);
    }

    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(segment => Uri.EscapeDataString(Uri.UnescapeDataString(segment))));
}
