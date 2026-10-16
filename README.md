# MinhaBelém — Portal Turístico Global & Cidade Conectada

Portal internacional de notícias, cultura, turismo, agenda, serviços e guia de Belém do Pará e da Amazônia. Desenvolvido com ASP.NET Core MVC/Razor (.NET 8), Dapper, MySQL 8, Workers .NET, Google Places e inteligência orquestrada via Groq, Gemini e DeepSeek.

Disponível nativamente em **7 idiomas**:
🇧🇷 Português (`pt-BR`) • 🇺🇸 English (`en`) • 🇫🇷 Français (`fr`) • 🇪🇸 Español (`es`) • 🇷🇺 Русский (`ru`) • 🇨🇳 简体中文 (`zh-Hans`) • 🇯🇵 日本語 (`ja`)

A Web utiliza rigorosamente a arquitetura em camadas `Core` e `Infrastructure`. A coleta prepara propostas; a publicação e a tradução exigem verificação editorial humana, preservação cultural e MFA. Não há conteúdo fictício apresentado como informação em tempo real.

---

## Banco Único & Idempotente

[MINHABELEM.SQL](MINHABELEM.SQL), na raiz, é a única fonte SQL, também embarcada em Infrastructure. Crie/selecione a base MySQL 8 no SGBD com charset `utf8mb4` e execute **o arquivo inteiro uma vez**.
* Inclui **51 tabelas**, 4 papéis, 17 permissões e 38 atribuições iniciais.
* Migração aditiva idempotente: `-- @migration 008_internacionalizacao_turismo` adiciona tabelas de idiomas (`mb_languages`), traduções (`mb_content_translations`, `mb_place_translations`), glossário cultural amazônico (`mb_cultural_glossary`) e roteiros turísticos (`mb_tourist_itineraries`, `mb_tourist_itinerary_stops`).
* Não cria administrador ou senha padrão; pode ser reexecutado sem perda de registros.

---

## Desenvolvimento Local

Abra `MinhaBelem.sln` no Visual Studio 2022+ com suporte a .NET 8 ou utilize o terminal:

```powershell
dotnet restore MinhaBelem.sln
dotnet build MinhaBelem.sln -c Release
dotnet test MinhaBelem.sln -c Release
dotnet run --project src/MinhaBelem.Web --launch-profile MinhaBelem.Web
```

Configure `ConnectionStrings:Default` nos segredos do projeto Web. O acesso usa HTTPS, Identity, TOTP e códigos de recuperação. Veja [operação e bootstrap](docs/OPERACAO.md).

---

## Módulos e Rotas Ativas

- **Explore Belém (Portal Turístico Internacional):** `/explore`, `/explore/roteiro/{slug}` (roteiros de 1, 2, 3 e 5 dias), `/explore/glossario` (preservação cultural de termos como *Ver-o-Peso*, *Tacacá*, *Círio de Nazaré*, *Açaí*, *Maniçoba*, *Carimbó*).
- **Internacionalização:** Seletor nativo no cabeçalho e rodapé, rotas prefixadas `/{culture}/...` e persistência via cookie `/idioma/mudar`.
- **Administração Multilíngue:** `/admin/idiomas` (gestão e ativação dos idiomas) e `/admin/traducoes` (fila de moderação, comparativo lado a lado `/admin/traducoes/comparar/{id}` e acionamento de tradução via orquestrador de IA).
- **Guia e Agenda:** `/guia` (curadoria própria integrada a Google Places), `/agenda`, `/proximos-eventos`, `/cinema-teatro`, `/esporte-lazer`, `/eventos-gratuitos`, `/meu-bairro`, `/fim-de-semana`.
- **Segurança e Conta:** `/conta/login`, TOTP, recuperação, auditoria e revogação de sessões.
- **SEO Internacional:** `/sitemap.xml` dinâmico com tags `hreflang` e links canônicos.

Documentação técnica: [Homologação Sprint 04](docs/SPRINT-04-HOMOLOGACAO.md) e [Arquitetura](docs/ARQUITETURA.md).
