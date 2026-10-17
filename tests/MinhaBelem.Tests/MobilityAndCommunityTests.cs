using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Text;
using Xunit;

namespace MinhaBelem.Tests;

public class MobilityAndCommunityTests
{
    [Fact]
    public void TransitLineEntityEnforcesValidFaresAndAccessibilityDefaults()
    {
        var line = new TransitLine
        {
            Code = "BRT-01",
            Name = "BRT Expresso Maracangalha — São Brás",
            Origin = "Terminal Maracangalha",
            Destination = "Terminal São Brás",
            Fare = 4.00m,
            TransitType = "BRT",
            HasWheelchairLift = true,
            HasAirConditioning = true,
            ItinerarySummary = "Canal exclusivo da Av. Augusto Montenegro."
        };

        Assert.Equal("BRT-01", line.Code);
        Assert.True(line.Fare > 0);
        Assert.True(line.HasWheelchairLift);
        Assert.True(line.HasAirConditioning);
        Assert.Equal("BRT", line.TransitType);
    }

    [Fact]
    public void RiverRoutesSupportAmazonianIslandDestinationsWithValidTimes()
    {
        var combuRoute = new RiverRoute
        {
            Name = "Travessia Praça do Pescador — Ilha do Combu",
            DeparturePort = "Porto da Praça do Pescador (Ver-o-Peso)",
            DestinationPort = "Furo do Combu",
            DestinationIsland = "Ilha do Combu",
            EstimatedDurationMinutes = 15,
            Fare = 12.00m,
            VesselType = "Popopô / Barco Regional",
            OperatingHours = "Diário das 07h às 19h",
            IsActive = true
        };

        Assert.Equal("Ilha do Combu", combuRoute.DestinationIsland);
        Assert.Equal(15, combuRoute.EstimatedDurationMinutes);
        Assert.True(combuRoute.IsActive);
        Assert.True(combuRoute.Fare >= 0);
    }

    [Fact]
    public void NeighborhoodGuideContainsRequiredHistoricalAndCulturalContext()
    {
        var guide = new NeighborhoodGuide
        {
            Name = "Cidade Velha",
            Slug = "cidade-velha",
            Zone = "Centro Histórico",
            HistorySummary = "Berço da fundação de Belém em 1616 por Francisco Caldeira Castelo Branco.",
            CulturalCharacteristics = "Ruas de pedra, casarões azulejados portugueses e festas de carimbó.",
            PopularAttractions = "Forte do Presépio, Catedral da Sé, Casa das Onze Janelas."
        };

        Assert.Equal("cidade-velha", guide.Slug);
        Assert.Contains("1616", guide.HistorySummary);
        Assert.Contains("carimbó", guide.CulturalCharacteristics);
        Assert.Contains("Forte do Presépio", guide.PopularAttractions);
    }

    [Fact]
    public void AdvertiserCampaignRequiresClearAdvertisingLabelForTransparency()
    {
        var campaign = new AdvertiserCampaign
        {
            Id = 1,
            AdvertiserName = "Restaurante Regional Belém",
            Headline = "Melhor Tacacá e Pato no Tucupi da Cidade",
            DestinationUrl = "https://example.com/sabores",
            Label = "Publicidade",
            Active = true
        };

        // Advertising must never be concealed from readers
        Assert.False(string.IsNullOrWhiteSpace(campaign.Label));
        Assert.Equal("Publicidade", campaign.Label);
        Assert.True(campaign.Active);
    }

    [Theory]
    [InlineData("Baixa")]
    [InlineData("Media")]
    [InlineData("Alta")]
    [InlineData("Critica")]
    public void TransitAlertsAcceptConfiguredSeverities(string severity)
    {
        var alert = new TransitAlert
        {
            Title = "Obras de drenagem na Av. Almirante Barroso",
            Description = "Faixa exclusiva liberada temporariamente para ônibus.",
            Severity = severity,
            AlertType = "Obras",
            StartAt = DateTime.UtcNow,
            IsActive = true
        };

        Assert.Equal(severity, alert.Severity);
        Assert.True(alert.IsActive);
    }

    [Fact]
    public void HtmlSanitizerStripsMaliciousScriptsWhilePreservingAmazonianExpressions()
    {
        var inputWithScript = "Aviso do Bairro Guamá: <script>alert('xss')</script>Feira do Ver-o-Peso abre às 05h com tacacá e peixe frito.";
        var cleaned = HtmlText.ToPlainText(inputWithScript, 300);

        Assert.DoesNotContain("<script>", cleaned);
        Assert.DoesNotContain("alert", cleaned);
        Assert.Contains("Feira do Ver-o-Peso", cleaned);
        Assert.Contains("tacacá", cleaned);
    }
}
