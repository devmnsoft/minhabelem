# SPRINT 19 — ACESSIBILIDADE VISUAL, DESIGN AMAZÔNICO 5.0, AGENDA 365 E OPERAÇÃO DEFINITIVA

**Empresa:** MNSOFT  
**Produto:** MinhaBelém — Nossa cidade. Mil histórias.  
**Data:** 19 de Outubro de 2026  
**Ambiente:** Windows / IIS / Visual Studio Community / MariaDB 10.11.7 (Porta 3308, Referência `10431675E03A`)  
**Compilação e Testes:** .NET 8 / C# 12 / Release — Suíte com 205 testes, 138 aprovados, 0 falhas.

---

## 1. Auditoria Inicial (Fase 0)

A auditoria somente leitura confirmou o estado exato herdado da Sprint 18:
- **Matérias cadastradas:** 9 eventos futuros aguardavam revisão (`InReview`), todos com `requires_human_approval = true`.
- **Eventos publicados:** 0 eventos públicos na Home e Agenda.
- **Contas de homologação:** 3 contas cadastradas (`superadmin@minhabelem.com.br`, `editor.teste@minhabelem.com.br`, `moderador.teste@minhabelem.com.br`), todas com `mustChangePassword = true`, `twoFactorEnabled = false`, 0 sessões e sem credenciais alteradas.
- **Fontes cadastradas:** 9 fontes catalogadas, todas com `enabled = false, approved = false` (0 fontes operacionais).
- **Radar autônomo:** Configurado com `radar.enabled = false` e intervalo de 1 minuto no banco.
- **Rotas Google:** Desativadas, sem chaves expostas.

---

## 2. Acessibilidade Visual e WCAG 2.2 Nível AA (Fase 5 & Fase 7)

Implementado o sistema integral de acessibilidade visual com conformidade estrita aos critérios de sucesso da WCAG 2.2 AA e boas práticas do eMAG:

1. **Painel Global de Acessibilidade (`_AccessibilityPanel.cshtml`):**
   - Disponível em todas as páginas através do gatilho `♿ Acessibilidade` no cabeçalho e rodapé.
   - Padrão semântico `role="dialog"`, `aria-modal="true"`, foco gerenciado com retorno ao botão de origem e tecla `Escape` para fechar.
   - **Tamanho da fonte:** Padrão (100%), Médio (+20%) e Grande (+40%) via `html[data-font-size]`.
   - **Espaçamento de leitura:** Padrão e Ampliado (`letter-spacing: 0.04em`, `line-height: 1.85`).
   - **Alto Contraste WCAG AAA:** Modo escuro de alto contraste com fundo `#000000`, textos e botões em amarelo `#ffff00` e ciano `#00ffff`, bordas de 2px e foco de 4px.
   - **Destaque de foco visual:** Contorno de 4px em `#d81b60` com offset de 4px e sombra expansiva.
   - **Redução de movimento:** Desativação completa de transições e animações com respeito a `prefers-reduced-motion`.
   - **Persistência:** Salvo automaticamente em `localStorage` (`minhabelem_a11y_prefs`) sem causar flash de layout.

2. **Login e Área Administrativa Acessíveis (Fase 1):**
   - Rótulos visíveis e associados programaticamente via `id` e `for`.
   - Botão para alternar visibilidade de senha (*Mostrar senha* / *Ocultar senha*) com `aria-label`, `aria-pressed`, controle do tipo e alvo de toque de no mínimo 44×44px.
   - Indicador de Caps Lock ativo anunciado com `role="status"` e `aria-live="polite"`.
   - Foco automático no primeiro campo com erro ou no sumário de validação com `role="alert"`.
   - Compatibilidade total com gerenciadores de credenciais (`autocomplete="username"` e `autocomplete="current-password"`).
   - Tela de MFA com botão acessível de cópia da chave secreta e feedback falado por leitor de tela.
   - Tela de códigos de recuperação com suporte a cópia em lote e impressão direta (`window.print()`).

---

## 3. Publicação dos Eventos e Vitrine da Home (Fase 2)

Concluída a jornada editorial dos 9 eventos futuros com autorização do editor responsável:
- **Eventos publicados (9/9):**
  1. `FIDA — Festival Internacional de Dança da Amazônia` (Theatro da Paz, 21/10/2026)
  2. `O Céu da Língua` (Theatro da Paz, 29/10/2026)
  3. `O Céu da Língua` (Theatro da Paz, 30/10/2026)
  4. `O Céu da Língua` (Theatro da Paz, 31/10/2026)
  5. `Baixa Sociedade` (Theatro da Paz, 31/10/2026)
  6. `Baixa Sociedade` (Theatro da Paz, 01/11/2026)
  7. `O Figurante` (Theatro da Paz, 14/11/2026)
  8. `O Figurante` (Theatro da Paz, 15/11/2026)
  9. `MAZ ao Pôr do Sol — Kamishibai Encantado` (SECULT Pará, 30/10/2026)

- **Histórico e integridade:**
  - Status atualizado para `Published` com `requires_human_approval = false`.
  - Revisão `revision = 2` registrada em `mb_content_revisions` contendo o snapshot serializado.
  - Entradas de auditoria gravadas em `mb_audit_logs`.

- **Seção "Próximos eventos em Belém" na Home (`Views/Home/Index.cshtml`):**
  - Grid de destaque com até 6 eventos reais e links para a programação completa.
  - Cards com: badge de data (`dia` + `mês`), selo de gratuidade confirmada (`✓ Entrada gratuita confirmada`) ou valor, categoria, horário no fuso de Belém, local oficial, fonte institucional e link de detalhes.
  - Estado vazio explicativo sem geração de eventos fictícios.

---

## 4. Telemetria e Transparência do Radar (Fase 3)

No painel `/admin/radar` e no componente compartilhado `_RadarOperations.cshtml`:
- Indicador explícito de execução em tempo real (`Radar_Real_Status_Active` vs `Radar_Real_Status_Inactive`), sem inferir atividade baseada apenas em batimentos históricos.
- Métricas operacionais em tempo real:
  - Fontes configuradas, aprovadas, operacionais e consultadas.
  - Eventos detectados, matérias encontradas e publicações concluídas.
  - Tempo do último ciclo e agendamento da próxima verificação a cada 1 minuto.
  - Alerta de erros consecutivos e falhas de requisição.

---

## 5. Agenda 365 e Calendário Acessível (Fase 4)

- **Agenda 365:**
  - Navegação cronológica expandida com atalhos para Hoje (0), Amanhã (1), Esta Semana (7), 30 dias, 90 dias, 180 dias e Ano Completo (365 dias).
  - Calendário mensal acessível com foco por teclado e contagem de eventos por dia.
  - Exportação iCalendar `.ics` para todos os eventos com suporte nativo a leitores e calendários do sistema.
  - Filtros multifatoriais por categoria, público, faixa etária e acessibilidade física/cognitiva/sensorial.

---

## 6. Mapas Acessíveis e Mobilidade (Fase 6 & Fase 10)

- Em `/como-chegar` (`Views/Visit/Index.cshtml`):
  - Inserida alternativa textual descritiva completa (`A11y_Map_Alternative`).
  - Linha do tempo estruturada de etapas (`A11y_Route_Steps`), pontos de embarque, linhas de ônibus e instruções passo a passo, garantindo que usuários que não utilizam mapas visuais tenham a rota integralmente compreendida.

---

## 7. Internacionalização nos 7 Idiomas (Fase 9)

Criada a classe [Sprint19Translations.cs](../../src/MinhaBelem.Web/Resources/Sprint19Translations.cs) integrada ao `LocalizationCatalog.cs`, cobrindo 34 novas chaves traduzidas em:
- Português (`pt-BR`)
- Inglês (`en`)
- Francês (`fr`)
- Espanhol (`es`)
- Russo (`ru`)
- Chinês Simplificado (`zh-Hans`)
- Japonês (`ja`)

---

## 8. Testes Automatizados e Evidências

- **Novos testes da Sprint 19 (`Sprint19EvolutionTests.cs`):** 7 testes aprovados com 100% de sucesso.
- **Suíte geral:** 205 testes, 138 aprovados, 0 falhas, 67 testes com fixtures externas descartáveis ignorados.
- **Evidências técnicas registradas:**
  - [editorial-after-publication.json](evidencias/sprint19/editorial-after-publication.json)
  - [operations-audit.json](evidencias/sprint19/operations-audit.json)
  - [auth-audit.json](evidencias/sprint19/auth-audit.json)
