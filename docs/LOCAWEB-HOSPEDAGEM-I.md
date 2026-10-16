# Implantação Locaweb — MinhaBelém
**Domínio:** minhabelem.com.br
**DNS:** ns1.locaweb.com.br e ns2.locaweb.com.br
**Plano:** Hospedagem I Linux (compartilhada)
**IP compartilhado informado:** 179.188.55.7
**Diretório informado:** /home/minhabelem1/
**Banco existente:** MySQL minhabelem

## ATENÇÃO: compatibilidade
Não assumir que Hospedagem I Linux compartilhada executa ASP.NET Core ou HostedServices long-running. Confirmar com o suporte da Locaweb se aceita runtime .NET 8, processo HTTP persistente, worker e agendamento por 10 minutos. Caso negativo, usar VPS/cloud com ASP.NET Core + worker, DNS do domínio apontando ao novo endpoint; manter hospedagem atual para conteúdo estático se desejado.
Não alterar DNS ou apontar IP até verificar a instalação destino.
Não publicar scripts .NET como se fossem executados por hospedagem PHP.

## MySQL
O esquema equivalente está em MINHABELEM.SQL, criado para MySQL 8.0+ no banco previamente provisionado. Antes de rodar: backup, verificar versão do MySQL, credenciais, host, SSL e permissões via painel Locaweb; não assumir host localhost.
A aplicação utiliza Dapper + MySqlConnector. O script inclui as tabelas atuais e as tabelas mb_ da infraestrutura em integração; os dados não são transferidos automaticamente entre elas.
Restrições compartilhadas incluem limite de 30 conexões simultâneas; usar pool pequeno e índices, não manter conexões abertas em coleta prolongada.

## Segurança operacional
API Google Places, tokens editoriais e senhas sempre em variáveis de ambiente/secret manager, jamais em repositório público ou páginas JS. Configurar cotas e restrições da API.
Para publicar no novo destino: HTTPS, reverse proxy, headers, backups testados, logging, política de privacidade, proteção de menores, sessões protegidas e MFA de administradores.
O coletor depende de fontes oficialmente permitidas, listas de hosts de saída, defesa contra SSRF/redirect e workers supervisionados.
Verificar titularidade e licença das fotos e fontes dos dados. Nenhuma localização de ônibus deve ser apresentada como ao vivo sem GTFS-RT/SIRI ou endpoint autorizado.

## Dados necessários para execução
Confirmação da capacidade .NET no plano, ou acesso autorizado a VPS/cloud, host MySQL e usuário (SEM senha no chat), e confirmação da versão MySQL e da política de conexão remota do provedor.
