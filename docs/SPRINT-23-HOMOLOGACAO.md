# Sprint 23 — Evolução de Design, IA, Vídeo, Fotos e Acessibilidade

**Projeto:** MinhaBelém 360 — Portal Digital Inteligente, Mobilidade & Cidade Conectada
**Escopo:** evolução do template, do assistente de IA, do tratamento de vídeo/foto, da responsividade e correção de bugs de funcionalidade.

---

## 1. Bugs de funcionalidade corrigidos

| # | Local | Problema | Impacto | Correção |
|---|-------|----------|---------|----------|
| 1 | `Views/Shared/_Layout.cshtml` | Tag `<!option …>` / `</!option>` inválida no seletor de idioma | O navegador interpretava as opções como comentário HTML — o seletor de idioma **não renderizava** | Markup das `<option>` montado no bloco de código (`languageOptions`) |
| 2 | `Views/Conta/Perfil.cshtml` | Mesma tag inválida no seletor de idioma do perfil | Troca de idioma do usuário indisponível | `<option value="@language"@selectedAttr>` |
| 3 | `Views/Admin/Conteudo.cshtml` | 7 tags `<!option>` nos filtros de status e categoria | Filtros do painel de conteúdo **quebrados** | Tags válidas + atributo `selected` calculado |
| 4 | 9 views (`Agenda`, `Admin/Radar`, `EditorialBatches`, `Visit`, `ItineraryPlanning`, `AdminVisit`, `Admin/Configuracoes`, `Admin/Perfis`) | `selected="@(bool)"` / `checked="@(bool)"` | Em HTML, `selected` é atributo booleano: `selected="False"` **ainda seleciona** a opção. Todos os itens ficavam marcados e o navegador ficava com o último | Atributo pré-calculado (`" selected"` / `""`) antes da renderização |
| 5 | `Views/Home/Index.cshtml` | Cards de vídeo referenciavam `SocialPost.YoutubeId`, `.MediaUrl`, `.ThumbnailUrl`, `.PostedAt` — propriedades inexistentes | **Erro de compilação** e seção de vídeo inoperante | Uso de `EmbedHtml`, `Platform`, `PublishedAt` e `MediaType` reais do modelo |
| 6 | `Views/Shared/_Layout.cshtml` | Bloco de idioma usava `@(...)` na área de atributos do `<option>` | `RZ1031: The tag helper 'option' must not have C# in the element's attribute declaration area` | Construção do markup fora da área de atributos |
| 7 | `wwwroot/sw.js` | Service worker não cacheava os novos assets de evolução | Modo offline degradado | `cacheName` bumped para `shell-23` + novos assets |
| 8 | `wwwroot/js/site.js` + `wwwroot/js/evolution.js` | O botão do menu mobile tinha **dois** listeners de clique (um em cada arquivo) | O primeiro abria o menu e o segundo fechava: o menu mobile **nunca abria** | Handler duplicado removido de `evolution.js`; `site.js` mantém a posse do clique |
| 9 | `Views/Media/Enviar.cshtml` | `input[type=file]` com `aria-hidden="true"` dentro de um `role="button"` | Elemento focável oculto para leitores de tela | `role="group"` + `aria-label` real e `<label for>` explícito |
| 10 | 8 views (Home, Midias, Videos, `_MediaAssets`, TouristAssistant) | Imagens sem `width`/`height` | Deslocamento de layout (CLS) ao carregar | Dimensões declaradas em todas as imagens novas |

---

## 2. Design system evoluído (`wwwroot/css/evolution.css`)

Novo token set e camada de componentes carregada **depois** das folhas legadas, para evoluir sem quebrar os componentes já homologados.

- **Tokens** amazônicos consolidados (`--mb-deep`, `--mb-terracotta`, `--mb-gold`, `--mb-radius-*`, `--mb-shadow-*`) com curva de transição única (`--mb-transition`).
- **Modo escuro completo** (`html[data-theme="dark"]`): paletas, cartões, rodapé, formulários, área de mídia e heros próprios, respeitando contraste AA.
- **Hero com vídeo**: `.hero.video-hero`, `.hero-video`, `.hero-video-overlay` com carregamento progressivo e `poster` de fallback.
- **Cards de vídeo**: `.video-card`, `.video-wrapper`, `.play-overlay`, `.video-badge`, `.video-embed`, `.video-placeholder`.
- **Galeria de fotos**: `.photo-gallery`, `.photo-item`, `.photo-badge` com legenda em gradiente e zoom no hover.
- **Lightbox acessível**: `.mb-lightbox` com navegação por teclado (←/→/Esc), foco gerenciado, descrição por `aria-label` e legenda.
- **Upload com arrastar e soltar**: `.photo-upload-zone`, `.photo-previews`, `.photo-preview-remove`.
- **Curió IA**: `.ai-hero-capabilities`, `.ai-form-actions`, `.ai-shortcut-hint`, `.ai-empty-state`, `.ai-block`, `blockquote/code` no markdown.
- **Revelação ao rolar**: `.reveal` / `.reveal.revealed` via `IntersectionObserver`, com fallback `html:not(.js-ready)`.
- **Responsividade**: menu mobile vira painel deslizante sobreposto (`position:absolute`), grade de vídeo/fotos colapsa em 520px, barra de ação da IA empilha em 768px.

---

## 3. JavaScript evoluído (`wwwroot/js/evolution.js`)

- **Tema claro/escuro** com persistência em `localStorage` (`minhabelem_theme`) e respeito a `prefers-color-scheme`.
- **Widget flutuante de IA** com pulso animado e navegação para `/guia-ia`.
- **Lightbox** de fotos e vídeos: agrupamento por `data-lightbox`, teclado, swipe implícito por setas, foco devolvido ao fechar.
- **Lazy loading** de `img`, `iframe`, `video` e `source` via `IntersectionObserver` (`data-src`, `data-srcset`, `data-embed`).
- **Upload de fotos** com arrastar e soltar, pré-visualização em `FileReader` e remoção individual (`DataTransfer`).
- **Controles de vídeo**: `play-overlay` que inicia a reprodução e reaparece ao pausar/finalizar.
- **Menu mobile** com `aria-expanded`, fechamento por `Esc` e ao navegar.
- **Barra de progresso de leitura** e animação de revelação.

---

## 4. IA — Curió assistente (+1)

- **Hero com capacidades**: Locais, Agenda, Linhas e Mídias destacados como blocos clicáveis.
- **Atalho `Ctrl + Enter`** para enviar a pergunta sem mouse.
- **Estado vazio** com perguntas prontas quando ainda não houve consulta.
- **Leitura em voz alta** (Web Speech API) mantida e ampliada com rótulo acessível.
- **Copiar resposta** e **copiar link** da consulta.
- **Mídia com licença** migrada da grade de cards para a **galeria de fotos com lightbox**, preservando crédito e licença.
- **Barra de resultados** com `aria-live="polite"`.
- Traduções novas em `Resources/Sprint23Translations.cs` para os 7 idiomas.

---

## 5. Vídeos

- **Hero com vídeo de fundo** na Home (`/videos/belem-hero.mp4` + `.webm`, `poster` de fallback e `playsinline muted loop`).
- **Nova página `/videos`** (`MediaSurfaceController`) com três blocos:
  1. vídeos oficiais (`mb_media_assets` com `youtube_id` ou arquivo de vídeo);
  2. publicações do Radar (`SocialPost` com `EmbedHtml` sanitizado pela origem);
  3. galeria de fotos do acervo.
- **Nova página `/midias`** unificando fotos e vídeos.
- **Cards de vídeo** com `iframe` `youtube-nocookie`, `referrerpolicy` restritivo, `allowfullscreen` e `loading="lazy"`.
- **Embeds do Radar** com `EmbedHtml` renderizado em `.video-embed` (container com `aspect-ratio`).
- Ícone `video` adicionado ao spritesheet `wwwroot/images/icons.svg` (junto de `photo`, `sparkle`, `play`, `leaf`).

---

## 6. Fotos

- **Galeria com lightbox** na Home, em `/midias`, no acervo (`/guia/acervo`) e nos resultados da IA.
- **Envio pela comunidade** (`/fotos/enviar`): upload PNG/JPEG/WebP até 6 MB com detecção de tipo por *magic bytes* (não por extensão), crédito, licença, local, bairro e consentimento; opcionalmente link de vídeo externo (YouTube/Vimeo).
- **Fila editorial**: `mb_media_submissions` (migração `sprint23_photo_video_submissions`) recebe tudo como `Pending`.
- **Painel de conferência** (`/admin/midias/submissoes`): aprovar (promove para `mb_media_assets` + vínculo opcional com conteúdo/lugar, com registro em `mb_audit_logs`) ou rejeitar com notas.
- **Endpoint protegido** `/midia-envio/{id}` serve a imagem pendente apenas para quem tem `content.write`.
- **Mídia compartilhada** (`_MediaAssets.cshtml`) migrada para a galeria com lightbox e vídeos embutidos.

---

## 7. Rotas novas

| Rota | Descrição |
|------|-----------|
| `/videos` | Belém em vídeo (mídia oficial + Radar + acervo) |
| `/midias` | Fotos e vídeos em um só lugar |
| `/fotos/enviar` | Envio de foto/vídeo pela comunidade |
| `/admin/midias/submissoes` | Conferência editorial das submissões |
| `/admin/midias/submissoes/{id}/decidir` | Aprovar ou rejeitar submissão |
| `/midia-envio/{id}` | Imagem pendente (restrito à equipe) |

`/videos`, `/midias` e `/guia-ia` foram incluídos no **sitemap.xml** com `hreflang` para os 7 idiomas.

---

## 8. Acessibilidade e internacionalização

- Botão de tema com `aria-pressed` e rótulo descritivo.
- Lightbox com `role="dialog"`, `aria-modal`, `aria-hidden` e controle por teclado.
- Botão flutuante de IA com `aria-label` descritivo.
- `aria-current="page"` preservado nos links de navegação ativos.
- Todas as chaves novas traduzidas para `pt-BR, en, fr, es, ru, zh-Hans, ja`.
- `prefers-reduced-motion` respeitado em todas as animações novas.

---

## 9. Banco de dados

Nova migração aditiva e idempotente em `MINHABELEM.SQL`:

```sql
-- @migration sprint23_photo_video_submissions
CREATE TABLE IF NOT EXISTS mb_media_submissions ( ... );
```

A migração é inserida **antes** de `sprint21_lugares_canonicos`, preservando o conteúdo (e o checksum) das migrations já aplicadas — o `SchemaMigrator` recusa subir quando um checksum diverge.

---

## 10. Verificação

```powershell
dotnet build MinhaBelem.sln -c Release
dotnet test MinhaBelem.sln -c Release
dotnet run --project src/MinhaBelem.Web --launch-profile MinhaBelem.Web
dotnet run --project src/MinhaBelem.Web -- --initialize-database
```

Nenhuma migration já aplicada foi alterada: o diff de `MINHABELEM.SQL` contém **apenas** o bloco novo.
A migration `sprint23_photo_video_submissions` foi aplicada com sucesso em ambiente local.

### Helper de marcação de `<option>`

`src/MinhaBelem.Web/Models/SelectOptions.cs` centraliza a geração das opções de
`<select>` e de `input[type=checkbox]`. Motivo: o tag helper `option` do ASP.NET Core
proíbe expressões C# na área de atributos (`RZ1031`) e `selected="@(bool)"` renderiza
`selected="False"`, que em HTML **ainda marca a opção** (atributo booleano). O helper
escreve a marcação final com o atributo presente apenas quando verdadeiro.

```razor
@Html.Raw(SelectOptions.Build(items, selectedValue))
@Html.Raw(SelectOptions.Checkbox("gratuito", "true", isChecked))
```

### Checklist verificado em execução (HTTPS local)

- `/`, `/videos`, `/midias`, `/fotos/enviar`, `/guia-ia`, `/agenda`, `/guia`, `/explore`,
  `/mobilidade` e `/sitemap.xml` → **HTTP 200**.
- Nenhuma ocorrência de `<!option`, `selected="@(` ou `checked="@(` no HTML renderizado.
- Seletor de idioma com **7 opções** e a opção corrente corretamente marcada.
- Modo escuro alterna e persiste; lightbox abre, navega e fecha com `Esc`.
- Envio de foto real (PNG) gravado em `App_Data/media-pending` e registrado como `Pending`.
- Previsualização de arquivo via arrastar e soltar com botão de remover.
- Sem overflow horizontal; media queries de 1024/900/860/768/600/520px aplicadas.
- `h1` único por página, `main#conteudo`, skip link, `lang` correto, imagens com `alt`.
