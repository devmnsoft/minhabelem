# Configurar MySQL no Visual Studio Community 2026

A aplicação utiliza **MySqlConnector + Dapper**, não Npgsql. Execute primeiro o script `database/mysql/001_schema.sql` no banco MySQL `minhabelem` usando uma conta autorizada pela Locaweb e verifique a política de acesso externo.

## No Visual Studio (máquina de desenvolvimento)

Abra `MinhaBelem.sln`, clique com botão direito em `MinhaBelem.Web` e escolha **Gerenciar Segredos do Usuário**. Configure:

```json
{
  "ConnectionStrings": {
    "Default": "Server=minhabelem.mysql.dbaas.com.br;Port=3306;Database=minhabelem;User ID=SEU_USUARIO;Password=SUA_SENHA;SslMode=Required;MaximumPoolSize=5;"
  },
  "Editorial": { "AccessToken": "CONFIGURE_UMA_CHAVE_ALEATORIA_DE_32_OU_MAIS_CARACTERES" }
}
```

*Esse JSON é apenas modelo, não utilize a chave literal ilustrativa.*

Não envie senha por chat e não grave credenciais no `appsettings.json` versionado. Para o User Secrets funcionar, inicialize com `dotnet user-secrets init --project src/MinhaBelem.Web/MinhaBelem.Web.csproj` se necessário.

## Diagnóstico da conexão

- **Connection string ausente:** a aplicação orientará a configurar `ConnectionStrings:Default`.
- **Unable to connect / timeout:** confirme liberação de acesso externo à Locaweb e regras de firewall.
- **Access denied:** confira credenciais e permissões do usuário MySQL.
- **Table doesn't exist:** execute `database/mysql/001_schema.sql` após backup e validação de compatibilidade de versão.
- **Certificado / TLS:** valide política de certificado e cadeia de confiança. Nunca use `SslMode=None` como solução de produção.

## Produção

Na VPS Windows, configure `ConnectionStrings__Default` em variável de ambiente protegida, com credenciais de menor privilégio. **User Secrets não é repositório de segredos de produção**. Configure autenticação administrativa definitiva, Data Protection persistente, monitoramento, backup, MFA e proteção contra SSRF antes de liberar coleta automática.

Nota: este guia não comprova que a instância Locaweb autoriza conexões externas da aplicação.
