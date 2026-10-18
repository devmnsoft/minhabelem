# Sprint 17 — execução e limites de homologação

Data: 18/10/2026 (America/Sao_Paulo). Base: `a5654bcf1ba717b7f88377689b08ae8953dc6891`, `main`, sincronizada por `git fetch origin`. O estado inicial tinha alterações do usuário nos dois `appsettings.json`; elas foram preservadas e não integram os commits desta entrega.

**Situação: entrega funcional incremental; a Sprint 17 integral não está homologada.** Não houve deploy, mudança de DNS, ativação de fontes contínuas ou acesso ao banco de produção. Nenhum percentual de conclusão é atribuído.

## 1. Estado inicial e divergências

Foram lidos os relatórios das Sprints 12, 14 e 15/15B, os testes, os hosts Web/Worker, os contratos, os repositórios e as Views. O [inventário](evidencias/sprint17/inventory.json) enumera os arquivos por camada. A suíte inicial tinha 163 casos: 120 aprovados e 43 ignorados sem configuração de integração. Com credenciais transmitidas em memória para uma base aleatória descartável, foram 161 aprovados e dois ignorados por falta de configuração OCR.

A base local configurada em **User Secrets**, consultada sem imprimir credenciais, era MariaDB **10.11.7**, com **71 tabelas**, zero conteúdos, zero fontes operacionais, cinco linhas legadas, zero paradas e travessias, zero das três contas de homologação. O contador antigo do Radar tinha 11 ciclos e nenhum histórico de páginas consultadas. Esses números não comprovam coleta contínua. A base auditada não contém os 32 registros/sessões e as três contas mencionados no relatório anterior; não foram recriados nem importados automaticamente.

O relatório da Sprint 12 apresenta módulos como operacionais em produção com evidências de build/código. Essa classificação não é sustentada pela auditoria desta execução. As ressalvas das Sprints 14 e 15/15B continuam aplicáveis.

## 2. Implementações e correções

- Radar: histórico persistente dos últimos 30 ciclos, horários de início/fim, situação, fontes consultadas e falhas, matérias/eventos, duração, recuperação de ciclos interrompidos, taxa por fonte (indisponível quando não há fonte consultada), próximo ciclo persistido e alertas de heartbeat e falhas sucessivas. Métricas reais de fontes habilitadas/aprovadas/consultadas, publicações e custo de IA registrado em USD. Telemetria do processo não comprova instalação como Windows Service.
- Crawler: respostas 304 passam a ser auditadas, preservando ETag/Last-Modified/hash; conteúdo que excede o limite durante a leitura passa a produzir erro explícito.
- Agenda: evidências preservam a sequência A → B → A; a volta ao estado anterior não deixa a evidência de B como se fosse atual. Repetição do estado corrente continua idempotente. A importação factual existente admite janela de até 365 dias, preservando validação de cada registro. O extrator heurístico deixou de assumir 19h quando só existe data: marca DateOnly, horário a confirmar e revisão obrigatória. Cancelamento durante consulta também passa a contar como falha de fonte nas métricas do ciclo.
- Editorial: histórico dos últimos 30 lotes do próprio usuário; progresso e resultados atualizados por GET autenticado sem cache, mantendo isolamento por proprietário; atalhos Alt+Shift+A e Escape, contagem e estado intermediário da seleção. Aprovação/publicação existentes, permissões, MFA e revisão humana foram reutilizados.
- Meu Dia: quantidade de visitantes de 1 a 20, custos de entrada e transporte coletivo calculados para o grupo, sem descontos presumidos; compartilhamento de nomes, links públicos e horários das paradas, sem origem ou campos privados do formulário. Custos desconhecidos continuam sem total confirmado.
- Comunidade: relatos sobre conteúdo público entram na fila privada de moderação existente, com consentimento, antiforgery e limite de solicitações. Rascunhos não podem ser denunciados por esse endpoint público. Relatos não são publicados automaticamente.
- Comercial: o formulário que antes apenas mostrava uma mensagem agora persiste solicitações com consentimento. Administradores autorizados consultam e marcam atendimento, com auditoria e sem cache. Cadastro/edição de campanhas, autorização de uso, URLs verificadas, período por dia de Belém e veiculação em Home/Parceiros identificada como publicidade. Atualizar campanha mantém o ID. Não foram criados cliques, impressões, preços de planos ou métricas de audiência.
- Design: fotografia real da Estação das Docas substitui a ilustração na Home; três variantes WebP, crédito e licença visíveis. Ajuste de quebra de textos longos na administração. Novos componentes e controles possuem traduções nos sete idiomas. A página comercial foi reescrita integralmente nesses idiomas, retirando promessas antigas de métricas, segmentação e integração não homologadas.
- Repositório: 165 arquivos gerados de `node_modules` removidos apenas do índice Git; instalação local, package.json, package-lock.json e scripts preservados. A regra de ignore já existia.
- Ferramentas: auditoria local e execução de testes/probe em base descartável usam User Secrets, sem imprimir nem inserir credenciais na linha de comando. CI ampliado com matriz MySQL 8/MariaDB 10.11; execução remota desta alteração ainda não observada.

## 3. Fontes e eventos reais

O catálogo pesquisado contempla SECULT Pará, FUMBEL institucional, Fundação Cultural do Pará, SEGBEL/SEMOB, Integra Belém, Estação das Docas, Mangal, Goeldi e Theatro da Paz. O cadastro é idempotente, inativo e não aprovado; frequência inicial de 1.440 minutos e limite de uma requisição/dia por fonte. Cadastro público **não equivale a autorização de coleta contínua**, nem a licença de mídia. O Probe verifica robots antes de cada consulta pontual.

Foram consultadas duas páginas do Theatro da Paz: uma sessão passada de Sonho Encantado de Cordel e o FIDA de 21/10/2026. No banco descartável: dois registros inseridos, duas repetições deduplicadas, um evento futuro, zero publicações. No banco local: **um candidato futuro encaminhado à revisão editorial**, sem importar rascunho/publicar ou ativar a fonte. Uma segunda consulta ao banco local inseriu zero candidatos e reconheceu um registro sem mudanças ([idempotência local](evidencias/sprint17/local-review-idempotency.json)). Evidências: [consulta](evidencias/sprint17/official-probe.json), [deduplicação descartável](evidencias/sprint17/disposable-ingestion.json), [encaminhamento local](evidencias/sprint17/local-review.json).

Fontes oficiais pesquisadas: [SECULT Pará](https://www.secult.pa.gov.br/), [FUMBEL institucional](https://secult.belem.pa.gov.br/institucional/sobre-a-fumbel/), [FCP — agendas](https://www.fcp.pa.gov.br/agendasculturais), [SEGBEL — serviços](https://seguranca.belem.pa.gov.br/servicos-semob/), [Mangal](https://mangaldasgarcas.com.br/). Foram cadastradas oito fontes novas, além da fonte do Theatro já encaminhada pelo Probe: nove no total, todas inativas/não aprovadas. Nenhuma autorização contínua foi obtida nesta execução. Nenhuma mudança real de data/preço/local foi observada entre consultas; cancelamento, alteração e reversão foram exercitados com fixtures identificadas.

## 4. Mobilidade e guia

GoogleRoutePlanner, consultas De-Para, filtros, mapa condicionado à integração e fallback oficial foram preservados. **Nenhuma rota Google real foi homologada; nenhuma tarifa ou horário de transporte foi importado como vigente.** As cinco linhas legadas não foram promovidas a vigentes. [Informações fluviais municipais de julho](https://agencia.belem.pa.gov.br/confira-os-horarios-do-geladao-fluvial-para-mosqueiro-e-cotijuba/) não foram convertidas em garantia de operação em outubro.

Horários, exceções, fechamento, última entrada, reservas, preços e custo parcial já têm contratos, persistência, administração e testes. O site oficial do Mangal apresenta horários e preços de setores, mas não foi produzido um vínculo novo com um identificador de lugar Google nem sobrescrito cadastro existente sem esse vínculo. Não houve importação desses valores. Clima, trânsito, maré e lotação não foram homologados. O planejador conserva a resposta indisponível quando faltam evidências e rotas, e não promete atrações abertas ou orçamento completo sem confirmação.

## 5. Acervo e revisão visual

Fotografia: [Estação das Docas Belém Pará 2023-8696.jpg](https://commons.wikimedia.org/wiki/File:Esta%C3%A7%C3%A3o_das_Docas_Bel%C3%A9m_Par%C3%A1_2023-8696.jpg), Prburley (Paul Burley), captura de 17/08/2023, [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Alterações: redimensionamento proporcional, compressão WebP e remoção de metadados; recorte por CSS. Arquivos de 640/1280/1920 px com aproximadamente 41/133/257 KB. [Manifesto de créditos](../src/MinhaBelem.Web/wwwroot/images/licensed/credits.json). Não há inferência de endosso. Isso inicia o acervo e não corresponde à biblioteca fotográfica completa de todas as atrações.

A automação usa HTML realmente renderizado por MVC em banco descartável, sem autenticação ou banco na etapa de snapshots. Verifica quatro resoluções (1440, 1280, 768 e 390 px), sete idiomas, largura, imagens, nomes de botões e rótulos. A primeira passagem de 1.456 combinações encontrou sete estouros no celular em Anunciantes; a quebra de textos foi corrigida e a página passou novamente nas quatro resoluções. Depois da reescrita comercial, essa página foi revalidada nas 28 combinações de idioma/resolução e seus registros substituíram os anteriores na matriz. A passagem final está registrada em [layout-results.json](evidencias/sprint17/layout-results.json). Capturas são evidências de layout com fixtures, não de conteúdo publicado na base local. Não equivalem a certificação WCAG ou auditoria humana integral de cada formulário.

## 6. Banco, contas e validação

Três migrations aditivas no final de `MINHABELEM.SQL`, sem modificar o conteúdo das anteriores: `sprint17_radar_operations`, `sprint17_discovery_observations` e `sprint17_commercial_inquiries`. Duas tabelas novas, uma coluna de agendamento e troca do índice único de versões por índice não único para registrar reversões. Aplicação local: o histórico de migrations não estava registrado; o instalador idempotente registrou também as anteriores. Reexecução seguinte: nenhuma migration pendente. A base local passou a 73 tabelas. A auditoria final encontrou nove fontes, um candidato pendente e zero eventos publicados. O contador agregado antigo chegou a 28 ciclos, sem histórico de páginas; a variação não é evidência de coleta real nem de execução na VPS. Não houve alteração no banco de produção nem teste destrutivo na base principal.

As contas `superadmin@minhabelem.com.br`, `editor.teste@minhabelem.com.br` e `moderador.teste@minhabelem.com.br` não existem nessa base. Nenhuma senha foi criada/redefinida, nenhum novo bootstrap foi executado. Login/MFA/RBAC, rotação obrigatória, revogação e isolamento foram exercitados nas contas temporárias das fixtures descartáveis; isso não homologa logins das três contas ausentes.

As execuções de teste, build e verificações de interação estão resumidas em [validation.json](evidencias/sprint17/validation.json). A suíte completa passou em **178 testes de 179**, com zero falhas e um caso de vídeo ignorado. Build Release: zero avisos/erros. A matriz completa passou em **1.456 combinações**, e três interações de navegador passaram com fixtures controladas. A fixture também verifica que o host de testes aponta para a base aleatória criada por ela antes de executar os casos. OCR de cartaz foi exercitado com Tesseract real instalado; o caso de vídeo depende de FFmpeg e vídeo configurados. O primeiro build Debug encontrou DLLs em uso pelo Visual Studio; os testes posteriores usaram Release e a sessão de desenvolvimento foi preservada.

## 7. GitHub Actions e commits

O [Actions do commit inicial](https://github.com/devmnsoft/minhabelem/actions/runs/39139844239), SHA `a5654bcf1ba717b7f88377689b08ae8953dc6891`, estava concluído com sucesso na consulta. O run mais recente listado, [39139863193](https://github.com/devmnsoft/minhabelem/actions/runs/39139863193), também estava concluído com sucesso, no SHA `6972cc26367351ee6c2adaf54b9efce40cab6b0c`. Esses resultados são anteriores aos commits desta entrega. Não houve push nem execução remota homologada dos novos commits. A API do conector retornou lista vazia por filtrar runs de pull request; a API pública de Actions foi usada para conferir os cinco últimos runs.

Os commits desta entrega constam de `git log` e da seção de entrega ao final deste arquivo. Os dois appsettings modificados pelo usuário permanecem fora dos commits.

## 8. Matriz de funcionalidades e pendências

| Fase | Evidência implementada/testada | Trabalho restante / condição externa |
|---|---|---|
| 0 Auditoria | Fetch, relatórios, inventário, banco, testes e CI anterior | Auditoria SQL/Dapper integral por consulta e restore real ainda pendentes |
| 1 Radar | Worker separado, fila existente, histórico/cadência/alertas novos | Autorizar fontes e observar operação real contínua na VPS |
| 2 Agenda 365 | Filtros existentes, janela anual de importação, reversões e um candidato real | Mais conectores/fontes autorizados, novas sessões e atualização real contínua |
| 3 Editorial | Lotes existentes, histórico e progresso novos, proteção por proprietário | Prévia rápida/duplicidades mais rica, revisão completa da UX e dos erros legados |
| 4 Mobilidade | De-Para, fallback e proveniência existentes, testes preservados | Credenciais Routes, itinerários/paradas/tarifas atuais e fluvial homologados |
| 5 Guia | Horários/custos por evidência existentes | Alimentação de lugares verificados, clima/trânsito e revisão humana das recomendações |
| 6 Meu Dia | Grupo e compartilhamento privado novos; horários e custos preservados | Integrar eventos/restaurantes/pausas ao roteiro e homologar rotas reais |
| 7 Design | Foto real licenciada, variantes, ajustes e matriz visual | Acervo de mais atrações, revisão visual individual de todos os estados e identidade completa |
| 8 Idiomas | Novos controles nos sete idiomas; HTTP nos sete | Revisão humana integral e localização das strings legadas |
| 9 Admin | Radar, lotes e comercial evoluídos | Redesign completo das demais páginas e analytics autorizado |
| 10 Monetização | Solicitação persistida, atendimento, criação/edição/veiculação de campanhas | Segmentação, limites de exibição, cupons e métricas reais com consentimento |
| 11 Comunidade | Moderação existente e relatos privados novos | Favoritos universais, histórico pessoal de contribuições e mais serviços por bairro |
| 12 Banco | Migrations aditivas, testes descartáveis e reexecução local | MySQL 8 remoto/CI novo, auditoria integral de schema, backup/restore real |
| 13 Contas | Ausência constatada sem alteração de senhas | Novo bootstrap local explicitamente autorizado e homologação das contas reais |
| 14 Limpeza | Dependências geradas desversionadas, lockfile mantido | Revisão contínua de artefatos |
| 15 Testes | Release, integração MariaDB/HTTP/segurança/OCR e Playwright | Vídeo FFmpeg, acessibilidade assistiva e integrações externas reais |
| 16 VPS | Scripts existentes e roteiro abaixo | Acesso/autorização de implantação e observação contínua |
| 17 Relatório | Evidências e matriz rastreáveis | Não declarar Sprint integral concluída enquanto os critérios externos e locais faltarem |

## 9. Roteiro de homologação na VPS (sem execução)

1. Identificar servidor e banco de homologação e obter autorização explícita antes de deploy, DNS ou migrations em produção.
2. Validar backup e restore em base separada, Hosting Bundle .NET 8, IIS, certificado HTTPS/domínio e conectividade MySQL.
3. Revisar `scripts/setup-windows-vps.ps1`, `scripts/deploy-iis.ps1` e `scripts/manage-worker-service.ps1`; confirmar diretórios e rollback antes de executar.
4. Configurar credenciais protegidas, Data Protection compartilhado conforme os hosts, ACLs NTFS mínimas, identidade e logs do Windows Service. Nunca versionar chaves ou secrets.
5. Aplicar migrations aprovadas e publicar o pacote validado, com health check HTTPS e rollback disponível.
6. Cadastrar e aprovar fontes apenas com política/robots/licença/cota documentadas; manter revisão humana para publicação.
7. Observar por período acordado os ciclos, heartbeat, pedidos manuais, consultas reais, deduplicação, IA e custos; revisar/publicar apenas os itens elegíveis.
8. Exercitar interrupção/reinício controlado, recuperação da fila, falhas sucessivas, indisponibilidade do banco/provedor e alertas. Guardar evidências sem conteúdo privado.
9. Só classificar como operacional na VPS depois desses resultados. Repetir backup/restore e acompanhar logs/monitoramento após atualizações.

## 10. Próximas entregas sugeridas

- Homologação operacional: fontes autorizadas, contas reais e observação do Radar em ambiente disponível.
- Mobilidade e visitas: produtos/quotas Google configurados e fontes atuais confirmadas com operadores; alimentação de horários/preços por lugar.
- Experiência integrada: eventos e alimentação no roteiro, histórico de contribuições e favoritos universais.
- Design e idiomas: acervo licenciado ampliado, revisão de todas as Views/estados e textos legados, validação assistiva.
- Comercial: segmentação, limites e relatórios somente depois de implementar medição autorizada.

## 11. Entrega registrada

| Commit | Entrega |
|---|---|
| `adc5210` | Dependências geradas removidas do controle de versão |
| `dfb59b7` | Radar, agenda, editorial, roteiros, comunidade, campanhas, foto licenciada e testes |
| `52905a3` | Interações Playwright e matriz MySQL/MariaDB no CI |

Este relatório e os arquivos de evidência são registrados em um commit documental posterior, identificável por `git log`. A entrega não foi enviada ao remoto.

Reprodução local, a partir da raiz, com User Secrets já configurado (nenhuma credencial no comando):

```powershell
dotnet build MinhaBelem.sln -c Release
$env:OCR_TEST_EXECUTABLE = 'C:\Program Files\Tesseract-OCR\tesseract.exe'
dotnet run --project scripts/MinhaBelem.Verify -c Release -- --test-disposable
node scripts/visual-qa/sprint17-interactions.mjs
```

Para gerar novamente os snapshots, definir `MINHABELEM_VISUAL_SNAPSHOTS` com um diretório local antes dos testes. O script visual usa `QA_SNAPSHOTS` para localizar esse diretório. Os testes criam/removem somente sua base aleatória; o comando de auditoria sem opções é somente leitura. Os comandos de migration/cadastro do utilitário são operações explícitas separadas e não fazem parte do teste padrão.
