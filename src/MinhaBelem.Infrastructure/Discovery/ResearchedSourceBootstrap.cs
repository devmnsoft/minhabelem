using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
namespace MinhaBelem.Infrastructure.Discovery;
/// <summary>Manual local registration only. Public consultation is not treated as permission to crawl/reuse media.</summary>
public sealed class ResearchedSourceBootstrap(MySqlDataSource data,IIngestionRepository sources,IDiscoveryRepository discovery)
{
    public async Task<int> RegisterAsync(CancellationToken ct)
    {
        if(new MySqlConnectionStringBuilder(data.ConnectionString).Server is not("localhost" or "127.0.0.1"))throw new InvalidOperationException("Cadastro exclusivo do banco local.");
        var existing=await sources.SourcesAsync(false,ct);var created=0;
        foreach(var (name,url,topics) in new[]{("SECULT Pará","https://www.secult.pa.gov.br/","agenda,cultura"),("FUMBEL — institucional municipal","https://secult.belem.pa.gov.br/institucional/sobre-a-fumbel/","agenda,cultura"),("Fundação Cultural do Pará — Agenda","https://www.fcp.pa.gov.br/agendasculturais","agenda,cultura"),("SEGBEL — Serviços SEMOB","https://seguranca.belem.pa.gov.br/servicos-semob/","mobilidade,itinerários"),("SEGBEL — Integra Belém","https://seguranca.belem.pa.gov.br/integra-belem/","mobilidade,integração"),("Estação das Docas","https://estacaodasdocas.com/","agenda,turismo"),("Mangal das Garças","https://mangaldasgarcas.com.br/","turismo,horários,preços"),("Museu Paraense Emílio Goeldi","https://www.gov.br/museugoeldi/pt-br/a-instituicao/visitacao/informacoes-sobre-ingressos-e-horarios","turismo,horários"),("Theatro da Paz — Programação","https://www.theatrodapaz.com.br/programa%C3%A7%C3%A3o","agenda,cultura")})
        {
            if(existing.Any(s=>string.Equals(s.FeedUrl.TrimEnd('/'),url.TrimEnd('/'),StringComparison.OrdinalIgnoreCase)))continue;
            var id=await sources.UpsertSourceAsync(new(){Name=name,FeedUrl=url,Protocol=FeedProtocol.WebDiscovery,Topics=topics,Municipality="Belém",Enabled=false,Approved=false,RespectRobots=true,LicenseNote="Consultada publicamente em 18/10/2026. Coleta contínua e reutilização de mídia não autorizadas neste cadastro; exige revisão de licença, robots e atualização pela curadoria."},ct);
            await discovery.SaveProfileAsync(new(){SourceId=id,Organization=name,FrequencyMinutes=1440,DailyRequestLimit=1,MaxItems=10},ct);created++;
        }
        return created;
    }
}
