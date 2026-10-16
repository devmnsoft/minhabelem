# MySQL Locaweb — MinhaBelém

- Banco informado: `minhabelem`
- Host informado: `minhabelem.mysql.dbaas.com.br`
- Porta padrão MySQL: 3306 (confirmar no painel)
- Usuário e senha: **não armazenar no repositório**
- Site: `minhabelem.com.br`

## Validações obrigatórias antes de usar a VPS
1. Verificar na Locaweb se a instância MySQL DBaaS autoriza conexões **de aplicação** vindas do IP público da VPS. Não confundir permissão administrativa remota com uso contínuo da aplicação.
2. Confirmar usuário, versão do servidor, porta, exigência de TLS e eventual allowlist. Não abrir 3306 indiscriminadamente.
3. Fazer teste de conexão da VPS usando cliente MySQL com TLS e usuário específico de aplicação, de menor privilégio; não enviar senha em chat nem comitar credenciais.
4. Verificar latência, limites de conexões, políticas de backup e custo de tráfego.
5. Se acesso remoto de aplicação for proibido, migrar o banco para a VPS e apontar conexão pelo nome de serviço privado.
6. Selecionar a base no SGBD e executar o arquivo único `MINHABELEM.SQL` da raiz. O portal utiliza MySqlConnector + Dapper.

## Exemplo de variáveis de ambiente (somente ilustração)
```text
ConnectionStrings__Default=Server=minhabelem.mysql.dbaas.com.br;Port=3306;Database=minhabelem;User ID=USUARIO_CONFIGURADO_NO_PROVEDOR;Password=SEGREDO_DO_SERVIDOR;SslMode=Required
```
O portal utiliza MySqlConnector. Nunca publique esse valor com segredos reais.
