# Sprint 16 — Agenda 365 e Amazônia 3.0

Data: 18/10/2026. Branch `sprint16/agenda365`, baseada na entrega 14/15/15B da PR #9. Implantação de produção não realizada. Este relatório distingue implementação, homologação com dados descartáveis e dados reais locais.

## Entregas e correções

| Área | Resultado desta etapa | Limite de operação |
|---|---|---|
| Agenda 365 | Períodos de 180/365 dias, mês, data, temas, gratuitos, calendário, favoritos locais, compartilhamento e ICS | A janela anual não significa programação confirmada para todos os dias |
| Sincronização | Identidade por sessão JSON-LD/iCalendar, identificador explícito preservado em remarcações, histórico A→B→A e leitura repetida idempotente | Sem ID estável, remarcação exige conciliação humana; nenhuma fonte nova foi ativada automaticamente |
| Precisão | Heurística sem hora não inventa 19h, preserva precisão de data e sempre exige revisão | Texto livre não constitui confirmação automática |
| Editorial | Seleção da página ou de todos os resultados filtrados, limite configurável padrão 250/máximo 500, prévia, confirmação, motivo, progresso, histórico por autor, prioridade 0–3 e filas específicas | Publicação mantém MFA, RBAC, revisão e autorização da fonte; registros sensíveis não passam silenciosamente |
| Mobilidade fluvial | Cadastro administrativo, operador, fonte, vigência, acessibilidade desconhecida/confirmada, revisão concorrente e auditoria | Cadastro inicial inativo; horários/tarifas atuais não foram inferidos de notícias antigas |
| Horários | Sugestão de visita de 90 minutos que caiba em uma janela confirmada nos próximos sete dias | Evidência vencida retorna indisponibilidade |
| Meu dia em Belém | `/meu-dia-em-belem`, pessoas, minutos disponíveis por dia, orçamento do grupo, chegada compatível com abertura e retorno dentro do período | Requer catálogo publicado, horários vigentes e provedor de rotas configurado; refeições e extras não cotados |
| Visual | Fotografia real no hero, três cartões fotográficos, cores de rio/cerâmica/areia, foco de teclado, tabelas adaptáveis, navegação e estados vazios | Fotos de pontos turísticos são ilustrativas; não representam os eventos |
| Traduções | Novos controles em pt-BR, en, fr, es, ru, zh-Hans e ja | Não foi contratado serviço externo de tradução de conteúdo |
| Robô | Worker separado do Web, agenda de minuto e processamento persistido já existentes, preservados | Não instalado como serviço de produção nesta sessão |
| Repositório | 165 arquivos de `node_modules` removidos apenas do índice; arquivos locais e lockfile preservados; SDK .NET 8 fixado | Nenhum arquivo de usuário foi excluído |

## Fontes e agenda real

As 32 ocorrências anteriormente cadastradas foram preservadas. Mais duas foram importadas explicitamente para a base local, com resumo próprio e fonte rastreável:

| Evento | Data em Belém | Evidência |
|---|---|---|
| MAZ ao Pôr do Sol — Kamishibai Encantado | 30/10/2026, 17h30; gratuito | [SECULT Pará](https://www.secult.pa.gov.br/noticia/2137/museu-das-amazonias-tem-programacao-gratuita-sobre-saberes-e-sabores-da-regiao-em-outubro) |
| Dominguinho — Belém, Hangar | 22/01/2027; horário a confirmar | [Agenda oficial da turnê](https://turnedominguinho.com.br/) |

Manifesto: `data/agenda/2026-10-18.sprint16.events.json`. Primeira execução: 2 inseridos (IDs 33 e 34). Reexecução: 0 inseridos, 2 existentes. Total local: 34 ocorrências, não 34 eventos distintos. O manifesto anterior continua disponível com suas fontes e sessões.

SECULT Pará, Sectur/Fumbel e UFPA/IFCH foram cadastradas como fontes pesquisadas **inativas e não aprovadas**, com frequência diária e cota baixa para eventual autorização. Cadastro de fonte não equivale a sincronização operacional. Fontes sem feed autorizado e revisado não são declaradas sincronizadas. Uma notícia com informação de julho sobre Cotijuba não foi usada como confirmação de operação em outubro.

## Transporte, De–Para, tarifas e roteiros

Corrigido o INSERT administrativo de linhas, que declarava os campos de fonte/vigência sem enviar seus valores. O teste de regressão cria e atualiza uma linha, preserva a fonte e oculta tarifa vencida. As cinco linhas legadas existentes não ganharam selo de verificação por esta entrega. Não há GTFS atual/GTFS-RT/SIRI integrado, paradas verificadas ou travessias reais confirmadas na base. O editor fluvial está funcional para receber dados oficiais do operador.

O De–Para conserva a integração Google Routes, seus limites, consentimento e proteção da origem, mas as chaves não estão configuradas neste ambiente. Os testes usam respostas controladas e identificadas como fixtures: não demonstram uma viagem pública real nem autorizam publicar uma tarifa. Não foram anunciados horários, clima, lotação ou maré em tempo real sem provedor. O roteiro valida custo conhecido por pessoa, total do grupo, chegada, visita e retorno; não oferece recomendação médica, previsão meteorológica ou reserva.

## Dados, identidades e recuperação

Auditoria inicial: 229 arquivos de fonte/testes lidos, 65 views inventariadas, 71 tabelas, 32 conteúdos e 3 usuários antes das alterações. Backup SQL com ACL restrita, fora do repositório e sem credenciais no comando. Restauração em MySQL 8 descartável preservou 71 tabelas, 32 conteúdos e 3 usuários. Para compatibilidade MariaDB→MySQL, somente os nomes gerados dos CHECKs foram renomeados na cópia de restauração; o backup original permaneceu intacto.

Duas migrations aditivas foram aplicadas explicitamente, fora do startup normal:

1. `sprint16_editorial_and_discovery_history`: prioridade/fila em conteúdo e candidatos; histórico de versões deixa de impedir retorno a um fingerprint anterior.
2. `sprint16_river_editor`: operador, acessibilidade opcional e revisão concorrente.

Contas locais preservadas: `superadmin@minhabelem.com.br` (SuperAdmin), `editor.teste@minhabelem.com.br` (Editor), `moderador.teste@minhabelem.com.br` (Moderator). Troca de senha e configuração de MFA continuam obrigatórias. Senhas ficam exclusivamente no relatório temporário local protegido; não são publicadas em Git, screenshots ou neste documento. Evidência atual de dados/login: `evidencias/sprint16/local-data.json`.

## Evidências e critérios de aceite

Build, MySQL, HTTP, autenticação/MFA/RBAC, lote de 200 itens, alterações de evento, abertura/validade, roteiro de grupo e sete idiomas são verificados por testes automatizados. O teste de lote executa prévia HTTP, confirmação, processamento persistido, resultado por item e isolamento do histórico/progresso por autor. Acessibilidade visual automatizada cobre nomes de controles, alt e campos rotulados; não substitui auditoria WCAG integral ou avaliação com leitor de tela.

CI do código: [execução 39133313580](https://github.com/devmnsoft/minhabelem/actions/runs/39133313580), commit `a6b74e304fb1360581d4c53a1369dd48a88d9352`: 177 testes aprovados, zero falhas e zero ignorados na suíte completa MySQL 8/HTTP/OCR real. Builds Windows e Linux: zero erros/avisos e 74 testes portáveis aprovados em cada plataforma. O teste `RadarMinuteScheduleCompletesTwoActualCycles` executa dois ciclos reais com intervalo de um minuto em banco descartável, sem hospedar o Worker dentro do Web.

No Windows local, a primeira suíte completa registrou 173 aprovados, duas dependências OCR ignoradas e uma falha por interferência entre fixtures de lugares. A publicação residual do lugar de teste foi corrigida com `finally`; a suíte completa posterior do CI aprovou todos os 176 casos e, após a regressão adicional do transporte, 177 casos. Os 15 testes direcionados no Windows passaram, seguidos pelo teste de persistência de linha (1/1). Este resultado anterior não é apresentado como uma execução verde.

Base real local: 77 GETs HTTP aprovados (11 páginas × sete idiomas), 34 sessões de 20 eventos e 3/3 logins reais com bloqueio até troca de senha. Matriz pública Playwright: 308 casos, sem overflow, imagens quebradas, campos sem rótulo, botões sem nome ou alt ausente. Menu móvel e favoritos/lista da agenda exercitados. A matriz administrativa somou 1.548 casos; a dedicada ao editorial, 56. Total: **1.912 verificações**, sem overflow, imagens quebradas ou falhas nos controles de acessibilidade medidos. Seleção da página/todos os filtrados e quantidade de confirmação passaram em 28 combinações de idioma/largura; o polling da barra/resultado foi exercitado no navegador com uma resposta controlada identificada como fixture. O processamento real de 200 itens e seu endpoint HTTP são testados separadamente no MySQL. O inventário de 67 views está em [SPRINT-16-VIEWS.md](SPRINT-16-VIEWS.md); estados condicionais sem captura própria estão explicitamente separados.

Arquivos HTML de teste, TRX e logs com infraestrutura local ficam ignorados em `TestResults`; somente evidências sanitizadas são versionadas. Fotografias e créditos: `data/media/credits.json` e `/creditos`. Capturas públicas em `evidencias/sprint16/live`, administrativas em `evidencias/sprint16/admin`.

Commits de implementação publicados: `fe595b9` (retirada de dependências geradas do índice), `0ca9417` (Agenda 365/editorial/visual/migrations) `dec0196` (isolamento do lugar usado no teste) e `a6b74e3` (fonte/vigência no cadastro de linhas e captura do progresso). [PR #10](https://github.com/devmnsoft/minhabelem/pull/10), em rascunho, depende da PR #9. A branch principal não foi modificada.

## Dependências de produção e próximas etapas

1. Autorizar e homologar individualmente fontes contínuas e seus termos, conectores e regras de cancelamento/adiamento. Obter feed atual do município e operadores.
2. Configurar chaves/cotas Google Routes/Places, validar viagens reais com operador e registrar fonte/vigência de horários, tarifas, acessibilidade e travessias.
3. Preencher e validar o catálogo de lugares, refeições e durações; contratar clima/maré se esses sinais forem necessários.
4. Concluir senha/MFA das contas de homologação e configurar instalação/monitoramento do Windows Worker em ambiente autorizado.
5. Implantar após revisão, backup no destino e execução do roteiro de migração. A entrega não é declarada 100% concluída em produção enquanto depender dessas integrações.

## Reprodução da revisão visual

Os testes HTTP gravam HTML sanitizado quando `MINHABELEM_VISUAL_SNAPSHOTS` está definido. `scripts/visual-qa/check.mjs` serve somente GETs desse HTML e dos assets locais, sem executar alterações no banco. Use `QA_SNAPSHOTS`, `QA_OUTPUT`, `QA_FILES` e `QA_CULTURES` para escolher a matriz. As matrizes CSV versionadas registram cada combinação; os JSONs completos com hash SHA-256 e as capturas móveis integrais ficam preservados em `TestResults/sprint16`. Fotos reais da página pública e dados fictícios dos testes administrativos são discriminados nos summaries.
