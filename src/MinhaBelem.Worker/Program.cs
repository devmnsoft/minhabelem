using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.Background;

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
