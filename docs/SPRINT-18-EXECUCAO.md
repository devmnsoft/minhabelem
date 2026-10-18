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

Em 18/10/2026, o operador respondeu **“Consegui concluir com os três perfis”** à jornada de login, troca, MFA, logout, novo login e painel. Em confirmação específica sobre persistência após parar/iniciar a aplicação, respondeu **“Sim, confirmei após reiniciar”**. Isso encerra o aceite pessoal solicitado, sem coletar credenciais ou códigos. A confirmação abrange o ambiente local identificado; não é homologação de uma instalação IIS remota.

Se a URL informada apontar para outro ambiente, primeiro auditar esse destino; não criar contas no banco local apenas para contornar uma instalação diferente. Para contas existentes, a rotina de recuperação de [AUTH-LOGIN-MFA-CORRECAO.md](AUTH-LOGIN-MFA-CORRECAO.md) continua exigindo autorização explícita e preserva MFA.

## Continuidade das demais fases

Radar, Agenda, Guia, Mobilidade, Design Amazônico 5.0 e administração continuam no escopo e sua execução foi retomada após o aceite pessoal. O operador confirmou **“Ainda não há autorização documentada”** para coleta contínua das fontes; nenhuma fonte deve ser ativada nessa condição. A auditoria inicial encontrou nove fontes inativas, zero fontes operacionais, zero eventos publicados, um candidato pendente, 49 ciclos agregados sem histórico de páginas consultadas e nenhum processo/serviço Worker local. Esses números não comprovam operação contínua. As pendências e evidências anteriores permanecem em [SPRINT-17-EXECUCAO.md](SPRINT-17-EXECUCAO.md).

## Validação de código

A suíte completa executou **189 casos: 188 aprovados, zero falhas e um ignorado**, de vídeo OCR por ausência de FFmpeg/vídeo configurados. Usou MySQL 8.0.46 local descartável e Tesseract real para imagem. A nova auditoria foi exercitada com contas ausentes/presentes, suspensão, bloqueio, perfis/permissões e falhas HTTP de senha/MFA, mantendo a mensagem pública genérica e eventos sem credenciais. Build Release final: zero avisos e erros. As bases de teste foram removidas pela fixture e o container descartável foi encerrado.

A primeira rodada dos dez testes de autenticação/diagnóstico encontrou um erro no novo diagnóstico ao converter o ID `BIGINT UNSIGNED` retornado pelo MySQL. A conversão explícita corrigiu esse erro; a suíte completa posterior passou e a auditoria local das contas criadas funcionou. O ajuste final de classificação de lockout (`LockoutEnabled` + data futura) teve verificação específica após a suíte completa: **dois testes aprovados, zero falhas**.

As evidências sanitizadas de testes estão em [validation.json](evidencias/sprint18/validation.json). Os TRX completos ficam em `TestResults`, fora do Git. Essa validação usa TestServer e não substitui TLS/IIS, reinício da instância pessoal nem confirmação do operador no painel. Não houve nova matriz Playwright de redesign, pois a fase visual ainda não foi iniciada.
