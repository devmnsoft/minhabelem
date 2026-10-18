using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

// Read-only local audit. Never prints connection strings, credentials, URLs or user content.
var config = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
var connection = config.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connection)) { Console.WriteLine("Conexão não configurada em User Secrets/ambiente."); return 2; }
try
{
    var builder = new MySqlConnectionStringBuilder(connection);
    if (builder.Server is not ("127.0.0.1" or "localhost")) { Console.WriteLine("Auditoria limitada ao banco local."); return 2; }
    builder.ConnectionTimeout = 5;
    await using var db = new MySqlConnection(builder.ConnectionString);
    await db.OpenAsync();
    Console.WriteLine("Conexão local: OK. Versão: " + await db.ExecuteScalarAsync<string>("SELECT VERSION()"));
    Console.WriteLine("Tabelas: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE()"));
    Console.WriteLine("Matérias: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_content_items"));
    Console.WriteLine("Fontes operacionais: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_ingestion_sources WHERE enabled=1 AND approved=1"));
    var queuePresent = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='collection_requests'");
    Console.WriteLine("Migration Sprint 14: " + (queuePresent == 1 ? "presente" : "pendente"));
    var cycles = await db.ExecuteScalarAsync<int?>("SELECT total_cycles_executed FROM mb_radar_heartbeats WHERE worker_name='RadarWorker'");
    Console.WriteLine("Ciclos registrados pelo Radar: " + (cycles?.ToString() ?? "sem heartbeat"));
    Console.WriteLine("Histórico de páginas consultadas: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_radar_crawl_history"));
    Console.WriteLine("Linhas cadastradas: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_transit_lines"));
    Console.WriteLine("Paradas cadastradas: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_transit_stops"));
    Console.WriteLine("Travessias cadastradas: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_river_routes"));
    Console.WriteLine("Contas de homologação persistidas: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_users WHERE email IN ('superadmin@minhabelem.com.br','editor.teste@minhabelem.com.br','moderador.teste@minhabelem.com.br')"));
    Console.WriteLine("Auditoria somente leitura concluída.");
    return 0;
}
catch (Exception ex) { Console.WriteLine("Auditoria indisponível: " + ex.GetType().Name); return 1; }
