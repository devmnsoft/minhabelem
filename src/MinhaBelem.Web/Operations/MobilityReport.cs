using Dapper;
using MySqlConnector;

namespace MinhaBelem.Web.Operations;

/// <summary>
/// Relatório somente-leitura do estado da mobilidade urbana: linhas, terminais,
/// estações, itinerários e-verificação editorial das fontes. Usado em Development.
/// </summary>
public sealed class MobilityReport(MySqlDataSource data)
{
    public async Task<string> RunAsync(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("== MOBILIDADE URBANA ==");

        var totals = await db.QuerySingleAsync<(long Linhas, long Paradas, long Terminais, long Rotas, long Verificadas, long ComHorario)>(new CommandDefinition("""
            SELECT
              (SELECT COUNT(*) FROM mb_transit_lines) AS Linhas,
              (SELECT COUNT(*) FROM mb_transit_stops) AS Paradas,
              (SELECT COUNT(*) FROM mb_transit_stops WHERE stop_type IN ('Terminal','Estação BRT')) AS Terminais,
              (SELECT COUNT(*) FROM mb_transit_route_points) AS Rotas,
              (SELECT COUNT(*) FROM mb_transit_lines WHERE source_url IS NOT NULL AND verified_at IS NOT NULL AND valid_until > UTC_TIMESTAMP()) AS Verificadas,
              (SELECT COUNT(*) FROM mb_transit_lines WHERE first_departure IS NOT NULL AND last_departure IS NOT NULL) AS ComHorario
            """, cancellationToken: ct));
        sb.AppendLine($"linhas={totals.Linhas} paradas={totals.Paradas} terminais_estacoes={totals.Terminais} pontos_de_itinerario={totals.Rotas} linhas_com_fonte_verificada={totals.Verificadas} linhas_com_horario={totals.ComHorario}");

        var byType = await db.QueryAsync<(string TransitType, long N)>(new CommandDefinition(
            "SELECT transit_type AS TransitType, COUNT(*) AS N FROM mb_transit_lines GROUP BY transit_type ORDER BY N DESC", cancellationToken: ct));
        foreach (var row in byType) sb.AppendLine($"  tipo {row.TransitType}: {row.N}");

        var semRota = (await db.QueryAsync<(string Code, string Name)>(new CommandDefinition("""
            SELECT code AS Code, name AS Name FROM mb_transit_lines l
            WHERE NOT EXISTS (SELECT 1 FROM mb_transit_route_points p WHERE p.line_code = l.code)
            ORDER BY code
            """, cancellationToken: ct))).ToList();
        if (semRota.Count > 0)
        {
            sb.AppendLine($"linhas sem itinerario ({semRota.Count}): " + string.Join(", ", semRota.Select(x => x.Code)));
        }

        var terminais = (await db.QueryAsync<(string Name, string Lines)>(new CommandDefinition("""
            SELECT name AS Name, COALESCE(lines_serviced,'') AS `Lines` FROM mb_transit_stops
            WHERE stop_type IN ('Terminal','Estação BRT') ORDER BY name
            """, cancellationToken: ct))).ToList();
        sb.AppendLine($"terminais/estacoes: {terminais.Count}");
        foreach (var t in terminais.Take(40)) sb.AppendLine($"  - {t.Name} [{t.Lines}]");

        var fluvial = (await db.QueryAsync<(string Name, string Hours)>(new CommandDefinition(
            "SELECT name AS Name, COALESCE(operating_hours,'') AS Hours FROM mb_river_routes WHERE is_active=TRUE ORDER BY name", cancellationToken: ct))).ToList();
        sb.AppendLine($"travessias fluviais: {fluvial.Count}");
        foreach (var r in fluvial) sb.AppendLine($"  - {r.Name} | {r.Hours}");

        return sb.ToString();
    }
}
