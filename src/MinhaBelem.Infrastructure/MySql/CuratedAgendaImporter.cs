using System.Globalization;
using System.Text.Json;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.MySql;

public sealed record CuratedAgenda(DateOnly FromDate, DateOnly ThroughDate, DateTimeOffset VerifiedAtUtc, IReadOnlyList<CuratedEvent> Events);
public sealed record CuratedEvent(string Title, DateOnly Date, string? Time, DateTimeOffset? End, string Subject, ContentCategory Category,
    string Venue, string Address, string? Neighborhood, string SourceName, string SourceUrl, string Summary, string? Price, string? Age, DateOnly? LastDate, bool IsFree=false);
public sealed record AgendaImportResult(int Inserted, int Existing, IReadOnlyList<long> ContentIds);

/// <summary>Explicit, reviewed factual import. No crawler activation or updates to existing editorial work.</summary>
public sealed class CuratedAgendaImporter(IContentRepository content)
{
    public static CuratedAgenda Parse(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var data = JsonSerializer.Deserialize<CuratedAgenda>(json, options) ?? throw new ArgumentException("Agenda inválida.");
        if (data.Events is not { Count: > 0 and <= 1000 } || data.ThroughDate < data.FromDate || data.ThroughDate > data.FromDate.AddDays(365)
            || data.VerifiedAtUtc > DateTimeOffset.UtcNow.AddMinutes(5) || data.VerifiedAtUtc < DateTimeOffset.UtcNow.AddDays(-7)) throw new ArgumentException("Período ou verificação inválidos.");
        foreach (var e in data.Events)
        {
            if (string.IsNullOrWhiteSpace(e.Title) || e.Title.Length > 250 || string.IsNullOrWhiteSpace(e.Summary) || e.Summary.Length > 1200
                || string.IsNullOrWhiteSpace(e.SourceName) || string.IsNullOrWhiteSpace(e.Venue) || string.IsNullOrWhiteSpace(e.Address)
                || !SafeUrl.Validate(e.SourceUrl).IsValid || e.Date < data.FromDate || e.Date > data.ThroughDate
                || e.LastDate is {} last && (last < e.Date || last > data.ThroughDate)
                || e.Category is not (ContentCategory.Cultura or ContentCategory.Familia or ContentCategory.Esporte or ContentCategory.Servicos or ContentCategory.Turismo)
                || !AgendaSubjects.Contains(e.Subject) || e.Time is {} time && !TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || e.End is {} end && end.UtcDateTime <= Start(e)) throw new ArgumentException("Evento sem evidência válida: " + e.Title);
        }
        return data;
    }
    public static readonly string[] AgendaSubjects = ["teatro", "danca", "musica", "humor", "infantil", "corrida", "congressos", "feiras", "cinema", "exposicoes", "religioso"];
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Belem");
    private static DateTime Start(CuratedEvent e) => TimeZoneInfo.ConvertTimeToUtc(e.Date.ToDateTime(e.Time is {} time ? TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture) : TimeOnly.MinValue), Zone);
    public async Task<AgendaImportResult> ImportAsync(CuratedAgenda data, CancellationToken ct, bool stageForReview=false)
    {
        // Validate all entries before the first write, including callers that constructed the record directly.
        var options = new JsonSerializerOptions(); options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        Parse(JsonSerializer.Serialize(data, options));
        var inserted = 0; var existing = 0; var ids = new List<long>();
        foreach (var e in data.Events)
        {
            var start = Start(e);
            // The same source can announce several sessions. Keep its real URL and key each occurrence independently.
            var occurrence = Hashing.NormalizeUrl(e.SourceUrl) + "|" + start.ToString("O");
            var slug = "agenda-" + Convert.ToHexString(Hashing.Sha256(occurrence)).ToLowerInvariant()[..32];
            if (await content.GetBySlugAsync(slug, ct) is {} found) { existing++; ids.Add(found.Id); continue; }
            var lastDay = e.LastDate ?? e.Date;
            var expires = TimeZoneInfo.ConvertTimeToUtc(lastDay.AddDays(1).ToDateTime(TimeOnly.MinValue), Zone);
            var end = e.End?.UtcDateTime ?? (e.Time is null ? expires.AddTicks(-1) : (DateTime?)null);
            var illustration = e.Subject switch { "corrida" => "sport", "musica" or "humor" => "music", "infantil" => "family", "congressos" or "feiras" => "city", _ => "stage" };
            var item = new ContentItem
            {
                Kind = ContentKind.Event, Category = e.Category, Subject = e.Subject, Title = e.Title, Slug = slug,
                Summary = e.Summary, Body = e.Summary, Address = e.Address, Neighborhood = e.Neighborhood, Municipality = "Belém",
                SourceName = e.SourceName, SourceUrl = e.SourceUrl, SourceUrlHash = Hashing.Sha256("curated-session|" + occurrence),
                AuthorName = "Curadoria MinhaBelém", EventStartAt = start, EventEndAt = end,
                TicketPriceText = e.Price, IsFree = e.IsFree, Status = stageForReview?ContentStatus.InReview:ContentStatus.Published, VerificationStatus = VerificationStatus.Verified,
                RequiresHumanApproval = stageForReview, LastVerifiedAt = data.VerifiedAtUtc.UtcDateTime, ExpiresAt = expires,
                ImageUrl = "/images/agenda/" + illustration + ".svg", ImageCredit = "Ilustração autoral MinhaBelém; não é foto do evento", ImageLicense = "Arte vetorial autoral do projeto",
                EventDetails = new(start, end, e.Venue, e.SourceName, e.Time is null ? "DateOnly" : "Scheduled", e.Price, e.IsFree, e.Age, null, e.SourceUrl),
                Audiences = e.Subject == "infantil" ? [Audience.Infantil] : []
            };
            ids.Add(await content.InsertAsync(item, null, "Cadastro factual pontual solicitado pelo usuário em 18/10/2026; evidência na fonte indicada; sem autorização de coleta contínua.", ct));
            inserted++;
        }
        return new(inserted, existing, ids);
    }
}
