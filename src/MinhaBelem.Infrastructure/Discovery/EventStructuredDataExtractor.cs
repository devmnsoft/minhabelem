using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed partial class EventStructuredDataExtractor : IEventStructuredDataExtractor
{
    private static readonly TimeSpan BelemOffset = TimeSpan.FromHours(-3); // Belém UTC-3

    [GeneratedRegex(@"BEGIN:VEVENT[\s\S]*?END:VEVENT", RegexOptions.IgnoreCase)]
    private static partial Regex IcalEventRegex();

    [GeneratedRegex(@"(?:(?:data|quando|data e hor[aá]rio|dia)\s*[:\-]\s*)?(\d{1,2})\s+(?:de\s+)?(janeiro|fevereiro|março|marco|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro)\s+(?:de\s+)?(\d{4})(?:\s+(?:[àa]s|as|hor[aá]rio[:\s]*)\s*(\d{1,2})(?:[h:](\d{2})?)?)?", RegexOptions.IgnoreCase)]
    private static partial Regex PortugueseDateRegex();

    [GeneratedRegex(@"(?:(?:data|quando)\s*[:\-]\s*)?(\d{1,2})/(\d{1,2})/(\d{4})(?:\s+(?:[àa]s|as|hor[aá]rio[:\s]*)\s*(\d{1,2})(?:[h:](\d{2})?)?)?", RegexOptions.IgnoreCase)]
    private static partial Regex NumericDateRegex();

    [GeneratedRegex(@"(?:local|endereço|localizaç[ãa]o|onde)\s*[:\-]\s*([^\n\r\.<]+)", RegexOptions.IgnoreCase)]
    private static partial Regex LocationRegex();

    [GeneratedRegex(@"(?:preço|ingresso|valor|entrada)\s*[:\-]\s*([^\n\r\.<]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PriceRegex();

    [GeneratedRegex(@"(?:classificaç[ãa]o(?:\s+et[aá]ria)?|censura)\s*[:\-]\s*([^\n\r\.<]+)", RegexOptions.IgnoreCase)]
    private static partial Regex AgeRatingRegex();

    public IReadOnlyList<DiscoveredItem> ExtractEvents(string html, string url)
    {
        var results = new List<DiscoveredItem>();

        // 1. JSON-LD extraction
        var jsonLdEvents = StructuredEvents.ParseJsonLd(html, url);
        results.AddRange(jsonLdEvents);

        // 2. iCalendar extraction if embedded
        foreach (Match match in IcalEventRegex().Matches(html))
        {
            var icalText = match.Value;
            var summary = ExtractIcalProperty(icalText, "SUMMARY");
            var dtStart = ParseIcalDate(ExtractIcalProperty(icalText, "DTSTART"));
            var dtEnd = ParseIcalDate(ExtractIcalProperty(icalText, "DTEND"));
            var location = ExtractIcalProperty(icalText, "LOCATION");
            var description = ExtractIcalProperty(icalText, "DESCRIPTION");

            if (!string.IsNullOrWhiteSpace(summary))
            {
                var evidence = new EventEvidence(
                    StartUtc: dtStart,
                    EndUtc: dtEnd,
                    Location: HtmlText.Clean(location, 300),
                    Organizer: null,
                    Status: "Confirmed",
                    Price: null,
                    IsFree: false,
                    AgeRating: null,
                    Participants: null,
                    EvidenceUrl: url,
                    Uncertain: dtStart is null
                );

                results.Add(new DiscoveredItem(
                    Title: HtmlText.Clean(summary, 300),
                    Summary: HtmlText.ToPlainText(description, 1000),
                    Url: url,
                    Author: null,
                    PostedAt: null,
                    Event: evidence
                ));
            }
        }

        // 3. Fallback to Microdata / HTML heuristic if no structured data found but page talks about an event
        if (results.Count == 0)
        {
            var heuristicEvidence = ExtractEventFromText(html, url);
            if (heuristicEvidence is not null && heuristicEvidence.StartUtc is not null)
            {
                var titleMatch = Regex.Match(html, @"<title\b[^>]*>([\s\S]*?)</title>", RegexOptions.IgnoreCase);
                var title = titleMatch.Success ? HtmlText.Clean(titleMatch.Groups[1].Value, 300) : "Evento em Belém";
                results.Add(new DiscoveredItem(
                    Title: title,
                    Summary: HtmlText.ToPlainText(html, 1000),
                    Url: url,
                    Author: null,
                    PostedAt: null,
                    Event: heuristicEvidence
                ));
            }
        }

        return results;
    }

    public EventEvidence? ExtractEventFromText(string text, string url)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        DateTime? startUtc = null;
        var hasTime = false;

        var portMatch = PortugueseDateRegex().Match(text);
        if (portMatch.Success)
        {
            var day = int.Parse(portMatch.Groups[1].Value);
            var monthName = portMatch.Groups[2].Value.ToLowerInvariant();
            var month = MonthNameToNumber(monthName);
            var year = int.Parse(portMatch.Groups[3].Value);
            hasTime = portMatch.Groups[4].Success;
            var hour = hasTime ? int.Parse(portMatch.Groups[4].Value) : 0;
            var minute = portMatch.Groups[5].Success && !string.IsNullOrWhiteSpace(portMatch.Groups[5].Value) ? int.Parse(portMatch.Groups[5].Value) : 0;

            if (month > 0)
            {
                try
                {
                    var localDate = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
                    var offset = new DateTimeOffset(localDate, BelemOffset);
                    startUtc = offset.UtcDateTime;
                }
                catch (ArgumentOutOfRangeException) { /* data inválida */ }
            }
        }
        else
        {
            var numMatch = NumericDateRegex().Match(text);
            if (numMatch.Success)
            {
                var day = int.Parse(numMatch.Groups[1].Value);
                var month = int.Parse(numMatch.Groups[2].Value);
                var year = int.Parse(numMatch.Groups[3].Value);
                hasTime = numMatch.Groups[4].Success;
                var hour = hasTime ? int.Parse(numMatch.Groups[4].Value) : 0;
                var minute = numMatch.Groups[5].Success && !string.IsNullOrWhiteSpace(numMatch.Groups[5].Value) ? int.Parse(numMatch.Groups[5].Value) : 0;

                try
                {
                    var localDate = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
                    var offset = new DateTimeOffset(localDate, BelemOffset);
                    startUtc = offset.UtcDateTime;
                }
                catch (ArgumentOutOfRangeException) { /* data inválida */ }
            }
        }

        var locationMatch = LocationRegex().Match(text);
        var location = locationMatch.Success ? HtmlText.Clean(locationMatch.Groups[1].Value, 250) : null;

        var priceMatch = PriceRegex().Match(text);
        var priceRaw = priceMatch.Success ? HtmlText.Clean(priceMatch.Groups[1].Value, 100) : null;
        var isFree = priceRaw is not null && (
            priceRaw.Contains("gratuit", StringComparison.OrdinalIgnoreCase) ||
            priceRaw.Contains("grátis", StringComparison.OrdinalIgnoreCase) ||
            priceRaw.Contains("gratis", StringComparison.OrdinalIgnoreCase) ||
            priceRaw.Contains("franca", StringComparison.OrdinalIgnoreCase) ||
            priceRaw.Contains("livre", StringComparison.OrdinalIgnoreCase) ||
            priceRaw.Trim() == "0");

        var ageRatingMatch = AgeRatingRegex().Match(text);
        var ageRating = ageRatingMatch.Success ? HtmlText.Clean(ageRatingMatch.Groups[1].Value, 50) : null;

        if (startUtc is null && location is null && priceRaw is null)
            return null;

        return new EventEvidence(
            StartUtc: startUtc,
            EndUtc: null,
            Location: location,
            Organizer: null,
            Status: startUtc is not null && !hasTime ? "DateOnly" : "Confirmed",
            Price: priceRaw,
            IsFree: isFree,
            AgeRating: ageRating,
            Participants: null,
            EvidenceUrl: url,
            Uncertain: startUtc is null || !hasTime
        );
    }

    private static int MonthNameToNumber(string name) => name switch
    {
        "janeiro" => 1,
        "fevereiro" => 2,
        "março" or "marco" => 3,
        "abril" => 4,
        "maio" => 5,
        "junho" => 6,
        "julho" => 7,
        "agosto" => 8,
        "setembro" => 9,
        "outubro" => 10,
        "novembro" => 11,
        "dezembro" => 12,
        _ => 0
    };

    private static string? ExtractIcalProperty(string ical, string prop)
    {
        var match = Regex.Match(ical, $@"^{prop}(?:;[^:]*)?:(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static DateTime? ParseIcalDate(string? icalDate)
    {
        if (string.IsNullOrWhiteSpace(icalDate)) return null;
        var cleaned = icalDate.Trim().TrimEnd('Z');
        if (DateTime.TryParseExact(cleaned, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return icalDate.EndsWith('Z') ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : new DateTimeOffset(dt, BelemOffset).UtcDateTime;
        }
        if (DateTime.TryParseExact(cleaned, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtDate))
        {
            return new DateTimeOffset(dtDate, BelemOffset).UtcDateTime;
        }
        return null;
    }
}
