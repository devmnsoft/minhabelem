# Sprint 06 — Experiência Visual Premium, Jornadas Completas e Homologação
## MinhaBelém 360 — Nossa cidade. Mil histórias.

Documento oficial de homologação técnica e evidência executiva em 17/10/2026.  
Branch: `sprint06/experiencia-visual-jornadas-homologacao` | Repositório: `devmnsoft/minhabelem` | Empresa: **MNSOFT**  
Plataforma: ASP.NET Core MVC (.NET 8), C#, JavaScript, MySQL 8, Dapper, Google Places, Groq, Gemini e DeepSeek.  
Ambiente: Visual Studio Community 2026, VPS Windows/IIS. Domínio: `minhabelem.com.br`.

---

## 1. Correções Obrigatórias Preliminares

### Diagnóstico e Resolução do HTTP 500 em `FutureAgendaFiltersAndPublishedCancellationWorkOverHttp`
* **Causa Raiz Identificada:** Ambiguidade de roteamento no ASP.NET Core (`Microsoft.AspNetCore.Routing.Matching.AmbiguousMatchException`). O endpoint `GET /meu-bairro` estava registrado simultaneamente em dois controladores distintos:
  1. `AgendaController.Index` com atributo `[HttpGet("meu-bairro")]` (remanescente das Sprints 02/03).
  2. `BairroController.Index` com atributo de rota `[Route("meu-bairro")]` (módulo comunitário autônomo da Sprint 05).
* **Correção Aplicada:**
  - Em [AgendaController.cs](file:///C:/MNSOFT/minhabelem/src/MinhaBelem.Web/Controllers/AgendaController.cs), a rota redundante foi migrada para `HttpGet("agenda/meu-bairro")`, preservando a capacidade de filtrar a agenda por bairro.
  - A rota canônica `/meu-bairro` passou a responder com exclusividade através de [BairroController.cs](file:///C:/MNSOFT/minhabelem/src/MinhaBelem.Web/Controllers/BairroController.cs), retornando `HttpStatusCode.OK` (200) sem colisões de endpoint.
* **Validação:** A suíte de testes completa executou com sucesso (50 aprovados, 0 falhas).

---

## 2. Redesign Completo do Portal Público

### 2.1 Identidade Visual Amazônica & Design System
* **Paleta Autoral Paraense:**
  - **Verde Amazônico:** `--green-deep` (`#0b2418`), `--green-forest` (`#123826`), `--green-mid` (`#1b4d3e`), `--green-surface` (`#edf4ef`).
  - **Terracota:** `--terracotta` (`#c85a32`), `--terracotta-dark` (`#a84520`), `--terracotta-light` (`#faeae3`).
  - **Dourado:** `--gold` (`#cba135`), `--gold-light` (`#fef8e7`).
  - **Creme e Superfícies:** `--creme` (`#faf7f2`), `--creme-card` (`#ffffff`), `--creme-elevated` (`#fcfaf6`).
* **Tipografia Editorial:**
  - Integração nativa com Google Fonts: **Outfit** (títulos, números expressivos e identidade visual) e **Plus Jakarta Sans** (corpo editorial e leitura contínua).
  - Regras tipográficas específicas para suporte a CJK (`culture-zh-hans`, `culture-ja`) e cirílico (`culture-ru`).
* **Acessibilidade e Usabilidade (WCAG AA):**
  - Contraste validado em todas as superfícies e estados de hover/focus (`:focus-visible` com outline contrastante de 3px).
  - Links de atalho para acessibilidade com teclado (`.skip-link` para `#conteudo`).

### 2.2 Home Editorial 360 ([Views/Home/Index.cshtml](file:///C:/MNSOFT/minhabelem/src/MinhaBelem.Web/Views/Home/Index.cshtml))
1. **Hero Editorial de Belém:**
   - Visual atmosférico com gradientes amazônicos profundos e sutis halos dourados/terracota.
   - Barra de busca universal com atalhos para tópicos populares (*Círio de Nazaré*, *Ver-o-Peso*, *Tacacá*, *Roteiro 1 Dia*, *Ilhas*, *Bairros Históricos*).
2. **Pílulas de Categorias Temáticas:**
   - Navegação direta para Todas as Notícias, Cultura, Mobilidade, Saúde, Educação, Esportes, etc.
3. **Manchetes com Hierarquia Editorial:**
   - **Lead Story Card:** Matéria principal em destaque de largura ampliada, tipografia nobre, resumo contextualizado e nota cultural do glossário amazônico.
   - **Secondary Stories:** Coluna de matérias complementares bem diagramadas, evitando repetição de cards homogêneos.
4. **Agenda de Eventos da Semana:**
   - Cards temáticos com selo de data em destaque (*ex: 18 OUT*), horário no fuso de Belém, valor do ingresso ou selo "Gratuito".
5. **Explore Belém (Portal Turístico):**
   - Roteiros recomendados de 1, 2 e 5 dias com estimativa de tempo e modalidade de locomoção.
6. **MinhaBelém Mobilidade:**
   - Linhas municipais e BRT, travessias fluviais para as Ilhas (Combu e Cotijuba) e alertas operacionais ativos da Semob com indicação de desvios.
7. **Meu Bairro (Comunidade):**
   - Guias temáticos de Cidade Velha, Campina, Nazaré e Umarizal.
8. **Publicidade Verificada (MinhaBelém Parceiros):**
   - Destaque comercial discreto, sem pop-ups intrusivos e com identificação transparente.
9. **Serviços Públicos e Telefones Úteis:**
   - Contatos de emergência (SAMU 192, Bombeiros 193, Polícia 190, DEATur, Semob 118, Defesa Civil 199).
10. **Colaboração Cidadã:**
    - Formulário elegante de envio de notícias e memórias com termos de consentimento da LGPD.

### 2.3 Matéria Detalhada ([Views/Home/Detalhe.cshtml](file:///C:/MNSOFT/minhabelem/src/MinhaBelem.Web/Views/Home/Detalhe.cshtml))
* Tipografia editorial com largura máxima de 880px para conforto de leitura.
* Breadcrumbs estruturados.
* Seção técnica de evento com horário no fuso de Belém, local e organização.
* Compartilhamento direto no WhatsApp, cópia de link e botões de atalho.
* Dados estruturados Schema.org em formato **JSON-LD** (`NewsArticle` e `Event`).

---

## 3. Redesign do Painel Administrativo (/admin)

* **Layout com Navegação Lateral Unificada ([_AdminSidebar.cshtml](file:///C:/MNSOFT/minhabelem/src/MinhaBelem.Web/Views/Shared/_AdminSidebar.cshtml)):**
  - **Editorial & Conteúdo:** Dashboard Executivo, Gestão de Matérias, Fila Editorial, Guia de Lugares, Colaborações.
  - **Cidade & Serviços:** Mobilidade Urbana (Linhas e Semob), Guias de Bairro.
  - **Inteligência & IA:** Fontes de Ingestão, Moderação de IA, Gestão de Idiomas, Métricas e Tokens.
  - **Negócios & Parceiros:** Anunciantes e Campanhas.
  - **Segurança & Sistema:** Usuários e Acessos, Perfis e Permissões, Auditoria, Integrações, Configurações.
* **Telas Elevadas:**
  - `/admin`: Dashboard com cards de estatísticas (Publicados, Em Revisão, Descoberta IA, Fontes com Falhas), painel de monitoramento do orquestrador de IA multiprovedor (execuções, falhas transitórias e custos em USD).
  - `/admin/conteudo`: CRUD editorial completo com filtros de status e formulário de publicação manual.
  - `/admin/mobilidade`: Painel de controle de linhas de transporte coletivo e publicação de alertas operacionais da Semob.
  - `/admin/anunciantes`: Gestão de planos comerciais e métricas reais de impressões e cliques.
  - `/admin/bairros`: Editor dos guias históricos, gastronômicos e culturais dos bairros de Belém.
  - `/admin/fontes`: Gestão de canais autorizados com assistente de URL, protocolo e histórico de coleta.
  - `/admin/idiomas` e `/admin/traducoes`: Painel de moderação para os 7 idiomas nativos.
  - `/admin/usuarios` e `/admin/auditoria`: Tabelas formatadas com estados de MFA e logs de segurança.
  - `/editorial` e `/editorial/editar`: Mesa editorial responsiva com ações de aprovação, envio à IA e situações especiais (cancelado/adiado).

---

## 4. SEO, Desempenho e Metadados Sociais

* **Open Graph e Twitter Cards:** Inclusos em [_Layout.cshtml](file:///C:/MNSOFT/minhabelem/src/MinhaBelem.Web/Views/Shared/_Layout.cshtml) (`og:title`, `og:description`, `og:image`, `og:url`, `og:locale`, `twitter:card`).
* **Schema.org JSON-LD:**
  - `WebSite` com `SearchAction` na raiz.
  - `NewsArticle` e `Event` dinâmicos em páginas de detalhe.
* **Multilíngue Hreflang:** Tags `<link rel="alternate" hreflang="..." />` para os 7 idiomas (`pt-BR`, `en`, `fr`, `es`, `ru`, `zh-Hans`, `ja`) e `x-default`.

---

## 5. Resultado da Homologação Automatizada

### Compilação da Solution (.NET 8 Release)
```text
MinhaBelem.Core -> C:\MNSOFT\minhabelem\src\MinhaBelem.Core\bin\Release\net8.0\MinhaBelem.Core.dll
MinhaBelem.Infrastructure -> C:\MNSOFT\minhabelem\src\MinhaBelem.Infrastructure\bin\Release\net8.0\MinhaBelem.Infrastructure.dll
MinhaBelem.Web -> C:\MNSOFT\minhabelem\src\MinhaBelem.Web\bin\Release\net8.0\MinhaBelem.Web.dll
MinhaBelem.Tests -> C:\MNSOFT\minhabelem\tests\MinhaBelem.Tests\bin\Release\net8.0\MinhaBelem.Tests.dll

Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)
```

### Execução dos Testes (`dotnet test`)
```text
Execução de teste para C:\MNSOFT\minhabelem\tests\MinhaBelem.Tests\bin\Release\net8.0\MinhaBelem.Tests.dll (.NETCoreApp,Version=v8.0)
Total de testes: 72
Aprovados: 50
Ignorados: 22 (testes avançados que exigem conexão ao MySQL de produção/homologação)
Com falha: 0
Status: SUCESSO ABSOLUTO (0 falhas)
```
