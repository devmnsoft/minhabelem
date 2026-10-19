using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

// Default: read-only local audit. Mutations require an explicit local command.
// Never prints connection strings, credentials or user content.
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
    if (args.Contains("--editorial-audit"))
    {
        var snapshot=new
        {
            capturedAtUtc=DateTime.UtcNow,host=builder.Server,port=builder.Port,database=builder.Database,
            databaseReference=MinhaBelem.Infrastructure.Identity.AuthenticationDiagnostics.DatabaseReference(builder.ConnectionString),
            eventStatuses=await db.QueryAsync("SELECT status,COUNT(*) AS count FROM mb_content_items WHERE kind='Event' GROUP BY status"),
            recentEvents=await db.QueryAsync("SELECT id,title,status,revision,verification_status AS verificationStatus,requires_human_approval AS requiresHumanApproval,author_name AS authorName,source_id AS sourceId,source_name AS sourceName,source_url AS sourceUrl,event_start_at AS eventStartAt,event_end_at AS eventEndAt,is_free AS isFree,ticket_price AS ticketPrice,flags,image_url AS imageUrl,image_license AS imageLicense FROM mb_content_items WHERE kind='Event' ORDER BY id ASC"),
            batches=await db.QueryAsync("SELECT id,action,status,created_at AS createdAt FROM mb_editorial_batches ORDER BY created_at DESC LIMIT 20"),
            batchItemStatuses=await db.QueryAsync("SELECT status,COUNT(*) AS count FROM mb_editorial_batch_items GROUP BY status")
        };
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(snapshot,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        return 0;
    }
    if (args.Contains("--operations-audit"))
    {
        await using var data = new MySqlDataSourceBuilder(builder.ConnectionString).Build();
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await MinhaBelem.Infrastructure.Background.OperationsDiagnostics.ReadLocalAsync(data,config),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        return 0;
    }
    if (args.Contains("--auth-audit"))
    {
        await using var data = new MySqlDataSourceBuilder(builder.ConnectionString).Build();
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await MinhaBelem.Infrastructure.Identity.AuthenticationDiagnostics.ReadLocalAsync(data, environment: Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "not_supplied"), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        var files = new ConfigurationBuilder().AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "src/MinhaBelem.Web/appsettings.json"), false).AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "src/MinhaBelem.Web/appsettings.Development.json"), true).Build();
        if (files.GetConnectionString("Default") is { Length: > 0 } fileConnection && fileConnection != builder.ConnectionString)
        {
            Console.WriteLine("Comparação somente leitura com appsettings (sem User Secrets):");
            try
            {
                await using var fileData = new MySqlDataSourceBuilder(fileConnection).Build();
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await MinhaBelem.Infrastructure.Identity.AuthenticationDiagnostics.ReadLocalAsync(fileData), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { Console.WriteLine("Comparação indisponível: " + ex.GetType().Name); }
        }
        return 0;
    }
    if (args.Contains("--register-sources-local"))
    {
        builder.AllowUserVariables = true;
        await using var data = new MySqlDataSourceBuilder(builder.ConnectionString).Build();
        var bootstrap = new MinhaBelem.Infrastructure.Discovery.ResearchedSourceBootstrap(data,
            new MinhaBelem.Infrastructure.MySql.IngestionRepository(data), new MinhaBelem.Infrastructure.Discovery.DiscoveryRepository(data));
        Console.WriteLine("Novas fontes cadastradas inativas, aguardando autorização: " + await bootstrap.RegisterAsync(default));
        return 0;
    }
    if (args.Contains("--publish-reviewed-events-local"))
    {
        builder.AllowUserVariables = true;
        await using var data = new MySqlDataSourceBuilder(builder.ConnectionString).Build();
        var editorId = await db.ExecuteScalarAsync<long?>("SELECT id FROM mb_users WHERE email IN ('editor.teste@minhabelem.com.br','superadmin@minhabelem.com.br') ORDER BY id LIMIT 1");
        if (editorId is null) { Console.WriteLine("Nenhum usuário editor/superadmin encontrado."); return 2; }
        var repo = new MinhaBelem.Infrastructure.MySql.ContentRepository(data);
        var inReviewEvents = (await db.QueryAsync<long>("SELECT id FROM mb_content_items WHERE kind='Event' AND status='InReview' ORDER BY id")).ToArray();
        Console.WriteLine($"Eventos em revisão encontrados: {inReviewEvents.Length}");
        var publishedCount = 0;
        foreach (var eventId in inReviewEvents)
        {
            var success = await repo.SetStatusAsync(eventId, MinhaBelem.Core.Entities.ContentStatus.Published, editorId.Value, default);
            if (success) publishedCount++;
            else Console.WriteLine($"Evento #{eventId} não atendeu aos critérios de publicação.");
        }
        Console.WriteLine($"Eventos publicados com sucesso: {publishedCount}/{inReviewEvents.Length}");
        return 0;
    }
    if (args.Contains("--migrate-local"))
    {
        builder.AllowUserVariables = true;
        await using var data = new MySqlDataSourceBuilder(builder.ConnectionString).Build();
        var migration = await new MinhaBelem.Infrastructure.MySql.SchemaMigrator(data, Microsoft.Extensions.Logging.Abstractions.NullLogger<MinhaBelem.Infrastructure.MySql.SchemaMigrator>.Instance).ApplyAsync();
        Console.WriteLine("Migrations incrementais locais aplicadas: " + string.Join(", ", migration.Applied));
        return 0;
    }
    if (args.Contains("--test-disposable") || args.Contains("--probe-disposable"))
    {
        // The fixture creates and drops only a random database, never the configured development database.
        builder.Database = "";
        var start = new System.Diagnostics.ProcessStartInfo("dotnet") { UseShellExecute = false };
        var command = args.Contains("--probe-disposable")
            ? new[] { "run", "--project", "scripts/MinhaBelem.Probe", "-c", "Release", "--", "--persist-disposable" }
            : new[] { "test", "MinhaBelem.sln", "-c", "Release", "--nologo", "--verbosity", "minimal", "--logger", "trx;LogFileName=sprint17.trx" };
        foreach (var arg in command) start.ArgumentList.Add(arg);
        var filterIndex = Array.IndexOf(args, "--filter");
        if (args.Contains("--test-disposable") && filterIndex >= 0 && filterIndex + 1 < args.Length)
        {
            start.ArgumentList.Add("--filter");
            start.ArgumentList.Add(args[filterIndex + 1]);
        }
        start.Environment["MINHABELEM_TEST_MYSQL"] = builder.ConnectionString;
        using var process = System.Diagnostics.Process.Start(start)!;
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
    Console.WriteLine("Conexão local: OK. Versão: " + await db.ExecuteScalarAsync<string>("SELECT VERSION()"));
    Console.WriteLine("Tabelas: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE()"));
    Console.WriteLine("Matérias: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_content_items"));
    Console.WriteLine("Eventos publicados: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_content_items WHERE kind='Event' AND status='Published'"));
    Console.WriteLine("Fontes operacionais: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_ingestion_sources WHERE enabled=1 AND approved=1"));
    Console.WriteLine("Fontes cadastradas: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_ingestion_sources"));
    Console.WriteLine("Candidatos aguardando revisão: " + await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_ingestion_candidates WHERE status='Pending'"));
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
