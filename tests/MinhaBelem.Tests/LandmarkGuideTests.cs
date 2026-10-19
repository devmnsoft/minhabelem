using System.Text.RegularExpressions;
using MinhaBelem.Core.Services;

namespace MinhaBelem.Tests;

public class LandmarkGuideTests
{
    [Theory]
    [InlineData("mb-landmark:ver-o-peso", true)]
    [InlineData("mb-landmark:", true)]
    [InlineData("ChIJreal", false)]
    [InlineData("mb-landmark", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OwnIdsStayInsideThePrefix(string? id, bool own) =>
        Assert.Equal(own, LandmarkGuide.IsOwn(id));

    [Fact]
    public void MapLinkUsesCoordinatesAndDoesNotSendASyntheticPlaceId()
    {
        var link = LandmarkGuide.MapLink(-1.4522, -48.5034);
        Assert.Equal("https://www.google.com/maps/search/?api=1&query=-1.4522,-48.5034", link);
        Assert.DoesNotContain("query_place_id", link, StringComparison.Ordinal);
    }

    [Fact]
    public void NoteDoesNotStateHoursOrAPrice()
    {
        Assert.Contains("fonte verificada", LandmarkGuide.Note, StringComparison.Ordinal);
        Assert.DoesNotContain("R$", LandmarkGuide.Note, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\b\d{1,2}:\d{2}\b"), LandmarkGuide.Note);
        Assert.DoesNotMatch(new Regex(@"\b\d+\s*(reais|real)\b", RegexOptions.IgnoreCase), LandmarkGuide.Note);
    }
}
