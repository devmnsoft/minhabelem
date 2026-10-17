using MinhaBelem.Core.Entities;
namespace MinhaBelem.Web.Models;
public sealed record AgendaPage(string Title, string Description, IReadOnlyList<ContentItem> Items, int Days);
public static class PortalTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Belem");
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public static DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
}
