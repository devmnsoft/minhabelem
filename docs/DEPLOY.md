# Publicação MinhaBelém
1. Servidor Linux com Docker e Docker Compose, HTTPS e proxy reverso (Caddy/Nginx).
2. DNS: apontar A/AAAA do domínio para IP do servidor; configurar TLS válido.
3. No servidor, definir variável MYSQL_PASSWORD e MYSQL_ROOT_PASSWORD longa e exclusiva, ConnectionStrings__Default, Editorial__AccessToken longa e secreta, GooglePlaces__ApiKey.
4. Restringir chave Google Places por API habilitada, cotas e projeto Google Cloud. A chave deve ficar **somente no servidor**, nunca no JavaScript, HTML ou git.
5. `docker compose up -d --build`. O bind web no host é 127.0.0.1:8080; configurar proxy HTTPS.
6. O arquivo único `MINHABELEM.SQL` inicializa o volume MySQL novo. Em uma base já criada, selecione-a no SGBD e execute o arquivo inteiro. Não altera colunas existentes nem migra dados entre tabelas antigas e mb_.
7. Não habilitar Collector__Enabled sem fontes cadastradas/aprovadas, allowlist de hosts, proteção SSRF e egress restrito.
8. Em produção substituir a autenticação provisória editorial por Identity com MFA, logs e segregação de funções; configurar ASP.NET Data Protection com chaves persistidas e criptografadas.
9. Monitorar /health, backups restauráveis, quotas Google Places, eventuais cobranças e falhas do feed.
10. Nunca hospedar com credenciais de demonstração; exigir validação de segurança, testes de integração, revisão LGPD e políticas de retenção.
