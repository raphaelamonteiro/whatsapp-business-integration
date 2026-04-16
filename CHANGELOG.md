# Registro de Alterações (Changelog)

Todas as mudanças importantes deste projeto serão documentadas neste arquivo.

## [2.0.0] - 08/04/2026

### Adicionado
- WebHook com Cloudflare Tunnel
- Resposta a mensagem do usuário (responde "oi" com "tudo bem?")

### Alterado
- Sem uso de template

### Mudanças que quebram compatibilidade
- Adição de Endpoints e de token do tunnel



## [1.2.0] - 15/04/2026

### Adicionado
- Envio de mensagens utilizando o número próprio (em vez do número de teste).
- Implementação de templates de mensagem personalizados.
- Suporte a variável `nome` nos templates.

### Alterado
- Ajustes no template de mensagens para melhoria de formatação e uso de variáveis.

### Mudanças que quebram compatibilidade
- O serviço de envio (`WhatsAppService.cs`) agora exige o parâmetro `nome` no `header` da requisição.

### Removido
- Remoção de comentários desnecessários no código.

---

## [1.0.0] - 08/04/2026

### Adicionado
- Documentação inicial do repositório.
- Criação do aplicativo na plataforma da Meta.
- Envio da primeira mensagem utilizando número de teste.