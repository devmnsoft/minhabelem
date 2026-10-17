# Configurar MySQL no Visual Studio Community 2026

A aplicação utiliza **MySqlConnector + Dapper**, não Npgsql. Execute primeiro o script `MINHABELEM.SQL` no banco MySQL `minhabelem` usando uma conta autorizada pela Locaweb e verifique a política de acesso externo.

## No Visual Studio (máquina de desenvolvimento)

Abra `MinhaBelem.sln`, clique com botão direito em `MinhaBelem.Web` e escolha **Gerenciar Segredos do Usuário**. Configure:

```json
{
  "ConnectionStrings": {
    "Default": "Server=minhabelem.mysql.dbaas.com.br;Port=3306;Database=minhabelem;User ID=SEU_USUARIO;Password=SUA_SENHA;SslMode=Required;MaximumPoolSize=5;"
  }
}
```

*Esse JSON é apenas modelo. A autenticação atual usa Identity e TOTP; não usa token editorial compartilhado.*

Não envie senha por chat e não grave credenciais no `appsettings.json` versionado. Para o User Secrets funcionar, inicialize com `dotnet user-secrets init --project src/MinhaBelem.Web/MinhaBelem.Web.csproj` se necessário.

## Diagnóstico da conexão

- **Connection string ausente:** a aplicação orientará a configurar `ConnectionStrings:Default`.
- **Unable to connect / timeout:** confirme liberação de acesso externo à Locaweb e regras de firewall.
- **Access denied:** confira credenciais e permissões do usuário MySQL.
- **Table doesn't exist:** execute `MINHABELEM.SQL` após backup e validação de compatibilidade de versão.
- **Certificado / TLS:** valide política de certificado e cadeia de confiança. Nunca use `SslMode=None` como solução de produção.

## Produção

Na VPS Windows, configure `ConnectionStrings__Default` em variável protegida, com credenciais de menor privilégio. **User Secrets não é cofre de produção**. Configure Data Protection persistente, bootstrap Identity, TOTP, monitoramento e backup conforme [OPERACAO.md](OPERACAO.md). Homologue as fontes antes de habilitar coleta.

Nota: este guia não comprova que a instância Locaweb autoriza conexões externas da aplicação.
