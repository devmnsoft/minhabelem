# Sprints 03 e 03B — integração e homologação

Evidência local em 16/10/2026. Base da retomada: commit `b7b6204`. A Sprint 03B foi incluída por solicitação explícita. A aplicação anterior ainda precisava conectar Web/Core/Infrastructure; este trabalho substituiu os caminhos Web paralelos e implementou descoberta supervisionada. **A entrega não está homologada para produção.** APIs externas, ambiente IIS real, revisão visual e governança ainda têm pendências abaixo.

## Resultado por fluxo

| Área | Implementação e evidência | Limite de aceite |
| --- | --- | --- |
| Arquitetura | Web referencia Infrastructure/Core; composição única, stores/repositórios reais, serviços Web antigos removidos | Build Solution Windows/Linux e testes HTTP/MySQL passaram |
| Identity | Login, lockout, TOTP, oito recovery codes protegidos e de uso único, troca de senha, sessões/revogação | Login/TOTP HTTP, suspensão, recovery e revogação testados; SMTP/reset por e-mail não implementados |
| Administração | Usuários, perfis/permissões, fontes, integrações, auditoria, configurações, moderação e guia | Políticas exigem MFA; revogação de perfil persiste após reexecutar SQL e invalida sessão |
| Editorial | Coleta → dedup → IA → fila → conferência → edição → publicação → atualização → arquivamento | Fluxo HTTP com persistência/revisões/auditoria testado; política sensível exige confirmação humana |
| Descoberta | Cadastro completo, perfil por fonte, agendamento, quotas, locks, checkpoint, retries e backoff | Coleta com fonte/resposta simuladas e MySQL real; zero fontes reais ativadas por esta entrega |
| Agenda | Períodos hoje/amanhã/7/30/90/180/365 dias, páginas temáticas, filtros, alterações/cancelamento | Rotas HTTP, janela futura e cancelamento público testados; sem programação real cadastrada |
| Geografia | 144 municípios IBGE, bairros de referência CODEM, aliases de distritos e exclusão de Portugal | Testes de reconhecimento; catálogo de espaços culturais curado e delimitação oficial completa da região metropolitana pendentes |
| Mídia | OCR Tesseract/por, primeiro frame MP4 por FFmpeg, prévia privada/descrição e direitos persistidos | Dois testes com executáveis reais passaram; uso real depende de licença, allowlist e instalação no servidor |
| IA | Gateway único Groq/Gemini/DeepSeek, fallback limitado, auditoria de uso/custo/latência, triagem sensível local | Falhas/fallback, payload, tokens e custos testados com respostas simuladas; nenhum provedor real homologado |
| Guia | Curadoria própria persistida, detalhes/fotos Google consultados na requisição, créditos, logo e links Maps | HTTP com cliente simulado; detalhes externos não sobrescrevem dados próprios; API real e mapa incorporado pendentes |
| UX pública | Razor responsivo, navegação, skip link, formulários, estados vazios, mensagens e compartilhamento Web Share/WhatsApp/link | Razor compila e rotas HTTP passam; revisão visual, teclado/leitor de tela e dispositivos reais pendentes |

## Conectores e autorizações

| Conector | Caminho implementado/testado | Estado real nesta entrega |
| --- | --- | --- |
| RSS / Atom | Feed XML sem DTD, ETag/Last-Modified, robots e fluxo do worker | Nenhuma fonte real habilitada |
| iCalendar | TZID/UTC, data do evento separada de DTSTAMP, recorrência limitada | Testes de parser; calendário autorizado pendente |
| JSON-LD / Html | Extração Event, datas com fuso, organizador, preço, licença, cancelamento/disponibilidade | Testes estruturados; URL/licença real pendentes |
| JSON / Custom / GovernmentApi | Adaptador de envelope JSON e campos suportados | Cada endpoint real exige contrato/autorização e homologação |
| Google Search autorizado | Custom Search API, credencial por header, consulta geográfica | Chave, motor `cx`, elegibilidade/contratação e cota pendentes |
| Instagram | API oficial, campos e token Bearer por referência vinculada ao host | App, conta elegível, escopos e token pendentes |
| Facebook | Graph API, campos e token por referência | App, versão, permissões/página e token pendentes |
| TikTok | Display API, POST com cursor/limite e campos | Aprovação/scopes/conta e token pendentes |
| YouTube | Search API, campos e links por videoId | Projeto, chave/token e quota pendentes |
| Google Places | Gateway existente e conector só de IDs/links para curadoria | API habilitada, chave restrita, cobrança/cotas e teste real pendentes |

**Conectores efetivamente ativos com terceiros: nenhum.** Fontes de testes são fixtures e bancos descartáveis, não cadastros operacionais. Não foram acessadas contas sociais nem contornados autenticação, CAPTCHA ou bloqueios. A obtenção/renovação OAuth não tem interface completa. `Html` não é crawler geral e o JSON personalizado não entende todo esquema de API automaticamente.

## Banco e atualização

`MINHABELEM.SQL` é o único arquivo SQL, raiz e recurso embarcado. Instalação nova: **45 tabelas**, 4 papéis, 15 permissões e 33 vínculos iniciais, sem usuário/senha padrão. Tabelas legadas permanecem; a Web consulta `mb_`.

Seções aditivas: `003_operacao` (sessões), `004_descoberta` (perfis/requisições/snapshots/evidências/uso), `005_localidades` (seeds), `006_midia` (permissões/extrações), `007_relacionamentos` (FKs condicionais). O baseline anterior foi preservado. Índices e FKs são executados no teste de instalação/reexecução. Dados órfãos interrompem criação de FK sem serem removidos. Tabelas antigas divergentes não são reconstruídas automaticamente.

Há CLI explícita para schema, bootstrap e importação legada. Importação é aditiva, preserva IDs públicos e não transfere senhas. **Nenhum banco de produção foi conectado/alterado e nenhum script de produção foi executado.** Plano de backup, ensaio e recuperação em [OPERACAO.md](OPERACAO.md).

## Testes executados

| Verificação | Resultado local |
| --- | --- |
| `dotnet build MinhaBelem.sln -c Release` | Windows: sucesso, zero erros/avisos |
| Build limpo da Solution na imagem .NET SDK 8 | Linux: sucesso |
| Suite com MySQL 8.0.46 em Windows | 43 aprovados, 2 ignorados (Tesseract/FFmpeg ausentes), zero falhas |
| Suite completa em Linux, MySQL real + Tesseract/FFmpeg reais | **45 aprovados, zero ignorados e zero falhas** |
| Execução anterior isolada dos dois testes de mídia real | 2 aprovados, sem simular OCR/FFmpeg |
| SQL inteiro executado duas vezes em banco descartável | Dados preservados, 45 tabelas, seeds sem duplicação |
| Migrations/checksums e concorrência | Reexecução idempotente e atualização concorrente rejeitada |
| Publish Release `win-x64`, framework-dependent | Artefato IIS com `web.config` gerado; sem implantação |
| NuGet `--vulnerable --include-transitive` | Nenhuma vulnerabilidade conhecida reportada no momento da consulta |
| Revisão visual no navegador | Não executada: nenhuma superfície de navegador disponível nesta sessão |

Testes HTTP usam `WebApplicationFactory` com cookies, TOTP, antiforgery e MySQL reais. IA/Google/conectores usam respostas simuladas. Cobrem bloqueio de rascunho, permissão sem MFA, publicação sensível, revisões, reimportação, cancelamento, agenda, quotas atômicas, host de credencial, menores, SSRF IPv6, truncamento e códigos recovery protegidos/uso único. Isso não é teste contra APIs reais, teste de carga ou pentest independente.

`Dockerfile.homologacao` e [instruções reproduzíveis](OPERACAO.md) permitem repetir a suíte. Logs/TRX ficam ignorados pelo Git. GitHub Actions compila a Solution em Windows/Linux e tem job MySQL/HTTP com mídia real; o resultado remoto deve ser consultado na execução da PR, sem inferir aprovação a partir do teste local.

## Segurança corrigida e limites

- Chave editorial compartilhada substituída por Identity/MFA/permissões. Tokens TOTP/recovery protegidos e bootstrap sem senha fixa.
- Sessões verificadas contra suspensão, revogação e security stamp; troca de perfil invalida sessões e não é desfeita pelos seeds.
- SQL parametrizado, revisões concorrentes protegidas, antiforgery, escaping Razor, CSP e cookies seguros. Redirecionamento de status de erro restrito a GET preserva os erros POST.
- SSRF considera IPv4/IPv6/DNS e rebinding; saída conecta ao IP público conferido, sem seguir redirects. Tokens não ficam na URL e são vinculados ao host.
- Fontes isoladas, cotas persistidas e tentativas limitadas; uso de IA e direitos de mídia registrados. Conteúdo não verificado ou sensível não é publicado pelo worker.

Auditoria de pacotes sem achados não comprova ausência de vulnerabilidades. A triagem local não identifica todos os dados pessoais. Controle distribuído de rate limit, retenção/anonimização completa, alertas externos e avaliação de qualidade de IA em corpus real continuam pendentes.

## Impedimentos de produção e próxima evolução

1. Homologar credenciais, escopos, contas e fontes licenciadas por plataforma. Validar obtenção/renovação de tokens e falhas reais, sem marcar integração ativa apenas pela chave.
2. Disponibilizar homologação IIS/VPS com TLS, persistência/proteção de Data Protection, supervisão do worker, egress, backup restaurado, monitoramento e chaves restritas. O publish não comprova instalação no Visual Studio Community 2026 ou IIS real.
3. Fazer revisão visual/acessibilidade e fluxos em dispositivos/navegadores reais; mapa incorporado, personalização persistente e catálogo de espaços culturais ainda não foram entregues.
4. Definir responsável/contato de privacidade, retenção por classe, atendimento a titulares, contratos com provedores e descarte/anônimização auditável. O sistema ainda não tem expurgo geral de dados pessoais nem conformidade LGPD integral homologada.
5. Completar OAuth/renovação na interface, recuperação de conta por canal verificado e painel administrativo de todas as sessões (a tela atual gerencia sessões próprias). Evoluir alertas para notificações externas e métricas para observabilidade operacional.
6. Completar classificação geográfica oficial, busca com todos os filtros cruzados entre conteúdos e lugares, corpus de avaliação semântica/contradições e validação de custo/qualidade dos três provedores reais. A busca atual une resultados de conteúdo e lugares por texto; filtros detalhados ficam nas páginas específicas.

Estas pendências não são marcadas como concluídas. A próxima sprint deve fechar os itens de produto acima e fazer um piloto com poucas fontes autorizadas, medir erros/custos e homologar a publicação supervisionada em servidor real antes de liberar operação pública.
