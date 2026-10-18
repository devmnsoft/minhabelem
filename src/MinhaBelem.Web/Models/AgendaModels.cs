using MinhaBelem.Core.Entities;
namespace MinhaBelem.Web.Models;
public sealed record AgendaPage(string Title, string Description, IReadOnlyList<ContentItem> Items, int Days)
{
    public DateOnly? SelectedDate { get; init; }
    public DateOnly? Month { get; init; }
    public IReadOnlyList<ContentItem> CalendarItems { get; init; } = [];
    public static bool DateOnlyEvent(ContentItem item) => item.EventDetails?.Status == "DateOnly";
    public static bool OccursOn(ContentItem item, DateOnly day) => item.EventStartAt is {} start
        && DateOnly.FromDateTime(PortalTime.Local(start)) <= day
        && DateOnly.FromDateTime(PortalTime.Local(item.EventEndAt ?? start)) >= day;
}
public static class PortalTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Belem");
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public static DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
}
