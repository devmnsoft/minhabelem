# MinhaBelém

Portal de notícias, cultura, lazer, serviços e mobilidade de Belém do Pará.

Stack: ASP.NET Core MVC/Razor, MySQL 8.0+, JavaScript. Atualização por fontes autorizadas, moderação editorial e adaptadores para Groq, Gemini e DeepSeek.

> Projeto em desenvolvimento. Sem credenciais ou dados fictícios apresentados como tempo real.

## Banco de dados

O único script SQL é [MINHABELEM.SQL](MINHABELEM.SQL), na raiz.
No SGBD, selecione a base já criada e execute o arquivo inteiro.
Ele inclui as tabelas do portal atual, as tabelas da nova infraestrutura
e os papéis/permissões iniciais, sem usuários ou senhas padrão.
Pode ser reexecutado sem apagar dados; não altera colunas de tabelas já existentes
nem transfere dados das tabelas antigas para as novas.

O SQL é embarcado diretamente desse mesmo arquivo no projeto Infrastructure.
A aplicação Web atual não executa migrations na inicialização ou publicação.

Validação do script: MySQL 8.0.46, 37 tabelas, duas execuções completas,
sem duplicar os 4 papéis, 15 permissões e 33 vínculos, preservando dados existentes.
