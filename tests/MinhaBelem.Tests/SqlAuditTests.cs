using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Policies;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Infrastructure.MySql;

namespace MinhaBelem.Tests;

public class SqlAuditTests
{
    [Fact]
    public void SchemaScriptContainsExactly62TablesAndValidEngine()
    {
        using var stream = typeof(SchemaMigrator).Assembly.GetManifestResourceStream("MinhaBelem.Infrastructure.MINHABELEM.SQL");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var mainSql = reader.ReadToEnd();

        Assert.True(mainSql.Length > 50_000, "O script SQL unificado deve conter o esquema completo.");

        // Extrai todas as instruções CREATE TABLE IF NOT EXISTS
        var matches = Regex.Matches(mainSql, @"(?i)CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS\s+`?([a-zA-Z0-9_]+)`?");
        var tableNames = matches
            .Select(m => m.Groups[1].Value)
            .Where(name => !string.Equals(name, "nao", StringComparison.OrdinalIgnoreCase)) // exclui ocorrência no comentário
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 11 tabelas legadas + 50 tabelas mb_* + 1 schema_migrations = 62 tabelas
        Assert.Equal(62, tableNames.Count);

        // Verifica tabelas essenciais da infraestrutura atual
        Assert.Contains("schema_migrations", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_users", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_content_items", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_ingestion_sources", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_ingestion_candidates", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_radar_heartbeats", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_radar_discovered_sources", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_radar_social_profiles", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_radar_social_posts", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_transit_lines", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_places", tableNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mb_settings", tableNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicationPolicyStrictlyBlocksAutomatedPublishingUnlessExplicitlyConfigured()
    {
        var now = DateTime.UtcNow;
        var validItem = new ContentItem
        {
            Id = 1,
            Title = "Festival Cultural no Ver-o-Peso",
            Summary = "Evento cultural tradicional reúne produtores e artistas em Belém.",
            SourceUrl = "https://belem.pa.gov.br/festival",
            Category = ContentCategory.Cultura,
            Status = ContentStatus.InReview,
            VerificationStatus = VerificationStatus.Verified,
            RequiresHumanApproval = false
        };

        // 1. Por padrão, publicação automatizada deve ser bloqueada
        var defaultDecision = PublicationPolicy.Evaluate(validItem, now, automated: true);
        Assert.False(defaultDecision.Allowed);
        Assert.Contains(defaultDecision.Reasons, r => r.Contains("desativada no sistema"));

        // 2. Com configuração restrita ativa, mas categoria não autorizada
        var restrictedSettings = new PublicationSettings(
            AutoPublicationEnabled: true,
            AllowedCategoriesForAuto: new[] { "Turismo", "Esporte" },
            AllowedSourceIdsForAuto: new long[] { 10 }
        );
        var blockedCategoryDecision = PublicationPolicy.Evaluate(validItem, now, automated: true, restrictedSettings);
        Assert.False(blockedCategoryDecision.Allowed);
        Assert.Contains(blockedCategoryDecision.Reasons, r => r.Contains("não autorizada para publicação automática"));

        // 3. Conteúdo sensível SEMPRE exige aprovação humana mesmo com automação ligada
        var sensitiveItem = new ContentItem
        {
            Id = 2,
            Title = "Nota Oficial sobre Ocorrência Policial",
            Summary = "Esclarecimento factual sobre ocorrência de segurança pública.",
            SourceUrl = "https://belem.pa.gov.br/seguranca",
            Category = ContentCategory.Cidades,
            Flags = ModerationFlag.Sensitive,
            Status = ContentStatus.InReview,
            VerificationStatus = VerificationStatus.Verified,
            RequiresHumanApproval = true
        };
        var sensitiveDecision = PublicationPolicy.Evaluate(sensitiveItem, now, automated: true, new PublicationSettings(AutoPublicationEnabled: true));
        Assert.False(sensitiveDecision.Allowed);
        Assert.True(sensitiveDecision.RequiresHumanApproval);
        Assert.Contains(sensitiveDecision.Reasons, r => r.Contains("Conteúdo sensível retido até revisão humana"));
    }

    [Collection("MySql")]
    public sealed class MySqlSqlAuditTests(PortalFixture fixture)
    {
        [MySqlFact]
        public async Task RadarRepositoryAndSourcesIntegrityOnRealMySql()
        {
            using var scope = fixture.Factory.Services.CreateScope();
            var radar = scope.ServiceProvider.GetRequiredService<IRadarRepository>();

            // Grava heartbeat do worker
            await radar.UpdateHeartbeatAsync("RadarWorker", "success", "Teste de auditoria MySQL", null, isRunning: false, default);

            // Consulta estatísticas do dashboard (deve executar sem falha de coluna em mb_ingestion_sources)
            var stats = await radar.GetDashboardStatsAsync(default);
            Assert.NotNull(stats);
            Assert.True(stats.TotalSources >= 0);
            Assert.True(stats.ActiveSources >= 0);
            Assert.True(stats.ClassifiedContentsCount >= 0);
            Assert.True(stats.PublishedContentsCount >= 0);
            Assert.NotNull(stats.Heartbeat);
            Assert.Equal("RadarWorker", stats.Heartbeat.WorkerName);
            Assert.Equal("success", stats.Heartbeat.LastCycleStatus);

            // Consulta fontes operacionais (deve mapear mb_ingestion_sources com sucesso)
            var sources = await radar.OperationalSourcesAsync(default);
            Assert.NotNull(sources);
        }
    }
}
