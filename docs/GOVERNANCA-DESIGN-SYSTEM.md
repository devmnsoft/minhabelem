# GOVERNANÇA DO DESIGN SYSTEM MINHABELÉM
## Regras Permanentes de Arquitetura Visual, Qualidade e Prevenção de Débito Técnico

**Empresa:** MNSOFT  
**Projeto:** MinhaBelém 360  
**Versão:** 1.0 (Instituída na Sprint 20)  
**Aplicação:** Obrigatória para todas as sprints subsequentes e revisões de Pull Request.  

---

## 1. Princípios Fundamentais

O MinhaBelém 360 passou por uma refatoração estrutural definitiva na Sprint 20 para erradicar anos de débito técnico acumulado decorrentes de sobreposições contínuas de CSS e ausência de governança visual.

Para garantir que o portal permaneça limpo, ágil, acessível e visualmente estável a longo prazo, as seguintes 10 Regras Permanentes tornam-se mandatórias para qualquer desenvolvimento futuro.

---

## 2. As 10 Regras Permanentes

### Regra 1: Proibição de CSS Global para Ajustes Locais
Nunca declare regras em `:root`, `body` ou seletores globais como `h1`, `p`, `.card` em folhas de módulo (`agenda.css`, `site.css`) para corrigir um problema específico de uma única página. Se uma View necessita de um ajuste local, utilize classes com escopo explícito ou o arquivo de página dedicado (`pages/*.css`).

### Regra 2: Fonte Única para Tokens Visuais
Tokens de cores, tipografia fluida, espaçamento, sombras e raios de borda existem **exclusivamente** em [`src/MinhaBelem.Web/wwwroot/css/tokens.css`](file:///c:/MNSOFT/minhabelem/src/MinhaBelem.Web/wwwroot/css/tokens.css).
- **Proibido:** Redefinir variáveis CSS com novos valores ou inventar novas cores fora da paleta canônica paraense (`#082f2b`, `#124838`, `#174554`, `#c25732`, `#d4a034`, `#faf7f0`).
- **Obrigatório:** Reutilizar os tokens padronizados (`var(--color-...)`, `var(--space-...)`, `var(--font-size-...)`).

### Regra 3: Não Recriar Componentes Existentes
Antes de codificar um novo botão, card, badge, alerta, modal, tabela ou formulário, consulte [`docs/DESIGN-SYSTEM-6.0.md`](file:///c:/MNSOFT/minhabelem/docs/DESIGN-SYSTEM-6.0.md) e [`components.css`](file:///c:/MNSOFT/minhabelem/src/MinhaBelem.Web/wwwroot/css/components.css). Utilize sempre as classes do Design System 6.0 (`.btn`, `.btn-primary`, `.card`, `.badge`, `.alert`, `.table-scroll`). Não crie variações redundantes (como `.meu-novo-botao-azul`).

### Regra 4: Arquitetura Única de Navegação (Sem Menus Duplicados)
- Páginas públicas utilizam o cabeçalho compacto [`_Layout.cshtml`](file:///c:/MNSOFT/minhabelem/src/MinhaBelem.Web/Views/Shared/_Layout.cshtml) com drawer mobile acessível.
- Páginas administrativas utilizam exclusivamente o menu lateral [`_AdminSidebar.cshtml`](file:///c:/MNSOFT/minhabelem/src/MinhaBelem.Web/Views/Shared/_AdminSidebar.cshtml).
- É terminantemente proibido renderizar barras horizontais adicionais duplicando os mesmos links da barra lateral na mesma tela.

### Regra 5: Eliminação de Estilos Inline nas Views Razor
Estilos do tipo `style="display:flex;gap:12px;color:red"` são estritamente vetados em arquivos `.cshtml`.
- Todo layout deve ser controlado por classes semânticas e utilitários do Design System.
- Estilos inline impedem a customização por temas acessíveis (Alto Contraste) e dificultam testes automatizados de regressão visual.

### Regra 6: Proibição de `!important` como Correção de Atalho
A arquitetura CSS utiliza Camadas de Cascata (`@layer tokens, base, layout, components, accessibility, pages`).
- A especificidade e a precedência são resolvidas nativamente pela ordem das camadas.
- O uso de `!important` é considerado falha grave de arquitetura e será bloqueado na revisão de código.

### Regra 7: Mobile-First e Proibição de Larguras Rígidas em `px`
Nenhum container, card, modal, tabela ou imagem pode possuir `width: [X]px` fixo que impeça a adaptação fluida em telas móveis estreitas (320px–390px).
- Utilize sempre `width: 100%`, `max-width: ...`, `clamp(...)`, `minmax(...)` e CSS Grid / Flexbox.
- É terminantemente proibido aplicar `overflow-x: hidden` no `body` como paliativo para ocultar elementos que estão quebrando a largura da viewport.

### Regra 8: Acessibilidade Obrigatória (WCAG 2.2 AA)
Nenhuma tela pode ser aceita sem conformidade comprovada:
- Todo `<input>` e `<select>` deve possuir `<label for="...">` explícito e correspondente.
- Todo `<button>` deve possuir texto visível ou `aria-label` descritivo.
- Toda `<img>` deve possuir atributo `alt` significativo.
- O anel de foco (`--color-focus: #0f62fe`) deve ser claramente visível ao navegar via teclado (`Tab`).
- Alvos de toque móveis devem ter no mínimo 44×44px.

### Regra 9: Nenhuma Nova Tela Sem Snapshot e Teste Automatizado
Ao adicionar uma nova rota ou View:
- Deve ser incluída na matriz de integração HTTP do ASP.NET Core (`Sprint14Tests` ou suíte equivalente).
- Deve gerar snapshot HTML testado nos 7 idiomas do portal (`pt-BR`, `en`, `fr`, `es`, `ru`, `zh-Hans`, `ja`).

### Regra 10: Validação de Regressão Visual por Capturas de Tela
Nenhuma alteração global de CSS pode ser mesclada na branch principal sem a execução prévia da suíte visual do Playwright:
```powershell
node scripts/visual-qa/check.mjs
```
O script deve reportar `0 failures` e `0 overflows` antes da aprovação do Pull Request.

---

## 3. Checklist Obrigatório para Revisão de Pull Requests (PR)

Antes de aprovar qualquer PR que altere frontend:
- [ ] O PR adicionou novos `!important`? (Se sim, rejeitar).
- [ ] O PR adicionou estilos `style="..."` inline nas Views? (Se sim, rejeitar).
- [ ] O PR redefiniu variáveis em `:root` fora de `tokens.css`? (Se sim, rejeitar).
- [ ] Os testes unitários e de integração passaram (`dotnet test`)?
- [ ] A auditoria Playwright (`node scripts/visual-qa/check.mjs`) foi executada e teve 0 falhas?
- [ ] A tela foi testada em 320px (mobile estreito) sem qualquer rolagem horizontal indesejada?
- [ ] O contraste de texto atende a WCAG 2.2 AA (mínimo de 4.5:1)?
