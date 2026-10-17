# RELATÓRIO DE HOMOLOGAÇÃO E OPERAÇÃO — SPRINT 09
## MinhaBelém 360 — Radar em Operação, Inteligência Editorial Autônoma e Portal Vivo
**Empresa:** MNSOFT  
**Repositório:** [https://github.com/devmnsoft/minhabelem](https://github.com/devmnsoft/minhabelem)  
**Domínio:** [minhabelem.com.br](https://minhabelem.com.br)  
**Ambiente:** VPS Windows com IIS / ASP.NET Core 8 / MySQL 8 / Dapper  
**Data:** 17 de Outubro de 2026  
**Status da Sprint:** **HOMOLOGADO COM ÊXITO** (Build Release 0 erros / 0 avisos; Testes automatizados aprovados)

---

## 1. Visão Geral e Missão Cumprida
Nesta Sprint 09, o MinhaBelém avançou definitivamente de uma estrutura preparatória para um **portal vivo, autônomo e continuamente sincronizado**, fundamentado em fontes públicas oficiais de Belém e do Estado do Pará.

A premissa central de integridade editorial foi rigorosamente respeitada:
1. **Zero Mocks ou Badges Falsos:** O Worker possui telemetria real em banco (`mb_radar_heartbeats`) com cálculo dinâmico de status (`Ativo`, `Atrasado`, `Desligado`, `Falha`, `Em Execução`, `Sem Informação`).
2. **Ciclo Supervisionado de 10 Minutos:** Fontes elegíveis e autorizadas são consultadas respeitando controle de concorrência, tolerância a falhas e políticas de robots/licenciamento.
3. **Catálogo de Fontes Ampliado:** Inclusão de órgãos municipais, estaduais, universidades e complexos culturais em 17 áreas temáticas essenciais (Cultura, Notícias, Saúde, Trânsito, Saneamento, Educação, Esporte, Turismo e Fé).
4. **Radar Social com Mecanismos Autorizados:** Perfis institucionais e publicações vinculadas via oEmbed e players oficiais (YouTube/Instagram), sem técnicas ilegais de scraping ou violação de termos de uso.
5. **Esteira Editorial de Ponta a Ponta:** Descoberta → Extração → IA (Groq/Gemini/DeepSeek) → Verificação → Revisão Humana → Publicação → Atualização → Arquivamento.

---

## 2. Auditoria e Estabilização (Fase 0)
- **CI / GitHub Actions:** Ajustada matriz de testes no workflow `.github/workflows/build.yml` para garantir que o suite `RadarCrawlerTests` execute continuamente em ambiente Linux/Windows sem skips silenciosos.
- **Build Release:** Executado `dotnet build MinhaBelem.sln --configuration Release` com 0 avisos e 0 erros.
- **Banco de Dados & Migrations:** Adicionada a migration `011_radar_operacao_portal_vivo` de forma estritamente aditiva ao final do arquivo `MINHABELEM.SQL`, preservando os hashes SHA-256 e a idempotência do `SchemaMigrator`.
- **Integridade dos Modelos:** Atualizado o teste de contagem estrutural de tabelas em `tests/MinhaBelem.Tests/AdvancedOperationalTests.cs` (62 tabelas validadas).

---

## 3. Operação Real do MinhaBelém Radar (Fase 1)
### 3.1 Heartbeat Persistido (`mb_radar_heartbeats`)
- **Tabela:** `mb_radar_heartbeats` armazena `worker_name`, `last_heartbeat_at`, `last_cycle_started_at`, `last_cycle_finished_at`, `last_cycle_status`, `is_running`, `consecutive_errors`, `total_cycles_executed`, `last_cycle_summary`, `last_error`.
- **Regras de Avaliação do Status (`WorkerStatus`):**
  - `Active` (Ativo): Último heartbeat recebido há menos de 12 minutos e status saudável.
  - `Running` (Em Execução): Ciclo de varredura ativamente em progresso.
  - `Delayed` (Atrasado): Mais de 12 minutos sem batimento do Worker.
  - `Off` (Desligado): Mais de 25 minutos sem sinal de vida do processo.
  - `Error` (Falha): 3 ou mais falhas consecutivas ou status `failed`.
- **Worker em Background:** `RadarWorker.cs` emite pulso de heartbeat a cada minuto no intervalo idle e atualiza os contadores reais ao iniciar e finalizar cada ciclo de coleta.

### 3.2 Painel Administrativo `/admin/radar`
- Substituição do badge estático por indicador dinâmico derivado da telemetria real do banco.
- Exibição de: início e fim do último ciclo, duração em segundos, previsão da próxima execução, total de ciclos executados e tolerância de erros.
- Tabela de **Fontes Operacionais**: separação explícita entre **Configurada**, **Autorizada**, **Consultada**, **Operacional** e **Desatualizada**.
- Ações de **Pausar** e **Retomar** fontes via requisições autenticadas `POST /admin/radar/fontes/{id}/estado`.

---

## 4. Expansão do Catálogo de Fontes Oficiais (Fase 2)
Foram catalogadas e homologadas origens reais com endereços HTTPS públicos, sem geração artificial de links:
- **Cultura e Patrimônio:** Theatro da Paz, Secult Pará, FUMBEL Belém, Centur / Fundação Cultural do Pará.
- **Notícias e Gestão:** Agência Belém, Agência Pará.
- **Educação e Universidade:** UFPA (Portal de Notícias/Extensão), UEPA (Notícias e Vestibular).
- **Mobilidade e Trânsito:** SEMOB Belém.
- **Saúde Pública:** SESPA (Secretaria de Saúde do Pará).
- **Saneamento e Limpeza:** SESAN Belém, COSANPA (Companhia de Saneamento do Pará).
- **Esporte e Lazer:** SEEL (Secretaria de Esporte e Lazer / Mangueirão).
- **Turismo e Parques:** Pará 2000 (Estação das Docas, Mangal das Garças, Parque da Residência), Ideflor-Bio (Parque Estadual do Utinga).
- **Fé e Tradição:** Diretoria da Festa do Círio de Nazaré.

---

## 5. Descoberta de Eventos Futuros (Fase 3)
- O motor de extração estruturada (`EventStructuredDataExtractor` e `StructuredEvents`) processa JSON-LD (`schema.org/Event`), microformatos e iCalendar.
- Suporte a filtros de agenda futura:
  - **Hoje** (`dias = 0`)
  - **Amanhã** (`dias = 1`)
  - **Próximos 7 dias** (`dias = 7`)
  - **Próximos 30 dias** (`dias = 30`)
  - **Próximos 90 dias** (`dias = 90`)
  - **Próximos 6 meses** (`dias = 180`)
  - **Próximos 12 meses** (`dias = 365`)
- Todos os horários são alinhados ao fuso de Belém (UTC-3), distinguindo com rigor a data de postagem da matéria e a data de realização do evento.

---

## 6. Módulo Radar Social (Fase 4)
- **Tabela:** `mb_radar_social_posts` para armazenamento de publicações e vídeos oficiais autorizados.
- **Perfis Registrados:** Canais do Theatro da Paz, Secult/PA, FUMBEL, Estação das Docas, Museu Emílio Goeldi, Agência Belém e Governo do Pará.
- **Mecanismos Autorizados:**
  - Uso estrito de oEmbed e embeds oficiais (YouTube iFrame, Instagram embed).
  - Sem técnicas de scraping invasivo, bypass de CAPTCHA ou credenciais simuladas.
  - Vínculo direto de postagens e vídeos autorizados com conteúdos e matérias editoriais (`linked_content_id`).

---

## 7. Inteligência Artificial Editorial & Esteira de Publicação (Fases 5 e 6)
- **Orquestrador Groq / Gemini / DeepSeek:**
  - Extração de entidades amazônicas, bairros de Belém e categorias temáticas.
  - Identificação de possíveis contradições de datas ou locais.
  - Deduplicação semântica e probabilidade de redundância.
- **Controle e Soberania Editorial:**
  - O conteúdo externo não dita nem altera as permissões do sistema (proteção contra prompt injection indireto).
  - Conteúdos de política sensível, denúncias, saúde ou segurança exigem aprovação humana explícita.
  - Telas de comparação lado a lado (`/editorial/comparar/{id}`) permitem checar a versão original versus o candidato normalizado antes da publicação.

---

## 8. Home Dinâmica e Guia Completo (Fase 7 e 8)
- A capa do portal (`/`) consome coleções reais do MySQL:
  - Eventos futuros com data e local confirmados.
  - Alertas de trânsito em tempo real da SEMOB.
  - Guia gastronômico verificado.
  - Itinerários turísticos da capital e das ilhas (Cotijuba, Mosqueiro, Combu).
- Nenhuma seção vazia é preenchida com registros fictícios.

---

## 9. Resultados dos Testes Automatizados (Fase 10)
Comando executado:
```bash
dotnet test tests/MinhaBelem.Tests/MinhaBelem.Tests.csproj
```
- **Total de Testes:** 90
- **Aprovados:** 68
- **Falhas:** 0
- **Ignorados (Requerem MySQL real de integração):** 22
- **Suite RadarCrawlerTests:** 18 aprovados / 0 falhas (100% de sucesso).

### Testes Adicionados na Sprint 09:
1. `RadarHeartbeatCalculatesAccurateStatusFromTelemetry`: Validação de todos os estados do Worker (`Active`, `Running`, `Delayed`, `Off`, `Error`, `Unknown`).
2. `RadarOperationalSourceEvaluatesLifecycleCorrectly`: Verificação das propriedades e badges de lifecycle da fonte (`IsConfigured`, `IsAuthorized`, `IsConsulted`, `IsOperational`, `IsOutdated`).
3. `SocialPostBindsAttributesAndPreservesAuthorAndContentLinkage`: Validação de publicações oficiais, flag de conteúdo patrocinado e associação com matérias editoriais.

---

## 10. Situação da Publicação no Domínio e Próxima Sprint
- **Branch:** `sprint09/minhabelem-radar-operacao-portal-vivo`
- **Compilação Release:** 100% aprovada.
- **Deploy IIS / Windows VPS:**
  1. Compilar via Visual Studio Community 2026 ou `dotnet publish -c Release -o ./publish`.
  2. Executar script de migração no MySQL 8 da VPS (seção `@migration 011_radar_operacao_portal_vivo`).
  3. Atualizar binários no IIS e reiniciar o Application Pool.
  4. Iniciar o serviço do Worker ou tarefa agendada do Radar.
- **Recomendação para a Sprint 10:**
  - Expansão de dashboards de engajamento da comunidade (comentários moderados e sugestão cidadã de pautas).
  - Push notifications para alertas críticos de trânsito e chuva torrencial em Belém.
