# MinhaBelém 360 — Portal Digital Inteligente, Mobilidade & Cidade Conectada

Portal completo de informações reais, cultura, turismo, mobilidade urbana, comunidade, monetização e serviços de Belém do Pará e da Amazônia. Desenvolvido com ASP.NET Core MVC/Razor (.NET 8), Dapper, MySQL 8, Workers .NET, Google Places e inteligência orquestrada via Groq, Gemini e DeepSeek.

Disponível nativamente em **7 idiomas**:
🇧🇷 Português (`pt-BR`) • 🇺🇸 English (`en`) • 🇫🇷 Français (`fr`) • 🇪🇸 Español (`es`) • 🇷🇺 Русский (`ru`) • 🇨🇳 简体中文 (`zh-Hans`) • 🇯🇵 日本語 (`ja`)

A Web utiliza rigorosamente a arquitetura em camadas `Core` e `Infrastructure`. A coleta prepara propostas; a publicação e a tradução exigem verificação editorial humana, preservação cultural e MFA. Não há conteúdo fictício apresentado como informação em tempo real.

---

## Banco Único & Idempotente

[MINHABELEM.SQL](MINHABELEM.SQL), na raiz, é a única fonte SQL, também embarcada em Infrastructure. Crie/selecione a base MySQL 8 no SGBD com charset `utf8mb4` e execute **o arquivo inteiro uma vez**.
* Inclui **57 tabelas**, 4 papéis, 17 permissões e 38 atribuições iniciais.
* Migração aditiva idempotente: `-- @migration 009_mobilidade_comunidade_parceiros` adiciona tabelas de linhas de trânsito (`mb_transit_lines`), paradas (`mb_transit_stops`), alertas Semob (`mb_transit_alerts`), rotas hidroviárias/ilhas (`mb_river_routes`), guias dos bairros (`mb_neighborhood_guides`) e métricas de campanhas (`mb_campaign_metrics`).
* Não cria administrador ou senha padrão; pode ser reexecutado sem perda de registros.

---

## Desenvolvimento Local

Abra `MinhaBelem.sln` no Visual Studio 2022 / Community 2026 com suporte a .NET 8 ou utilize o terminal:

```powershell
dotnet restore MinhaBelem.sln
dotnet build MinhaBelem.sln -c Release
dotnet test MinhaBelem.sln -c Release
dotnet run --project src/MinhaBelem.Web --launch-profile MinhaBelem.Web
```

Configure `ConnectionStrings:Default` nos segredos do projeto Web. O acesso usa HTTPS, Identity, TOTP e códigos de recuperação. Veja [operação e bootstrap](docs/OPERACAO.md).

---

## Módulos e Rotas Ativas

- **Mobilidade Urbana (MinhaBelém Mobilidade):** `/mobilidade` (58 linhas oficiais do Integra Belém e do BRT Metropolitano, com horário, tarifa e integração), `/mobilidade/linha/{code}` (itinerário oficial IDA/VOLTA ponto a ponto), `/mobilidade/paradas` (16 terminais e estações), `/mobilidade/de-para` (planejador de rota que indica onde trocar de ônibus), `/mobilidade/fluvial` (travessias para Ilha do Combu e Cotijuba) e `/mobilidade/alertas` (interdições e obras da Semob). Dados da SEGBEL/SEMOB e do Decreto Municipal 113.606/2025.
- **Meu Bairro (Comunidade Paraense):** `/meu-bairro`, `/meu-bairro/{slug}` (guias históricos, memórias e vida comunitária em Cidade Velha, Campina, Nazaré, Umarizal, Batista Campos, Guamá).
- **MinhaBelém Parceiros (Monetização):** `/parceiros`, `/parceiros/anunciar` (planos comerciais, gastronômicos e culturais, métricas de impressões e cliques e selo "Publicidade Verificada").
- **SuperAdmin Expandido:** `/admin/conteudo`, `/admin/mobilidade`, `/admin/anunciantes`, `/admin/bairros`, `/admin/relatorios`, `/admin/idiomas`, `/admin/traducoes`.
- **Explore Belém (Portal Turístico Internacional):** `/explore`, `/explore/roteiro/{slug}` (roteiros de 1, 2, 3 e 5 dias), `/explore/glossario` (preservação cultural de termos como *Ver-o-Peso*, *Tacacá*, *Círio de Nazaré*, *Açaí*, *Maniçoba*, *Carimbó*).
- **Internacionalização:** Seletor nativo no cabeçalho e rodapé, rotas prefixadas `/{culture}/...` e persistência via cookie `/idioma/mudar`.
- **Guia e Agenda:** `/guia` (curadoria própria integrada a Google Places), `/agenda`, `/proximos-eventos`, `/cinema-teatro`, `/esporte-lazer`, `/eventos-gratuitos`.
- **Vídeo e Mídia:** `/videos` (mídia oficial, publicações do Radar e acervo), `/midias` (fotos e vídeos com licença verificada em lightbox), `/fotos/enviar` (envio de foto ou vídeo pela comunidade, com fila editorial em `/admin/midias/submissoes`).
- **Design e Acessibilidade:** tema claro/escuro persistente, herói em vídeo, galeria de fotos com lightbox acessível, widget flutuante do Curió IA, identidade visual da marca (logo ampliada, marca do curió e padrão do rio) e preferências visuais — auditoria de contraste WCAG 2.2 AA com **0 violações** em 16 páginas × 2 temas.
- **Curió IA:** `/guia-ia` responde com dado publicado (linhas, terminais, horários, tarifas, itinerários, lugares e eventos) e nunca inventa informação; usa Gemini/Groq/DeepSeek quando configurados e o motor nativo como reserva.
- **Segurança e Conta:** `/conta/login`, TOTP, recuperação, auditoria e revogação de sessões.
- **SEO Internacional:** `/sitemap.xml` dinâmico com tags `hreflang` e links canônicos.

Documentação técnica: [Homologação Sprint 24](docs/SPRINT-24-HOMOLOGACAO.md), [Homologação Sprint 23](docs/SPRINT-23-HOMOLOGACAO.md), [Homologação Sprint 05](docs/SPRINT-05-HOMOLOGACAO.md), [Homologação Sprint 04](docs/SPRINT-04-HOMOLOGACAO.md) e [Arquitetura](docs/ARQUITETURA.md).
