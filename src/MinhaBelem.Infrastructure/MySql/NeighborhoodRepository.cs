using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class NeighborhoodRepository(MySqlDataSource dataSource) : INeighborhoodRepository
{
    public async Task<IReadOnlyList<NeighborhoodGuide>> GetNeighborhoodsAsync(CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = @"SELECT id, name, slug, zone, history_summary AS HistorySummary, cultural_characteristics AS CulturalCharacteristics, safety_tips AS SafetyTips, popular_attractions AS PopularAttractions, display_order AS DisplayOrder, created_at AS CreatedAt 
                    FROM mb_neighborhood_guides 
                    ORDER BY display_order ASC, name ASC";
        var rows = await db.QueryAsync<NeighborhoodGuide>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<NeighborhoodGuide?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = @"SELECT id, name, slug, zone, history_summary AS HistorySummary, cultural_characteristics AS CulturalCharacteristics, safety_tips AS SafetyTips, popular_attractions AS PopularAttractions, display_order AS DisplayOrder, created_at AS CreatedAt 
                    FROM mb_neighborhood_guides 
                    WHERE slug = @slug LIMIT 1";
        return await db.QuerySingleOrDefaultAsync<NeighborhoodGuide>(new CommandDefinition(sql, new { slug }, cancellationToken: ct));
    }

    public async Task<long> UpsertNeighborhoodAsync(NeighborhoodGuide guide, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = @"INSERT INTO mb_neighborhood_guides (name, slug, zone, history_summary, cultural_characteristics, safety_tips, popular_attractions, display_order)
                    VALUES (@Name, @Slug, @Zone, @HistorySummary, @CulturalCharacteristics, @SafetyTips, @PopularAttractions, @DisplayOrder)
                    ON DUPLICATE KEY UPDATE 
                        name = VALUES(name),
                        zone = VALUES(zone),
                        history_summary = VALUES(history_summary),
                        cultural_characteristics = VALUES(cultural_characteristics),
                        safety_tips = VALUES(safety_tips),
                        popular_attractions = VALUES(popular_attractions),
                        display_order = VALUES(display_order);
                    SELECT id FROM mb_neighborhood_guides WHERE slug = @Slug;";
        return await db.ExecuteScalarAsync<long>(new CommandDefinition(sql, guide, cancellationToken: ct));
    }
}
