# Inventário e revisão de views — Sprint 16

Todas as views são compiladas no build. A evidência dedicada indica o HTML efetivamente renderizado por HTTP e usado na matriz Playwright em sete idiomas e quatro larguras (1440/1280/768/390). Partials e estados condicionais não são apresentados como páginas independentes homologadas. A verificação automatizada de acessibilidade é limitada a controles rotulados, alt e botões com nome.

| View | Alteração e revisão | Evidência dedicada |
|---|---|---|
| `_ViewImports.cshtml` | Componente compartilhado; herda foco, cores e comportamento responsivo das páginas hospedeiras. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `_ViewStart.cshtml` | Componente compartilhado; herda foco, cores e comportamento responsivo das páginas hospedeiras. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Admin/Anunciantes.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-anunciantes.html` — HTTP + matriz visual |
| `Admin/Auditoria.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-auditoria.html` — HTTP + matriz visual |
| `Admin/Bairros.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-bairros.html` — HTTP + matriz visual |
| `Admin/Colaboracoes.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-colaboracoes.html` — HTTP + matriz visual |
| `Admin/Comparar.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Admin/Configuracoes.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-configuracoes.html` — HTTP + matriz visual |
| `Admin/Conteudo.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-conteudo.html` — HTTP + matriz visual |
| `Admin/Evolucao.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-evolucao.html` — HTTP + matriz visual |
| `Admin/Fluvial.cshtml` | Composição/controles alterados; tema Amazônia, foco e estados vazios revisados. | `admin-mobilidade-fluvial.html` — HTTP + matriz visual |
| `Admin/Fontes.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-fontes.html` — HTTP + matriz visual |
| `Admin/Idiomas.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-idiomas.html` — HTTP + matriz visual |
| `Admin/Index.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin.html` — HTTP + matriz visual |
| `Admin/Integracoes.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-integracoes.html` — HTTP + matriz visual |
| `Admin/Lugares.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-lugares.html` — HTTP + matriz visual |
| `Admin/Mobilidade.cshtml` | Composição/controles alterados; tema Amazônia, foco e estados vazios revisados. | `admin-mobilidade.html` — HTTP + matriz visual |
| `Admin/Perfis.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-perfis.html` — HTTP + matriz visual |
| `Admin/Radar.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-radar.html` — HTTP + matriz visual |
| `Admin/Relatorios.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-relatorios.html` — HTTP + matriz visual |
| `Admin/Traducoes.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-traducoes.html` — HTTP + matriz visual |
| `Admin/Usuarios.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `admin-usuarios.html` — HTTP + matriz visual |
| `AdminVisit/Index.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Agenda/Index.cshtml` | Composição/controles alterados; tema Amazônia, foco e estados vazios revisados. | `agenda.html` — HTTP + matriz visual |
| `Bairro/Detalhe.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Bairro/Index.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Conta/AcessoNegado.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Conta/Login.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `conta-login.html` — HTTP + matriz visual |
| `Conta/Mfa.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Conta/Perfil.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `conta-perfil.html` — HTTP + matriz visual |
| `Conta/Recovery.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Conta/Sessoes.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `conta-sessoes.html` — HTTP + matriz visual |
| `Conta/Verificar.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Editorial/Comparar.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Editorial/Editar.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Editorial/Index.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `editorial.html` — HTTP + matriz visual |
| `Editorial/Ocr.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `EditorialBatches/Details.cshtml` | Progresso, resultado por item, estados e tema compartilhado revisados. | `editorial-batch-detail.html` — HTTP + matriz dedicada em sete idiomas/quatro larguras |
| `EditorialBatches/Index.cshtml` | Composição/controles alterados; tema Amazônia, foco e estados vazios revisados. | `editorial-lotes.html` — HTTP + matriz visual |
| `Explore/Glossario.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Explore/Index.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `explore.html` — HTTP + matriz visual |
| `Explore/Roteiro.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Guia/Detalhe.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Guia/Index.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `guia.html` — HTTP + matriz visual |
| `Home/Credits.cshtml` | Composição/controles alterados; tema Amazônia, foco e estados vazios revisados. | `creditos.html` — HTTP + matriz visual |
| `Home/Detalhe.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `agenda-detail.html` — HTTP + matriz visual |
| `Home/Error.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Home/Index.cshtml` | Composição/controles alterados; tema Amazônia, foco e estados vazios revisados. | `home.html` — HTTP + matriz visual |
| `Home/Privacidade.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `privacidade.html` — HTTP + matriz visual |
| `Home/Status.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Home/Termos.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `termos.html` — HTTP + matriz visual |
| `ItineraryPlanning/Index.cshtml` | Composição/controles alterados; tema Amazônia, foco e estados vazios revisados. | `meu-dia-em-belem.html` — HTTP + matriz visual |
| `Media/Index.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Mobilidade/Alertas.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `mobilidade-alertas.html` — HTTP + matriz visual |
| `Mobilidade/Fluvial.cshtml` | Composição/controles alterados; tema Amazônia, foco e estados vazios revisados. | `mobilidade-fluvial.html` — HTTP + matriz visual |
| `Mobilidade/Index.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `mobilidade.html` — HTTP + matriz visual |
| `Mobilidade/Linha.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Parceiros/Index.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `parceiros.html` — HTTP + matriz visual |
| `Shared/_AdminLayout.cshtml` | Componente compartilhado; herda foco, cores e comportamento responsivo das páginas hospedeiras. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Shared/_AdminSidebar.cshtml` | Componente compartilhado; herda foco, cores e comportamento responsivo das páginas hospedeiras. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Shared/_CollectionQueue.cshtml` | Componente compartilhado; herda foco, cores e comportamento responsivo das páginas hospedeiras. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Shared/_Layout.cshtml` | Componente compartilhado; herda foco, cores e comportamento responsivo das páginas hospedeiras. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Shared/_MediaAssets.cshtml` | Componente compartilhado; herda foco, cores e comportamento responsivo das páginas hospedeiras. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Shared/_MediaGallery.cshtml` | Componente compartilhado; herda foco, cores e comportamento responsivo das páginas hospedeiras. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `Shared/_PlaceMediaGallery.cshtml` | Componente compartilhado; herda foco, cores e comportamento responsivo das páginas hospedeiras. | Build e testes funcionais quando aplicáveis; sem captura dedicada deste estado. |
| `TouristAssistant/Index.cshtml` | Tema compartilhado de cores, foco, formulários e tabelas; fluxo específico preservado. | `guia-ia.html` — HTTP + matriz visual |
| `Visit/Index.cshtml` | Composição/controles alterados; tema Amazônia, foco e estados vazios revisados. | `visit-place.html` — HTTP + matriz visual |
