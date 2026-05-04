# 🧪 WhatsApp Cloud API POC

<div align="center">
  <img src="https://github.com/raphaelamonteiro/whatsapp-business-integration/blob/main/assets/Pixel%20Coding%20Sticker.gif" width="240" />
  <br/>
  <strong>Automação de mensagens, templates e simulação de atendimento via WhatsApp</strong>
</div>

---

## 📖 Sobre o projeto

Este projeto é uma **Proof of Concept (POC)** focada na integração com a **WhatsApp Cloud API (Meta)** para envio de mensagens automatizadas, simulação de fluxos conversacionais e testes de notificações em tempo real.

A proposta é servir como base para sistemas maiores, como:

* Plataformas de atendimento automatizado
* Sistemas de notificação transacional
* Campanhas de engajamento via WhatsApp

---

## ✨ Destaques

* 📩 Envio de mensagens via API oficial da Meta
* 🧾 Uso de templates aprovados (com e sem variáveis)
* 🤖 Simulação de atendimento com IA (fluxo de delivery)
* 🔔 Estrutura preparada para Webhooks

---

## 🧠 Arquitetura (visão simplificada)

```
Cliente / Sistema
        │
        ▼
  WhatsAppService
        │
        ├──► WhatsApp Cloud API (Meta)
        │
        ├──► Webhook (eventos: enviado, entregue, lido)
        │
        └──► IA (Ollama)
                │
                ▼
        Simulação de atendimento
```

---

## 🔄 Fluxo de comunicação

1. O sistema dispara mensagens via `WhatsAppService`
2. A **WhatsApp Cloud API** processa o envio
3. Eventos (enviado, entregue, lido) retornam via Webhook
4. A IA interpreta mensagens e gera respostas
5. O sistema continua o fluxo conversacional

---

## 🧩 Casos de uso

* 📦 Notificação de status de pedidos
* 🍕 Simulação de pedidos (delivery)
* 🔁 Reengajamento de clientes
* 📣 Disparo controlado de campanhas
* 💬 Protótipo de chatbot com IA

---

## 📦 Pré-requisitos

* .NET 10 SDK
* Conta no Meta for Developers
* WhatsApp Cloud API configurada
* Template aprovado pela Meta
* Cloudflare Tunnel (para exposição de Webhook)
* Ollama configurado (local ou remoto)

---

## ⚙️ Configuração

Crie o arquivo `appsettings.json`:

```json
{
  "WhatsApp": {
    "PhoneNumberId": "ID-TELEFONE",
    "AccessToken": "TOKEN-META",
    "ApiUrl": "https://graph.facebook.com/v17.0",
    "Recipient": "5511999999999",
    "API_TOKEN": "TOKEN-API",
    "VerifyToken": "TOKEN-WEBHOOK"
  },
  "Ollama": {
    "ApiKey": "CHAVE-OLLAMA",
    "Endpoint": "https://ollama.com/v1",
    "ModelId": "ministral-3:14b"
  },
  "DeliveryApi": {
    "BaseUrl": "URL-DA-API"
  }
}
```

> 💡 Consulte `appsettings.example.json` como referência

Você pode utilizar outros modelos de IA, como:

* Qwen (recomendado)
* Llama

---

## 🏗️ Estrutura do projeto

```
WhatsApp.Business/
├── Program.cs
├── appsettings.json                # Configurações e credenciais
├── Dto/
│   ├── ConsultaDto.cs             # Consulta produtos disponíveis
│   └── ProdutoDto.cs              # Nome e preço do produto
├── Plugins/
│   └── DeliveryPlugin.cs          # Plugin (Semantic Kernel) integrando IA + API
├── Services/
│   ├── DeliveryApiService.cs      # Integração com API de delivery
│   └── WhatsAppService.cs         # Integração com WhatsApp Cloud API
├── State/
│   └── PedidoState.cs             # Estado do pedido/conversa
└── WhatsApp.Business.csproj
```

---

## ⚠️ Limitações

* Não possui persistência de dados
* Não há gerenciamento de campanhas
* Não há controle de billing/custos
* Tokens podem expirar (ambiente de testes)

---

## 🔮 Próximos passos

* [x] Webhook para status de mensagens
* [x] Integração com IA
* [ ] Persistência (PostgreSQL ou similar)
* [ ] Orquestração de campanhas
* [ ] Agendamento de mensagens
* [ ] Dashboard de monitoramento

---

## 🛠️ Stack

* C# / .NET 10
* WhatsApp Cloud API (Meta)
* Microsoft.Extensions.Configuration
* Ollama (LLM local ou remoto)

---

## 📌 Observação

Este projeto tem fins **experimentais**, sendo ideal para validar integrações antes de evoluir para uma solução em produção.
