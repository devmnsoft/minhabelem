# Autenticação — troca de senha, MFA e recuperação segura

Data: 18/10/2026. Base auditada: `3a2a43c`. Código e testes: `6352606`. Correção no código existente, sem migrations, sem implantação e sem redefinição de contas reais.

## Diagnóstico e causa reproduzida

A falha confirmada está na consistência da operação de troca de senha sob concorrência. `ContaController.Senha` chamava `ChangePasswordAsync` e depois fazia outra atualização para limpar `MustChangePassword`. A primeira gravação podia persistir o hash e o SecurityStamp, enquanto a segunda falhava com conflito. Nesse caso, a resposta era HTTP 409 e a conta permanecia com restrição de primeiro acesso, embora a senha já tivesse sido alterada. Um teste com alteração controlada do ConcurrencyStamp no limite do primeiro commit reproduziu esse estado na versão anterior. O trigger é exclusivo da fixture descartável e não faz parte da aplicação ou de migrations.

O store também trocava `user.ConcurrencyStamp` em memória antes de saber se o UPDATE havia sido confirmado. Um teste separado reproduziu o carimbo em memória alterado mesmo após `ConcurrencyFailure`. O WHERE otimista impedia sobrescrever o hash de outro escritor, mas a instância ficava inconsistente para novas operações. Esse teste também falhou na versão anterior.

**Limite do diagnóstico:** no fluxo sem concorrência, os testes anteriores conseguiram persistir e verificar a senha nova e validar o TOTP. Não foi reproduzido desaparecimento espontâneo do hash depois de configurar MFA. As três jornadas iniciais falharam no requisito de destino: o código enviava todos para `/editorial`, sem escolher a entrada por permissões. Esse destino não constitui, por si só, falha de senha.

A leitura local de `mb_users`, sem modificar dados nem imprimir hashes/carimbos, não encontrou nenhuma das três contas informadas. Appsettings e User Secrets apontaram para a mesma referência de banco, `10431675E03A`, MariaDB 10.11.7. Portanto, não é possível atribuir conclusivamente o incidente histórico de uma conta real a um estado que não está presente nessa base. A referência permite comparar ambientes sem expor conexão ou senha.

## Correção implementada

- Hash, SecurityStamp, data de alteração e `MustChangePassword=false` são persistidos no mesmo UPDATE realizado pelo Identity. Não há segunda gravação de metadados depois de `ChangePasswordAsync`.
- Após sucesso, o usuário é recarregado e a nova senha é conferida por `UserManager.CheckPasswordAsync` antes de confirmar a operação. Conflitos retornam resposta explícita, sem confirmação falsa ou tentativa de sobrescrever dados concorrentes.
- `MySqlUserStore.UpdateAsync` altera o ConcurrencyStamp do objeto somente depois de UPDATE bem-sucedido. `CreateAsync` também persiste `MustChangePassword` desde a criação.
- Troca de senha revoga todas as sessões anteriores do usuário e encerra os cookies. O Identity continua responsável por hashing e mudança do SecurityStamp.
- Login e conclusão MFA recarregam o usuário antes de atualizar `LastLoginAt`, verificam o resultado do Identity e não ignoram conflitos.
- A entrada é escolhida pelas permissões efetivas: dashboard, editorial, moderação, anúncios ou perfil. Os três perfis padrão possuem `dashboard.read` e chegam a `/admin`. A restrição de troca de senha tem prioridade.
- O cookie intermediário MFA agora inclui a versão das credenciais. Troca/reset de senha, bloqueio ou suspensão invalidam desafios pendentes. Cookies intermediários antigos sem esse carimbo exigem novo login.
- MFA permanece obrigatório nas políticas. A confirmação continua pelo provedor TOTP do ASP.NET Core Identity; autenticação definitiva mantém `amr=mfa` e uma única claim `mb:session`.
- Códigos de recuperação permanecem cifrados, consumidos de forma transacional e de uso único. Tentativas incorretas de recuperação também contam para o bloqueio da conta.
- A troca obrigatória deve ser concluída antes de iniciar/configurar MFA. Habilitar MFA encerra as sessões anteriores; o próximo acesso exige senha e segundo fator.

O comportamento de emissão de `amr=mfa` pelo SignInManager foi conferido no [código oficial do ASP.NET Core 8](https://github.com/dotnet/aspnetcore/blob/v8.0.21/src/Identity/Core/src/SignInManager.cs). As operações de troca/reset continuam usando o [UserManager oficial](https://github.com/dotnet/aspnetcore/blob/v8.0.21/src/Identity/Extensions.Core/src/UserManager.cs).

## Matriz de verificação

| Item | Verificação |
|---|---|
| Hash persistido | Usuário recarregado, senha nova aceita e temporária recusada pelo Identity |
| Concorrência | Falha explícita, objeto não recebe carimbo fictício e hash novo não é sobrescrito por instância antiga |
| SecurityStamp | Muda após troca/reset, sem atribuição manual de hash |
| Sessões antigas | Revogação em `mb_user_sessions` e rejeição em `PortalCookieEvents.ValidatePrincipal` |
| Login após troca | Exercitado por HTTP para SuperAdmin, Editor e Moderator |
| Cookie MFA | HTTPS, Secure, HttpOnly, Path=/, cinco minutos e vínculo ao SecurityStamp |
| TOTP | Código do autenticador validado; cinco erros causam bloqueio |
| Sessão definitiva | Claims `amr=mfa` e exatamente uma `mb:session`, dashboard autorizado |
| Redirects | Senha obrigatória → perfil; setup → MFA; MFA concluído → destino por permissões |
| Recuperação | Código válido autentica; repetição é recusada; rotina de senha preserva MFA e papéis |
| Lockout | Cinco erros de senha/TOTP, duração de 15 minutos preservada |
| Reinício | Host encerrado e novo host criado com a mesma base e diretório de chaves; cookie e novo login MFA funcionam |
| Dados reais | Somente leitura da base de desenvolvimento; nenhuma conta real criada, habilitada ou redefinida |
| IIS | Configuração auditada no código; sem acesso/implantação/homologação real do servidor nesta execução |

## Testes e evidências

Resultado em 18/10/2026: **8/8 testes de autenticação aprovados**, incluindo as três jornadas completas. A suíte geral aprovou **186 testes, zero falhas e um ignorado** (vídeo OCR exige FFmpeg/vídeo configurados). A rodada final dos oito testes foi repetida após os ajustes de rejeição de desafio sem carimbo e gravação antecipada do relatório protegido. Os dois testes de concorrência falharam na base anterior e passaram na versão corrigida.

Os testes HTTP usam a pipeline MVC/Identity real por `WebApplicationFactory`, com **MySQL 8.0.46 real em container local descartável**, sem simular password hashing, TOTP, cookies ou repositórios. Cada jornada dos três perfis usa uma base aleatória própria. Em Windows, usa o bootstrap real e consome o relatório protegido apenas em memória, apagando o arquivo. No CI Linux, a preparação equivalente usa Identity, pois o relatório ACL é exclusivo do Windows.

Jornada: bootstrap → login temporário → troca → leitura e verificação do hash persistido → sessão encerrada → login novo → setup MFA → encerramento → login senha + TOTP → dashboard → logout → novo login → revogação → novo login → encerramento do host → novo host → cookie válido → logout → novo login MFA.

Casos adicionais: concorrência controlada, preservação de hash, lockout de senha e TOTP, recuperação de uso único, desafio expirado, desafio invalidado por reset e rotina local protegida para as três contas de teste. Resultados e limites estão em [validation.json](evidencias/auth-fix/validation.json). TRX completos permanecem em `TestResults`, fora do Git; o relatório versionado não contém credenciais, hashes, secrets TOTP, recovery codes ou cookies.

Para repetir, fornecer uma conexão **sem Database**, exclusivamente para um servidor MySQL local descartável. A fixture recusa servidor remoto ou conexão com banco selecionado e cria/remove apenas bases próprias com nome aleatório:

```powershell
$env:MINHABELEM_TEST_MYSQL = '<conexão do MySQL local descartável, sem Database>'
dotnet test MinhaBelem.sln -c Release --filter 'FullyQualifiedName~AuthenticationJourneyTests'
# Suíte completa, mantendo a mesma conexão descartável:
dotnet test MinhaBelem.sln -c Release
Remove-Item Env:MINHABELEM_TEST_MYSQL
```

Esses testes usam TestServer: exercitam requests/responses HTTP, antiforgery, redirects, cookies e autorização na pipeline real; não fazem handshake TLS nem validam uma instalação IIS. A persistência do chaveiro é conferida encerrando e recriando o host, sem simular a restauração do cookie.

## Recuperação segura das contas existentes

A rotina **não foi executada em contas reais**. Ela existe apenas como comando local explícito, nunca como tarefa de startup, Worker ou endpoint HTTP. Exige simultaneamente:

1. Windows, ambiente Development e conexão loopback com banco selecionado.
2. Autorização local `Homologation:AllowCredentialRecovery=true`.
3. `Homologation:RecoveryDatabaseReference` igual à referência obtida na auditoria do banco autorizado.
4. Flag de confirmação explícita do operador.
5. Conta na lista das três contas de homologação, existente, habilitada e com o perfil esperado. Não cria conta, habilita usuário, altera papéis ou desativa MFA.

Todas as contas selecionadas são conferidas antes da primeira redefinição. Se as chaves do autenticador existente não puderem ser lidas, a operação exige restaurar o chaveiro antes de prosseguir. Para cada conta, usa `GeneratePasswordResetTokenAsync` e `ResetPasswordAsync`, gera senha temporária criptograficamente aleatória, verifica a persistência, exige troca no próximo acesso, remove lockout de homologação e revoga as sessões anteriores. Conflitos interrompem a execução, sem retry que sobrescreva outra alteração.

### Somente quando o operador autorizar o banco correto

Consultar sem alterações:

```powershell
dotnet run --project scripts/MinhaBelem.Verify -c Release -- --auth-audit
# Confere a configuração efetiva do host Web em Development:
dotnet run --project src/MinhaBelem.Web -c Release -- --audit-auth
```

Depois de conferir ambiente, referência e autorização, o operador pode configurar os dois valores de homologação em User Secrets. Não habilitar isso em produção. Para recuperar uma única conta permitida:

```powershell
dotnet run --project src/MinhaBelem.Web -c Release -- --recover-homologation-passwords --confirm-local-homologation --account editor.teste@minhabelem.com.br
```

Sem `--account`, a seleção é das três contas permitidas. Conta ausente não é recriada. `--account` sem valor é recusado. A operação imprime somente o caminho do relatório, nunca a senha ou o token.

O relatório fica em `%LOCALAPPDATA%\MNSOFT\Homologacao\Recovery\<id>`, com herança NTFS desabilitada e acesso exclusivo ao usuário Windows executor. A validade do **relatório** é de 24 horas. A leitura local única pelo script abaixo remove o arquivo antes de mostrar a janela; não envia as credenciais ao console. As senhas exigem troca no primeiro uso; a expiração do relatório não é anunciada como expiração automática da senha.

```powershell
./scripts/windows/show-homologation-credentials.ps1 -ReportPath '<caminho retornado localmente>'
```

Após uso, desabilitar a autorização de recuperação em User Secrets. Se ocorrer interrupção parcial, consultar a auditoria e executar nova recuperação autorizada apenas para a conta necessária; não tentar adivinhar qual senha temporária ficou válida.

## Data Protection, localhost e IIS

A correção preserva `ApplicationName=MinhaBelem`, o propósito `MinhaBelem.Identity.Tokens.v1`, o formato de hash e o armazenamento cifrado existente. Não move, apaga ou recria automaticamente o chaveiro das contas reais.

- No desenvolvimento, usar **https://localhost:53406** conforme o perfil atual. A porta HTTP 53407 serve para redirecionamento. Cookies `__Host-` continuam Secure, sem Domain e com Path=/; não serão relaxados para HTTP. Confiar no certificado de desenvolvimento quando necessário, sem ignorar a validação TLS da aplicação.
- No IIS, preservar `DataProtection:KeysPath`, as chaves antigas, a identidade e as permissões NTFS do App Pool. Com DPAPI, a identidade que protege e lê as chaves precisa ser compatível; com certificado, preservar certificado e chave privada. Configurar HTTPS e proxies conhecidos corretamente quando houver terminação TLS. Não confiar indiscriminadamente em headers de qualquer proxy.
- Não excluir `App_Data`, pastas de chaves ou caches do servidor como tentativa de resolver MFA. Senhas são verificadas por hash; perder Data Protection afeta cookies, tokens e leitura do secret MFA, e uma redefinição de senha isolada não restaura essas chaves.
- Se a senha passar e a etapa MFA falhar, conferir relógio UTC/NTP e segundo fator. Usar um recovery code ainda não consumido quando disponível. Se o dispositivo e os códigos foram perdidos, é necessário um procedimento separado e expressamente autorizado de reenrolamento; esta rotina não desabilita MFA.
- Depois de publicar esta versão, desafios temporários MFA antigos podem pedir novo login por não possuírem o vínculo ao SecurityStamp. Não se trata de redefinição de senha.

## Limites e confirmação operacional

A correção do código é validada pelos testes e pela comparação antes/depois. Para confirmar o incidente das contas existentes, ainda é necessário identificar o ambiente/banco onde essas contas estão presentes e executar a jornada com o próprio operador. Não foram solicitadas nem coletadas senhas ou secrets TOTP do usuário. Não houve deploy, alteração de DNS, modificação de permissões de contas reais ou reset de produção.
