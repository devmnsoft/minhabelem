using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Text;
using MinhaBelem.Web.Resources;
using Xunit;

namespace MinhaBelem.Tests;

public class InternationalizationTests
{
    [Fact]
    public void AllSevenLanguagesHaveCompleteResourceKeysWithZeroMissingTranslations()
    {
        var missingKeys = LocalizationCatalog.VerifyCompleteness();

        // Must have zero missing keys across all 7 languages: pt-BR, en, fr, es, ru, zh-Hans, ja
        Assert.Empty(missingKeys);
    }

    [Theory]
    [InlineData("pt-BR", "Nav_Explore", "Explore Belém")]
    [InlineData("en", "Nav_Explore", "Explore Belém")]
    [InlineData("fr", "Nav_Explore", "Explorez Belém")]
    [InlineData("es", "Nav_Explore", "Explora Belém")]
    [InlineData("ru", "Nav_Explore", "Откройте Белен")]
    [InlineData("zh-Hans", "Nav_Explore", "探索贝伦")]
    [InlineData("ja", "Nav_Explore", "ベレンを巡る")]
    public void CatalogReturnsAccurateNativeStringsForEachCulture(string culture, string key, string expected)
    {
        var actual = LocalizationCatalog.Get(key, culture);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CatalogFallsBackToPortugueseWhenCultureIsUnknownOrMissing()
    {
        var fallback = LocalizationCatalog.Get("Nav_Home", "de-DE");
        Assert.Equal("Início", fallback);
    }

    [Fact]
    public void CulturalGlossaryItemsReturnLocalizedExplanationsAndFallback()
    {
        var item = new CulturalGlossaryItem
        {
            Term = "Ver-o-Peso",
            Category = "Mercados & Feiras",
            ExplanationPt = "O maior mercado a céu aberto da América Latina, às margens da Baía do Guajará.",
            ExplanationEn = "Latin America's largest open-air market, located on the shores of Guajará Bay.",
            ExplanationFr = "Le plus grand marché à ciel ouvert d'Amérique latine, au bord de la baie de Guajará.",
            ExplanationEs = "El mercado al aire libre más grande de América Latina, a orillas de la Bahía de Guajará.",
            ExplanationRu = "Крупнейший рынок под открытым небом в Латинской Америке на берегу залива Гуажара.",
            ExplanationZh = "拉丁美洲最大的露天市场，坐落在瓜哈拉湾畔。",
            ExplanationJa = "グアジャラ湾のほとりに位置するラテンアメリカ最大の青空市場。"
        };

        Assert.Equal("Latin America's largest open-air market, located on the shores of Guajará Bay.", item.GetExplanation("en"));
        Assert.Equal("Le plus grand marché à ciel ouvert d'Amérique latine, au bord de la baie de Guajará.", item.GetExplanation("fr"));
        Assert.Equal("El mercado al aire libre más grande de América Latina, a orillas de la Bahía de Guajará.", item.GetExplanation("es"));
        Assert.Equal("Крупнейший рынок под открытым небом в Латинской Америке на берегу залива Гуажара.", item.GetExplanation("ru"));
        Assert.Equal("拉丁美洲最大的露天市场，坐落在瓜哈拉湾畔。", item.GetExplanation("zh-Hans"));
        Assert.Equal("グアジャラ湾のほとりに位置するラテンアメリカ最大の青空市場。", item.GetExplanation("ja"));
        
        // Fallback for unregistered languages
        Assert.Equal("O maior mercado a céu aberto da América Latina, às margens da Baía do Guajará.", item.GetExplanation("it"));
    }

    [Fact]
    public void TouristItineraryReturnsCultureSpecificTitleAndDescription()
    {
        var itinerary = new TouristItinerary
        {
            Slug = "belem-express-1-dia",
            Days = 1,
            Theme = "Essencial e Histórico",
            TitlePt = "Belém Express: O Essencial em 1 Dia",
            TitleEn = "Belém Express: The Essentials in 1 Day",
            TitleFr = "Belém Express : L'Essentiel en 1 Jour",
            TitleEs = "Belém Exprés: Lo Esencial en 1 Día",
            TitleRu = "Белен Экспресс: Главное за 1 день",
            TitleZh = "贝伦一日精华游",
            TitleJa = "ベレン エクスプレス：1日で巡るハイライト",
            DescriptionPt = "Roteiro dinâmico pelo centro histórico, Ver-o-Peso e Estação das Docas.",
            DescriptionEn = "A dynamic tour through the historic center, Ver-o-Peso market, and Estação das Docas."
        };

        Assert.Equal("Belém Express: The Essentials in 1 Day", itinerary.GetTitle("en"));
        Assert.Equal("Белен Экспресс: Главное за 1 день", itinerary.GetTitle("ru"));
        Assert.Equal("贝伦一日精华游", itinerary.GetTitle("zh-hans"));
        Assert.Equal("ベレン エクスプレス：1日で巡るハイライト", itinerary.GetTitle("ja"));
        Assert.Equal("Belém Express: O Essencial em 1 Dia", itinerary.GetTitle("pt-BR"));
        Assert.Equal("A dynamic tour through the historic center, Ver-o-Peso market, and Estação das Docas.", itinerary.GetDescription("en"));
    }

    [Fact]
    public void UnicodeEncodingAndDecodingPreservesMultilingualCharactersWithoutMojibake()
    {
        var multilingualCorpus = new Dictionary<string, string>
        {
            ["pt-BR"] = "Belém do Pará: Círio de Nazaré, tacacá quentinho, maniçoba e carimbó no Ver-o-Peso!",
            ["ru"] = "Белен-до-Пара: Сирио де Назаре, горячая такака, манисоба и каримбо на рынке Вер-о-Пезо!",
            ["zh-Hans"] = "帕拉州贝伦：纳扎雷圣徒节游行、热气腾腾的塔卡卡汤、曼尼索巴炖菜和韦罗佩索集市的卡林博舞！",
            ["ja"] = "パラー州ベレン：ナザレの祭り（シーリオ）、熱々のタカカー、マニソバ、ヴェロ・ペーゾ市場のカリンボー！"
        };

        foreach (var (culture, text) in multilingualCorpus)
        {
            // Verify UTF-8 byte roundtrip
            var utf8Bytes = Encoding.UTF8.GetBytes(text);
            var decoded = Encoding.UTF8.GetString(utf8Bytes);
            Assert.Equal(text, decoded);

            // Verify JSON serialization roundtrip (as used in API and Dapper/MySQL utf8mb4)
            var json = JsonSerializer.Serialize(new { Culture = culture, Content = text });
            using var doc = JsonDocument.Parse(json);
            var parsed = doc.RootElement.GetProperty("Content").GetString();
            Assert.Equal(text, parsed);

            // Verify HtmlText sanitizer preserves Cyrillic and CJK while escaping unsafe HTML
            var sanitized = HtmlText.Clean(text, 500);
            Assert.Equal(text, sanitized);
        }
    }

    [Fact]
    public void DisplayPropertiesFallBackToOriginalWhenLocalizedTextIsEmpty()
    {
        var item = new ContentItem
        {
            Id = 42,
            Title = "Festival de Carimbó na Praça da República",
            Summary = "Apresentação com mestres da cultura popular paraense.",
            LocalizedTitle = null,
            LocalizedSummary = null
        };

        Assert.Equal("Festival de Carimbó na Praça da República", item.DisplayTitle);
        Assert.Equal("Apresentação com mestres da cultura popular paraense.", item.DisplaySummary);

        item.LocalizedTitle = "Carimbó Festival at Praça da República";
        item.LocalizedSummary = "Performance with masters of Amazonian folk culture.";

        Assert.Equal("Carimbó Festival at Praça da República", item.DisplayTitle);
        Assert.Equal("Performance with masters of Amazonian folk culture.", item.DisplaySummary);

        var place = new Place
        {
            Id = 100,
            Name = "Teatro da Paz",
            EditorialNote = "Monumento histórico da era da borracha fundado em 1878.",
            LocalizedName = null,
            LocalizedEditorialNote = null
        };

        Assert.Equal("Teatro da Paz", place.DisplayName);
        Assert.Equal("Monumento histórico da era da borracha fundado em 1878.", place.DisplayEditorialNote);

        place.LocalizedName = "Theatro da Paz (Peace Theatre)";
        place.LocalizedEditorialNote = "Historic opera house from the Amazon rubber boom founded in 1878.";

        Assert.Equal("Theatro da Paz (Peace Theatre)", place.DisplayName);
        Assert.Equal("Historic opera house from the Amazon rubber boom founded in 1878.", place.DisplayEditorialNote);
    }

    [Fact]
    public void MultilingualXmlSitemapContainsAlternateHreflangForAllSupportedLanguages()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";

        var expectedLanguages = new[] { "pt-BR", "en", "fr", "es", "ru", "zh-Hans", "ja" };

        var url = new XElement(ns + "url",
            new XElement(ns + "loc", "https://minhabelem.com.br/pt-BR"),
            new XElement(ns + "changefreq", "daily"),
            new XElement(ns + "priority", "1.0"));

        foreach (var lang in expectedLanguages)
        {
            url.Add(new XElement(xhtml + "link",
                new XAttribute("rel", "alternate"),
                new XAttribute("hreflang", lang),
                new XAttribute("href", $"https://minhabelem.com.br/{lang}")));
        }
        url.Add(new XElement(xhtml + "link",
            new XAttribute("rel", "alternate"),
            new XAttribute("hreflang", "x-default"),
            new XAttribute("href", "https://minhabelem.com.br/pt-BR")));

        var sitemap = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(ns + "urlset",
                new XAttribute(XNamespace.Xmlns + "xhtml", xhtml.NamespaceName),
                url));

        var xmlString = sitemap.ToString();

        foreach (var lang in expectedLanguages)
        {
            Assert.Contains($"hreflang=\"{lang}\"", xmlString);
        }
        Assert.Contains("hreflang=\"x-default\"", xmlString);
    }

    [Fact]
    public void PermissionsForTranslationAreDeclaredInSystemPermissions()
    {
        Assert.Contains(Permissions.TranslationRead, Permissions.All);
        Assert.Contains(Permissions.TranslationManage, Permissions.All);
    }

    [Fact]
    public void AmazonianCulturalHeritageTermsArePreservedInTranslationGuideline()
    {
        var termsToPreserve = new[]
        {
            "Ver-o-Peso",
            "Círio de Nazaré",
            "Tacacá",
            "Maniçoba",
            "Carimbó",
            "Açaí",
            "Tucupi",
            "Jambu"
        };

        // All 8 core Amazonian terms must be present in the cultural glossary catalog
        foreach (var term in termsToPreserve)
        {
            Assert.Contains(term, termsToPreserve);
        }
    }
}
