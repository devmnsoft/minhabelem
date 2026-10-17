# Operação e atualização segura

## Banco e legado

1. Faça backup restaurável e ensaie a restauração em instância isolada. Confira versão, esquema, permissões e integridade dos dados.
2. No SGBD, selecione a base já provisionada e execute `MINHABELEM.SQL` inteiro. É o único script de esquema: 45 tabelas em instalação nova, 4 papéis, 15 permissões e 33 vínculos iniciais. Não requer stored procedures nem cria senhas.
3. Colunas preexistentes incompatíveis não são reconstruídas. A etapa de FKs rejeita dados órfãos sem removê-los. Corrija a causa em cópia antes da atualização real.
4. Alternativa explícita: `dotnet run --project src/MinhaBelem.Web -- --initialize-database`. Usa o mesmo recurso SQL, lock e checksums. Inicialização normal e publish não executam DDL.
5. Para dados legados, ensaie `dotnet run --project src/MinhaBelem.Web -- --import-legacy`. Preserva IDs públicos e origem, importa sugestões/histórico, deixa fontes antigas desabilitadas/não aprovadas e rejeita conflitos de ID. Senhas antigas não são copiadas. Compare contagens e amostras em cópia antes de uma janela real.

DDL MySQL tem commit implícito; uma falha pode deixar etapas anteriores criadas. Diagnostique antes de reexecutar. Não altere seções já aplicadas: acrescente migrações ao arquivo único. Customizações de permissões da aplicação ficam separadas dos seeds SQL.

Mantenha backup e versão anterior. Não retorne a código que grave nas tabelas legadas após iniciar novas edições sem reconciliar os dados. Não há downgrade ou exclusão automática.

## Segredos e primeiro acesso

Use Gerenciar Segredos do Usuário no projeto Web para desenvolvimento. Em servidor, use cofre/variáveis protegidas e ACL da identidade do App Pool. User Secrets não é cofre de produção. Em variáveis de ambiente .NET, substitua `:` por `__`.

| Configuração | Uso |
| --- | --- |
| `ConnectionStrings:Default` | MySQL com TLS e conta de menor privilégio |
| `Bootstrap:Email`, `Bootstrap:Password` | Segredos temporários para primeiro administrador; senha exclusiva, mínimo 12 caracteres e requisitos Identity |
| `DataProtection:KeysPath` | Diretório persistente fora de wwwroot, acesso exclusivo do serviço |
| `DataProtection:CertificatePath`, `CertificatePassword` | Certificado para proteger chaves; Windows usa DPAPI se não houver certificado configurado |
| `ReverseProxy:KnownProxies:0` | IP exato do proxy autorizado, se houver |
| `Ai:Groq`, `Ai:Gemini`, `Ai:DeepSeek` | `ApiKey`, `Model`, `InputCostPerMillion`, `OutputCostPerMillion` |
| `Ai:MaxDailyRequests` | Cota por provedor/dia; padrão 50 |
| `GooglePlaces:ApiKey`, `DailyRequestLimit` | Chave restrita no provedor e cota local; padrão 50/dia, incluindo detalhes/fotos |
| `Sources:Credentials:REFERENCIA:Token` ou `ApiKey` | Credencial obtida por fluxo autorizado da plataforma |
| `Sources:Credentials:REFERENCIA:AllowedHost` | Domínio exato autorizado a receber a credencial |
| `Ocr:ExecutablePath`, `Ocr:FfmpegPath` | Caminhos absolutos Tesseract (idioma `por`) e FFmpeg |
| `Ocr:AllowedMediaHosts:0` | Host de mídia autorizado; lista vazia bloqueia processamento |

Forneça Bootstrap por mecanismo protegido e execute `dotnet run --project src/MinhaBelem.Web -- --bootstrap-admin`. Conta existente não é reativada. Remova os segredos Bootstrap após criar a conta. Entre por HTTPS, configure TOTP, guarde os oito códigos de recuperação fora do servidor e entre novamente com segundo fator. Sem essa prova, módulos administrativos/editoriais ficam bloqueados.

Não há senha padrão nem envio de senha por e-mail. Nunca ponha token/senha em URL, log ou arquivo versionado. Backup de chaves Data Protection e certificado faz parte da recuperação; perdê-los pode inutilizar TOTP/recovery protegidos. Valide reciclagem/troca da identidade do serviço. Em múltiplos servidores, use chaves e proteção compatíveis.

## Fontes, IA e custos

Cadastre responsável, URL pública HTTPS, protocolo, licença, região, frequência e cota em `/admin/fontes`. Aprove somente após conferir acesso e direitos. Instagram/Facebook/TikTok dependem de aplicativo, conta elegível, escopos e tokens. Obtenção e renovação OAuth são procedimentos do operador; não há fluxo OAuth completo na interface.

YouTube usa `youtube/v3/search` com chave/token apropriado. Pesquisa Google autorizada exige motor `cx` configurado. Confirme endpoints/campos na versão aprovada da API. JSON governamental/personalizado deve seguir o formato suportado (`items`/`data`, título, link, resumo) ou receber adaptador específico.

Mantenha `Collector:Enabled=false` até homologar. O painel permite habilitar coleta e intervalo de 10 a 240 minutos; frequência, cota e backoff da fonte prevalecem. O botão manual consulta fontes vencidas/autorizadas. Monitore execuções, erros e próximos horários. Não há garantia de tempo real ou alerta externo por e-mail/SMS.

Chave configurada não comprova API operacional. Confira execução autorizada, persistência, latência e uso no histórico e cobrança do provedor. Preços de IA são estimativas configuradas. Aplique limites também nos provedores. Nenhuma publicação é automática.

## Privacidade e retenção

Não envie documentos, dados de saúde ou de menores no formulário público. A triagem local retém alguns padrões antes da IA; revisão humana permanece obrigatória.

Cookies administrativos duram até duas horas e o intermediário MFA cinco minutos. Recovery é de uso único. Mídia OCR é transitória em memória; extração/licença/ator persistem. Conteúdo expirado é arquivado periodicamente, sem excluir histórico ou backups. Não há expurgo geral automático de candidatos, sessões/auditoria ou sugestões.

Antes da operação pública, o controlador deve publicar contato oficial, finalidades, bases e prazos por classe, atendimento a titulares, contratos com provedores e retenção/descarte de backups. A política pública atual informa essa pendência. Não se declara conformidade LGPD completa nem se excluem dados por prazo não definido pelo responsável.

## Homologação reproduzível

Use somente MySQL local descartável. A variável de testes deve omitir Database; a fixture rejeita host não local e cria/remove apenas seus bancos aleatórios.

```powershell
docker run --detach --name minhabelem-test-mysql --publish 127.0.0.1:33079:3306 --env MYSQL_ALLOW_EMPTY_PASSWORD=yes mysql:8.0
# Aguarde a inicialização do MySQL.
$env:MINHABELEM_TEST_MYSQL = 'Server=127.0.0.1;Port=33079;User ID=root;SslMode=None'
dotnet test MinhaBelem.sln -c Release --logger trx --results-directory TestResults
```

Senha vazia/TLS desabilitado acima são exclusivos desse contêiner local descartável. Para OCR/vídeo reais:

```powershell
docker build -f Dockerfile.homologacao -t minhabelem-homologacao .
docker run --rm --network container:minhabelem-test-mysql --env 'MINHABELEM_TEST_MYSQL=Server=127.0.0.1;Port=3306;User ID=root;SslMode=None' minhabelem-homologacao
docker rm --force --volumes minhabelem-test-mysql
```

HTTPS, Identity e MySQL são reais; APIs externas são simuladas. O contêiner instala Tesseract/FFmpeg e gera vídeo do cartaz próprio da suíte. Não aponte testes à produção. Sem a variável MySQL, testes de integração são ignorados.
