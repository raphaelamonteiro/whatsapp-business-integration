# WhatsApp.Business — POC com WhatsApp Cloud API

Proof of concept para testes de envio de mensagens, notificações e automações utilizando a **WhatsApp Cloud API (Meta)**.

Este repositório foi criado para validar cenários de comunicação automatizada (ex: campanhas, notificações e fluxos baseados em eventos), servindo como base experimental para futuras integrações em sistemas maiores.

---

## 🚀 O que este projeto demonstra

* Envio de mensagens via **WhatsApp Cloud API**
* Uso de **templates aprovados pela Meta**
* Suporte a templates:

  * Sem variáveis
  * Com variáveis (ex: `"E aí, {{1}}, pizza hoje?"`)
* Simulação de **disparo em lote com controle de taxa (rate limit)**

> ⚠️ Este projeto **não é um sistema completo de marketing**, mas sim um ambiente de testes e validação de integrações.

---

## 🧩 Possíveis aplicações

* Notificações automatizadas (ex: status de pedido)
* Campanhas simples de marketing
* Testes de integração com sistemas de CRM ou ERP
* Base para sistemas de retenção de clientes

---

## 📦 Pré-requisitos

* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* Conta em [Meta for Developers](https://developers.facebook.com)
* Aplicação com WhatsApp configurado
* Template aprovado pela Meta

---

## ⚙️ Configuração

### 1. Clone o repositório

```bash
git clone https://github.com/raphaelamonteiro/whatsapp-business-integration.git
cd whatsapp-business-integration/WhatsApp.Business
```

### 2. Configure o `appsettings.json`

Crie o arquivo na raiz do projeto:

```json
{
  "WhatsApp": {
    "PhoneNumberId": "SEU_PHONE_NUMBER_ID",
    "AccessToken": "SEU_ACCESS_TOKEN",
    "ApiUrl": "https://graph.facebook.com/v17.0",
    "Recipient": "+5511999999999"
  }
}
```

> ⚠️ O arquivo não está versionado por conter credenciais.

---

## ▶️ Como executar

```bash
dotnet run
```

Saída esperada:

```
✅ Mensagem enviada! ID: wamid.XXXXXXXXXXXX
```

---

## 🏗️ Estrutura

```
WhatsApp.Business/
├── WhatsAppService.cs
├── Program.cs
├── appsettings.json (não versionado)
└── WhatsApp.Business.csproj
```

---

## 💬 Exemplo — template com variável

```csharp
await service.SendTemplateWithVariablesAsync(
    to: "+5511999999999",
    templateName: "friday_pizza_promo",
    languageCode: "pt_BR",
    variables: ["João"]
);
```

---

## 📣 Exemplo — envio em lote

```csharp
foreach (var cliente in clientes)
{
    await service.SendTemplateWithVariablesAsync(
        to: cliente.telefone,
        templateName: "friday_pizza_promo",
        languageCode: "pt_BR",
        variables: [cliente.nome]
    );

    await Task.Delay(500);
}
```

---

## ⚠️ Limitações deste POC

* Não possui persistência de dados
* Não há controle de campanhas
* Não implementa webhook de status
* Não gerencia custos ou billing
* Uso de token pode ser temporário (ambiente de teste)

---

## 🔮 Próximos experimentos

* [ ] Webhook para status de mensagens
* [ ] Persistência em banco
* [ ] Simulação de campanhas
* [ ] Rate limiting mais robusto
* [ ] Agendamento de envios

---

## 🛠️ Tecnologias

* C# / .NET 10
* WhatsApp Cloud API (Meta)
* Microsoft.Extensions.Configuration

---

## 📌 Nota

Este projeto é apenas para fins de experimentação e validação técnica da API do WhatsApp.
