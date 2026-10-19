# Sprint 25 — Marco A para aprovação visual

19/10/2026, America/Sao_Paulo. Branch `feature/sprint-25-identidade-navegacao`. **Marco A implementado; aprovação visual pendente. A Sprint 25 integral permanece em andamento.**

## Entrega concreta

- Home reconstruída sobre os repositórios existentes: pergunta principal, busca, hoje/agenda/Como Chegar/guia, Curió, notícias, eventos, lugares, roteiros, central de mobilidade, gastronomia, bairros, acervo, vídeos, parceiros e colaboração. Estados sem dados ficam explícitos; não foram inseridas notícias ou programações no produto.
- Fotografia licenciada da Estação das Docas com autor, fonte e licença; texto e imagem em superfícies distintas. Marca, favicon e identidade do Curió preservados. Fotos do acervo aparecem uma vez, com atribuição individual. Galeria abre por teclado e mantém links de crédito independentes.
- Navegação principal com Início, Agenda, Explorar, Rotas e Mais. O menu usa um controlador para cabeçalho e barra inferior, sincroniza `aria-expanded`, funciona por toque/teclado e devolve foco ao fechar. A gaveta permanece no fluxo, inclusive em orientação horizontal.
- Busca disponível no cabeçalho das páginas públicas. Curió acessível na abertura e no menu; o botão flutuante que disputava espaço com a navegação foi retirado.
- Tema claro explícito prevalece sobre sistema escuro e persiste após recarregar. Tokens canônicos para branco, links, campos, foco e superfícies. Datas, fontes, botões de contorno e links legíveis em ambos os temas.
- Painel de acessibilidade recuperado: seletor de abertura coerente com o JavaScript, classes dos controles correspondentes à View, isolamento do fundo, navegação por Tab e devolução do foco. Movimento reduzido corrigido em CSS válido.
- `site.css` é o ponto de entrada: importa tokens, base, layout, componentes e acessibilidade uma vez, respeitando as layers já declaradas nos arquivos. Regras de acessibilidade repetidas foram removidas de `site.css`; não foi criada uma nova folha cumulativa. Folhas legadas sem migração completa permanecem no repositório para a auditoria dos consumidores nos próximos marcos.
- A consulta da capa agora exclui eventos do bloco de notícias. A busca continua encontrando notícias e eventos. O teste descartável comprova que eventos em revisão não aparecem publicamente.

## Evidências e limites do método

Auditoria e matriz de rastreabilidade: [SPRINT-25-AUDITORIA.md](SPRINT-25-AUDITORIA.md). Inventário de Views/controllers/CSS e histórico de pipelines em `evidencias/sprint25`.

Capturas iniciais foram produzidas antes das alterações com HTML real de MVC e sessão MFA de teste em banco descartável; as capturas finais da Home usam visitante anônimo. Essa diferença de sessão explica a faixa administrativa presente nas capturas iniciais. A imagem histórica da Sprint 14 também foi comparada, mas não substitui o baseline desta execução.

As páginas são renderizadas pela aplicação .NET em banco local aleatório, criado e removido pela fixture existente. Playwright serve esse HTML e os assets locais via HTTP. Esse servidor de capturas não executa POST, Google Routes, provedores de IA, publicação editorial ou navegação dinâmica entre endpoints da aplicação. Os testes HTTP verificam separadamente os destinos reais.

Fixtures de notícia/evento são identificadas como testes descartáveis. A captura `home-data.html` não comprova programação real nem deve ser usada como conteúdo de produção. Nenhuma credencial, cookie ou token de autenticação integra as evidências versionadas.

Capturas principais:

- [Antes — desktop](evidencias/sprint25/before/home-desktop.jpg) e [antes — celular](evidencias/sprint25/before/home-mobile.jpg).
- [Depois — desktop claro](evidencias/sprint25/after/home-1440-light.jpg) e [desktop escuro](evidencias/sprint25/after/home-1440-dark.jpg).
- [Depois — celular claro](evidencias/sprint25/after/home-390-light.jpg), [celular escuro](evidencias/sprint25/after/home-390-dark.jpg) e [menu aberto](evidencias/sprint25/after/menu-390-dark.jpg).
- Versões de página inteira, 360 px e tablet estão na mesma pasta.

## Verificação

O resultado final consolidado é registrado em `evidencias/sprint25/validation-summary.json`. Relatórios detalhados: `after/results.json`, `regression/results.json`, `landscape/results.json` e `text-200/results.json`.

| Verificação | Resultado |
|---|---|
| Compilação Release | Sucesso, sem erros ou avisos |
| Suíte .NET completa com MariaDB descartável | 229 aprovados, zero falhas, dois OCR ignorados por ausência dos executáveis |
| Home: sete idiomas × sete larguras × dois temas, mais estados abertos | 101 verificações, zero falhas; 49 casos de navegação e persistência de tema |
| 44 páginas MVC adicionais, incluindo Home com dados, em dois temas | 88 verificações, zero falhas de layout/imagens; axe adicional na Home com dados |
| Orientação 844 × 390 | Duas verificações, zero falhas |
| Texto ampliado a 200%, incluindo estados abertos | Quatro verificações, zero falhas |
| Revisão final de ícone Curió no escuro e barra inferior sobre o menu | Sete verificações repetidas, zero falhas; relatório `final-review/results.json` |

As capturas principais de 390/1440 px e menu foram atualizadas pela revisão final. A primeira execução integral teve uma asserção de CSS obsoleta, corrigida, e ausência de candidato no teste de ingestão. O teste de ingestão passou isoladamente e na repetição integral sem alteração do coletor; a causa da primeira falha não foi determinada. Os dois testes OCR continuam explicitamente pendentes.

Playwright verifica a Home nos sete idiomas, temas claro/escuro e larguras 320, 360, 390, 430, 768, 1024 e 1440 px. Axe verifica critérios automatizáveis WCAG A/AA da Home, mais menu e preferências abertos em português. A regressão das demais páginas verifica layout e imagens nos dois temas a 390 px; axe adicional cobre a Home com dados. Isso não equivale a aprovação visual ou certificação WCAG integral das 44 páginas.

Fontes web e demais requisições externas foram bloqueadas para repetibilidade das capturas; o fallback tipográfico do sistema foi exercitado. A revisão com carregamento externo definitivo permanece parte da homologação em navegador conectado.

Testes adicionais verificam orientação 844 × 390 e ampliação do texto a 200%. A ampliação de texto não substitui a homologação manual do zoom real do navegador, teclado virtual e leitores de tela em aparelhos físicos.

Para reproduzir, configure `MINHABELEM_TEST_MYSQL` com servidor **local descartável**, sem Database, e execute `dotnet test MinhaBelem.sln -c Release`. Para exportar a matriz existente, configure `MINHABELEM_VISUAL_SNAPSHOTS` para `TestResults/sprint25-after-html` e execute o teste `MainPublicAndAdministrativePagesRenderAcrossSevenLanguages`; depois execute `Sprint25ExperienceTests`, que exporta a Home anônima e a variante com dados. Em `scripts/visual-qa`, use `npm ci`; na raiz, `node scripts/visual-qa/sprint25.mjs`. O script usa Edge headless instalado no Windows. `QA_HOME_ONLY=1` restringe à Home; `QA_REGRESSION_ONLY=1` restringe à regressão. `QA_WIDTHS`, `QA_HEIGHT`, `QA_CULTURES`, `QA_TEXT_SCALE`, `QA_SNAPSHOTS` e `QA_OUTPUT` permitem reproduzir as variantes registradas.

## Próximos marcos e aceite

Conforme a seção 18 do pedido, o operador deve aprovar a identidade, Home, header e menu mobile antes de propagar a composição nos Marcos B/C.

O Marco B ainda exige jornadas completas de mobilidade, De-Para, BRT, fluvial, Agenda, Guia e Curió. Permanecem pendentes a fonte canônica de tarifas com vigência/ato, reconfirmação oficial de horários e preços, geometria confiável e investigação das 27 alimentadoras sem percurso completo. A presença de valores no seed histórico não foi tratada como confirmação atual. Não foram acrescentadas travessias sem cadastro.

O Marco C ainda exige a revisão visual específica das demais páginas e do shell administrativo, aprovação em lote com verificação de publicação, estados de erro/carregamento e validação manual assistiva. RBAC, MFA, auditoria, fila Web/Worker, VisitRecommendationService, motor nativo Curió, roteiros, favoritos, comunidade e integrações foram preservados.

Nenhum deploy, push, mudança de credencial, ativação de provedor ou alteração de banco de produção foi realizado. A aprovação deste marco não aprova automaticamente os demais nem confirma valores de transporte em vigor.

Commits locais: `d23fd38` registra auditoria/inventário; `4d170f1` contém a implementação e os testes. As evidências e este relatório são versionados em um commit de entrega separado.
