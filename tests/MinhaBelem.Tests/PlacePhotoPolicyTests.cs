using MinhaBelem.Core.Services;

namespace MinhaBelem.Tests;

public class PlacePhotoPolicyTests
{
    [Theory]
    [InlineData("CC BY-SA 4.0", true)]
    [InlineData("CC BY 4.0", true)]
    [InlineData("CC0", true)]
    [InlineData("Public domain", true)]
    [InlineData("GFDL", true)]
    [InlineData("CC BY-NC-SA 4.0", false)]
    [InlineData("CC BY-ND 4.0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OpenLicensesCanBeShownAndNoncommercialOnesCannot(string? license, bool publishable) =>
        Assert.Equal(publishable, PhotoLicense.IsPublishable(license));

    [Fact]
    public void MetroBoxKeepsBelemAndDropsTheLisbonParish()
    {
        Assert.True(BelemMetro.Contains(-1.4522, -48.5034));
        Assert.True(BelemMetro.Contains(-1.1245, -48.472));
        Assert.False(BelemMetro.Contains(38.6916, -9.2159));
    }

    [Fact]
    public void CommonsParserKeepsAnOpenLicenseInsideTheMetroAndDropsTheRest()
    {
        const string json = """
        {"query":{"pages":{
          "1":{"title":"File:Ver-o-Peso.jpg","imageinfo":[{"url":"https://upload.wikimedia.org/wikipedia/commons/a/a.jpg","thumburl":"https://upload.wikimedia.org/wikipedia/commons/thumb/a/a.jpg","descriptionurl":"https://commons.wikimedia.org/wiki/File:Ver-o-Peso.jpg","mime":"image/jpeg","thumbwidth":960,"thumbheight":640,"extmetadata":{"LicenseShortName":{"value":"CC BY-SA 4.0"},"LicenseUrl":{"value":"https://creativecommons.org/licenses/by-sa/4.0"},"Artist":{"value":"<a href=\"https://commons.wikimedia.org\">Ana</a>"},"GPSLatitude":{"value":"1.4522"},"GPSLongitude":{"value":"48.5034"},"GPSLatitudeRef":{"value":"S"},"GPSLongitudeRef":{"value":"W"}}}]},
          "2":{"title":"File:Lisboa.jpg","imageinfo":[{"url":"https://upload.wikimedia.org/wikipedia/commons/b/b.jpg","descriptionurl":"https://commons.wikimedia.org/wiki/File:Lisboa.jpg","mime":"image/jpeg","extmetadata":{"LicenseShortName":{"value":"CC BY 4.0"},"Artist":{"value":"Autor"},"GPSLatitude":{"value":"38.69"},"GPSLongitude":{"value":"-9.21"}}}]},
          "3":{"title":"File:Fechada.jpg","imageinfo":[{"url":"https://upload.wikimedia.org/wikipedia/commons/c/c.jpg","descriptionurl":"https://commons.wikimedia.org/wiki/File:Fechada.jpg","mime":"image/jpeg","extmetadata":{"LicenseShortName":{"value":"CC BY-NC 4.0"},"Artist":{"value":"Autor"},"GPSLatitude":{"value":"-1.45"},"GPSLongitude":{"value":"-48.50"}}}]},
          "4":{"title":"File:Svg.svg","imageinfo":[{"url":"https://upload.wikimedia.org/wikipedia/commons/d/d.svg","descriptionurl":"https://commons.wikimedia.org/wiki/File:Svg.svg","mime":"image/svg+xml","extmetadata":{"LicenseShortName":{"value":"CC0"},"GPSLatitude":{"value":"-1.45"},"GPSLongitude":{"value":"-48.50"}}}]}
        }}}
        """;
        var photos = CommonsPhotoParser.Parse(json, "ver-o-peso", "Ver-o-Peso", "Belém", trustGeosearch: false);
        var photo = Assert.Single(photos);
        Assert.Equal("Ana", photo.Credit);
        Assert.Equal("CC BY-SA 4.0", photo.LicenseName);
        Assert.True(photo.Publishable);
        Assert.StartsWith("https://upload.wikimedia.org/", photo.ImageUrl);
    }

    [Fact]
    public void GeosearchWithoutCoordinatesIsAcceptedOnlyWhenTheQueryWasLocal()
    {
        const string json = """
        {"query":{"pages":{"9":{"title":"File:Estacao das Docas.png","imageinfo":[{"url":"https://upload.wikimedia.org/wikipedia/commons/e/e.jpg","thumburl":"https://upload.wikimedia.org/wikipedia/commons/thumb/e/e.jpg","descriptionurl":"https://commons.wikimedia.org/wiki/File:Estacao_das_Docas.png","mime":"image/png","extmetadata":{"LicenseShortName":{"value":"CC BY 2.0"},"Artist":{"value":"Foto"}}}]}}}}
        """;
        Assert.Empty(CommonsPhotoParser.Parse(json, "docas", "Estação das Docas", "Belém", trustGeosearch: false));
        Assert.Single(CommonsPhotoParser.Parse(json, "docas", "Estação das Docas", "Belém", trustGeosearch: true));
    }
}
