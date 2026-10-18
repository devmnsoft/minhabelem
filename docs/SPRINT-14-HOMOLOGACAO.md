# Sprint 14 — redesign e homologação

Data: 18 de outubro de 2026. Repositório: [devmnsoft/minhabelem](https://github.com/devmnsoft/minhabelem/tree/sprint14/redesign-radar).

A implementação corrige a separação Web/Worker, acrescenta a fila persistente e aplica o sistema visual às páginas principais. O build Release passou sem erros ou avisos. A suíte local passou em **112 de 114 testes**, com dois testes de OCR ignorados por ausência de Tesseract/FFmpeg. A validação de layout passou em **1.204 combinações** de 43 páginas, sete idiomas e quatro resoluções. A homologação operacional de produção continua pendente: o serviço Windows não está instalado nesta máquina e não há fontes operacionais configuradas no banco local.

## Problemas técnicos corrigidos

- Os quatro testes reprovados no [Actions anterior](https://github.com/devmnsoft/minhabelem/actions/runs/39049678506) falhavam com a ativação do `AdminController`. O Web dependia de `ISourceCollection` e `IRadarWorker`, registrados somente no host Worker. O controller passou a depender de `ICollectionQueue`; o Web não registra executores nem os serviços de coleta em segundo plano.
- Pedidos de feeds, Radar e URL específica são persistidos em `collection_requests`. O Windows Service processa a fila. O modo operacional `--run-once` também processa até dez pedidos pelo executável Worker.
- O processador mantém um advisory lock MySQL durante a execução, registra tentativas e conclusão, recupera pedidos interrompidos após reinício e limita tentativas a três, com espera de um minuto. Não grava mensagens externas de exceção na fila.
- As consultas do Radar agora usam `mb_ingestion_sources`, a coluna `name` e os protocolos do modelo atual. A promoção e a alteração de estado deixam de atingir a tabela legada.
- Descobertas do crawler sem execução de feed usam `run_id = NULL`; o valor zero violava a chave estrangeira.
- O host Worker deixava de iniciar em Development por depender dos serviços de autenticação HTTP. O SignInManager agora é registrado no caminho Web.
- Radar respeita frequência e cota por fonte; feeds e crawler atendem protocolos distintos. O agendamento usa PeriodicTimer e conta o intervalo desde o início do ciclo, sem somar o tempo de execução ao intervalo configurado. Timeouts geram falha, e o lock é liberado mesmo quando a atualização de heartbeat falha.
- Heartbeats ociosos preservam o resultado anterior. Um heartbeat antigo ou sem ciclo concluído não apresenta o Radar como ativo. O painel Evolução deixou de apresentar testes, commits e implantação em produção como fatos fixos.
- Os layouts ausentes de Radar/Evolução foram corrigidos com `_AdminLayout`. O seletor de idioma passou a preservar `returnUrl`.

## Design e componentes

A Home apresenta hero de Belém, busca, hierarquia editorial, Belém Agora, agenda, eventos futuros, cinema/teatro, programação infantil, gastronomia, roteiros, mobilidade fluvial, serviços, bairros, vídeos e parceiros. Os blocos usam consultas existentes e estados vazios; nenhuma notícia fictícia foi inserida no banco local.

O sistema compartilhado inclui tokens de cor e tipografia, seções editoriais, listas de eventos, calendário semanal com filtro por dia, estados vazios, breadcrumbs, formulários, tabelas com rolagem interna, menu móvel, sidebar administrativa, foco visível, respeito a movimento reduzido e estilos de impressão. Foram centralizadas 420 ocorrências de estilos repetidos em 102 regras compartilhadas. Estilos exclusivos e valores dinâmicos das telas antigas ainda existem; a migração para classes semânticas pode continuar sem alterar o comportamento.

Ativos: `belem-guajara-hero.jpg`, `favicon.svg` e sprite `icons.svg` com busca, rio, calendário, barco e cidade. A marca SVG existente foi preservada. O hero foi gerado com IA e é identificado como imagem ilustrativa, não como registro documental de Belém. Nenhuma foto de terceiros foi adicionada.

As novas mensagens e componentes têm traduções nos sete idiomas. Também foram corrigidas as chaves literais sem resolução nas Views. Isso não representa uma revisão linguística integral: ainda há textos estáticos legados em português em telas administrativas e de conta, e a qualidade das traduções exige revisão humana.

## Páginas e evidências

Foram renderizadas por MVC e verificadas via HTTP as 37 rotas principais de Home/busca, agenda, guia, turismo, glossário, mobilidade, bairros, parceiros, conta, documentos institucionais, administração e editorial. Outras seis telas de detalhe foram verificadas com fixtures explicitamente identificadas em banco descartável: matéria, lugar, edição editorial, acesso negado, bairro e roteiro.

A [matriz completa](evidencias/sprint14/layout-results.json) registra cada página, idioma, resolução e largura medida. O [inventário de Views](SPRINT-14-VIEWS.md) distingue as telas cobertas e os fluxos condicionais.

Capturas selecionadas:

- [Home desktop](evidencias/sprint14/home-desktop.jpg) e [celular](evidencias/sprint14/home-mobile.jpg).
- [Admin desktop](evidencias/sprint14/admin-desktop.jpg) e [celular](evidencias/sprint14/admin-mobile.jpg).
- [Radar desktop](evidencias/sprint14/admin-radar-desktop.jpg) e [celular](evidencias/sprint14/admin-radar-mobile.jpg).
- [Agenda](evidencias/sprint14/agenda-desktop.jpg), [Explore](evidencias/sprint14/explore-desktop.jpg), [login](evidencias/sprint14/conta-login-desktop.jpg) e [mobilidade](evidencias/sprint14/mobilidade-desktop.jpg).

A verificação visual usa HTML efetivamente renderizado nos testes HTTP, servido por um servidor local de snapshots sem acesso ao banco. A automação verifica largura, imagens e abertura/fechamento do menu em quatro resoluções: 1440×1000, 1280×800, 768×1024 e 390×844. As capturas selecionadas foram inspecionadas visualmente. Essa técnica não substitui testes de interação real em todos os formulários; os fluxos de autenticação, MFA, permissões, coleta, publicação e persistência são cobertos pela suíte HTTP/MySQL.

Um teste de integração iniciou o RadarWorker e confirmou dois ciclos reais em aproximadamente um minuto no MySQL descartável, sem fontes habilitadas. Ele valida o agendamento e a persistência de heartbeat; não comprova coleta externa nem execução pelo Service Control Manager.

## Banco local e Radar

A conexão local foi transferida dos arquivos de configuração para **User Secrets**, compartilhado por Web/Worker em Development. Valores e credenciais não constam neste relatório nem nos commits. As alterações locais remanescentes em `appsettings.json` não foram incluídas nos commits.

O servidor local é **MariaDB 10.11.7**, e não MySQL 8. A migration acrescentou a fila: agora há **63 tabelas**, zero matérias e zero fontes operacionais. A leitura posterior confirmou esses totais. As migrations existentes foram registradas pelo instalador idempotente; os testes verificam reaplicação sem perda de dados. Testes destrutivos foram executados somente em bancos aleatórios `minhabelem_test_*` no **MySQL 8** do contêiner isolado.

O executável Worker concluiu o modo `--run-once` após a correção de DI, registrando ciclo/heartbeat. Como não há fontes operacionais, o histórico de páginas consultadas permanece zero. **Isso não comprova descoberta externa nem atividade na VPS.** Nenhum serviço `MinhaBelemWorker` está instalado no Windows local. O processamento contínuo pelo Service Control Manager ainda precisa de homologação no ambiente de implantação.

## Reprodução

Build e testes:

```powershell
dotnet build MinhaBelem.sln --configuration Release
# MINHABELEM_TEST_MYSQL deve apontar a um MySQL local descartável, sem Database.
$env:MINHABELEM_VISUAL_SNAPSHOTS = Join-Path (Get-Location) 'TestResults/sprint14-html'
dotnet test MinhaBelem.sln --configuration Release --no-build --logger trx
npm ci --prefix scripts/visual-qa
npm run --prefix scripts/visual-qa check
```

A ferramenta visual usa Microsoft Edge instalado; pode-se adaptar o canal ao Chromium do ambiente de CI. Os snapshots intermediários ficam em `TestResults` e não são versionados. Para auditar a conexão local sem escrever dados nem exibir credenciais:

```powershell
dotnet run --project scripts/MinhaBelem.Verify
```

## Pendências para encerrar a homologação

1. Validar o resultado do Actions do commit final, incluindo os dois testes reais de OCR no Linux.
2. Instalar/configurar o Windows Service no servidor de homologação com identidade restrita e conexão fornecida por variável de ambiente; verificar reinício, heartbeat, dois ciclos consecutivos e fila processada com IIS parado.
3. Cadastrar/aprovar fontes com autorização documentada, frequência e limites; registrar coleta externa real e submeter um item a aprovação humana e publicação.
4. Homologar credenciais de IA/Places, custos reais e limites. Os testes usam substitutos controlados para essas APIs.
5. Completar a revisão linguística e a tradução dos textos estáticos legados; revisar acessibilidade com tecnologias assistivas e interações dos formulários em dispositivos reais.
6. Confirmar backup, TLS, chaves de Data Protection, permissões de serviço, monitoramento e rollback antes da publicação.

A Sprint 14 não é declarada operacional em produção. A homologação integral depende das evidências operacionais e de linguagem acima.
