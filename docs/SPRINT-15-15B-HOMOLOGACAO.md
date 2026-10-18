# MinhaBelém — Sprints 15 e 15B

Data da verificação: 18/10/2026. Implementação na branch `sprint15/agenda-editorial`, preservando a sprint 14 e o `main` anterior. A sprint 17 do Belém Restaurantes foi deixada com a outra sessão, conforme orientação do usuário.

## Correções e módulos entregues

- A falha dos quatro testes do run 39118915704 era a resolução de `ISourceCollection` pelo painel Web. A integração da sprint 14 mantém Web → fila MySQL → Worker. A base integrada passou no [Actions 39121626138](https://github.com/devmnsoft/minhabelem/actions/runs/39121626138).
- `/editorial/lotes`: seleção individual/página/filtro, quantidade confirmada, prévia persistida, confirmação explícita, fila, estados e resultados individuais. O serviço revalida MFA, conta e permissões por item. Cada alteração e sua auditoria usam uma transação curta; revisões impedem sobrescrever alterações de outros editores. Reconfirmar o mesmo lote não duplica a operação.
- Ações: importação para edição, rejeição, revisão, categoria, localidade, análise IA, duplicidade, arquivo, publicação elegível, tradução e situação de evento. A retomada de candidato devolve à revisão; não declara uma coleta externa reexecutada. Tradução exige idioma habilitado com aprovação humana.
- Conteúdo sensível, duplicado, evento com dados incertos, fonte sem autorização e mídia sem licença são bloqueados na publicação em lote. Alterações editoriais retornam à revisão. Pedidos externos interrompidos ficam para conferência, evitando repetição silenciosa de cobrança.
- Agenda com calendário mensal e semanal; filtros e janelas de hoje, amanhã, 7/30/90/180/365 dias e fim de semana reutilizam o conteúdo do Radar e o histórico de versões.
- `/admin/midias`: upload autorizado PNG/JPEG/WebP, créditos, licença, descrição acessível, YouTube por ID, associação a conteúdo/evento e a lugares. Arquivos ficam fora de `wwwroot`; mídia de rascunho não é servida publicamente. Galerias usam carregamento adiado e dimensões de apresentação.
- `/como-chegar`: origem/destino, localização somente mediante ação do visitante, data, saída/chegada, modos, preferências, limites de caminhada, alternativas, linha, embarque, desembarque e custos fornecidos pelo provedor. A origem não entra no link compartilhado nem no banco de rotas.
- Origem e destino oferecem sugestões do catálogo local publicado. Autocomplete externo de endereços depende da integração contratada; os campos também aceitam texto livre.
- Google Routes tem habilitação explícita e limite diário atômico. Não persiste conteúdo do Google ou origem do visitante. Quando a integração não está configurada, oferece o planejador oficial. A geometria do provedor pode ser exibida em Google Maps mediante chave de navegador apropriada.
- `/admin/lugares/{id}/visita`: horários por dia, múltiplos turnos, exceções, setores, fechamento provisório, reserva, última entrada, entrada/meia-entrada, condições, fonte, validade, concorrência e histórico. Complexo e setores têm horários distintos.
- `VisitRecommendationService`: abertura verificada na data escolhida, exceções e turnos que atravessam meia-noite. Fonte vencida ou setor desconhecido resulta em horário não confirmado. O custo total fica incompleto quando ida, entrada ou volta são desconhecidas.
- `/guia-ia`: pesquisa em conteúdo publicado/verificado, lugares publicados e linhas verificadas. Com consentimento e provedor habilitado, a IA ordena somente IDs fornecidos. O texto exibido permanece o fato cadastrado com seus links; respostas livres da IA não viram horários, tarifas ou notícias.
- `/explore/planejar`: meio dia e 1/2/3/5 dias, categorias, orçamento, público infantil, acessibilidade e gratuidade. Só inclui visita com horário confirmado e trecho calculado. A duração da visita é escolhida pelo visitante. Retorno e valores desconhecidos impedem confirmar orçamento.
- Preferências de idioma e acessibilidade persistem nos claims existentes do Identity, com auditoria. Formulários numéricos interpretam decimais HTML sem transformar `1.50` em `150` sob `pt-BR`.
- Novos componentes usam o design existente da sprint 14 e recursos para sete idiomas; ícones SVG autorais de ônibus, BRT, caminhada e rota complementam barco e cidade.
- Corrigidos slugs de novos lugares: nomes iguais não unem diferentes IDs de provedor. URLs já existentes são preservadas. Também foram adicionadas as views ausentes de fluvial e alertas; alertas futuros/vencidos não entram como ativos.

## Fontes efetivamente consultadas

| Fonte | Resultado e limitação |
|---|---|
| [Theatro da Paz](https://www.theatrodapaz.com.br/programa%C3%A7%C3%A3o) | Consulta pontual com `robots.txt` e extração JSON-LD pelo código do projeto em duas páginas. Dois registros extraídos; um futuro: FIDA em 21/10/2026, 09h local. Persistência, deduplicação e encaminhamento à revisão testados em banco descartável; zero publicações. |
| [Estação das Docas](https://estacaodasdocas.com/) | Site/programação consultados; direitos de imagens e coleta contínua não presumidos. |
| [Mangal das Garças](https://mangaldasgarcas.com.br/) | Há informações separadas de funcionamento e espaços pagos. A gratuidade de toda a visita não foi presumida. |
| [Museu Goeldi](https://www.gov.br/museugoeldi/pt-br/a-instituicao/visitacao/informacoes-sobre-ingressos-e-horarios) | Página contém calendário 2025; há divergência com outra página institucional de visitação. Não foi convertida em confirmação automática para 2026. |
| [SEGBEL/SEMOB](https://seguranca.belem.pa.gov.br/servicos-semob/) | Link público de itinerários aponta para PDF em caminho de 2021. Sem confirmação atual, nenhuma linha foi importada como vigente. |
| [Integra Belém](https://seguranca.belem.pa.gov.br/integra-belem/) | Texto de implantação de 2021 e tarifas antigas; não reutilizadas como tarifas de 2026. |
| [Google Routes](https://developers.google.com/maps/documentation/routes/transit-route) | Contrato técnico consultado. Tarifas só aparecem quando o provedor as fornece; não foram inventadas tarifas ou posições de ônibus. |

As seis fontes locais foram cadastradas **inativas e não aprovadas**, com uma verificação diária como perfil inicial. Cadastro não significa autorização de coleta. Não foi encontrado e homologado feed GTFS-Realtime/SIRI autorizado nesta execução. A interface informa a indisponibilidade do rastreamento.

Evidências factuais da consulta e do teste descartável: `docs/evidencias/sprint15/official-probe.json` e `disposable-ingestion.json`. Não contêm cópia de artigos ou imagens.

## Banco e contas reais locais

Auditoria do banco local MariaDB 10.11.7: **71 tabelas, 0 matérias, 0 fontes operacionais, 5 linhas legadas, 0 paradas e 0 travessias**. As linhas foram preservadas; valores sem fonte e validade não são apresentados como confirmados. Não houve importação fictícia de atrações ou linhas.

As três contas solicitadas existem no MySQL local, criadas pelo `UserManager`: `superadmin@minhabelem.com.br` (SuperAdmin), `editor.teste@minhabelem.com.br` (Editor) e `moderador.teste@minhabelem.com.br` (Moderator). Login HTTP e perfil passaram para as três; acesso ao painel foi redirecionado para troca obrigatória da senha. MFA precisa ser configurado no primeiro uso e continua obrigatório para operações privilegiadas.

O bootstrap é explícito, restrito a Windows/Development e MySQL local. Não altera nem reativa contas existentes. Credenciais aleatórias foram disponibilizadas somente em relatório local com ACL protegida, herança removida e uma regra para o usuário proprietário. A validade é de 24 horas; o visualizador recusa/apaga relatório expirado e consome o arquivo antes de exibi-lo, sem imprimir senhas no console.

Comandos locais:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project src/MinhaBelem.Web -c Release -- --initialize-database
dotnet run --project src/MinhaBelem.Web -c Release -- --bootstrap-homologation
dotnet run --project src/MinhaBelem.Web -c Release -- --register-researched-sources
```

O caminho do relatório é informado apenas pelo comando local. Para leitura única, executar `scripts/windows/show-homologation-credentials.ps1 -ReportPath <caminho-local>`. Nenhuma senha ou token foi colocado no SQL, nas evidências ou no Git.

Migrations aditivas em `MINHABELEM.SQL`: `sprint15_editorial_identity`, `sprint15_media_library`, `sprint15b_mobility_provenance`, `sprint15_place_media`. Reutilizam Identity, catálogo, linhas, paradas, travessias e alertas existentes. A correção de idempotência da migration inédita foi feita antes da publicação; o checksum local de desenvolvimento foi reconciliado de forma restrita à versão ainda não lançada.

## Validação e limites operacionais

O [Actions 39125102683](https://github.com/devmnsoft/minhabelem/actions/runs/39125102683), commit `93277dc9ad84e71bae289d1d43166b64dec635e3`, passou em Windows, Ubuntu e MySQL/HTTP. A suíte completa aprovou **154 testes, zero falhas e zero ignorados**, com OCR e processamento de vídeo reais. Inclui a troca obrigatória de senha por HTTP: reutilizar a senha temporária mantém o bloqueio; uma senha diferente libera a etapa, invalida a antiga e encerra a sessão. Builds sem avisos ou erros. As migrations também foram aplicadas e reexecutadas no banco descartável preservando suas estruturas.

Validações locais anteriores à última correção: suíte completa com 149 aprovados e dois casos de OCR ignorados por falta dos executáveis locais; 28 verificações focadas de HTTP, rotas, horários, culturas e decimais aprovadas; uma verificação HTTP adicional de rótulos acessíveis aprovada. O OCR real foi executado no CI. Resumo sem segredos em [tests-local.json](evidencias/sprint15/tests-local.json).

A matriz visual passou **1.456 cenários**: 52 páginas × sete idiomas × quatro tamanhos (1440, 1280, 768 e 390 px). Zero estouros horizontais, imagens quebradas, imagens sem atributo `alt`, botões sem nome ou campos sem rótulo nas verificações implementadas. Menu móvel abriu e fechou por Escape nos sete idiomas. Esse resultado cobre verificações específicas e não equivale a uma certificação integral de acessibilidade. As capturas usam HTML renderizado pelo MVC com dados de teste em banco descartável; não representam conteúdo publicado no banco local. Resultado detalhado: [layout-results.json](evidencias/sprint15/layout-results.json).

| Página | Desktop | Celular |
|---|---|---|
| Como chegar | [Captura](evidencias/sprint15/como-chegar-desktop.jpg) | [Captura](evidencias/sprint15/como-chegar-mobile.jpg) |
| Lotes editoriais | [Captura](evidencias/sprint15/editorial-lotes-desktop.jpg) | [Captura](evidencias/sprint15/editorial-lotes-mobile.jpg) |
| Biblioteca de mídias | [Captura](evidencias/sprint15/admin-midias-desktop.jpg) | [Captura](evidencias/sprint15/admin-midias-mobile.jpg) |
| Planejamento de roteiro | [Captura](evidencias/sprint15/explore-planejar-desktop.jpg) | [Captura](evidencias/sprint15/explore-planejar-mobile.jpg) |
| Guia IA | [Captura](evidencias/sprint15/guia-ia-desktop.jpg) | [Captura](evidencias/sprint15/guia-ia-mobile.jpg) |

Trajetos efetivamente calculados em provedor real nesta execução: **nenhum**, pois Google Routes não está configurado. Tarifas e horários atuais confirmados e importados: **nenhum**. Há cinco linhas legadas preservadas, sem promovê-las a informação atual verificada. As rotas com baldeação, exceções de horário, custos parciais, fontes vencidas e falhas de integração foram exercitadas com cenários controlados; não representam homologação de operadores de transporte.

Não houve implantação em produção. O serviço Windows local não foi instalado por esta execução. A telemetria local ainda mostra dois ciclos antigos e zero páginas no histórico do Radar; a consulta pública controlada do Probe não foi apresentada como operação contínua do Radar.

Pendências externas: credenciais/produtos contratados e homologação real do Google Routes/Maps; feeds autorizados e atualizados de mobilidade/posição, tarifas e travessias; confirmação das divergências de horários; autorização contínua das agendas; clima, maré e lotação. Sem essas integrações, o sistema mostra indisponibilidade ou links oficiais.

Pendências de acabamento: geração de variantes/miniaturas otimizadas no servidor, acervo regional real licenciado e revisão integral dos textos legados que ainda estão em português. O hero da sprint 14 continua identificado como ilustração gerada.

Próxima etapa recomendada: autorizar fontes e quotas, confirmar os dados com os operadores, instalar o Worker em homologação e acompanhar telemetria de coleta contínua antes de habilitar publicação ou promover para produção.
