using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MinhaBelem.Core.Services;

/// <summary>Região metropolitana de Belém usada para não misturar fotos de outras cidades homônimas.</summary>
public static class BelemMetro
{
    public static bool Contains(double latitude, double longitude) =>
        latitude is >= -1.72 and <= -0.95 && longitude is >= -48.75 and <= -48.15;
}

/// <summary>Licença que pode aparecer no portal. Conteúdo não comercial ou sem licença fica de fora.</summary>
public static class PhotoLicense
{
    public static bool IsPublishable(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.Trim().ToLowerInvariant();
        if (System.Text.RegularExpressions.Regex.IsMatch(n, @"\bnc\b|noncommercial|non-commercial|\bnd\b"))
            return false;
        if (n.Contains("copyright", StringComparison.Ordinal) && !n.Contains("no known copyright", StringComparison.Ordinal))
            return false;
        return n.Contains("cc0", StringComparison.Ordinal)
            || n.Contains("cc by", StringComparison.Ordinal)
            || n.Contains("cc-by", StringComparison.Ordinal)
            || n.Contains("public domain", StringComparison.Ordinal)
            || n.Contains("domínio público", StringComparison.Ordinal)
            || n.Contains("dominio publico", StringComparison.Ordinal)
            || n.Contains("gfdl", StringComparison.Ordinal);
    }
}

public sealed record PlacePhotoCandidate(
    string TargetKey,
    string PlaceName,
    string Municipality,
    string ImageUrl,
    string? ThumbUrl,
    string PageUrl,
    string Credit,
    string LicenseName,
    string? LicenseUrl,
    string? Mime,
    int? Width,
    int? Height,
    double? Latitude,
    double? Longitude,
    bool Publishable);

public static class CommonsPhotoParser
{
    public static IReadOnlyList<PlacePhotoCandidate> Parse(string json, string targetKey, string placeName, string municipality, bool trustGeosearch)
    {
        var list = new List<PlacePhotoCandidate>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("query", out var query) || !query.TryGetProperty("pages", out var pages))
            return list;
        foreach (var page in pages.EnumerateObject())
        {
            if (!page.Value.TryGetProperty("imageinfo", out var infos)) continue;
            var fileTitle = page.Value.TryGetProperty("title", out var titleNode) ? titleNode.GetString() : null;
            if (!TitleMatches(fileTitle, placeName)) continue;
            foreach (var info in infos.EnumerateArray())
            {
                var candidate = Read(info, targetKey, placeName, municipality, trustGeosearch);
                if (candidate is not null) list.Add(candidate);
            }
        }
        return list;
    }

    private static PlacePhotoCandidate? Read(JsonElement info, string targetKey, string placeName, string municipality, bool trustGeosearch)
    {
        var image = String(info, "url");
        var thumb = String(info, "thumburl");
        var page = String(info, "descriptionurl");
        if (!AllowedCommonsHost(image) || !AllowedCommonsHost(page)) return null;
        var meta = info.TryGetProperty("extmetadata", out var ext) ? ext : default;
        var license = Meta(meta, "LicenseShortName");
        var publishable = PhotoLicense.IsPublishable(license);
        if (!publishable) return null;
        var mime = String(info, "mime");
        if (mime is not ("image/jpeg" or "image/png" or "image/webp" or "image/gif")) return null;
        var lat = Coordinate(Meta(meta, "GPSLatitude"), Meta(meta, "GPSLatitudeRef"));
        var lng = Coordinate(Meta(meta, "GPSLongitude"), Meta(meta, "GPSLongitudeRef"));
        if (lng is > 0 and < 90) lng = -lng;
        if (lat is > 0 and < 20 && lng is < -40 and > -60) lat = -lat;
        if (lat is { } la && lng is { } lo)
        {
            if (!BelemMetro.Contains(la, lo)) return null;
        }
        else if (!trustGeosearch)
        {
            return null;
        }
        else
        {
            lat = null;
            lng = null;
        }
        var credit = Credit(Meta(meta, "Artist"));
        int? width = info.TryGetProperty("thumbwidth", out var w) && w.TryGetInt32(out var wi) ? wi : null;
        int? height = info.TryGetProperty("thumbheight", out var h) && h.TryGetInt32(out var hi) ? hi : null;
        return new PlacePhotoCandidate(targetKey, placeName, municipality, image!, AllowedCommonsHost(thumb) ? thumb : image, page!, credit, license!.Trim(), CleanUrl(Meta(meta, "LicenseUrl")), mime, width, height, lat, lng, true);
    }

    public static bool AllowedCommonsHost(string? url)
    {
        var check = Security.SafeUrl.Validate(url);
        if (!check.IsValid || check.Uri is null) return false;
        var host = check.Uri.Host;
        return host.Equals("upload.wikimedia.org", StringComparison.OrdinalIgnoreCase)
            || host.Equals("commons.wikimedia.org", StringComparison.OrdinalIgnoreCase);
    }

    private static string? CleanUrl(string? url) => Security.SafeUrl.Validate(url).IsValid ? url!.Trim() : null;

    private static double? Coordinate(string? raw, string? hemisphere)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim().Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return null;
        if (value > 0 && hemisphere is "S" or "W") value = -value;
        return value is >= -180 and <= 180 ? value : null;
    }

    private static string Credit(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "Wikimedia Commons";
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length > 280) text = text[..280];
        return text.Length == 0 ? "Wikimedia Commons" : text;
    }

    private static bool TitleMatches(string? fileTitle, string placeName)
    {
        var title = Fold(fileTitle ?? "");
        var tokens = placeName.Split([' ', '-', '’', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Select(Fold)
            .Where(token => token.Length >= 4 && token is not "belem" and not "para" and not "centro" and not "orla")
            .Distinct()
            .ToArray();
        return tokens.Length == 0 || tokens.Any(token => title.Contains(token, StringComparison.Ordinal));
    }

    private static string Fold(string value)
    {
        var form = value.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = form.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        return new string(chars);
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Meta(JsonElement meta, string name)
    {
        if (meta.ValueKind != JsonValueKind.Object || !meta.TryGetProperty(name, out var node)) return null;
        if (node.ValueKind == JsonValueKind.String) return node.GetString();
        if (node.ValueKind == JsonValueKind.Object && node.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String) return value.GetString();
        return null;
    }
}
