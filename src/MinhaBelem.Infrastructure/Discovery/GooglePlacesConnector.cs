using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
namespace MinhaBelem.Infrastructure.Discovery;
public sealed class GooglePlacesConnector(IPlacesClient places) : IGooglePlacesConnector
{
    public bool Supports(FeedProtocol protocol) => protocol == FeedProtocol.GooglePlaces;
    public string? ConfigurationIssue(SourceContext context) => places.IsConfigured ? null : "Google Places aguarda chave e autorização no provedor.";
    public async Task<ConnectorResult> ReadAsync(SourceContext context, CancellationToken ct)
    {
        var rows = await places.SearchAsync("tourist_attraction", -1.4558, -48.4902, 10000, context.Profile.MaxItems, ct);
        // Persistir apenas IDs/referencias, nunca nomes, fotos ou avaliacoes do Google.
        return new(rows.Select(x => new DiscoveredItem("Local para curadoria no Google Maps", "Identificador de estabelecimento descoberto para avaliação editorial.", "https://www.google.com/maps/search/?api=1&query=local&query_place_id=" + Uri.EscapeDataString(x.GooglePlaceId), null, null, null, x.GooglePlaceId)).ToArray());
    }
}
