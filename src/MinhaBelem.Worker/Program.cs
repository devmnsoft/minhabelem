using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.Background;
using MinhaBelem.Core.Abstractions;

var builder = Host.CreateApplicationBuilder(args);

// Suporte para execução como Serviço do Windows (Windows Service)
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MinhaBelemWorker";
});

// Registro de logs estruturados (Console, EventLog em Windows)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(settings =>
    {
        if (OperatingSystem.IsWindows())
        {
            settings.SourceName = "MinhaBelemWorker";
        }
    });
}

// Configuração completa de infraestrutura (MySql, RadarWorker, FeedCollectorWorker, IA, etc.)
builder.Services.AddMinhaBelemInfrastructure(builder.Configuration);

var host = builder.Build();

if (args.Contains("--audit-operations"))
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await OperationsDiagnostics.ReadLocalAsync(host.Services.GetRequiredService<MySqlConnector.MySqlDataSource>(),builder.Configuration,environment:builder.Environment.EnvironmentName),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    host.Dispose();
    return 0;
}

// Explicit local drain of already-confirmed editorial work. Never starts source collection.
if (args.Contains("--run-editorial-once"))
{
    var connection=new MySqlConnector.MySqlConnectionStringBuilder(host.Services.GetRequiredService<MySqlConnector.MySqlDataSource>().ConnectionString);
    if(!builder.Environment.IsDevelopment() || connection.Server is not("localhost" or "127.0.0.1"))
        throw new InvalidOperationException("Execução editorial pontual exclusiva de Development local.");
    using var scope=host.Services.CreateScope();
    using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
    var batches=scope.ServiceProvider.GetRequiredService<IEditorialBatches>();
    var processed=0;
    while(processed<10 && await batches.ProcessNextAsync(timeout.Token))processed++;
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{editorialBatchesProcessed=processed,sourceCollectionStarted=false}));
    host.Dispose();
    return 0;
}

// Modo de execução única (útil para rotinas agendadas no Task Scheduler ou testes de homologação)
if (args.Contains("--run-once"))
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("MinhaBelem.Worker executando em modo pontual (--run-once)...");

    using var scope = host.Services.CreateScope();
    var radar = scope.ServiceProvider.GetRequiredService<IRadarWorker>();
    var collector = scope.ServiceProvider.GetRequiredService<ISourceCollection>();
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

    try
    {
        logger.LogInformation("Processando lotes editoriais já confirmados...");
        var batches = scope.ServiceProvider.GetRequiredService<IEditorialBatches>();
        for (var i = 0; i < 10 && await batches.ProcessNextAsync(cts.Token); i++) { }
        logger.LogInformation("Processando solicitações manuais pendentes...");
        var queue = scope.ServiceProvider.GetRequiredService<CollectionQueueWorker>();
        for (var i = 0; i < 10 && await queue.ProcessNextAsync(cts.Token); i++) { }
        logger.LogInformation("Disparando coleta de feeds...");
        await collector.CollectOnceAsync(cts.Token);

        logger.LogInformation("Disparando varredura autônoma do Radar...");
        await radar.TriggerOnceAsync(cts.Token);

        logger.LogInformation("Ciclo pontual concluído com sucesso.");
    }
    catch (Exception ex)
    {
        logger.LogError("Ciclo pontual interrompido: {ErrorType}", ex.GetType().Name);
        return 1;
    }

    return 0;
}

await host.RunAsync();
return 0;
