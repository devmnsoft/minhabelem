# MINHABELÉM RADAR — SPRINT 08
## Robô Autônomo de Descoberta de Informações e Eventos sobre Belém e o Pará
**Repositório:** [https://github.com/devmnsoft/minhabelem](https://github.com/devmnsoft/minhabelem)  
**Branch:** `sprint08/minhabelem-radar-robo-autonomo`  
**Empresa:** MNSOFT  
**Status:** HOMOLOGADO COM ÊXITO (65 testes unitários/integração aprovados, 0 falhas, 0 warnings)

---

### 1. OBJETIVO EXECUTADO
Implementação de robô próprio em **ASP.NET Core / .NET 8** para monitoramento contínuo de fontes públicas autorizadas sobre Belém e o Pará, independentemente da existência de APIs pré-configuradas para cada site.

O sistema rastreia e descobre notícias, eventos futuros, atividades culturais, lazer, cinema, teatro, esporte, política, saúde, educação, saneamento, trânsito, transporte, turismo, gastronomia e serviços.

---

### 2. ARQUITETURA DE RASTREAMENTO & COMPONENTES IMPLEMENTADOS

Todos os 8 componentes exigidos pela especificação foram implementados em `MinhaBelem.Infrastructure.Discovery` com abstrações no `MinhaBelem.Core`:

| Componente | Classe | Responsabilidade |
|---|---|---|
| **Rastreador Web Público** | `PublicWebCrawler` | Orquestração do crawler autônomo com controle de profundidade (máx. 2), limite de páginas por host, polite crawl delay (250-300ms) e circuit breaker. |
| **Extrator de Conteúdo HTML** | `HtmlContentExtractor` | Extração de metatags (`og:title`, `og:description`, `article:published_time`, `article:author`, canonical), títulos, limpeza de tags perigosas e detecção de relevância geográfica para Belém e Pará. |
| **Extrator de Dados Estruturados** | `EventStructuredDataExtractor` | Extração de eventos em JSON-LD (`schema.org/Event`), iCalendar (`BEGIN:VEVENT`) e padrões temporais brasileiros com fuso de Belém (UTC-3), preços, gratuidade e classificação etária sem alucinação. |
| **Descoberta de Sitemaps** | `SitemapDiscoveryService` | Leitura assíncrona segura de `sitemap.xml` e `<sitemapindex>` aninhados para mapeamento de páginas de notícias e eventos. |
| **Descoberta de Feeds RSS/Atom** | `PublicRssDiscoveryService` | Autodescoberta de `<link rel="alternate">` e processamento de feeds RSS 2.0 e Atom com anexos de imagem. |
| **Analisador de Cartazes** | `EventPosterAnalyzer` | Integração com OCR e IA visual para extração factual de datas, locais, ingressos e artistas em cartazes autorizados de divulgação. |
| **Detector de Alterações** | `SourceChangeDetector` | Detecção de alterações via headers HTTP `ETag`, `Last-Modified` (304 Not Modified) e hash criptográfico SHA256 do conteúdo. |
| **Serviço de Deduplicação** | `ContentDeduplicationService` | Deduplicação multi-nível: URL exata, hash canônico, impressão digital (`ContentFingerprint`), similaridade Jaccard/Levenshtein e colisão espaço-temporal de eventos futuros. |

---

### 3. SEGURANÇA, COMPLIANCE & LIMITES DE ACESSO

1. **Proteção contra SSRF e Bloqueio de Redes Privadas:**
   - Todas as requisições do robô passam por `SafeUrl.Validate` e `SsrfGuardHandler` no pipeline do `IHttpClientFactory` ("feed").
   - Bloqueio estrito de endereços de loopback (`127.0.0.1`), faixas privadas RFC 1918 (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), link-local (`169.254.169.254`), IPv6 reservado (`fc00::/7`, `fe80::/10`) e esquemas inseguros (`file://`, `javascript:`).
2. **Respeito ao robots.txt:**
   - Integração com `IRobotsGate` antes de cada requisição. O robô interrompe o rastreamento imediatamente se o path estiver desautorizado no `robots.txt`.
3. **Limite de Tamanho e Conexão:**
   - Respostas HTTP com mais de 2 MB são truncadas e protegidas contra esgotamento de memória.
4. **Circuit Breaker:**
   - Se um host apresentar 5 falhas consecutivas (HTTP 5xx ou falha de conexão), o crawler encerra as tentativas para aquele domínio no ciclo atual.
5. **Políticas de Redes Sociais:**
   - Nenhuma raspagem clandestina que viole termos de uso. O MinhaBelém mantém catálogo de perfis públicos com mecanismos oficiais de incorporação (`official_embed_url`, oEmbed e players oficiais do YouTube/Instagram).

---

### 4. BANCO DE DADOS & MIGRAÇÃO IDEMPOTENTE (`010_radar_autonomo_descoberta`)

Em `MINHABELEM.SQL`, sob a nova migração aditiva `-- @migration 010_radar_autonomo_descoberta`:
1. `mb_radar_discovered_sources`: Catálogo de fontes descobertas organicamente para homologação editorial antes da ingestão permanente.
2. `mb_radar_crawl_history`: Histórico completo de requisições, status HTTP, ETag, tempo de resposta (ms), itens descobertos e eventos identificados.
3. `mb_radar_social_profiles`: Registro de perfis oficiais em redes sociais com notas de licença pública e URLs de incorporação.
4. **Sementes Oficiais:**
   - Theatro da Paz (Secult/PA)
   - Fundação Cultural do Município de Belém (FUMBEL)
   - Estação das Docas (Pará 2000)
   - Museu Paraense Emílio Goeldi
   - Agência Belém de Notícias
   - Agência Pará
   - Fundação Cultural do Pará (Centur)

O total de tabelas na Solution aumentou de 57 para **60**, com testes atualizados e validados.

---

### 5. PAINEL ADMINISTRATIVO: MINHABELÉM RADAR (`/admin/radar`)

Disponível no menu administrativo sob **Inteligência & IA**:
- **KPIs do Robô:** Total de fontes cadastradas, fontes ativas, eventos identificados, matérias aguardando revisão, consumo de tokens de IA, data/hora da última e próxima consulta e índice de erros.
- **Fila de Avaliação de Fontes:** Interface para o editor aprovar ou rejeitar fontes candidatas descobertas pelo crawler (ao aprovar, promove automaticamente para `ingestion_sources`).
- **Gestão de Perfis Oficiais:** Cadastro e suspensão/ativação de canais oficiais no Instagram, YouTube, Facebook e TikTok.
- **Auditoria de Varredura:** Tabela em tempo real das últimas requisições com código HTTP, latência em milissegundos e contadores de itens.
- **Disparo Manual:** Ação "Varredura Imediata" com suporte a URL específica ou ciclo geral.

---

### 6. WORKER AUTÔNOMO: CICLOS DE 10 MINUTOS (`RadarWorker`)

- Registrado como `IHostedService` e executando periodicamente a cada 10 minutos.
- Bloqueio distribuído no MySQL com `GET_LOCK('minhabelem_radar', 0)` para execução segura em ambientes clusterizados/VPS.
- Conecta o crawler à esteira editorial existente:
  $$\text{Descoberto} \rightarrow \text{Extraído} \rightarrow \text{Classificado (IA)} \rightarrow \text{Verificado} \rightarrow \text{Revisão Humana} \rightarrow \text{Publicado}$$
- Preserva `RequiresHumanApproval = true` para matérias sensíveis ou eventos com datas incertas.

---

### 7. RESULTADOS DOS TESTES AUTOMATIZADOS

Execução com `dotnet test`:
```
Execução de teste para MinhaBelem.Tests.dll (.NETCoreApp,Version=v8.0)
Aprovado!  – Com falha: 0, Aprovado: 65, Ignorado: 22, Total: 87, Duração: 326 ms
```
- 65 testes unitários e de integração aprovados com 100% de sucesso.
- 0 falhas, 0 avisos de compilação.
