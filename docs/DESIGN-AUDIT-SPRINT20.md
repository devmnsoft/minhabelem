# MINHABELÉM 360 — SPRINT 20: AUDITORIA REAL E CONGELAMENTO VISUAL

**Empresa:** MNSOFT  
**Projeto:** MinhaBelém 360  
**Data da Auditoria:** 19 de Outubro de 2026  
**Commit Baseline:** `85781ed`  
**Branch de Trabalho:** `feature/sprint-20-design-system-paraense`  
**Ambiente:** .NET 8 / C# 12 / MariaDB 10.11 (Porta 3308, 127.0.0.1)  
**Validação Inicial de Testes:** 160 testes aprovados, 0 falhas, 67 testes de banco ignorados em modo unitário, 3 testes integrados executados com sucesso gerando 47 snapshots HTML reais em 7 culturas.

---

## 1. Inventário Completo de Arquivos de Frontend e Layout

### 1.1 Folhas de Estilo (CSS) — Estado Herdado
Localização: `src/MinhaBelem.Web/wwwroot/css`

| Arquivo | Tamanho | Linhas | Responsabilidade Declarada | Problemas Identificados |
|---|---|---|---|---|
| `site.css` | 48.3 KB | 1.830 | Folha original legada do portal | 36 ocorrências de `!important`, 22 variáveis redefinidas, 142 seletores sobrescritos pelas folhas subsequentes. Regras acumuladas ao longo de sprints passadas. |
| `view-formats.css` | 6.5 KB | 245 | Utilitários de grid, tabela e lista | Regras isoladas que deveriam ser unificadas nos componentes do Design System. |
| `components.css` | 18.7 KB | 720 | Componentes reutilizáveis | Redefine `:root` com `--paper`, `--ink`, `--clay`, duplica botões, tabs e cards. 5 `!important`. |
| `amazonico.css` | 18.6 KB | 236 | Identidade "Amazônica 5.0" | Força tipografia com serifa (`Georgia`) globalmente em `body.portal h1`, `h2`, sobrescreve `.header`, `.brand-logo`, `.hero`, `.card`. 1 `!important`. |
| `agenda.css` | 8.7 KB | 380 | Módulo da Agenda Cultural | Duplicidade com `evolution.css` nos seletores `.agenda-month-nav`, `.agenda-save`, etc. 1 `!important`. |
| `evolution.css` | 73.4 KB | 1.617 | Camada cumulativa adicionada na Sprint 18/19 | Folha gigante contendo overrides para todas as outras folhas. 23 ocorrências de `!important`, redefinição de variáveis com prefixo `--mb-*` em conflito com `--green-*` e `--paper`. |
| **TOTAL** | **174.2 KB** | **5.028** | — | **66 ocorrências de `!important`, 22 colisões de variáveis globais, 180 seletores duplicados.** |

### 1.2 Scripts JavaScript
Localização: `src/MinhaBelem.Web/wwwroot/js`
- `site.js` (10.9 KB): Gerenciamento de menu mobile, navegação, CSRF duplo clique, detecção offline.
- `evolution.js` (15.5 KB): Lightbox de mídia, alternância de tema escuro/claro, detecção de suporte a vídeo hero, filtros dinâmicos.
- `agenda.js` (2.4 KB): Favoritos em `localStorage`, alternância entre grade e lista, calendários.
- `saved.js` (1.7 KB): Planos e favoritos salvos sincronizados via API.
- `guide-map.js` (1.7 KB): Carregamento on-demand de mapas Google/OpenStreetMap.
- `route-map.js` (0.8 KB): Visualização simplificada de rotas.
- `editorial-batches.js` (1.2 KB) e `editorial-progress.js` (1.9 KB): Ações editoriais em lote.
- `visit.js` (0.4 KB): Interações de visita e rotas.

### 1.3 Layouts e Views Razor
Localização: `src/MinhaBelem.Web/Views`
- **Layouts Globais:**
  - `Shared/_Layout.cshtml` (276 linhas, 16.0 KB): Layout principal com cabeçalho, navegação, ferramentas, `.admin-nav` condicional, rodapé institucional, lightbox e painel de acessibilidade.
  - `Shared/_AdminLayout.cshtml` (13 linhas, 275 B): Envoltório administrativo com barra lateral `_AdminSidebar`.
- **Componentes Compartilhados (`Shared/`):**
  - `_AdminSidebar.cshtml`: Menu lateral administrativo completo com permissões `mb:permission`.
  - `_AccessibilityPanel.cshtml`: Painel WCAG 2.2 AA (fonte, contraste, foco, movimento).
  - `_CityWindows.cshtml`: Três janelas temáticas da cidade (Ver-o-Peso, Tacacá, Guajará).
  - `_SponsoredCampaigns.cshtml` e `_CampaignForm.cshtml`: Banners e formulário de patrocinadores.
  - `_MediaAssets.cshtml`, `_MediaGallery.cshtml`, `_PlaceMediaGallery.cshtml`: Acervo visual.
  - `_RadarOperations.cshtml`: Telemetria em tempo real do radar de fontes.
  - `_PhotoReviewTable.cshtml` e `_CollectionQueue.cshtml`: Moderação editorial.
- **Módulos Públicos:**
  - `Home/`: `Index.cshtml`, `Detalhe.cshtml`, `Salvos.cshtml`, `Privacidade.cshtml`, `Termos.cshtml`, `Status.cshtml`, `Operacao.cshtml`, `Error.cshtml`.
  - `Agenda/`: `Index.cshtml` (Agenda 365, hoje, semana, detalhe, calendário).
  - `Guia/`: `Index.cshtml`, `Detalhe.cshtml`, `Acervo.cshtml`.
  - `Explore/`: `Index.cshtml`, `Roteiro.cshtml`, `Glossario.cshtml`.
  - `ItineraryPlanning/`: `Index.cshtml` (Planejador de viagens Curió).
  - `Mobilidade/`: `Index.cshtml`, `Linha.cshtml`, `Paradas.cshtml`, `DePara.cshtml`, `Fluvial.cshtml`, `Alertas.cshtml`.
  - `Visit/`: `Index.cshtml` (Como Chegar acessível com rota descritiva e mapas).
  - `Media/`: `Videos.cshtml`, `Midias.cshtml`, `Enviar.cshtml`.
  - `Bairro/`: `Index.cshtml`, `Detalhe.cshtml`.
  - `Parceiros/`: `Index.cshtml`.
  - `TouristAssistant/`: `Index.cshtml` (Curió Assistente IA com streaming/fallback).
  - `Conta/`: `Login.cshtml`, `Perfil.cshtml`, `Mfa.cshtml`, `RecoveryCodes.cshtml`, `Sessoes.cshtml`, `AlterarSenha.cshtml`.
- **Módulos Administrativos e Editoriais:**
  - `Admin/`: Dashboard, Radar, Evolução, Conteúdo, Fontes, Lugares, Fotos, Visitas, Mobilidade, Bairros, Usuários, Perfis, Integrações, Idiomas, Traduções, Relatórios, Auditoria, Configurações, Anunciantes, Colaborações.
  - `Editorial/` e `EditorialBatches/`: Gestão editorial de matérias e aprovação em lote.

---

## 2. Inventário de Rotas do Portal

### 2.1 Rotas Públicas Principais
1. `/` (Home com Hero de Belém, notícias, agenda, gastronomia, mobilidade e acervo)
2. `/{cultura}` (Localização nos 7 idiomas suportados: `pt-BR`, `en`, `fr`, `es`, `ru`, `zh-Hans`, `ja`)
3. `/explore` (Roteiros urbanos, ilhas e glossário papa-chibé)
4. `/explore/roteiro/{slug}` (Detalhes do roteiro com itinerário diário)
5. `/explore/glossario` (Expressões paraenses autênticas)
6. `/explore/planejar` (Planejador interativo com estimativa de tempo e custo)
7. `/guia` (Guia de patrimônio, turismo e gastronomia de Belém)
8. `/guia/{id}` (Detalhes de locais, horários, acessibilidade e mapa)
9. `/guia/acervo` (Fotografias documentais licenciadas)
10. `/agenda` (Agenda Cultural 365 com filtros de data, público e gratuidade)
11. `/agenda/semana` e `/agora` (Atalhos cronológicos para a programação)
12. `/cinema-teatro`, `/programacao-infantil`, `/eventos-gratuitos` (Filtros temáticos)
13. `/materia/{id}` e `/evento/{id}` (Detalhes editoriais com metadados e fonte oficial)
14. `/mobilidade` (Linhas de ônibus, paradas, alertas e mapa)
15. `/mobilidade/fluvial` (Horários e travessias fluviais para Combu e Mosqueiro)
16. `/mobilidade/alertas` (Alertas em tempo real do trânsito da cidade)
17. `/como-chegar` (Cálculo multimodal de rotas com alternativa textual acessível)
18. `/guia-ia` (Curió IA — Assistente turístico autônomo com motor regional)
19. `/videos` (Galeria de vídeos de Belém da comunidade e canais culturais)
20. `/midias` (Galeria pública de imagens catalogadas)
21. `/fotos/enviar` (Submissão pública de fotos com termos de licença de uso)
22. `/meu-bairro` e `/meu-bairro/{slug}` (Guias dos 8 distritos administrativos de Belém)
23. `/salvos` (Planos, eventos e locais favoritados offline)
24. `/parceiros` (Programa de apoiadores culturais e comerciais)
25. `/conta/login` (Autenticação segura com detecção de Caps Lock e visualização de senha)
26. `/conta/mfa` (Autenticação de dois fatores TOTP com cópia acessível)
27. `/conta/perfil` (Preferências de conta e idioma)
28. `/privacidade` e `/termos` (Termos de uso e conformidade com LGPD)
29. `/operacao` e `/status` (Transparência de integridade da infraestrutura)

### 2.2 Rotas Administrativas e Editoriais
1. `/admin` (Dashboard analítico operacional)
2. `/admin/radar` (Radar autônomo de fontes com telemetria ativa)
3. `/admin/evolucao` (Métricas de cobertura por sprint e metas)
4. `/admin/conteudo` (Acervo de matérias e eventos capturados/publicados)
5. `/editorial` (Área editorial com fluxo de publicação e histórico de revisões)
6. `/editorial/lotes` (Aprovação editorial em lote com visualização prévia)
7. `/admin/fontes` (Gestão de fontes RSS, portais e canais oficiais)
8. `/admin/lugares` e `/admin/fotos` (Catálogo de locais turísticos e fotos)
9. `/admin/visitas` (Estatísticas de visitação e agendamentos)
10. `/admin/midias` e `/admin/midias/submissoes` (Moderação de mídia enviada)
11. `/admin/mobilidade` (Gestão de linhas, horários e alertas urbanos)
12. `/admin/bairros` (Cadastro e guia dos bairros de Belém)
13. `/admin/usuarios` e `/admin/perfis` (Gestão de operadores e RBAC)
14. `/admin/anunciantes` (Campanhas publicitárias e patrocinadores)
15. `/admin/idiomas` e `/admin/traducoes` (Revisão e moderação de traduções IA nos 7 idiomas)
16. `/admin/relatorios` (Relatórios de tráfego, audiência e acessibilidade)
17. `/admin/auditoria` (Trilha de auditoria criptografada e imutável)
18. `/admin/configuracoes` e `/admin/integracoes` (Ajustes de conectores externos e coletores)

---

## 3. Matriz de Auditoria de Problemas Visuais e Estruturais

Classificação:
- **P0:** Quebra funcional, conteúdo inacessível ou interface inutilizável.
- **P1:** Problema de responsividade, legibilidade ou acessibilidade WCAG.
- **P2:** Inconsistência visual, regras conflitantes ou repetição/duplicação.
- **P3:** Acabamento, polimento estético e refinamento editorial.

| ID | Página / Módulo | Severidade | Categoria | Descrição do Problema | Impacto e Evidência |
|---|---|---|---|---|---|
| **ISS-01** | `Views/Home/Index.cshtml` | **P0** | Duplicação Estrutural | Duas seções inteiras de vídeos (`video-list-title` com `videos.Skip(6)` e `video-section-title` com `videos.Take(6)`). | Título repetido `@Loc["VideoTitle"]`, cards de vídeo duplicados ou dois estados vazios consecutivamente na Home se o banco tiver poucos vídeos. |
| **ISS-02** | `Views/Shared/_Layout.cshtml` e `_AdminLayout.cshtml` | **P0** | Duplicação de Navegação | O menu horizontal `.admin-nav` é renderizado no topo em `_Layout.cshtml` para usuários autenticados com MFA, enquanto a sidebar `.admin-sidebar` é renderizada logo abaixo na mesma página administrativa. | 16 links administrativos repetidos simultaneamente na mesma tela (topo e lateral), gerando confusão cognitiva e poluição visual severa. |
| **ISS-03** | `wwwroot/css/*.css` | **P0** | Conflito de Cascade / CSS | 22 variáveis redefinidas e 180 seletores duplicados com 66 ocorrências de `!important` para tentar forçar precedência entre 6 arquivos CSS concorrentes. | Imprevisibilidade na renderização de botões, headers, cores e fontes em navegadores com diferentes caches ou ordens de resolução. |
| **ISS-04** | `Views/Home/Index.cshtml` | **P1** | Duplicação de Fotos | As imagens do Ver-o-Peso, Tacacá e Guajará aparecem na seção `_CityWindows` e são repetidas logo abaixo no bloco `#home-photo-gallery`. | Falta de curadoria visual; a Home aparenta ter acervo fotográfico escasso por repetir a mesma imagem 3 vezes na mesma rolagem. |
| **ISS-05** | `Views/Home/Index.cshtml` | **P1** | Elementos Falsos / Quebra | A tag `<video>` do Hero aponta para `/videos/belem-hero.mp4` e `/videos/belem-hero.webm`, mas esses arquivos não existem no repositório (apenas um `README.md`). | Se o script JavaScript falhar ou for bloqueado por rede/política de CSP, o navegador exibe um player quebrado ou uma tela preta antes da imagem de pôster. |
| **ISS-06** | `wwwroot/css/amazonico.css` | **P1** | Quebra de Identidade e Legibilidade | Força `Georgia, 'Times New Roman', serif` com `letter-spacing: -.045em` em todos os `h1`, `h2` do portal público, colidindo com a tipografia moderna `Outfit` definida em `evolution.css`. | Títulos exibem tipografias misturadas sem hierarquia intencional; quebra de legibilidade em telas móveis estreitas. |
| **ISS-07** | `Shared/_AccessibilityPanel.cshtml` e `scripts/visual-qa/check.mjs` | **P1** | Falso Positivo / Overflow | O painel de acessibilidade com `position: fixed` gera detecção de overflow horizontal em telas móveis quando recolhido com deslocamento fora do viewport. | Em navegadores móveis com viewport dinâmico, pode causar oscilação na barra de rolagem horizontal se não contido por `visibility: hidden` ou `contain: paint`. |
| **ISS-08** | `Views/Home/Index.cshtml`, `Parceiros`, `Admin` | **P2** | Estilos Inline Abusivos | Dezenas de tags utilizam `style="display:flex;gap:10px..."`, `style="color:var(--text-muted,#59675f);margin-top:6px..."`. | Dificuldade de manutenção, impossibilidade de sobrescrever via temas escuros/alto contraste sem `!important`. |
| **ISS-09** | `Views/Shared/_AdminSidebar.cshtml` | **P2** | Responsividade Administrativa | A barra lateral administrativa em telas pequenas (320px–390px) não possui recolhimento suave acessível com botão hambúrguer independente do portal. | Usuários administrativos em smartphones têm parte do conteúdo útil da tabela cortado ou deslocado pela sidebar. |
| **ISS-10** | `wwwroot/images/favicon.svg` e Marca | **P3** | Identidade Visual Regional | O favicon e a marca institucional não possuem variantes padronizadas para ícone de toque, manifest e contraste de alto padrão com elementos amazônicos autênticos. | Falta refinamento estético premium que diferencie claramente o portal de um site padrão de notícias. |

---

## 4. Plano Estrutural de Resolução (Fases 1 a 17)

1. **Consolidação do CSS:**
   - Criar arquitetura modular em `@layer` (tokens, base, layout, components, pages).
   - Eliminar os `!important` desnecessários e consolidar todas as variáveis em uma única fonte de verdade (`tokens.css`).
   - Carregar um CSS limpo, rápido e estruturado, sem camadas conflitantes de sprints anteriores.
2. **Design System MinhaBelém 6.0:**
   - Paleta cromática autêntica do Pará: Verde Floresta Profundo (`#0B2E24`), Azul da Baía do Guajará (`#1B4353`), Terracota das Olarias de Icoaraci (`#C25732`), Dourado Solar (`#D4A034`), Creme Aconchegante (`#FAF7F0`), Branco Puro (`#FFFFFF`) e Superfícies Neutras de Alto Contraste.
   - Escalas tipográficas fluidas (`clamp()`) com fontes modernas (`Outfit` para display/títulos, `Plus Jakarta Sans` para textos e dados).
   - Componentes padronizados e sem estilos inline.
3. **Eliminação de Duplicidades:**
   - Unificar a navegação: menu público no portal, barra lateral unificada na administração (sem barra horizontal repetida).
   - Reorganizar a Home: 1 seção destacada de vídeos com carrossel ou grid harmônico, 1 acervo fotográfico diversificado com imagens de Belém, eliminação de duplicação de cards e banners.
4. **Validação Contínua:**
   - Testar em 4 resoluções de referência (Desktop 1440px, Notebook 1280px, Tablet 768px, Mobile 390px e 320px).
   - Validar acessibilidade WCAG 2.2 AA (contraste >= 4.5:1 para texto normal, 3:1 para display, foco visível, navegação por teclado e leitor de tela).
   - Executar os testes automatizados do .NET e a suíte visual Playwright registrando evidências antes/depois.
