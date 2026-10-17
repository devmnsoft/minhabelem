# Publicação MinhaBelém

O procedimento atual está em [OPERACAO.md](OPERACAO.md). Identity com TOTP, permissões e sessões substituiu a chave editorial compartilhada; `Editorial:AccessToken` não participa mais do acesso.

No IIS, instale o Hosting Bundle .NET 8, configure site HTTPS e App Pool dedicado e gere o artefato com `scripts/publish-windows.ps1`. O script compila/testa a Solution e publica a Web; não instala o artefato em um site nem altera banco. O servidor precisa manter o processo ativo para o worker; valide inicialização e reciclagem em homologação.

No Linux, o Dockerfile publica a Web com Core/Infrastructure e SQL embarcado. Compose inicializa o SQL somente em volume MySQL novo e mantém a porta Web em loopback. Banco existente exige atualização explícita. Configure TLS, proxy confiável, segredos e chaves Data Protection persistentes e criptografadas antes de usar contas reais.

Não habilite coleta sem fontes autorizadas. Uma chave configurada não comprova integração ativa. Consulte [evidências e impedimentos](SPRINT-03-HOMOLOGACAO.md). Não houve implantação, alteração de DNS ou execução no MySQL de produção nesta entrega.
