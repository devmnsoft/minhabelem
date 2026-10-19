# Sprint 25 — auditoria e marcos de aprovação

19/10/2026, America/Sao_Paulo. Baseline `829820a`, `main` sincronizada com `origin/main` por fetch, árvore inicialmente limpa. Trabalho em `feature/sprint-25-identidade-navegacao`. Sem deploy ou alterações no banco do produto.

Os relatórios disponíveis das Sprints 14, 15/15B, 17, 18, 19, 20, 22, 23 e 24 foram consultados. Não existem relatórios independentes 16 e 21 neste checkout. A evolução preserva fila Web/Worker, aprovação humana, Agenda 365, horários verificados, motor Curió e RBAC/MFA. Os relatórios 20/22 declaram homologação integral; o código atual ainda apresenta divergências verificáveis abaixo. Relatos históricos não são aceites desta sprint.

O inventário inicial em `evidencias/sprint25/initial-inventory.json` enumera todas as Views, controllers/atributos de rota e folhas CSS. Inclui páginas públicas, conta, editorial e administração. Serviços e repositórios permanecem nas camadas Core/Infrastructure: TransitRepository, GoogleRoutePlanner, VisitRecommendationService, NativeBelemAiEngine, TouristAssistantService, ContentRepository e fluxo EditorialBatch. Nenhum será substituído por demonstrações.

| Funcionalidade | Anteriormente disponível | Situação inicial / problema | Correção prevista | Teste de aceite |
|---|---|---|---|---|
| Home / busca / notícias | S14–24 | Muitas entradas repetidas, conteúdo sem hierarquia; mesmas fotos em janelas e galeria | Composição editorial com busca e acessos principais, sem duplicar mídia | MVC real; desktop/celular; busca preservada |
| Menu mobile | S14–24 | JS aplica `.is-open`; layout exige `body.menu-open` | Um controlador e um estado, foco e Escape | Abrir Mais, percorrer links, fechar e restaurar foco |
| Navegação desktop | S22–24 | Mais depende de hover/foco sem sincronizar aria-expanded | Disclosure com botão e estado explícito | Mouse, toque e teclado |
| Tema / contraste | S20–24 | Tema claro remove atributo e reativa preferência escura; busca branca herda texto escuro/claro; token `--color-white` ausente | Tokens semânticos; escolha explícita e persistente | Claro/escuro com preferência do sistema oposta; axe |
| CSS | S20–24 | Layout carrega quatro folhas diretamente e site.css importa as mesmas; imports com layers aninhadas; evolution.css não é carregada apesar do relatório S24 | Um ponto de entrada e responsabilidades existentes | Rede sem duplicação; regressão das páginas consumidoras |
| Agenda / eventos | S15–24 | Calendários, filtros, favoritos e publicação existem | Preservar consulta e aprovação em lote | Jornadas C/F no Marco B/C |
| Ônibus / BRT / linhas / paradas | S24 | Catálogo SQL documenta 58 linhas, 664 pontos e 16 terminais; 27 alimentadoras sem geometria | Preservar catálogo; acesso direto pela Home | Conferir base descartável e navegação no Marco B |
| De-Para / Como Chegar | S15–24 | Dois serviços complementares existentes; rotas externas dependem de integração | Conservar endpoints e fallback transparente | Origem → destino → linha/paradas no Marco B |
| Fluvial | S24 | Duas travessias; Home promete Mosqueiro sem registro correspondente | Retirar promessa de cobertura não cadastrada | Embarque, volta, fonte e validade no Marco B |
| Tarifas / horários | S15–24 | Valores históricos e validades não constituem confirmação em 19/10/2026 | Auditar fonte canônica e vigência no Marco B | Expirado nunca apresentado como vigente |
| Guia / visita / roteiros | S15–24 | Serviços de recomendação e planejamento existentes | Preservar e evidenciar informações práticas | Jornada B no Marco B |
| Curió | S24 | Motor nativo com contexto de transporte; nuvem depende de configuração | Acesso contextual sem widget cobrindo conteúdo | Resposta e links reais no Marco B |
| Bairros / comunidade / parceiros | S14–24 | Endpoints, moderação e anúncios existentes | Preservar e manter descoberta | Rotas e formulários no Marco C |
| Vídeos / fotos | S23–24 | Home repete acervo já usado nas janelas; galeria sem créditos individuais | Acervo único com atribuição por foto | Mídia sem duplicação e links acessíveis |
| Conta / administração | S14–24 | Layout administrativo próprio já existe; cabeçalho público ainda aparece | Preservar RBAC, MFA, auditoria e sidebar | Jornada F e revisão do shell no Marco C |

Evidências anteriores disponíveis: `evidencias/sprint14`, `sprint17`, `sprint18`, `sprint20/before`, `sprint20/after` e `sprint22`. Não foram anexadas capturas adicionais do operador ao pedido. As novas capturas iniciais usam HTML MVC renderizado do baseline em banco aleatório descartável, sem copiar dados reais.

Verificação inicial sem banco: 160 aprovados, 67 ignorados, zero falhas. A integração local usa a fixture existente, que restringe o host a localhost e cria/remove somente seu banco aleatório. Credenciais permanecem em memória.

Pipelines: a consulta inicial por SHA curto devolveu lista vazia. A consulta posterior da API de Actions confirmou o [baseline main aprovado](https://github.com/devmnsoft/minhabelem/actions/runs/39325828484), com quatro jobs bem-sucedidos: Windows, Ubuntu, MySQL 8 e MariaDB 10.11. Foram conferidas as 12 execuções recentes, registradas em `evidencias/sprint25/recent-pipelines.json`. A execução `39308808758`, de `5dcd6b6`, falhou nos testes MariaDB e cancelou MySQL; os builds passaram. Isso não representa falha do baseline atual nem CI desta branch local.

Comparação visual: a captura desktop da Sprint 14 apresenta ilustração de IA, navegação com nove entradas e título sobre a imagem; o baseline atual apresenta fotografia licenciada, header com quebra de linha e cinco chips mais um bloco grande de mobilidade. O Marco A conserva a marca e a fotografia documental, separa leitura e imagem, reduz a navegação principal a cinco destinos e elimina o indicador público sem dados atuais.

O Marco A entrega Home, identidade, navegação e fundamentos CSS para revisão visual. A aprovação do operador é exigida pela seção 18 do pedido antes da propagação nos Marcos B/C. Compilação, overflow e axe não substituem essa aprovação. Esta sprint não será declarada concluída ao entregar o Marco A.
