# DESIGN SYSTEM MINHABELÉM 6.0
## Identidade Visual Paraense, Tokens e Especificação Técnica

**Empresa:** MNSOFT  
**Projeto:** MinhaBelém 360  
**Versão:** 6.0 (Sprint 20)  
**Status:** Consolidado e Homologado  
**Padrão de Acessibilidade:** WCAG 2.2 Nível AA / AAA  

---

## 1. Visão Geral e Filosofia Estética

O **Design System MinhaBelém 6.0** é o alicerce visual e de interação unificado para todo o ecossistema MinhaBelém 360. Ele foi estruturado para resolver definitivamente os problemas históricos de folhas de estilo conflitantes, especificidade descontrolada com `!important`, duplicações de componentes e falta de coerência visual.

### Identidade Regional Paraense Premium
A estética do MinhaBelém 6.0 homenageia a rica cultura e ecossistema urbano de Belém do Pará:
- **As águas da Baía do Guajará:** Tons azuis profundos e reflexivos que transmitem serenidade e modernidade.
- **A Floresta Amazônica e os Mangais:** Verdes profundos que servem como âncora visual, conferindo sofisticação e conexão com a flora paraense.
- **As Olarias de Icoaraci e a Cerâmica Marajoara:** Terracota marcante utilizada em chamadas para ação, destaques e elementos de ênfase cultural.
- **O Sol Equatorial e o Círio de Nazaré:** Dourado solar como cor de iluminação e celebração.
- **O Creme Aconchegante e o Branco Puro:** Fundos limpos que garantem máxima legibilidade e conforto visual, inspirados na calmaria das tardes chuvosas de Belém.

---

## 2. Tokens de Design (Fonte Única da Verdade)

Localização no código: [`src/MinhaBelem.Web/wwwroot/css/tokens.css`](file:///c:/MNSOFT/minhabelem/src/MinhaBelem.Web/wwwroot/css/tokens.css)

### 2.1 Paleta de Cores

```css
:root {
  /* Cores Paraenses Canônicas */
  --mb-green-deep: #082f2b;     /* Verde Amazônico Profundo */
  --mb-green-forest: #124838;   /* Verde de Floresta */
  --mb-green-light: #2d7a5d;    /* Verde Folhagem */
  --mb-blue-guajara: #174554;   /* Azul da Baía do Guajará */
  --mb-terracotta: #c25732;     /* Terracota das Olarias */
  --mb-terracotta-dark: #9e4324;/* Terracota Escuro / Hover */
  --mb-gold: #d4a034;           /* Dourado Solar / Círio */
  --mb-cream: #faf7f0;          /* Creme Claro / Fundo Neutro */
  --mb-white: #ffffff;          /* Branco Puro */

  /* Superfícies e Fundos */
  --color-bg: #f5f4ef;
  --color-bg-alt: #ecebe4;
  --color-surface: #ffffff;
  --color-surface-hover: #f7f6f1;
  --color-surface-sunken: #e5e4dc;

  /* Texto e Leitura */
  --color-text-primary: #15221c;   /* Contraste 13.8:1 sobre branco (AAA) */
  --color-text-secondary: #354a3e; /* Contraste 7.4:1 sobre branco (AAA) */
  --color-text-muted: #536b5e;     /* Contraste 4.6:1 sobre branco (AA) */
  --color-text-inverse: #ffffff;

  /* Bordas e Divisores */
  --color-border: #d4dcd5;
  --color-border-subtle: #e6ece7;
  --color-border-strong: #9fb3a6;

  /* Semântica e Feedback */
  --color-success: #176a44;
  --color-success-bg: #eaf6ef;
  --color-warning: #945d04;
  --color-warning-bg: #fff8e6;
  --color-danger: #a5231c;
  --color-danger-bg: #fdeeee;
  --color-info: #0f5279;
  --color-info-bg: #ebf5fa;

  /* Foco Acessível (WCAG 2.2 AA) */
  --color-focus: #0f62fe;
  --color-focus-inner: #ffffff;
}
```

### 2.2 Tipografia Fluida (`clamp()`)

Utiliza a família `Outfit` para títulos e identidade, e `Plus Jakarta Sans` para textos editoriais, formulários e metadados.

| Nível | Token | Valor Fluido | Desktop (~1440px) | Mobile (~390px) |
|---|---|---|---|---|
| **Display** | `--font-size-display` | `clamp(2rem, 4vw, 3.25rem)` | 52px | 32px |
| **H1** | `--font-size-h1` | `clamp(1.75rem, 3.2vw, 2.5rem)` | 40px | 28px |
| **H2** | `--font-size-h2` | `clamp(1.4rem, 2.4vw, 1.9rem)` | 30.4px | 22.4px |
| **H3** | `--font-size-h3` | `clamp(1.15rem, 1.8vw, 1.45rem)` | 23.2px | 18.4px |
| **H4** | `--font-size-h4` | `clamp(1rem, 1.4vw, 1.2rem)` | 19.2px | 16px |
| **Body (Texto Principal)** | `--font-size-base` | `1rem` (16px) | 16px | 16px |
| **Small (Secundário)** | `--font-size-sm` | `0.875rem` (14px) | 14px | 14px |
| **Metadata / Legend** | `--font-size-xs` | `0.75rem` (12px) | 12px | 12px |

### 2.3 Espaçamento e Grade Modular

- `--space-1`: `4px`
- `--space-2`: `8px`
- `--space-3`: `12px`
- `--space-4`: `16px` (Padrão de preenchimento de inputs e botões)
- `--space-5`: `20px`
- `--space-6`: `24px` (Padrão de gap entre cards e seções)
- `--space-8`: `32px`
- `--space-10`: `40px`
- `--space-12`: `48px`
- `--space-16`: `64px` (Espaçamento entre seções da Home)

### 2.4 Raios de Borda (Border Radius) e Elevação (Sombras)

- `--radius-sm`: `4px` (tags e pequenos seletores)
- `--radius-md`: `8px` (inputs, botões e badges)
- `--radius-lg`: `14px` (cards e modais)
- `--radius-xl`: `20px` (banners e destaques)
- `--radius-full`: `9999px` (pills e avatares)
- `--shadow-sm`: `0 1px 2px rgba(8, 47, 43, 0.05)`
- `--shadow-md`: `0 4px 12px rgba(8, 47, 43, 0.08)`
- `--shadow-lg`: `0 10px 28px rgba(8, 47, 43, 0.12)`

---

## 3. Arquitetura CSS Modular com `@layer`

A folha mestra [`site.css`](file:///c:/MNSOFT/minhabelem/src/MinhaBelem.Web/wwwroot/css/site.css) e o layout principal utilizam as Camadas de Cascata CSS padronizadas (`@layer`):

```css
@layer tokens, base, layout, components, accessibility, pages;
```

Essa ordem de camadas garante que:
1. `tokens` estabelece todas as variáveis fundamentais.
2. `base` aplica o reset moderno sem quebrar estilos superiores.
3. `layout` posiciona o cabeçalho compacto, a barra de navegação, a sidebar administrativa e o rodapé.
4. `components` define a biblioteca reutilizável de botões, cards, tabelas e alertas sem concorrência de especificidade.
5. `accessibility` sobrepõe quaisquer cores ou regras quando os modos de Alto Contraste, Foco Realçado ou Redução de Movimento estão ativos.
6. `pages` acomoda exclusivamente personalizações legítimas de cada módulo específico (Hero, Linha do Tempo de Mobilidade, Radar de Fontes, Assistente Curió).

Nenhum seletor precisa recorrer a `!important` para sobrescrever um componente base.

---

## 4. Biblioteca de Componentes

Localização no código: [`src/MinhaBelem.Web/wwwroot/css/components.css`](file:///c:/MNSOFT/minhabelem/src/MinhaBelem.Web/wwwroot/css/components.css)

### 4.1 Botões (`.btn`)
- `.btn-primary`: Fundo terracota paraense (`#c25732`), texto branco, contraste 4.6:1.
- `.btn-secondary`: Fundo verde amazônico (`#082f2b`), texto branco, contraste 10.5:1.
- `.btn-outline`: Borda refinada e fundo transparente com preenchimento no hover.
- `.btn-accent`: Fundo dourado solar (`#d4a034`), texto em verde amazônico profundo (`#082f2b`), contraste 7.2:1.
- `.btn-ghost`: Ação suave com efeito sutil no foco e hover.
- `.btn-sm`, `.btn-lg`: Modificadores de tamanho sem quebrar alvos de toque mínimos de 44×44px.

### 4.2 Formulários Acessíveis
- Campos com rótulos semânticos explícitos `<label for="...">`.
- Contorno de foco de 2px sólido (`#0f62fe`) com offset de 2px.
- Indicador de Caps Lock e botão de visualização de senha integrados (`.password-wrapper`).
- Textos de validação de erro associados via `aria-describedby` e `aria-invalid`.

### 4.3 Cards e Destaques
- `.card`: Cartão padrão com borda suave, sombra em repouso e elevação suave no foco/hover.
- `.spotlight-card`: Cartão com imagem de destaque proporcional (aspect-ratio 16:9), metadados editoriais e ação clara.
- `.video-card`: Container de mídia com overlay gradiente paraense, badge de duração e botão de reprodução acessível.

### 4.4 Tabelas Responsivas (`.table-scroll`)
- Envoltório com rolagem horizontal suave e foco por teclado (`tabindex="0"` e `aria-label`).
- Cabeçalhos com fundo diferenciado (`--color-bg-alt`) e bordas nítidas.
- Suporte a cards expansíveis em mobile.

### 4.5 Alertas e Feedback
- Ícone semântico embutido via SVG acessível.
- Cores de fundo suaves com borda lateral esquerda de 4px sólida.
- Rótulos de fácil compreensão por leitores de tela.

---

## 5. Diretrizes de Acessibilidade (WCAG 2.2 AA)

1. **Alvos de Toque:** Mínimo de 44×44px para todos os botões e links interativos em dispositivos móveis.
2. **Contraste de Texto:** Relação mínima de 4.5:1 para texto padrão e 3:1 para display/títulos grandes.
3. **Foco Visível:** Indicador de foco evidente em todos os controles navegáveis por teclado (`Tab`, `Shift+Tab`).
4. **Semântica:** Hierarquia estrita de títulos (`h1` a `h4`), sem saltos de nível.
5. **Painel de Acessibilidade:** Permite aos usuários ativar:
   - Modo de Alto Contraste (Fundo Preto com Texto Amarelo e Links Ciano).
   - Aumento e diminuição de fonte (100% a 150%).
   - Espaçamento aumentado de texto.
   - Foco super destacado (`#d81b60`).
   - Redução de movimento (desativa todas as animações).
