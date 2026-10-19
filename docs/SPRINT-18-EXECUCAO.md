# Sprint 18 — acesso real e condição de continuidade

Data: 18/10/2026. Base inicial: `4fed942`. Código inicial de diagnóstico e testes: `7764114`. **Prioridade zero homologada pessoalmente pelo operador para os três perfis, inclusive após reiniciar a aplicação.** Houve bootstrap local expressamente autorizado após identificar o destino correto. Não houve redefinição de contas existentes, implantação nem ativação de fontes.

## Ambiente encontrado

A auditoria executou o host Web compilado com `ASPNETCORE_ENVIRONMENT=Development`, no diretório do projeto, usando a configuração efetiva do ASP.NET Core, inclusive User Secrets. Resultado seguro:

| Campo | Valor |
|---|---|
| Host MySQL | `localhost` |
| Porta | `3308` |
| Banco | `minhabelem` |
| Ambiente | `Development` |
| Referência do destino | `10431675E03A` |
| Servidor | MariaDB 10.11.7 |
| Contas solicitadas | As três ausentes |

A referência é um SHA-256 truncado de host/porta/banco. Não contém usuário ou senha e identifica o destino, não a versão das credenciais. As contas ausentes são `superadmin@minhabelem.com.br`, `editor.teste@minhabelem.com.br` e `moderador.teste@minhabelem.com.br`.

Não há senha válida, MFA, perfil ou sessão a conferir para uma conta inexistente. Não foi tentado login nas contas reais nem alterado seu bloqueio. A ausência explica a falha **nesse banco**; não demonstra que o operador acessa esse ambiente.

O perfil versionado do Visual Studio usa `https://localhost:53406` e `http://localhost:53407`, com Development. No momento da inspeção, não foi encontrado processo Web do projeto, `w3wp` ou IIS Express servindo a aplicação. O IIS completo não está instalado nesta máquina (sem serviço W3SVC/WAS nem `applicationHost.config` global). A configuração em `.vs` contém apenas o site genérico `WebSite1`, porta 8080, sem associação ao projeto. Dois processos Visual Studio estavam abertos.

**Não foi possível comparar com uma instalação IIS remota sem identificar sua URL e configuração efetiva.** O comando local de auditoria não representa automaticamente o ambiente de um processo IIS ou de outro computador.

O operador informou `https://localhost:63859/admin/login`. Foi constatado que essa porta corresponde ao perfil de **outro projeto**, `C:\MNSOFT\belemrestaurantes\src\BelemRestaurantes.Web\Properties\launchSettings.json`, com HTTPS 63859/HTTP 63860. Essa outra aplicação possui a rota `/admin/login`; o MinhaBelém usa `/conta/login`. O outro repositório não foi alterado nem teve banco ou credenciais consultados. No momento da consulta, não havia listener na porta 63859 e o teste HTTPS falhou na conexão TCP, antes de qualquer senha ou MFA.

Isso identifica uma divergência concreta de aplicação/URL, sem atribuir a falha ao hash ou MFA do MinhaBelém. Não foram modificadas as portas do projeto nem criada uma rota que confundisse as duas aplicações.

## Diagnóstico implementado

`--audit-auth` permanece um comando local de Development e somente leitura. Agora informa host/porta/banco/ambiente/referência, enumera explicitamente as três contas ausentes e, quando presentes, informa estado, normalização, perfis, permissões efetivas e últimas tentativas. Não exibe connection string, usuário do banco, senha, hash, carimbos, secret ou códigos de recuperação.

As permissões vêm do mesmo repositório usado para autorização e respeitam as revogações do administrador. `missingDefaultPermissions` é uma diferença em relação ao padrão do perfil: pode representar uma restrição intencional e **não** dispara restauração automática.

As tentativas HTTP passam a registrar `auth.attempt` na auditoria administrativa, com somente `stage` e `reason` no JSON. A identidade alvo é o ID da conta existente; o e-mail informado não é gravado nesse evento. A mensagem pública de falha de login continua genérica.

| Motivo administrativo | Interpretação |
|---|---|
| `account_absent` | Conta não encontrada no banco efetivo |
| `disabled` | Conta suspensa |
| `locked_out` | Bloqueio Identity após tentativas incorretas |
| `invalid_password` | Senha recusada pelo Identity; não é diagnóstico de MFA |
| `sign_in_not_allowed` | Restrição do SignInManager |
| `mfa_required` | Senha aceita; falta concluir o segundo fator |
| `password_accepted` | Senha aceita para sessão inicial de troca/setup; não equivale a acesso administrativo MFA |
| `invalid_factor` | TOTP ou código de recuperação recusado; etapa indicada separadamente |
| `challenge_missing_or_expired` | Ausência/expiração/invalidação do cookie intermediário |
| `authenticated` | Segundo fator aceito e sessão definitiva criada |

O histórico novo começa nesta versão. Sua ausência não permite inferir a senha ou reconstruir tentativas antigas. Nenhuma senha é testada por comando de diagnóstico.

## Próximo passo autorizado pelo operador

Inicialmente, o operador escolheu confirmar a URL antes de criar contas. Essa resposta foi respeitada. Após informar a correspondência da URL com o BelémRestaurantes, o operador autorizou explicitamente **“Autorizar bootstrap do MinhaBelém local”**, no destino `localhost:3308/minhabelem`, Development, referência `10431675E03A`.

A auditoria imediatamente anterior à operação reconfirmou host, porta, banco, ambiente, referência e três contas ausentes. O bootstrap foi executado e a auditoria posterior confirmou, para as três contas:

- Existência, `Enabled=true`, `MustChangePassword=true`, sem lockout ou tentativas incorretas.
- Hash e carimbos existentes, e-mail normalizado corretamente e nenhuma sessão ativa.
- Perfil esperado e permissões efetivas correspondentes ao padrão, sem permissões padrão faltantes.
- MFA ainda não configurado, para ser cadastrado pessoalmente pelo operador. As políticas administrativas continuam exigindo MFA; a conta inicial não autoriza acesso ao painel sem segundo fator.

O relatório de senhas temporárias foi criado com NTFS protegido, validade de 24 horas e leitor local de uso único. A auditoria de metadados confirmou herança desabilitada e acesso concedido somente ao usuário Windows executor, sem ler o conteúdo. O caminho foi informado ao operador, sem ler ou copiar senhas para o chat, logs de execução ou arquivos versionados. Não houve reset de conta existente.

O [snapshot seguro posterior ao bootstrap](evidencias/sprint18/bootstrap-local-snapshot.json) registra somente destino, estados das contas e permissões; não é o relatório de credenciais e não contém senhas, hashes, carimbos, secrets ou códigos.

O operador foi orientado a iniciar **MinhaBelem.Web** no Visual Studio e usar `https://localhost:53406/conta/login`. A tentativa do agente de iniciar o servidor em segundo plano foi rejeitada pela revisão automática com `blocked by policy`; esse processo não foi iniciado. A inicialização pelo próprio operador não foi declarada concluída.

Roteiro de aceite pessoal realizado:

1. Iniciar MinhaBelem.Web com o perfil correto, preservando a configuração e o chaveiro já existentes.
2. Ler as credenciais pelo script `scripts/windows/show-homologation-credentials.ps1` com o caminho local informado. O leitor remove o relatório antes de exibir a janela e não imprime senhas no console.
3. O operador realiza pessoalmente login, troca de senha e cadastro MFA em seu dispositivo. Não enviar credenciais ou códigos ao chat.
4. Para cada perfil, confirmar painel autorizado, logout, novo login com senha nova e MFA; reiniciar a aplicação de modo controlado e repetir o acesso.
5. Registrar confirmação do operador e evidências administrativas sem material secreto. Não confundir o teste automatizado com esse aceite.

Em 18/10/2026, o operador respondeu **“Consegui concluir com os três perfis”** à jornada de login, troca, MFA, logout, novo login e painel. Em confirmação específica sobre persistência após parar/iniciar a aplicação, respondeu **“Sim, confirmei após reiniciar”**. A confirmação foi registrada sem coletar credenciais ou códigos. A auditoria posterior revelou estados incompatíveis na base local; a reconciliação da instância utilizada permanece pendente, conforme a seção final deste relatório. Não é homologação de IIS remoto.

Se a URL informada apontar para outro ambiente, primeiro auditar esse destino; não criar contas no banco local apenas para contornar uma instalação diferente. Para contas existentes, a rotina de recuperação de [AUTH-LOGIN-MFA-CORRECAO.md](AUTH-LOGIN-MFA-CORRECAO.md) continua exigindo autorização explícita e preserva MFA.

## Continuidade das demais fases

Radar, Agenda, Guia, Mobilidade, Design Amazônico 5.0 e administração continuam no escopo e sua execução foi retomada após o aceite pessoal. O operador confirmou **“Ainda não há autorização documentada”** para coleta contínua das fontes; nenhuma fonte deve ser ativada nessa condição. A auditoria inicial encontrou nove fontes inativas, zero fontes operacionais, zero eventos publicados, um candidato pendente, 49 ciclos agregados sem histórico de páginas consultadas e nenhum processo/serviço Worker local. Esses números não comprovam operação contínua. As pendências e evidências anteriores permanecem em [SPRINT-17-EXECUCAO.md](SPRINT-17-EXECUCAO.md).

## Validação inicial de autenticação

A suíte completa executou **189 casos: 188 aprovados, zero falhas e um ignorado**, de vídeo OCR por ausência de FFmpeg/vídeo configurados. Usou MySQL 8.0.46 local descartável e Tesseract real para imagem. A nova auditoria foi exercitada com contas ausentes/presentes, suspensão, bloqueio, perfis/permissões e falhas HTTP de senha/MFA, mantendo a mensagem pública genérica e eventos sem credenciais. Build Release final: zero avisos e erros. As bases de teste foram removidas pela fixture e o container descartável foi encerrado.

A primeira rodada dos dez testes de autenticação/diagnóstico encontrou um erro no novo diagnóstico ao converter o ID `BIGINT UNSIGNED` retornado pelo MySQL. A conversão explícita corrigiu esse erro; a suíte completa posterior passou e a auditoria local das contas criadas funcionou. O ajuste final de classificação de lockout (`LockoutEnabled` + data futura) teve verificação específica após a suíte completa: **dois testes aprovados, zero falhas**.

As evidências sanitizadas dessa etapa estão em [validation.json](evidencias/sprint18/validation.json). Os TRX completos ficam em `TestResults`, fora do Git. Essa validação usa TestServer e não substitui TLS/IIS, reinício da instância pessoal nem confirmação do operador no painel. A matriz de redesign foi executada na etapa posterior descrita abaixo.

## Evolução operacional e visual após o aceite

O relato de confirmação após reinício permitiu retomar a evolução em paralelo ao diagnóstico do ambiente. As nove fontes permanecem inativas e não aprovadas: o operador confirmou que ainda não há autorização documentada. A descoberta/avaliação de uma fonte agora não concede autorização de coleta; habilitar uma fonte exige aprovação e referência preenchida. Reavaliar não sobrescreve uma autorização existente.

A auditoria `--audit-operations` informa somente destino, estados operacionais, contagens e presença de configuração de provedores. Web e Worker apontaram para `localhost:3308/minhabelem`, Development, referência `10431675E03A`. Foi encontrada divergência dos padrões do Collector: Web pausada/10 minutos, Worker habilitado/1 minuto. O padrão do Worker foi alinhado à Web e a conexão embutida foi removida; a conexão protegida existente continua sendo usada. A auditoria deve ser executada no diretório de cada aplicação, com seu ambiente efetivo. Isso não instala nem comprova um serviço contínuo.

Foram preparados **nove registros de sessões futuras reais**, com fontes oficiais do Theatro da Paz e SECULT, entre 21/10 e 15/11/2026. O [manifesto factual](evidencias/sprint18/official-agenda.json) registra as sessões, datas, consultas e URLs. O comando `--stage-agenda` criou os IDs 1–9 como **InReview**, exigindo aprovação humana. Não substituiu conteúdo editorial existente, não ativou fontes e não publicou registros. Horários de término desconhecidos permanecem desconhecidos; gratuidade só foi marcada quando expressamente confirmada. As imagens são ilustrações autorais, identificadas como tal.

A publicação foi encaminhada ao operador na [Central Editorial local](https://localhost:53406/editorial/lotes?kind=content), usando SuperAdmin ou Editor. Moderator não possui permissão de publicação. O Worker ganhou `--run-editorial-once`, exclusivo de Development/loopback, para processar até dez lotes **já confirmados** sem iniciar coleta. Esse comando não cria nem confirma decisões editoriais. A opção geral `--run-once` também passou a processar lotes editoriais, mantendo suas demais tarefas.

O dashboard separa fontes com falhas recorrentes de execuções parciais/falhas; execuções em andamento não são contadas como falhas. Painel e Radar mostram configuração e limitações reais. Ciclos sem fontes e heartbeat antigo não são apresentados como prova de coleta.

A identidade Amazônico 5.0 foi aplicada às superfícies compartilhadas, home, agenda, guia de atrações, formulários e administração: paleta de floresta/papel/argila, títulos editoriais, calendário e cards, fotografias reais com créditos/licenças visíveis, ícones SVG próprios e adaptação mobile. Os seis novos arquivos WebP possuem autoria, licença, transformação e SHA256 em [sprint18-credits.json](../src/MinhaBelem.Web/wwwroot/images/licensed/sprint18-credits.json). Fotografias de acervo não representam condições atuais dos locais.

As páginas de atrações integram informações de visita verificadas e mapa sob solicitação, com estado de falha e alternativa externa. A chave de servidor não entra no HTML. Google Routes e Maps continuam sem configuração local: não houve chamada real nem homologação desses provedores. O modo fluvial aponta para SEGBEL e não envia a origem do visitante ao Google. Horários e tarifas sazonais de julho não foram apresentados como vigentes em outubro. Informações sem validade/fonte confirmada permanecem indisponíveis.

A escolha de idioma por URL passa a persistir para a navegação seguinte. Os novos componentes têm textos nos sete idiomas. O [inventário Razor](evidencias/sprint18/razor-inventory.json) delimita a revisão compartilhada: renderizar rotas em sete idiomas não significa que todos os estados de cada formulário legado receberam revisão linguística individual.

A proteção de envio duplicado preserva nomes/valores dos botões e antiforgery, libera o formulário ao retornar pelo histórico e informa falta de conexão. A validação também encontrou e corrigiu a posição do tratamento de status HTTP, que convertia um GET inválido em 404 durante a reexecução da página de erro. Nenhuma política de senha, MFA, claims ou autorização foi relaxada.

## Divergência pendente entre aceite informado e base inspecionada

Uma nova auditoria somente leitura, em 18/10/2026 às 21:19 UTC, encontrou as três contas da referência `10431675E03A` ainda com `MustChangePassword=true`, `TwoFactorEnabled=false`, zero sessões e sem tentativas recentes. Isso **não reconcilia** o relato do operador de ter concluído a jornada após reinício. Nenhuma conta foi redefinida ou alterada. A confirmação pessoal permanece registrada como relato; não deve ser apresentada como prova técnica de conclusão nessa base. A evidência atual está em [auth-after-personal-acceptance.json](evidencias/sprint18/auth-after-personal-acceptance.json).

O perfil versionado do MinhaBelem.Web usa Development e HTTPS 53406/HTTP 53407, sem override de conexão no launchSettings. No momento da consulta não havia listener nessas portas; o applicationhost.config local contém apenas o site padrão HTTP 8080. Isso não identifica a instância efetivamente usada pelo operador. Foi solicitada a URL da instância aceita e seus metadados efetivos, sem credenciais.

Para diagnosticar a própria instância em execução, o novo GET `/admin/ambiente` exige permissão `settings.manage` e MFA pelas políticas existentes, não é cacheado e devolve somente host, porta, banco, ambiente e referência. O operador SuperAdmin pode consultar essa URL após autenticar. Não retorna connection string, usuário de banco, senha, hashes ou material MFA. Essa consulta evita presumir que o processo do Visual Studio/IIS carregou a mesma configuração do comando local.


## Resultado da validação desta evolução

- Build Release da solução: zero avisos, zero erros.
- Suíte completa após correção do tratamento de status: **198 casos, 197 aprovados, zero falhas, um ignorado** (vídeo OCR sem FFmpeg/vídeo configurados). MySQL 8.0.46 descartável; imagem OCR com Tesseract real. As jornadas Identity/MFA e permissões existentes permaneceram aprovadas.
- A rodada inicial teve duas falhas nos testes novos: expectativa de 403 direto em vez do redirecionamento de acesso negado do cookie, e GET inválido convertido em 404 pelo middleware de status. O teste passou a seguir a recusa até o 403, e a ordem do middleware foi corrigida. Os nove testes de evolução passaram; a suíte completa posterior também.
- Playwright/Edge: **1.484 combinações**, 53 páginas × sete idiomas × quatro tamanhos, sem falhas nos critérios automatizados de largura, imagens, alternativas de imagem e identificação de campos/botões. [Resultado](evidencias/sprint18/visual/layout-results.json) e capturas desktop/mobile no mesmo diretório. São snapshots MVC de fixtures, com rede externa bloqueada; não provam publicação de conteúdo real nem chamadas Google.
- Quatro grupos de interação aprovados: envio único com submitter/antiforgery preservados, retorno pelo histórico, bloqueio de envio offline com mensagem, e carregamento do mapa apenas por clique com recuperação de falha. [Evidência](evidencias/sprint18/interaction-results.json).
- Seis variantes fotográficas verificadas contra tamanho e SHA256 do manifesto. Fotografias e créditos inspecionados na home; painel, agenda, login e atração também receberam inspeção das capturas.

Os TRX locais e resultados sanitizados estão relacionados em [evolution-validation.json](evidencias/sprint18/evolution-validation.json). Não houve deploy nem push. A revisão de superfícies compartilhadas não certifica todos os estados das 69 Views/partials Razor, todas as traduções legadas, WCAG completo ou navegação real em IIS.

Permanecem pendentes: reconciliar o ambiente do acesso pessoal informado; decisão humana sobre publicação dos nove eventos; autorização documentada das fontes; instalação/monitoramento do Worker real e operação contínua; monitoramento real de cancelamentos/adiamentos; configuração protegida e homologação Google; catálogo atual ampliado de linhas/pontos/travessias com fontes vigentes; revisão individual restante de telas e traduções legadas. Não declarar a Sprint inteira concluída nem o acesso desse banco tecnicamente homologado enquanto a divergência de contas persistir.

Após a suíte completa, o novo diagnóstico da instância em execução e os demais casos de evolução foram verificados novamente: **dez aprovados, zero falhas**, incluindo negação a anônimo/Moderator, autorização do SuperAdmin e retorno de somente cinco campos de metadados com `no-store`. Build final: zero avisos/erros. Confirmada ausência de schemas de teste remanescentes; o container descartável foi encerrado e removido. Código registrado em `4d989a7`.

## Conferência do relato de publicação dos nove eventos

Em 18/10/2026, o operador informou “Revisei e publiquei os nove eventos”. A auditoria posterior somente leitura da referência `10431675E03A` encontrou os nove IDs ainda `InReview`, revisão 1, aprovação humana requerida e **nenhum lote ou item de lote registrado**. Portanto não há lote confirmado a ser drenado pelo Worker nesse destino. Não foi executada publicação, reset ou alteração de contas para reconciliar o relato.

O operador informou não saber a URL utilizada. O inventário do navegador conectado à sessão não disponibilizou abas ou navegadores; isso não prova ausência de navegador na máquina. A instância usada continua não identificada. O [snapshot editorial](evidencias/sprint18/editorial-after-operator-report.json) preserva somente metadados, sem conteúdo editorial, justificativas ou credenciais.

Auditoria editorial local reproduzível, somente leitura:

```powershell
dotnet run --project scripts/MinhaBelem.Verify -c Release -- --editorial-audit
```

Para identificar a página utilizada, basta pressionar `Ctrl+L` no navegador e copiar o endereço. O comando `--import-agenda` é uma operação pontual e termina ao concluir; para servir o portal, iniciar `dotnet run --project src/MinhaBelem.Web -c Release` sem a opção de importação e abrir `https://localhost:53406/conta/login`. Não é necessário enviar senha, cookie ou código MFA.
