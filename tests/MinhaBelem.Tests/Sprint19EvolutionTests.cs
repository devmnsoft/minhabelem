using System.Globalization;
using System.IO;
using MinhaBelem.Core.Entities;
using MinhaBelem.Infrastructure.Ai;
using MinhaBelem.Web.Resources;
using Xunit;

namespace MinhaBelem.Tests;

public sealed class Sprint19EvolutionTests
{
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MinhaBelem.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    [Fact]
    public void Sprint19TranslationsAreRegisteredAcrossAllSevenCultures()
    {
        var requiredKeys = new[]
        {
            "A11y_Panel_Title",
            "A11y_Open_Panel",
            "A11y_Close_Panel",
            "A11y_Font_Size",
            "A11y_Font_Normal",
            "A11y_Font_Medium",
            "A11y_Font_Large",
            "A11y_Spacing",
            "A11y_Spacing_Normal",
            "A11y_Spacing_Wide",
            "A11y_High_Contrast",
            "A11y_Reduced_Motion",
            "A11y_Focus_Highlight",
            "A11y_Reset",
            "Password_Show",
            "Password_Hide",
            "CapsLock_Warning",
            "Upcoming_Belém_Events",
            "Upcoming_Belém_Events_Subtitle",
            "Event_Confirmed_Free",
            "Event_Paid",
            "Event_Check_Price",
            "Event_Details_Link",
            "Event_Official_Source",
            "Event_Add_Calendar",
            "A11y_Map_Alternative",
            "A11y_Route_Steps",
            "Radar_Service_Status",
            "Radar_Real_Status_Active",
            "Radar_Real_Status_Inactive"
        };

        foreach (var culture in SupportedCultures.All)
        {
            foreach (var key in requiredKeys)
            {
                var translation = LocalizationCatalog.Get(key, culture);
                Assert.False(string.IsNullOrWhiteSpace(translation), $"Chave '{key}' ausente ou vazia para a cultura '{culture}'.");
            }
        }
    }

    [Fact]
    public void AccessibilityPreferencesTokensAreConfiguredInCatalog()
    {
        Assert.Equal("Preferências de acessibilidade", LocalizationCatalog.Get("A11y_Panel_Title", "pt-BR"));
        Assert.Equal("Accessibility preferences", LocalizationCatalog.Get("A11y_Panel_Title", "en"));
        Assert.Equal("Préférences d'accessibilité", LocalizationCatalog.Get("A11y_Panel_Title", "fr"));
        Assert.Equal("Preferencias de accesibilidad", LocalizationCatalog.Get("A11y_Panel_Title", "es"));
        Assert.Equal("Настройки доступности", LocalizationCatalog.Get("A11y_Panel_Title", "ru"));
        Assert.Equal("无障碍偏好设置", LocalizationCatalog.Get("A11y_Panel_Title", "zh-Hans"));
        Assert.Equal("アクセシビリティ設定", LocalizationCatalog.Get("A11y_Panel_Title", "ja"));
    }

    [Fact]
    public void LoginPageMarkupAdheresToWcagAccessibilityGuidelines()
    {
        var root = FindRepositoryRoot();
        var loginViewPath = Path.Combine(root, "src", "MinhaBelem.Web", "Views", "Conta", "Login.cshtml");
        Assert.True(File.Exists(loginViewPath), "View Login.cshtml não encontrada.");

        var content = File.ReadAllText(loginViewPath);
        Assert.Contains("autocomplete=\"username\"", content);
        Assert.Contains("autocomplete=\"current-password\"", content);
        Assert.Contains("class=\"password-toggle-btn\"", content);
        Assert.Contains("aria-label=\"@Loc[\"Password_Show\"]\"", content);
        Assert.Contains("aria-pressed=\"false\"", content);
        Assert.Contains("id=\"caps-lock-warning\"", content);
        Assert.Contains("role=\"status\"", content);
        Assert.Contains("aria-live=\"polite\"", content);
        Assert.Contains("asp-validation-summary=\"All\"", content);
        Assert.Contains("role=\"alert\"", content);
    }

    [Fact]
    public void AccessibilityPanelMarkupAdheresToDialogPattern()
    {
        var root = FindRepositoryRoot();
        var a11yViewPath = Path.Combine(root, "src", "MinhaBelem.Web", "Views", "Shared", "_AccessibilityPanel.cshtml");
        Assert.True(File.Exists(a11yViewPath), "View _AccessibilityPanel.cshtml não encontrada.");

        var content = File.ReadAllText(a11yViewPath);
        Assert.Contains("role=\"dialog\"", content);
        Assert.Contains("aria-modal=\"true\"", content);
        Assert.Contains("data-a11y-font=\"default\"", content);
        Assert.Contains("data-a11y-font=\"medium\"", content);
        Assert.Contains("data-a11y-font=\"large\"", content);
        Assert.Contains("data-a11y-spacing=\"wide\"", content);
        Assert.Contains("data-a11y-toggle=\"contrast\"", content);
        Assert.Contains("data-a11y-toggle=\"focus\"", content);
        Assert.Contains("data-a11y-toggle=\"motion\"", content);
    }

    [Fact]
    public void HomeIndexContainsBelemUpcomingEventsSection()
    {
        var root = FindRepositoryRoot();
        var homeViewPath = Path.Combine(root, "src", "MinhaBelem.Web", "Views", "Home", "Index.cshtml");
        Assert.True(File.Exists(homeViewPath), "View Home/Index.cshtml não encontrada.");

        var content = File.ReadAllText(homeViewPath);
        Assert.Contains("home-events-highlight", content);
        Assert.Contains("@Loc[\"Upcoming_Belém_Events\"]", content);
        Assert.Contains("events-spotlight-grid", content);
        Assert.Contains("event-date-badge", content);
        Assert.Contains("event-price-tag", content);
        Assert.Contains("event-spotlight-actions", content);
    }

    [Fact]
    public void MapAlternativesContainExplicitTextStepList()
    {
        var root = FindRepositoryRoot();
        var visitViewPath = Path.Combine(root, "src", "MinhaBelem.Web", "Views", "Visit", "Index.cshtml");
        Assert.True(File.Exists(visitViewPath), "View Visit/Index.cshtml não encontrada.");

        var content = File.ReadAllText(visitViewPath);
        Assert.Contains("@Loc[\"A11y_Map_Alternative\"]", content);
        Assert.Contains("@Loc[\"A11y_Route_Steps\"]", content);
        Assert.Contains("class=\"journey-timeline\"", content);
    }

    [Fact]
    public void SiteCssIncludesHighContrastAndAccessibilityTokens()
    {
        var root = FindRepositoryRoot();
        var cssPath = Path.Combine(root, "src", "MinhaBelem.Web", "wwwroot", "css", "site.css");
        Assert.True(File.Exists(cssPath), "Arquivo site.css não encontrado.");

        // The entry point imports styles by responsibility; rules are no longer duplicated here.
        var css = File.ReadAllText(cssPath);
        Assert.Contains("@import url('accessibility.css')", css);
        Assert.Contains("@import url('components.css')", css);
        css += File.ReadAllText(Path.Combine(Path.GetDirectoryName(cssPath)!, "accessibility.css"));
        css += File.ReadAllText(Path.Combine(Path.GetDirectoryName(cssPath)!, "components.css"));
        Assert.Contains("html[data-contrast=\"high\"]", css);
        Assert.Contains("html[data-font-size=\"medium\"]", css);
        Assert.Contains("html[data-spacing=\"wide\"]", css);
        Assert.Contains("html[data-focus-highlight=\"true\"]", css);
        Assert.Contains("html[data-reduced-motion=\"true\"]", css);
        Assert.Contains(".password-wrapper", css);
        Assert.Contains(".password-toggle-btn", css);
        Assert.Contains(".a11y-panel", css);
    }

    [Fact]
    public void NativeBelemAiEngineGeneratesAuthenticGuidanceForGastronomyAndIslands()
    {
        var dummyPlaces = new List<Place>
        {
            new Place { Id = 1, Name = "Restaurante Ilha do Combu", PrimaryType = "restaurant", FormattedAddress = "Ilha do Combu, Belém" },
            new Place { Id = 2, Name = "Tacacá da Dona Maria", PrimaryType = "restaurant", FormattedAddress = "Avenida Nazaré, Belém" },
            new Place { Id = 3, Name = "Theatro da Paz", PrimaryType = "tourist_attraction", FormattedAddress = "Praça da República, Belém" }
        };

        var gastroResult = NativeBelemAiEngine.Generate("Onde comer o melhor tacacá e açaí em Belém?", "pt-BR", dummyPlaces, Array.Empty<ContentItem>(), Array.Empty<TransitLine>());
        Assert.NotNull(gastroResult.ResponseText);
        Assert.Contains("Tacacá", gastroResult.ResponseText);
        Assert.Contains("Açaí", gastroResult.ResponseText);
        Assert.NotEmpty(gastroResult.Suggestions);

        var islandResultEn = NativeBelemAiEngine.Generate("How to take the boat to Combu Island?", "en-US", dummyPlaces, Array.Empty<ContentItem>(), Array.Empty<TransitLine>());
        Assert.NotNull(islandResultEn.ResponseText);
        Assert.Contains("Combu", islandResultEn.ResponseText);
        Assert.Contains("boat", islandResultEn.ResponseText, StringComparison.OrdinalIgnoreCase);

        var heritageResult = NativeBelemAiEngine.Generate("História e ingressos do Theatro da Paz", "pt-BR", dummyPlaces, Array.Empty<ContentItem>(), Array.Empty<TransitLine>());
        Assert.NotNull(heritageResult.ResponseText);
        Assert.Contains("Theatro da Paz", heritageResult.ResponseText);
    }

    [Fact]
    public void HomologationConnotationsAreCleanedFromUserMessages()
    {
        var root = FindRepositoryRoot();
        var perfilPath = Path.Combine(root, "src", "MinhaBelem.Web", "Views", "Conta", "Perfil.cshtml");
        Assert.True(File.Exists(perfilPath));
        var perfil = File.ReadAllText(perfilPath);
        Assert.DoesNotContain("senha provisória de homologação", perfil);
        Assert.Contains("senha temporária pendente de redefinição", perfil);

        var evolucaoPath = Path.Combine(root, "src", "MinhaBelem.Web", "Views", "Admin", "Evolucao.cshtml");
        Assert.True(File.Exists(evolucaoPath));
        var evolucao = File.ReadAllText(evolucaoPath);
        Assert.DoesNotContain("Evolução e homologação", evolucao);
        Assert.Contains("Evolução e Governança Operacional", evolucao);
    }
}

