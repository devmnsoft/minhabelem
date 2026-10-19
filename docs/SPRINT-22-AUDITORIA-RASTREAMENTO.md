# SPRINT 22 — AUDITORIA VISUAL E FUNCIONAL OBRIGATÓRIA (FASE 0)
## Rastreamento de Defeitos, Diagnóstico Técnico e Plano de Recuperação

**Empresa:** MNSOFT  
**Produto:** MinhaBelém — Nossa cidade. Mil histórias.  
**Repositório:** [https://github.com/devmnsoft/minhabelem](https://github.com/devmnsoft/minhabelem)  
**Branch de Trabalho:** `feature/sprint-22-recuperacao-mobilidade-design`  
**Data da Auditoria:** 19 de Outubro de 2026  
**Commit Baseline:** `5dcd6b6`  

---

## 1. Inventário de Defeitos Prioritários Identificados pelo Operador

| Código | Defeito Identificado | Componente / Rota Afetada | Causa Raiz Identificada | Ação Corretiva na Sprint 22 |
|---|---|---|---|---|
| **DEF-001** | **Texto invisível:** títulos e descrições claros sobre fundo claro | Múltiplas Views (`.card`, `.story`, formulários, modais) | Classes utilitárias e regras herdadas aplicando `color: #ffffff` ou variáveis de texto invertido dentro de containers com fundo claro (`--color-surface` / `--color-cream-card`). | Forçar tipografia com cores canônicas de leitura de alto contraste (`--color-text-primary: #152721` e `--color-text-secondary: #3d5248`) sobre qualquer superfície clara. |
| **DEF-002** | **Menu ilegível:** vários links com contraste insuficiente e quebra de linhas | Header principal (`Shared/_Layout.cshtml` e `layout.css`) | 13 links diretos exibidos simultaneamente no cabeçalho desktop causando quebra em duas linhas e colisão com botões de idioma e acessibilidade. | Reestruturação do Header: 5 links centrais no desktop (Início, Agenda, Explorar, Como Chegar, Notícias) + menu dropdown "Mais" para os demais recursos. No mobile: 5 abas de toque (Início, Agenda, Explorar, Rotas, Mais). |
| **DEF-003** | **Hero excessivamente escuro:** fotografia de Belém quase invisível | Home (`Home/Index.cshtml` e `site.css`) | Camada dupla de gradiente escuro (`rgba(8, 47, 43, 0.94)` sobreposto a outro overlay com 94% de opacidade) encobrindo totalmente a foto da Estação das Docas / Baía do Guajará. | Calibração do gradiente para `rgba(8, 47, 43, 0.72) 0%, rgba(8, 47, 43, 0.40) 50%, rgba(8, 47, 43, 0.15) 100%`, revelando a fotografia panorâmica com nitidez e mantendo contraste WCAG para o título branco. |
| **DEF-004** | **Conteúdo desalinhado:** blocos ocupando larguras inconsistentes | Home, Guia de Lugares, Agenda Cultural | Variações arbitrárias de `max-width` (alguns blocos com 1400px, outros com 960px, outros sem container padronizado). | Padronização global de container para `.container { max-width: 1200px; margin: 0 auto; padding: 0 var(--space-4); }`. |
| **DEF-005** | **Curió IA quebrado:** cabeçalho, descrição e formulário sem hierarquia visual | `/guia-ia` (`TouristAssistant/Index.cshtml`) | Caixa de chat estática com textos claros sobre cartões claros, botões flutuantes invadindo a área de digitação e ausência de balões visuais distintos. | Reconstrução da interface do Curió IA em estilo mensageiro premium: balões de diálogo contrastados, chips clicáveis com perguntas frequentes, fontes oficiais citadas e layout mobile-first. |
| **DEF-006** | **Formulários ilegíveis:** campos, labels e mensagens sem contraste | Formulários de Como Chegar, Agendamento de Visita, Login, De-Para | Inputs sem bordas nítidas, textos de apoio e placeholders em cinza muito claro (< 2.5:1 de contraste). | Aplicação dos tokens de formulários do Design System com bordas de 1.5px sólidas (`#b5c4ba`), anel de foco visível (`#0f62fe`), labels explícitas em `#152721` e mensagens de ajuda em `#3d5248` (>= 4.5:1). |
| **DEF-007** | **Footer inconsistente:** marca e informações mal distribuídas | Rodapé global (`Shared/_Layout.cshtml` e `layout.css`) | Colunas com larguras desbalanceadas, espaçamento assimétrico e alinhamento vertical quebrado em tablets e smartphones. | Grid responsivo de 4 colunas proporcionais (Institucional, Navegação, Turismo & Cultura, Serviços Urbanos), marca com descrição de direitos e seletor de idioma acessível. |
| **DEF-008** | **Funcionalidades escondidas:** mobilidade, De-Para, horários e rotas sem destaque adequado | Portal público e navegação principal | Links de De-Para, Paradas e Transporte Fluvial dispersos em páginas secundárias sem atalhos visíveis na Home nem no menu de acesso rápido. | Criação do Bloco de Destaque "Como se deslocar por Belém?" na Home e nova Central de Mobilidade (`/mobilidade`) unificando Ônibus, BRT, Fluvial, De-Para e Como Chegar. |
| **DEF-009** | **Componentes repetidos:** navegação e seções duplicadas | Layouts compartilhados e páginas de gestão | Barra `.admin-nav` competindo com sidebar; duplicações conceituais entre `/como-chegar` e `/mobilidade/de-para`. | Unificação da experiência de deslocamento em "Como Chegar" com suporte direto aos modais e redirecionamentos suaves. |
| **DEF-010** | **Experiência mobile insuficiente:** excesso de informações e navegação complexa | Todas as páginas em viewports <= 390px | Páginas densas demais com tabelas estáticas não roláveis e menus laterais que cobriam o conteúdo. | Menus otimizados para toque, tabelas envelopadas em cartões expansíveis ou `.table-scroll`, e navegação móvel de 5 abas ergonômica na parte inferior/superior. |

---

## 2. Inventário de Arquivos do Módulo de Mobilidade e Rotas

### 2.1 Controladores e Serviços
- `src/MinhaBelem.Web/Controllers/MobilidadeController.cs`: Central de mobilidade (rotas `/mobilidade`, `/mobilidade/linha/{code}`, `/mobilidade/paradas`, `/mobilidade/de-para`, `/mobilidade/fluvial`, `/mobilidade/alertas`).
- `src/MinhaBelem.Web/Controllers/VisitController.cs`: Planejador de deslocamento e visitação (rotas `/como-chegar` e `/mobilidade/como-chegar`).
- `src/MinhaBelem.Infrastructure/Repositories/TransitRepository.cs`: Repositório de dados das linhas de ônibus, BRT, paradas, terminais, travessias fluviais e alertas SEMOB.
- `src/MinhaBelem.Infrastructure/Services/GoogleRoutePlanner.cs`: Integração de roteamento multimodal com detecção de chave, cálculo de etapas e fallback transparente.
- `src/MinhaBelem.Infrastructure/Services/VisitRecommendationService.cs`: Avaliação de horários de funcionamento, previsão de custos e melhor momento de visitação.

### 2.2 Views Razor
- `src/MinhaBelem.Web/Views/Mobilidade/Index.cshtml`: Portal da Mobilidade Urbana.
- `src/MinhaBelem.Web/Views/Mobilidade/DePara.cshtml`: Planejador de rotas de ônibus e conexões.
- `src/MinhaBelem.Web/Views/Mobilidade/Linha.cshtml`: Ficha detalhada da linha, itinerário e paradas.
- `src/MinhaBelem.Web/Views/Mobilidade/Paradas.cshtml`: Terminais, estações BRT e pontos de embarque.
- `src/MinhaBelem.Web/Views/Mobilidade/Fluvial.cshtml`: Travessias fluviais amazônicas (Combu, Cotijuba, Mosqueiro).
- `src/MinhaBelem.Web/Views/Mobilidade/Alertas.cshtml`: Alertas e desvios de trânsito em tempo real.
- `src/MinhaBelem.Web/Views/Visit/Index.cshtml`: Como Chegar com mapa, etapas descritivas, custo total e recomendação de visita.
- `src/MinhaBelem.Web/Views/Guia/Detalhe.cshtml`: Ficha do local com horários, dias de funcionamento e dicas de visita.
- `src/MinhaBelem.Web/Views/TouristAssistant/Index.cshtml`: Curió IA Assistente de Turismo e Mobilidade.
- `src/MinhaBelem.Web/Views/Home/Index.cshtml`: Página Inicial do Portal MinhaBelém.
- `src/MinhaBelem.Web/Views/Shared/_Layout.cshtml`: Layout mestre com cabeçalho compacto, drawer e rodapé.

---

## 3. Matriz de Rastreabilidade das Fases da Sprint 22

- **Fase 1:** Recuperação das funcionalidades de Mobilidade (Central, Linhas, BRT, De-Para, Paradas).
- **Fase 2:** Mapa com traçado real e alternativa textual acessível.
- **Fase 3:** Transporte Fluvial com identidade ribeirinha e dados verificados.
- **Fase 4:** Horários e funcionamento dos locais no Guia de Turismo.
- **Fase 5:** Unificação e integração entre Como Chegar e De-Para.
- **Fase 6:** Correção crítica de contraste e cores (WCAG 2.2 AA).
- **Fase 7:** Consolidação definitiva do CSS.
- **Fase 8:** Reconstrução do Header (Desktop compacto + Mobile 5 abas).
- **Fase 9:** Nova Home Premium com destaque de mobilidade e fotografia nítida.
- **Fase 10:** Curió IA — Reconstrução completa da interface conversacional.
- **Fase 11:** Recuperação e integração de Agenda, Roteiros e Bairros.
- **Fase 12:** Design regional autêntico de Belém do Pará.
- **Fase 13:** Responsividade em 10 larguras (320px a 1920px).
- **Fase 14:** Acessibilidade transversal (teclado, foco, leitores de tela).
- **Fase 15:** Testes automatizados e visuais (Playwright Edge + XUnit).
- **Fase 16:** Regressão visual comparativa antes e depois.
- **Fase 17:** Relatório final de homologação.
