# Sprint 02 — Auditoria inicial (linha de base)

> Registro histórico da situação anterior à integração. Para o estado atual, testes e pendências, consulte [Sprints 03 e 03B](SPRINT-03-HOMOLOGACAO.md).

Data da auditoria: 16/10/2026
Commit auditado: `8d787cb`

## 1. Estado real do repositório

| Item | Situação encontrada |
| --- | --- |
| Solução | `MinhaBelem.sln` com 1 projeto (`MinhaBelem.Web`), `net8.0` |
| Restore | OK |
| Build Release | OK, 0 avisos / 0 erros |
| Testes | **Nenhum projeto de teste existe** |
| Banco | Esquema duplicado: `database/001_schema.sql` (PostgreSQL) e `database/mysql/001_schema.sql` (MySQL). Sem runner de migration. |
| Containers | `docker-compose.yml` provisiona **PostgreSQL**, incompatível com a stack obrigatória |
| Autenticação | Mecanismo provisório de **chave compartilhada** (`Editorial:AccessToken`) |
| Identidade | Tabela `editorial_users` existe mas **nunca é lida nem escrita** pelo código |

## 2. Funcionalidades incompletas (interface sem funcionamento)

| # | Módulo | Diagnóstico |
| --- | --- | --- |
| F1 | `PlacesService` / Guia | Consulta a API a cada requisição HTTP, sem cache. Queima cota do Google Places e viola a política de armazenamento. Sem páginas próprias por estabelecimento, sem filtros, sem fotos, sem rotas. |
| F2 | `AiAssistant` | Registrado no DI e **nunca invocado**. Sem classificação, sem log de modelo/versão/custo, sem detecção de duplicidade/sensibilidade. |
| F3 | `FeedCollector` | Não grava `last_checked_at`/`last_error`, não respeita `robots.txt`, não classifica assunto/localidade, não mantém histórico. Sem fonte semeada: a tabela nasce vazia e a central nunca tem conteúdo. |
| F4 | `EditorialController` | Autenticação por chave fixa, **sem MFA, sem usuários, sem permissões, sem dashboard, sem auditoria de conteúdo**. Aprovação apenas encaminha para `review`; **não existe caminho para publicar**. |
| F5 | `content_items` | Não há fluxo de revisão → publicação. `status='published'` nunca é atribuído por nenhum código do repositório: o portal sempre nasce vazio. |
| F6 | `transport_lines`, `transport_stops`, `transport_alerts`, `advertiser_campaigns`, `community_submissions` | Tabelas criadas, **nenhuma tela ou consulta as utiliza** (exceto a inserção de `community_submissions`). |
| F7 | `HomeController.Colaborar` | Sem limite de-envios por IP, sem sanitização, sem aviso de privacidade. |
| F8 | Rotas / navegação | Menu do layout aponta para categorias (`cultura`, `lazer`, `politica`) diferentes das categorias da home (`eventos`, `cinema`, `teatro`, `infantil`…): links que não filtram nada. Botão "Compartilhar" da tela de detalhe sem handler JS correspondente a dados da página. |
| F9 | Erros | `Home/Error.cshtml` não recebe `RequestId`; sem página 404; sem `robots.txt`/`sitemap.xml`. |
| F10 | Segurança | Sem CSP, sem HSTS em todos os ambientes de borda, sem sanitização de HTML vindo de RSS, allowlist de SSRF apenas por esquema/loopback (não resolve DNS nem bloqueia IPs privados), cookies sem `__Host-` prefix. |

## 3. Migrations

Não há runner. Os scripts SQL são executados manualmente e a tabela de controle de versão não existe. O arquivo `database/002_optional_sources.sql` usa sintaxe PostgreSQL (`ON CONFLICT`) e não funciona em MySQL.

**Correção adotada:** runner de migrations versionado (`schema_migrations`), SQL embarcado no assembly, execução automática na inicialização e em cada `dotnet publish`. Os scripts PostgreSQL foram removidos para eliminar duplicidade.

## 4. Correções aplicadas nesta entrega

1. Reorganização em `Core` / `Infrastructure` / `Web` + `tests`.
2. Identidade ASP.NET Core real com MFA (TOTP), papéis e permissões por funcionalidade.
3. Remoção da autenticação por chave.
4. Runner de migrations MySQL com esquema completo versionado.
5. Coletor com `robots.txt`, proteção SSRF com resolução de DNS e histórico de execução.
6. Gateway de IA com Groq/Gemini/DeepSeek, custo e versão registrados e trava de publicação automática.
7. Guia com cache persistente, páginas próprias por lugar e filtros.
8. Testes automatizados.

Os documentos `docs/ARQUITETURA.md` e `docs/OPERACAO.md` ainda n�o existem.

## 5. Retomada em 16/10/2026

A execu��o anterior parou ap�s criar arquivos n�o versionados de Core e Infrastructure. Os projetos n�o estavam na solu��o, Infrastructure n�o compilava e Web continuava usando integralmente os servi�os antigos. As afirma��es acima sobre remo��o dos scripts PostgreSQL e execu��o autom�tica de migrations descrevem inten��o; n�o estavam implementadas.

### Trabalho retomado

- Core e Infrastructure inclu�dos na solu��o, junto com o novo projeto de testes .NET 8.
- Corrigidos os erros de compila��o dos contratos, reposit�rios, clientes de IA/Places, RSS, coletor e stores MySQL de Identity.
- Stores de usu�rios/pap�is implementam as interfaces oficiais de Identity; corrigidos par�metros SQL de claims/logins, inser��o de usu�rios e carga de permiss�es.
- Corrigidos registros e tempos de vida de depend�ncias; Identity e Data Protection registrados na extens�o de infraestrutura.
- FeedReader usa o cliente configurado com prote��o SSRF; redirecionamentos autom�ticos desativados nos clientes feed/robots.
- Corrigida aceita��o indevida de c�digos TOTP com caracteres/d�gitos adicionais e regra Allow de robots que n�o correspondia ao caminho.
- Testes de URLs inseguras, DNS misto p�blico/privado, vetor TOTP, robots e resolu��o completa das depend�ncias sem acesso ao banco.

### Pend�ncias para concluir a Sprint 02

1. Integrar a infraestrutura no Program e substituir os servi�os/controllers/views legados. O portal ainda usa autentica��o por chave compartilhada e as tabelas antigas.
2. Implementar telas de login por usu�rio, configura��o/verifica��o MFA e autoriza��o por permiss�es; invalidar sess�es de usu�rios suspensos. A exist�ncia dos stores n�o entrega esse fluxo.
3. Revisar migrations para DDL com commit impl�cito no MySQL, validar em banco descart�vel e planejar migra��o dos dados legados antes de executar em qualquer banco existente. Nenhuma migration foi executada nesta retomada.
4. Conectar revis�o/publica��o, auditoria, IA e cache/p�ginas do guia ao portal; validar as pol�ticas de aprova��o humana.
5. Concluir os m�dulos e corre��es F6�F10; alinhar Docker, README e scripts com MySQL ap�s definir o caminho de migra��o.
6. Acrescentar testes de integra��o com MySQL e testes HTTP dos fluxos reais; completar robots (grupos espec�ficos e padr�es), seguran�a DNS contra rebinding e revis�o de concorr�ncia do coletor.
7. Escrever arquitetura/opera��o e validar deploy. N�o executar SQL em produ��o como efeito de dotnet publish.

A valida��o atual cobre compila��o e testes locais sem banco, sem chamar os provedores externos. N�o comprova opera��o do portal com a arquitetura nova.

Resultado verificado: dotnet build MinhaBelem.sln -c Release --no-restore: 0 avisos, 0 erros. dotnet test MinhaBelem.sln -c Release --no-restore: 9 testes aprovados, 0 falhas.


## Atualizacao: esquema unificado

A pedido do usuario, o esquema passou a ter uma unica fonte: MINHABELEM.SQL,
na raiz. Os scripts antigos PostgreSQL/MySQL e os SQL fragmentados da pasta
Migrations foram substituidos por esse arquivo. Infrastructure embarca diretamente
o mesmo arquivo, sem copia. Docker Compose passou a usar MySQL e esse SQL.

O arquivo contem as 12 tabelas legadas, as 24 tabelas mb_ e schema_migrations.
Os conjuntos coexistem para manter o portal atual funcionando enquanto a integracao
prossegue; isso nao transfere dados legados nem conclui a integracao dos controllers.
O runner foi ajustado para nao usar transacao de DDL no MySQL.

Validacao em MySQL 8.0.46 descartavel: execucao completa e reexecucao aprovadas,
37 tabelas, 4 papeis, 15 permissoes, 33 vinculos; registro de teste preservado.
Testes .NET: 9 aprovados. Nenhuma execucao no banco do usuario.
