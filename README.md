# MinhaBelém

Portal de notícias, cultura, agenda, serviços e guia de Belém e do Pará. ASP.NET Core MVC/Razor (.NET 8), Dapper e MySQL 8.

A Web usa Core/Infrastructure. A coleta prepara propostas; a publicação exige revisão, verificação, permissão e MFA. Não há conteúdo fictício apresentado como informação em tempo real.

## Banco único

[MINHABELEM.SQL](MINHABELEM.SQL), na raiz, é a única fonte SQL, também embarcada em Infrastructure. Crie/selecione a base MySQL no SGBD e execute **o arquivo inteiro uma vez**. Inclui 45 tabelas, índices, relacionamentos, localidades e papéis iniciais; não cria administrador ou senha padrão. Pode ser reexecutado sem apagar registros. Faça backup e ensaie em cópia antes de atualizar uma base existente.

A aplicação e a publicação não executam DDL automaticamente. A transferência das tabelas antigas para `mb_` usa comando explícito, preservando IDs públicos. Colunas divergentes ou dados órfãos precisam de diagnóstico: `CREATE TABLE IF NOT EXISTS` não corrige esquemas incompatíveis.

## Desenvolvimento

Abra `MinhaBelem.sln` no Visual Studio com suporte a .NET 8 ou use:

```powershell
dotnet restore MinhaBelem.sln
dotnet build MinhaBelem.sln -c Release
dotnet test MinhaBelem.sln -c Release
dotnet run --project src/MinhaBelem.Web --launch-profile MinhaBelem.Web
```

Configure `ConnectionStrings:Default` nos segredos do projeto Web. O acesso usa HTTPS, Identity, TOTP e códigos de recuperação. O administrador inicial é criado por comando explícito com segredos do operador. Veja [operação e bootstrap](docs/OPERACAO.md).

Sem `MINHABELEM_TEST_MYSQL`, testes de banco são ignorados: isso **não** equivale à homologação completa. A suíte completa usa MySQL descartável, cookies e antiforgery reais, APIs simuladas e OCR/vídeo reais em Linux. Consulte o [relatório das Sprints 03 e 03B](docs/SPRINT-03-HOMOLOGACAO.md).

## Fluxos conectados

- `/conta`: login, TOTP, recuperação, senha e sessões.
- `/admin`: usuários, perfis/permissões, fontes/execuções, integrações, auditoria, configurações, colaborações e lugares.
- `/editorial`: propostas, comparação com a fonte, edição, IA/OCR, verificação, publicação, atualização e arquivamento.
- `/agenda`, `/proximos-eventos`, `/programacao-infantil`, `/cinema-teatro`, `/esporte-lazer`, `/eventos-gratuitos`, `/meu-bairro`, `/fim-de-semana`, `/eventos-do-para`, `/feriados`, `/agora` e `/guia`.

Nenhuma API externa ou fonte real foi ativada nesta entrega. Veja [pendências e evidências](docs/SPRINT-03-HOMOLOGACAO.md), [arquitetura](docs/ARQUITETURA.md) e [publicação](docs/DEPLOY.md).
