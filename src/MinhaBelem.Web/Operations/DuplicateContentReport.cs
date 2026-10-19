using Dapper;
using MySqlConnector;

namespace MinhaBelem.Web.Operations;

/// <summary>
/// Relatório somente-leitura de conteúdo duplicado na agenda: agrupa eventos
/// publicados com o mesmo título normalizado e a mesma data de início.
/// Usado em Development.
/// </summary>
public sealed class DuplicateContentReport(MySqlDataSource data)
{
    public async Task<string> RunAsync(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("== CONTEUDO DUPLICADO (AGENDA) ==");

        var groups = (await db.QueryAsync<(string Chave, long Total, string Ids)>(new CommandDefinition("""
            SELECT
              CONCAT(LOWER(TRIM(title)), '|', COALESCE(DATE_FORMAT(event_start_at,'%Y-%m-%d'),'sem-data')) AS Chave,
              COUNT(*) AS Total,
              GROUP_CONCAT(id ORDER BY id SEPARATOR ',') AS Ids
            FROM mb_content_items
            WHERE status = 'Published' AND kind = 'Event'
            GROUP BY Chave
            HAVING COUNT(*) > 1
            ORDER BY Total DESC, Chave ASC
            LIMIT 100
            """, cancellationToken: ct))).ToList();

        sb.AppendLine($"grupos duplicados: {groups.Count}");
        foreach (var g in groups)
        {
            var parts = g.Chave.Split('|');
            var detail = await db.QueryAsync<(long Id, string Title, string Source, string Start)>(new CommandDefinition("""
                SELECT id AS Id, title AS Title, source_name AS Source,
                       COALESCE(DATE_FORMAT(event_start_at,'%Y-%m-%d %H:%i'),'sem-data') AS Start
                FROM mb_content_items
                WHERE id IN @Ids ORDER BY id
                """, new { Ids = g.Ids.Split(',').Select(long.Parse).ToArray() }, cancellationToken: ct));
            sb.AppendLine($"[{g.Total}x] {parts[0]} ({parts[1]}) ids={g.Ids}");
            foreach (var d in detail) sb.AppendLine($"     #{d.Id} [{d.Source}] {d.Start} — {d.Title}");
        }

        var totals = await db.QuerySingleAsync<(long Publicados, long Eventos, long Candidatos)>(new CommandDefinition("""
            SELECT
              (SELECT COUNT(*) FROM mb_content_items WHERE status='Published') AS Publicados,
              (SELECT COUNT(*) FROM mb_content_items WHERE status='Published' AND kind='Event') AS Eventos,
              (SELECT COUNT(*) FROM mb_ingestion_candidates) AS Candidatos
            """, cancellationToken: ct));
        sb.AppendLine($"publicados={totals.Publicados} eventos_publicados={totals.Eventos} candidatos={totals.Candidatos}");

        return sb.ToString();
    }
}
