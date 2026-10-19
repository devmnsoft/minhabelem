namespace MinhaBelem.Core.Services;

public static class LandmarkGuide
{
    public const string IdPrefix = "mb-landmark:";
    public const string Note = "Ponto de referência da região metropolitana de Belém. A coordenada serve para chegar. Horário, preço e lotação só aparecem quando uma fonte verificada os confirma.";

    public static bool IsOwn(string? googlePlaceId) =>
        !string.IsNullOrWhiteSpace(googlePlaceId) && googlePlaceId.StartsWith(IdPrefix, StringComparison.Ordinal);

    public static string MapLink(double latitude, double longitude) =>
        "https://www.google.com/maps/search/?api=1&query="
        + latitude.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
        + longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
