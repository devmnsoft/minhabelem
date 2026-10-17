using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Text;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Infrastructure.Networking;

namespace MinhaBelem.Infrastructure.Places;

public sealed class GooglePlacesOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 20;
    public int DailyRequestLimit { get; set; } = 50;
}

public sealed record PlaceQueryOptions(
    string? Type,
    double Latitude,
    double Longitude,
    double RadiusMeters,
    int MaxResults);

/// <summary>
/// Cliente Google Places (New). Dados retornados sao transitorios por requisicao;
/// o guia persiste apenas identificadores e informacoes editoriais proprias.
/// </summary>
public sealed class GooglePlacesClient(
    IHttpClientFactory httpClientFactory,
    GooglePlacesOptions options,
    ILogger<GooglePlacesClient> logger, ApiBudget budget) : IPlacesClient
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ApiKey);

    public async Task<string?> GetPhotoUriAsync(string? photoName, CancellationToken ct)
    {
        if (!IsConfigured || photoName is null || !System.Text.RegularExpressions.Regex.IsMatch(photoName, @"^places/[A-Za-z0-9_-]+/photos/[A-Za-z0-9_-]+$")) return null;
        if (!await budget.ReserveAsync("google-places", options.DailyRequestLimit, ct)) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://places.googleapis.com/v1/{photoName}/media?maxWidthPx=800&skipHttpRedirect=true");
        request.Headers.Add("X-Goog-Api-Key", options.ApiKey);
        using var response = await httpClientFactory.CreateClient("places").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var url = doc.RootElement.TryGetProperty("photoUri", out var value) ? value.GetString() : null;
        var validation = SafeUrl.Validate(url);
        return validation.IsValid && validation.Uri!.DnsSafeHost.EndsWith(".googleusercontent.com", StringComparison.OrdinalIgnoreCase) ? url : null;
    }

    public async Task<IReadOnlyList<Place>> SearchAsync(string placeType, double latitude, double longitude, double radiusMeters, int maxResults, CancellationToken ct)
    {
        if (!IsConfigured) return Array.Empty<Place>();
        if (!PlacesCatalog.IsAllowedType(placeType))
        {
            logger.LogWarning("Tipo de lugar não permitido: {Type}", placeType);
            return Array.Empty<Place>();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:searchNearby");
        request.Headers.Add("X-Goog-Api-Key", options.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", PlacesCatalog.SearchFieldMask);
        request.Content = JsonContent.Create(new
        {
            includedTypes = new[] { placeType },
            maxResultCount = Math.Clamp(maxResults, 1, options.MaxResults),
            locationRestriction = new
            {
                circle = new { center = new { latitude, longitude }, radius = Math.Clamp(radiusMeters, 100, 50_000) }
            },
            languageCode = "pt-BR",
            regionCode = "BR"
        });

        return await SendAsync(request, ct);
    }

    public async Task<Place?> GetDetailsAsync(string placeId, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(placeId)) return null;

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(placeId)}?languageCode=pt-BR&regionCode=BR");
        request.Headers.Add("X-Goog-Api-Key", options.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", PlacesCatalog.DetailsFieldMask);

        var places = await SendAsync(request, ct, single: true);
        return places.FirstOrDefault();
    }

    private async Task<IReadOnlyList<Place>> SendAsync(HttpRequestMessage request, CancellationToken ct, bool single = false)
    {
        try
        {
            if (!await budget.ReserveAsync("google-places", options.DailyRequestLimit, ct)) return Array.Empty<Place>();
            var client = httpClientFactory.CreateClient("places");
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google Places respondeu {Status}", (int)response.StatusCode);
                return Array.Empty<Place>();
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            var now = DateTime.UtcNow;
            var purgeAfter = now;

            if (single)
            {
                var place = Map(root, now, purgeAfter);
                return place is null ? Array.Empty<Place>() : new[] { place };
            }

            if (!root.TryGetProperty("places", out var places) || places.ValueKind != JsonValueKind.Array)
                return Array.Empty<Place>();

            return places.EnumerateArray()
                .Select(p => Map(p, now, purgeAfter))
                .Where(p => p is not null)
                .Select(p => p!)
                .ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Falha ao consultar o Google Places");
            return Array.Empty<Place>();
        }
    }

    private static Place? Map(JsonElement element, DateTime now, DateTime purgeAfter)
    {
        var id = GetString(element, "id");
        var display = element.TryGetProperty("displayName", out var dn) && dn.TryGetProperty("text", out var text)
            ? text.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(display)) return null;

        var addressParts = new List<string>();
        if (element.TryGetProperty("addressComponents", out var components) && components.ValueKind == JsonValueKind.Array)
        {
            foreach (var component in components.EnumerateArray())
            {
                var componentTypes = component.TryGetProperty("types", out var t) && t.ValueKind == JsonValueKind.Array
                    ? t.EnumerateArray().Select(x => x.GetString()).ToArray()
                    : Array.Empty<string?>();
                var longName = component.TryGetProperty("longText", out var lt) ? lt.GetString() : null;
                if (string.IsNullOrWhiteSpace(longName)) continue;
                if (componentTypes.Contains("street_number") || componentTypes.Contains("route"))
                    addressParts.Insert(0, longName);
                else if (componentTypes.Contains("sublocality") || componentTypes.Contains("neighborhood"))
                    addressParts.Add(longName);
                else if (componentTypes.Contains("locality") || componentTypes.Contains("administrative_area_level_2"))
                    addressParts.Add(longName);
            }
        }

        double? latitude = null, longitude = null;
        if (element.TryGetProperty("location", out var location))
        {
            if (location.TryGetProperty("latitude", out var lat)) latitude = lat.GetDouble();
            if (location.TryGetProperty("longitude", out var lng)) longitude = lng.GetDouble();
        }

        var types = element.TryGetProperty("types", out var typesElement) && typesElement.ValueKind == JsonValueKind.Array
            ? typesElement.EnumerateArray().Select(t => t.GetString()).Where(t => t is not null).Select(t => t!).ToArray()
            : Array.Empty<string>();

        var openingHours = ReadOpeningHours(element);

        string? photoName = null, photoAttribution = null;
        string? photoGoogleMapsUri = null;
        var authors = new List<PhotoAuthor>();
        if (element.TryGetProperty("photos", out var photos) && photos.ValueKind == JsonValueKind.Array && photos.GetArrayLength() > 0)
        {
            photoName = GetString(photos[0], "name");
            photoGoogleMapsUri = GetString(photos[0], "googleMapsUri");
            if (photos[0].TryGetProperty("authorAttributions", out var attributions) && attributions.ValueKind == JsonValueKind.Array && attributions.GetArrayLength() > 0)
            {
                foreach (var attribution in attributions.EnumerateArray())
                {
                    var uri = GetString(attribution, "uri");
                    if (uri?.StartsWith("//", StringComparison.Ordinal) == true) uri = "https:" + uri;
                    if (SafeUrl.Validate(uri).IsValid) authors.Add(new PhotoAuthor(GetString(attribution, "displayName") ?? "Autor", uri!) { PhotoUri = SafeUrl.Validate(GetString(attribution, "photoUri")).IsValid ? GetString(attribution, "photoUri") : null });
                }
                photoAttribution = string.Join(", ", authors.Select(x => x.Name));
            }
        }

        double? rating = element.TryGetProperty("rating", out var r) ? r.GetDouble() : null;
        int? ratingCount = element.TryGetProperty("userRatingCount", out var rc) && rc.TryGetInt32(out var rcv) ? rcv : null;
        int? priceLevel = element.TryGetProperty("priceLevel", out var pl) && pl.ValueKind == JsonValueKind.String
            ? MapPriceLevel(pl.GetString())
            : null;

        bool? wheelchair = null;
        if (element.TryGetProperty("accessibilityOptions", out var access) && access.ValueKind == JsonValueKind.Object)
        {
            if (access.TryGetProperty("wheelchairAccessibleEntrance", out var entrance) && entrance.ValueKind is JsonValueKind.True or JsonValueKind.False)
                wheelchair = entrance.GetBoolean();
        }

        var formattedAddress = GetString(element, "formattedAddress") ?? string.Join(", ", addressParts.Distinct());

        return new Place
        {
            GooglePlaceId = id,
            Name = HtmlText.Clean(display, 300),
            FormattedAddress = HtmlText.Clean(formattedAddress, 500),
            State = BelemGeography.State,
            Neighborhood = BelemGeography.ResolveNeighborhood(formattedAddress),
            Municipality = BelemGeography.ResolveMunicipality(formattedAddress)?.Name ?? "Belém",
            Latitude = latitude,
            Longitude = longitude,
            GoogleMapsUri = GetString(element, "googleMapsUri"),
            WebsiteUri = GetString(element, "websiteUri"),
            Phone = GetString(element, "nationalPhoneNumber") ?? GetString(element, "internationalPhoneNumber"),
            PrimaryType = GetString(element, "primaryType"),
            Types = types,
            Rating = rating,
            RatingCount = ratingCount,
            BusinessStatus = GetString(element, "businessStatus"),
            OpeningHours = openingHours,
            PriceLevel = priceLevel,
            PriceText = PriceLevelLabel(priceLevel),
            WheelchairAccessible = wheelchair,
            PhotoName = photoName,
            PhotoAttribution = photoName is null ? null : photoAttribution,
            PhotoAuthors = authors,
            PhotoGoogleMapsUri = photoGoogleMapsUri,
            IsPublished = true,
            CachedAt = now,
            PurgeAfter = purgeAfter,
            Slug = HtmlText.Slugify(display)
        };
    }

    private static int? MapPriceLevel(string? level) => level switch
    {
        "PRICE_LEVEL_FREE" => 0,
        "PRICE_LEVEL_INEXPENSIVE" => 1,
        "PRICE_LEVEL_MODERATE" => 2,
        "PRICE_LEVEL_EXPENSIVE" => 3,
        "PRICE_LEVEL_VERY_EXPENSIVE" => 4,
        _ => null
    };

    public static string? PriceLevelLabel(int? level) => level switch
    {
        0 => "Gratuito",
        1 => "Econômico",
        2 => "Moderado",
        3 => "Caro",
        4 => "Muito caro",
        _ => null
    };

    private static IReadOnlyList<OpeningHours> ReadOpeningHours(JsonElement element)
    {
        var result = new List<OpeningHours>();
        if (!element.TryGetProperty("regularOpeningHours", out var hours) || hours.ValueKind != JsonValueKind.Object)
            return result;
        if (!hours.TryGetProperty("weekdayDescriptions", out var descriptions) || descriptions.ValueKind != JsonValueKind.Array)
            return result;

        var index = 0;
        foreach (var description in descriptions.EnumerateArray().Select(d => d.GetString() ?? string.Empty))
        {
            var day = index++;
            var text = HtmlText.Clean(description, 120);
            if (text.Length == 0) continue;
            var closed = text.Contains("Fechado", StringComparison.OrdinalIgnoreCase)
                         || text.Contains("Closed", StringComparison.OrdinalIgnoreCase);
            result.Add(new OpeningHours(day, text, string.Empty, string.Empty, closed));
        }
        return result;
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object when value.TryGetProperty("text", out var text) => text.GetString(),
            _ => null
        };
    }
}

public static class PlacesCatalog
{
    public const string SearchFieldMask =
        "places.id,places.displayName,places.formattedAddress,places.location,places.googleMapsUri," +
        "places.primaryType,places.types,places.rating,places.userRatingCount,places.businessStatus," +
        "places.priceLevel,places.regularOpeningHours.weekdayDescriptions";

    public const string DetailsFieldMask =
        "id,displayName,formattedAddress,addressComponents,location,googleMapsUri,websiteUri," +
        "nationalPhoneNumber,internationalPhoneNumber,primaryType,types,rating,userRatingCount," +
        "businessStatus,priceLevel,regularOpeningHours.weekdayDescriptions,photos,accessibilityOptions";

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "tourist_attraction", "restaurant", "cafe", "bar", "bakery", "museum", "movie_theater",
        "performing_arts_theater", "park", "hospital", "library", "art_gallery", "shopping_mall",
        "lodging", "stadium", "amusement_park", "zoo", "church", "mosque", "school", "university",
        "pharmacy", "police", "fire_station", "post_office", "bank", "atm", "supermarket",
        "store", "gym", "spa", "night_club", "casino", "cemetery", "city_hall", "train_station",
        "bus_station", "airport", "subway_station", "taxi_stand", "parking", "courthouse", "embassy"
    };

    public static bool IsAllowedType(string? type) => type is not null && Allowed.Contains(type);

    public static IReadOnlyList<string> Types => Allowed.OrderBy(t => t, StringComparer.Ordinal).ToArray();
}
