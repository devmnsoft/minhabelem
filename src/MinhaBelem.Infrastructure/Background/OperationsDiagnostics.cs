using Dapper;
using MinhaBelem.Infrastructure.Identity;

namespace MinhaBelem.Infrastructure.Background;

/// <summary>Read-only local readiness. Never emits credentials or infers collection from heartbeat alone.</summary>
public static class OperationsDiagnostics
{
    public static async Task<object> ReadLocalAsync(MySqlDataSource data, IConfiguration config, CancellationToken ct=default, string? environment=null)
    {
        var cs=new MySqlConnectionStringBuilder(data.ConnectionString);
        if(cs.Server is not("localhost" or "127.0.0.1"))throw new InvalidOperationException("Auditoria exclusiva do banco local.");
        await using var db=await data.OpenConnectionAsync(ct);
        var sources=await db.QueryAsync(new CommandDefinition("""
            SELECT s.id,s.name,s.enabled,s.approved,s.protocol,
                COALESCE(LENGTH(TRIM(s.license_note))>0,FALSE) AS hasLicenseNote,
                s.last_checked_at AS lastCheckedAt,p.frequency_minutes AS frequencyMinutes,p.daily_request_limit AS dailyRequestLimit
            FROM mb_ingestion_sources s LEFT JOIN mb_source_profiles p ON p.source_id=s.id ORDER BY s.id
            """,cancellationToken:ct));
        var settings=(await db.QueryAsync<(string Key,string Value)>(new CommandDefinition("SELECT setting_key AS `Key`,setting_value AS Value FROM mb_settings WHERE setting_key IN ('radar.enabled','radar.intervalMinutes','collector.enabled')",cancellationToken:ct))).ToDictionary(x=>x.Key,x=>x.Value);
        return new
        {
            capturedAtUtc=DateTime.UtcNow,host=cs.Server,port=cs.Port,database=cs.Database,environment=environment??"not_supplied",databaseReference=AuthenticationDiagnostics.DatabaseReference(data.ConnectionString),
            radarEnabled=bool.TryParse(settings.GetValueOrDefault("radar.enabled"),out var enabled)?enabled:config.GetValue<bool>("Collector:Enabled"),
            checkIntervalMinutes=int.TryParse(settings.GetValueOrDefault("radar.intervalMinutes"),out var minutes)?Math.Clamp(minutes,1,240):Math.Clamp(config.GetValue("Collector:IntervalMinutes",1),1,240),
            sources, operationalSources=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_ingestion_sources WHERE enabled=TRUE AND approved=TRUE"),
            heartbeat=await db.QuerySingleOrDefaultAsync("SELECT last_heartbeat_at AS lastHeartbeatAt,last_cycle_started_at AS lastCycleStartedAt,last_cycle_finished_at AS lastCycleFinishedAt,total_cycles_executed AS totalCycles FROM mb_radar_heartbeats WHERE worker_name='RadarWorker'"),
            persistedCycles=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_radar_cycles"),
            consultedPages=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_radar_crawl_history"),
            publishedEvents=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_content_items WHERE kind='Event' AND status='Published'"),
            futureEvents=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_content_items WHERE kind='Event' AND event_start_at>UTC_TIMESTAMP(6)"),
            pendingCandidates=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_ingestion_candidates WHERE status='Pending'"),
            routes=new {enabled=config.GetValue<bool>("GoogleRoutes:Enabled"),serverKeyConfigured=!string.IsNullOrWhiteSpace(config["GoogleRoutes:ApiKey"]),browserKeyConfigured=!string.IsNullOrWhiteSpace(config["Maps:BrowserKey"]),dailyLimit=config.GetValue<int>("GoogleRoutes:DailyLimit"),providerRequestPerformed=false},
            workerInstallationVerified=false, continuousCollectionProven=false
        };
    }
}
