# Sprint 24 — Mobilidade Urbana Real, IA Operante, Identidade Visual e Contraste

**Projeto:** MinhaBelém 360 — Portal Digital Inteligente, Mobilidade & Cidade Conectada
**Foco:** dados oficiais de transporte, IA funcionando, correções de design e de eventos repetidos.

---

## 1. Dados de mobilidade: pesquisa e curadoria oficial

Toda a base de mobilidade foi reconstruída a partir das **fontes oficiais**, não de estimativas:

| Fonte | Uso |
|---|---|
| **SEGBEL/SEMOB** — `seguranca.belem.pa.gov.br/linhas/` | As 22 linhas do sistema **Integra Belém**, com os dois sentidos (de-para) |
| **SEGBEL/SEMOB** — página de cada linha | Itinerário oficial completo, eixo por eixo, nos sentidos IDA e VOLTA |
| **SEGBEL/SEMOB** — `seguranca.belem.pa.gov.br/terminais/` | 16 terminais e estações de integração (física e temporal) |
| **SEGBEL/SEMOB** — `seguranca.belem.pa.gov.br/integra-belem/` | Regras de integração, cartões Passe Fácil, gratuidades e cobrança por sentido |
| **Decreto Municipal nº 113.606 de 11/04/2025** | Tarifas oficiais em vigor desde 14/04/2025 |
| **BRT Amazônia / Pra Já Metropolitano** | 36 linhas do BRT Metropolitano (9 troncais expressas e 27 alimentadoras) |

### Tarifas oficiais aplicadas

| Trajeto | Tarifa | Meia-passagem |
|---|---|---|
| Ônibus urbano (Decreto, art. 1º) | **R$ 4,60** | R$ 2,30 |
| Belém–Mosqueiro (art. 2º) | **R$ 7,40** | R$ 3,70 |
| Hidroviário Icoaraci–Cotijuba, seg a sex (art. 3º, I) | **R$ 4,60** | R$ 2,30 |
| Hidroviário Icoaraci–Cotijuba, sáb/dom/fer (art. 3º, II) | **R$ 9,20** | R$ 4,60 |
| Micro-ônibus seletivo (art. 4º) | **R$ 7,40** | — |
| Complementar e suplementar de Mosqueiro (arts. 5º e 6º) | **R$ 4,60** | R$ 2,30 |

### Migrations aplicadas

| Migration | Conteúdo |
|---|---|
| `sprint24_transit_routes` | 22 linhas do Integra Belém + **610 pontos de itinerário** (IDA e VOLTA) + 11 terminais/estações + horários + tarifas + fonte verificada |
| `sprint25_transit_brt_fluvial` | 36 linhas do BRT Metropolitano + terminais do BRT + travessias fluviais com tarifas do decreto + remoção dos 5 registros legados duplicados |
| `sprint26_transit_brt_corridor` | Sequência do corredor exclusivo da BR-316 para as linhas troncais |
| `sprint27_transit_accents` | Grafia oficial dos nomes (acentuação correta de terminais, estações e linhas) |
| `sprint29_transit_accents` | Grafia oficial completa: linhas, terminais, estações, travessias e notas de integração |
| `sprint30_transit_address_accents` | Grafia oficial de endereços, bairros e municípios dos pontos de embarque |
| `sprint31_transit_final_accents` | Últimos nomes: Estação Centenário, Estação Outeiro (Brasília), Estação Praça da Bandeira |

> A `sprint28` foi descartada antes de ser aplicada (SQL mal formado) e removida do
> arquivo `MINHABELEM.SQL`, para não deixar marca de checksum inconsistente.

Cada linha e parada agora carrega `source_url`, `verified_at` e `valid_until` reais. Com isso
`IsVerified` passa a ser verdadeiro e a interface deixa de exibir "Tarifa não confirmada" ou
"Horário não publicado" — que era exatamente o sintoma relatado.

### Novas rotas

| Rota | Função |
|---|---|
| `/mobilidade/paradas` | 16 terminais e estações, com filtro por bairro e por linha |
| `/mobilidade/de-para?origem=&destino=` | Planejador que cruza as linhas e indica onde trocar de ônibus |
| `/mobilidade/linha/{código}` | Itinerário oficial completo, horário, tarifa, integração e terminais atravessados |
| `/mobilidade/fluvial` | Travessias com horários e tarifas oficiais |

A página de mobilidade ganhou ainda a seção **"Mais formas de se mover"** com os serviços
públicos oficiais: Rede Cicloviária, Táxi, Mototáxi, Integra Belém, mapa de radares e
locais de atendimento.

---

## 2. IA: de estática para operativa

### O que estava quebrado

1. `isTransit` era **calculado e nunca usado** no motor: toda pergunta de transporte caía na
   resposta genérica.
2. A nuvem (Groq/Gemini/DeepSeek) era usada **apenas para ranquear** os registros; o texto
   vinha sempre de um motor estático com respostas fixas, sem nenhum dado real.
3. O motor não tinha acesso a terminais, paradas nem itinerários.

### O que foi implementado

- **Motor Curió reescrito** (`NativeBelemAiEngine`) sobre um `CurioContext` que carrega
  lugares, eventos, **linhas, terminais, paradas, itinerários e travessias** reais.
- **Novo ramo de mobilidade** que responde com dado publicado:
  - linha citada → horário, tarifa, integração, observação e o itinerário IDA/VOLTA;
  - ponto citado ("onde fica o Terminal São Brás") → endereço, bairro, linhas que passam e as
    regras de integração física x temporal;
  - destino citado ("qual linha vai para o Ver-o-Peso?") → as linhas que atendem ao pedido
    (758, 785, 861, 911 no caso do Ver-o-Peso);
  - "de X até Y" → as linhas que **de fato ligam** os dois pontos; quando não existe linha
    direta, a resposta separa "a partir da origem" e "para chegar ao destino" e indica o
    terminal de integração onde trocar;
  - sem correspondência → panorama da rede com terminais e travessias.
- **Busca em duas direções**: `MentionedIn` (o nome do ponto aparece na pergunta) para
  responder "onde fica X", e `Mentions` (o texto do registro contém o termo) para citar a
  linha. Comparação sempre sem acento: "São Brás" = "Sao Bras".
- **Geração ancorada na nuvem**: quando há provedor configurado, o modelo responde **usando
  apenas os registros fornecidos**, com instrução de sistema que proíbe inventar lugar, linha,
  preço, horário ou duração, e exige citar a rota do portal. O motor nativo assume quando o
  provedor falha — a IA nunca fica muda.
- **Disjuntor por provedor**: 3 falhas consecutivas abrem o circuito por 10 minutos, para
  não gastar ~500 ms por requisição enquanto a credencial está sem permissão.
- Nova tarefa de auditoria `AiTask.TouristAnswer`, com registro de custo, latência e modelo.

### Chave do Gemini

A chave foi gravada em **user secrets** (`Ai:Gemini:ApiKey`), fora do repositório, junto de
`Ai:Gemini:Model=gemini-2.5-flash` e `Ai:DefaultProvider=Gemini`.

> **Atenção:** a chave é válida, mas a **Generative Language API está desativada** no projeto
> Google Cloud `572050271542`. Basta habilitá-la em
> `https://console.developers.google.com/apis/api/generativelanguage.googleapis.com/overview?project=572050271542`
> (e aguardar alguns minutos) para a geração na nuvem passar a responder. Até lá, o motor
> nativo do Curió já responde com os dados publicados.

---

## 3. Eventos repetidos

O relatório `--duplicate-report` mostrou **zero duplicatas por título e data** no banco. A
repetição vinha da **renderização**: a capa mostrava os mesmos eventos no bloco "Próximos
eventos" **e** no grid "Últimas notícias".

Correções:

1. `ContentQuery.ExcludeEvents` — a lista geral da capa passa a excluir eventos quando a
   categoria não é "eventos", já que existe um bloco próprio para eles.
2. **Deduplicação no SQL** (CTE com `ROW_NUMBER()`), na consulta pública: eventos com o
   mesmo título normalizado e a mesma data de início colapsam em um único registro, mantendo
   o verificado e o mais recente. Isso cobre também a Agenda, que antes podia listar o mesmo
   fato duas vezes.

Novos comandos operacionais (só em Development):

```powershell
dotnet run --project src/MinhaBelem.Web -- --mobility-report
dotnet run --project src/MinhaBelem.Web -- --duplicate-report
```

---

## 4. Design: contraste, logo e identidade

### 4.1 Causa raiz do "fundo confundindo com a fonte"

O sintoma real era o **modo escuro**: o texto passava a branco enquanto vários
componentes mantinham o fundo claro fixo das folhas legadas — texto branco sobre
cartão branco.

Havia ainda dois erros de cascata que anulavam as correções:

- `evolution.css` era carregada **antes** de `agenda.css`, e a view da Agenda
  carregava `agenda.css` **de novo** — as regras de contraste perdiam;
- a folha de evolução tem regras duplicadas (`.eyebrow`, `.button-link`…), e a
  mais antiga vincia por estar depois.

Correções: `evolution.css` passou a ser a **última** folha do layout, o `<link>`
duplicado da Agenda foi removido, o bloco de contraste foi movido para o
**final** do arquivo e as superfícies com fundo claro fixo (`.card`, `.story`,
`.event-spotlight-card`, `.agenda-card`, `.city-window`, `.account-card`,
`.sponsor-feature`, campos de formulário, painel de acessibilidade) recebem
superfície escura no tema escuro.

### 4.2 Paleta de texto auditada (WCAG 2.2 AA)

| Elemento | Antes | Depois | Razão |
|---|---|---|---|
| `.eyebrow` (dourado) | `#d9a447` (~2:1) | `#8a5f14` (~5,4:1) | dourado de marca como texto |
| `.button-link` (terracota) | `#c45835` (~3,9:1) | `#8f3a1b` (~5,2:1) | idem |
| `--text-muted` | `#59675f` | `#4c5f56` | textos de apoio |
| `.quick-pill` | branco translúcido | fundo claro com borda | era invisível fora do herói |
| `.lang-pill.active`, `.ai-float-badge` | `#c45835` | `#8f3a1b` | 4,38:1 → 5,2:1 |

Variantes para herói (fundo escuro) e para o modo escuro foram definidas à parte.

### 4.3 Resultado da auditoria

Auditoria automatizada de contraste (WCAG 2.2 AA) executada em **16 páginas × 2 temas**:

```
light: []   dark: []      → 0 violações
```

> **Nota de método:** o Chrome **pausa transições CSS em abas em segundo plano**.
> Sem desativar a transição (`transition:none`) antes de medir, os valores de
> `background-color` ficam congelados no valor inicial e a auditoria reporta
> falsos positivos.

### 4.4 Logo e identidade

- Logo de **208 px → 248 px** no desktop (196 px no tablet, 158 px no celular), com moldura,
  sombra e proporção preservada.
- Novo bloco de assinatura ao lado do logo: **"Nossa cidade. Mil histórias. · Belém do Pará ·
  Amazônia"**.
- **Marca do Curió** (`/images/curio-marca.svg`): silhueta do curió sobre a linha d'água,
  usada no herói, no rodapé, no avatar da IA e na assinatura do assistente.
- **Padrão do rio** (`/images/pattern-rio.svg`) aplicado como assinatura gráfica no rodapé.
- Logotipo maior também no rodapé, com a assinatura do Curió e link para a Central de
  Serviços.
- A IA trocou o emoji por **avatar em SVG**, com a assinatura "Curió · Motor de Inteligência
  Amazônica do MinhaBelém".

---

## 5. Verificação

```powershell
dotnet build MinhaBelem.sln -c Release      # 0 erros, 0 avisos
dotnet test  MinhaBelem.sln -c Release      # 160 aprovados, 0 falhas, 67 ignorados
```

### Rotas (HTTP 200 em execução local, HTTPS)

`/` · `/mobilidade` · `/mobilidade/linha/758` · `/mobilidade/paradas` ·
`/mobilidade/de-para` · `/mobilidade/fluvial` · `/guia-ia` · `/agenda` ·
`/videos` · `/midias` · `/fotos/enviar`

### Base de mobilidade

58 linhas oficiais (22 do Integra Belém + 36 do BRT Metropolitano) · 664 pontos de
itinerário IDA/VOLTA · 16 terminais e estações · 2 travessias fluviais · todas as linhas
com `source_url`, `verified_at` e `valid_until` reais.

**Pontos de embarque com grafia oficial (verificado, zero resíduo ASCII):**
Terminal Ananindeua · Terminal Mangueirão · Terminal Maracacuera · Terminal Marituba ·
Terminal São Brás · Terminal UFPA · Estação Centenário · Estação Itaiteua ·
Estação Jaderlândia · Estação Marambaia · Estação Marinha · Estação Outeiro (Brasília) ·
Estação Praça da Bandeira · Estação Presidente Vargas · Estação Tavares Bastos ·
Estação Ver-o-Peso

> **Limitação registrada:** as 9 linhas troncais do BRT Metropolitano têm a sequência do
> corredor exclusivo da BR-316; as 27 alimentadoras (A3xx/M3xx) **não** têm itinerário
> ponto a ponto, porque a fonte oficial publica a denominação das linhas, não a lista de
> estações de cada alimentadora. Preferimos deixar isso explícito no relatório
> (`--mobility-report`) a inventar um percurso.

### Curió respondendo com dado publicado

| Pergunta | Resposta verificada |
|---|---|
| "A que horas sai a linha 758?" | 05:00 às 22:30 · R$ 4,60 (meia R$ 2,30) · integração no Terminal Mangueirão · itinerário IDA com 11 pontos |
| "Qual linha de ônibus vai para o Ver-o-Peso?" | Linhas 758, 785, 861 e 911, com horário, tarifa e integração |
| "Onde fica o Terminal São Brás?" | Endereço, bairro, as 6 linhas que passam e as regras de integração física x temporal |
| "Quais linhas passam no Terminal Maracacuera?" | Linhas 204, 225, 226, 872.1, 879 e 881 |
| "Como faço do Terminal Mangueirão até o Ver-o-Peso?" | Sem linha direta: separa as linhas de cada lado e indica onde trocar |

### Contraste

16 páginas × 2 temas → **0 violações** de WCAG 2.2 AA.

---

## 6. O que ainda depende de ação externa

A **Generative Language API do Google Cloud está desativada** no projeto `572050271542`.
A chave é válida (autentica), mas toda chamada ao Gemini retorna `403 PERMISSION_DENIED`.

Para ativar a geração na nuvem:

1. abrir
   <https://console.developers.google.com/apis/api/generativelanguage.googleapis.com/overview?project=572050271542>;
2. clicar em **Ativar**;
3. aguardar alguns minutos para propagar.

Até lá, o motor nativo do Curió responde com os dados publicados — a IA **não fica muda**,
e o disjuntor evita o custo de latência de cada requisição.
