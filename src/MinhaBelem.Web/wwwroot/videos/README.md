# Vídeos do herói e do acervo

Coloque aqui os arquivos de vídeo usados pelo site.

## Herói da página inicial

O `Views/Home/Index.cshtml` referencia, nesta ordem:

| Arquivo | Formato | Uso |
|---------|---------|-----|
| `belem-hero.mp4` | MP4 (H.264) | fonte principal |
| `belem-hero.webm` | WebM (VP9) | fonte alternativa |

Recomendações de produção:

- **Resolução:** 1920×1080 (ou 2560×1440 para telas grandes).
- **Duração:** 12 a 20 segundos, em loop contínuo e silencioso.
- **Peso:** até 4 MB no MP4 e 2,5 MB no WebM.
- **Sem áudio:** o elemento usa `muted` e `playsinline`; com som, o
  autoplay seria bloqueado pelos navegadores.
- **Formato livre:** sem marcas d'água e sem texto sobreposto ao conteúdo,
  já que o herói aplica um gradiente sobre o vídeo.

A imagem `poster` (`/images/belem-guajara-hero.jpg`) aparece enquanto o vídeo
carrega. Se os arquivos não existirem, o `evolution.js` remove o elemento
`<video>` e o herói mantém o fundo estático — sem erro no console.

## Vídeos da galeria

Vídeos publicados pela equipe editorial (YouTube) ou enviados pela comunidade
(link externo) ficam em `mb_media_assets` e `mb_media_submissions` e são
exibidos em `/videos` e `/midias`. Arquivos de vídeo enviados pela comunidade
são armazenados pelo controlador de mídia com a mesma validação de tipo usada
para imagens (magic bytes) e ficam pendentes de conferência editorial.
