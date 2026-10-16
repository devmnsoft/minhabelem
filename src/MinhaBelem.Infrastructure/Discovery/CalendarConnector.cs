using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;
namespace MinhaBelem.Infrastructure.Discovery;
public sealed class CalendarConnector(ConnectorHttp http) : ICalendarConnector
{
    public bool Supports(FeedProtocol protocol) => protocol == FeedProtocol.ICalendar;
    public string? ConfigurationIssue(SourceContext context) => null;
    public async Task<ConnectorResult> ReadAsync(SourceContext context, CancellationToken ct)
    {
        var read = await http.ReadAsync(context, ct);
        return new(read.Body is null ? Array.Empty<DiscoveredItem>() : Parse(read.Body, context.Source.FeedUrl, context.NowUtc, context.Profile.MaxItems), read.ETag, read.Modified);
    }
    public static IReadOnlyList<DiscoveredItem> Parse(string text, string source, DateTime now, int limit)
    {
        var calendar = Calendar.Load(text) ?? throw new InvalidOperationException("Calendário inválido.");
        var result = new List<DiscoveredItem>();
        foreach (var occurrence in calendar.GetOccurrences(new CalDateTime(now.AddDays(-1), "UTC")).TakeWhile(x => x.Period.StartTime.AsUtc < now.AddYears(1)).Take(Math.Clamp(limit, 1, 200)))
        {
            if (occurrence.Source is not CalendarEvent item) continue;
            var floating = occurrence.Period.StartTime.IsFloating || !occurrence.Period.StartTime.HasTime;
            var start = floating ? (DateTime?)null : occurrence.Period.StartTime.AsUtc;
            var url = item.Url?.AbsoluteUri ?? source;
            var key = (item.Uid ?? "evento") + (item.RecurrenceRule is null ? "" : ":" + occurrence.Period.StartTime.Value.ToString("yyyyMMddTHHmmss", System.Globalization.CultureInfo.InvariantCulture));
            result.Add(new(HtmlText.Clean(item.Summary, 300), HtmlText.ToPlainText(item.Description, 1500), url, null, item.DtStamp?.AsUtc,
                new EventEvidence(start, floating ? null : occurrence.Period.EndTime?.AsUtc, HtmlText.Clean(item.Location, 500), null, item.Status, null, false, null, null, url, floating), key));
        }
        return result;
    }
}
