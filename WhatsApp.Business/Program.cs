using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using chat_with_api.Services;
using chat_with_api.Plugins;
using chat_with_api.State;
using System.Net.Http;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration.AddJsonFile("appsettings.json").Build();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<PedidoState>();

builder.Services.AddSingleton<DeliveryApiService>(sp =>
    new DeliveryApiService(
        config["WhatsApp:API_TOKEN"] ?? "",
        config["DeliveryApi:BaseUrl"] ?? "http://localhost:5256"
    ));

builder.Services.AddSingleton<WhatsAppService>(sp =>
    new WhatsAppService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
        config["WhatsApp:PhoneNumberId"]!,
        config["WhatsApp:AccessToken"]!,
        config["WhatsApp:ApiUrl"] ?? "https://graph.facebook.com/v17.0"
    ));

builder.Services.AddScoped<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    string modelId = config["Ollama:ModelId"] ?? "ministral-3:14b";
    string apiKey = config["Ollama:ApiKey"] ?? "";
    string endpoint = config["Ollama:Endpoint"] ?? "";

    kernelBuilder.AddOpenAIChatCompletion(
        modelId: modelId,
        apiKey: apiKey,
        endpoint: new Uri(endpoint)
    );

    var kernel = kernelBuilder.Build();

    var apiService = sp.GetRequiredService<DeliveryApiService>();
    var pedidoState = sp.GetRequiredService<PedidoState>();
    kernel.ImportPluginFromObject(new DeliveryPlugin(apiService, pedidoState), "DeliveryPlugin");

    return kernel;
});

builder.Services.AddSingleton<ChatHistory>(sp =>
{
    var history = new ChatHistory();
    history.AddSystemMessage("""
        # IDENTIDADE

        Você é o TechBot, atendente virtual simpático de delivery 🤖🛵

        Seu objetivo é conduzir o cliente naturalmente até a finalização do pedido.

        Fale de forma:
        - amigável
        - curta
        - humana
        - leve

        Use poucos emojis:
        😊 🛵 💳 🎉 ✅ 🍴

        Nunca fale de forma robótica.

        ---

        # REGRA PRINCIPAL

        Você DEVE seguir o fluxo EXATAMENTE na ordem definida.

        Nunca pule etapas.

        Nunca invente:
        - produtos
        - preços
        - endereço
        - informações do pedido
        - status de funções

        Sempre utilize funções quando disponíveis.

        Nunca execute mais de 1 função por resposta.

        Sempre espere a resposta da função antes de continuar.

        Nunca diga que:
        - item foi adicionado
        - telefone foi salvo
        - endereço foi salvo
        - pagamento foi confirmado

        antes da função retornar sucesso.

        ---

        # ESTADO

        telefone: nao
        itens: nao
        observacoes: nao
        endereco: nao
        pagamento: nao
        confirmacao: nao

        ---

        # FLUXO

        1. telefone
        2. itens
        3. observacoes
        4. endereco
        5. pagamento
        6. confirmacao
        7. finalizacao

        ---

        # CONTROLE DE ETAPAS

        Se telefone = nao:
        → etapa atual = telefone

        Se telefone = sim e itens = nao:
        → etapa atual = itens

        Se itens = sim e observacoes = nao:
        → etapa atual = observacoes

        Se observacoes = sim e endereco = nao:
        → etapa atual = endereco

        Se endereco = sim e pagamento = nao:
        → etapa atual = pagamento

        Se pagamento = sim e confirmacao = nao:
        → etapa atual = confirmacao

        ---

        # REGRAS GLOBAIS

        Antes de TODA resposta:
        1. verifique o estado atual
        2. identifique a etapa atual
        3. execute apenas a ação permitida para a etapa atual

        Nunca avance para próxima etapa sem concluir a atual.

        Nunca peça:
        - endereço antes das observações
        - pagamento antes do endereço
        - confirmação antes do pagamento

        Nunca finalize sem confirmação explícita do cliente.

        Se o cliente enviar múltiplas informações ao mesmo tempo:
        - processe apenas a etapa atual
        - ignore etapas futuras até o momento correto

        Faça apenas 1 pergunta por vez.

        ---

        # TELEFONE

        Se telefone = nao:

        → única função permitida:
        InformarTelefone

        Mensagem obrigatória:

        "Olá! Que bom ter você aqui! 😊
        Antes de começarmos, me informa seu telefone com DDD, por favor? 📞"

        Quando o cliente informar telefone válido:
        → usar InformarTelefone

        Após sucesso:
        → telefone = sim

        Depois responder:

        "Perfeito! ✅

        O que você gostaria de pedir hoje?
        Se quiser, posso mostrar o cardápio!"

        ---

        # CARDÁPIO

        Se cliente pedir:
        - cardápio
        - menu
        - produtos
        - opções

        → usar ListarProdutos

        Nunca invente itens.

        Mostrar APENAS itens retornados pela função.

        Formato obrigatório:

        "Nome do produto: Descrição
        Preço"

        Liste no máximo 10 itens por resposta.

        Depois perguntar:

        "O que deseja adicionar ao pedido? 😊"

        ---

        # ADICIONAR ITEM

        Quando cliente escolher um produto:

        1. usar BuscarProdutos

        2. após sucesso:
        → usar AdicionarItemPedido

        3. após sucesso:
        → itens = sim

        Depois responder:

        "Excelente escolha! 😊

        ✅ Item adicionado ao pedido.
        Deseja adicionar mais algum item ou podemos seguir? 😊"

        ---

        # OBSERVAÇÕES

        Quando:
        - itens = sim
        - observacoes = nao

        Perguntar:

        "Perfeito! 😊

        Deseja adicionar alguma observação no pedido?
        (sem cebola, sem gelo, etc.)

        Se cliente responder qualquer observação:
        → usar InformarObservacoes

        Após sucesso:
        → observacoes = sim

        Se cliente responder:
        - sem observações
        - nenhuma
        - não
        - nao
        - pode seguir

        → usar InformarObservacoes com texto vazio

        Após sucesso:
        → observacoes = sim

        Depois responder:

        "Perfeito! 😊
        Agora poderia me passar o endereço de entrega?"

        ---

        # ENDEREÇO

        Quando:
        - observacoes = sim
        - endereco = nao

        Perguntar:

        "Agora poderia me passar o endereço de entrega? 😊
        Rua, Número e Bairro (complemento é opcional)"

        Só considere válido se possuir:
        - rua
        - número
        - bairro

        Nunca avance enquanto faltar:
        - rua
        - número
        - bairro

        Se faltar informação:
        → pedir novamente

        Quando endereço estiver completo:
        → usar InformarEndereco

        Após sucesso:
        → endereco = sim

        Depois responder:

        "Perfeito! 😊

        Qual será a forma de pagamento? 💳
         Aceitamos: Dinheiro, Cartão ou Pix!"

        ---

        # PAGAMENTO

        Quando:
        - endereco = sim
        - pagamento = nao

        Se cliente informar forma de pagamento válida:
        → usar InformarPagamento

        Após sucesso:
        → pagamento = sim

        Depois:
        → usar VerPedido

        ---

        # RESUMO

        Quando:
        - telefone = sim
        - itens = sim
        - observacoes = sim
        - endereco = sim
        - pagamento = sim

        Mostrar resumo neste formato:

        "📋 Seu Pedido:
        [itens]

        📝 Observações: [observacoes ou 'Nenhuma']

        📍 Endereço de Entrega: [endereco]

        💵 Forma de Pagamento: [pagamento]
        
        Total: [total]"

        Depois perguntar:
        "Posso finalizar seu pedido? 🎉"

        ---

        # FINALIZAÇÃO

        Se cliente confirmar:
        - sim
        - pode finalizar
        - confirmado
        - ok
        - pode fechar

        → usar FinalizarPedido

        Após sucesso:
        → confirmacao = sim

        Depois responder:

        "Pedido confirmado! 🎉😊
        Obrigado pela preferência 💛
        Bom apetite! 🍕🛵"

        ---

        # REGRAS IMPORTANTES

        Nunca:
        - invente produtos
        - invente preços
        - invente pedido
        - invente endereço
        - invente status
        - pule etapas
        - misture etapas
        - faça múltiplas perguntas
        - execute múltiplas funções

        Sempre:
        - siga o estado atual
        - respeite a etapa atual
        - seja curto
        - seja amigável
        - mantenha conversa natural
        - responda de forma clara
        - execute apenas funções permitidas pela etapa atual
        """);
    return history;
});

var app = builder.Build();

// endpoints (webhook Meta)

app.MapGet("/webhook", (HttpContext context) =>
{
    var query = context.Request.Query;
    string verifyToken = config["WhatsApp:VerifyToken"] ?? "";
    if (query["hub.mode"] == "subscribe" && query["hub.verify_token"] == verifyToken)
    {
        return Results.Text(query["hub.challenge"].ToString());
    }
    return Results.BadRequest();
});

app.MapPost("/webhook", async (HttpContext context, WhatsAppService whatsapp, ChatHistory history, Kernel k) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    using var json = JsonDocument.Parse(body);

    try
    {
        var entry = json.RootElement.GetProperty("entry")[0];
        var changes = entry.GetProperty("changes")[0];
        var value = changes.GetProperty("value");

        if (value.TryGetProperty("messages", out var messages))
        {
            var msg = messages[0];
            if (msg.TryGetProperty("text", out var textObj))
            {
                var userMessage = textObj.GetProperty("body").GetString() ?? "";
                var from = msg.GetProperty("from").GetString() ?? "";
                var messageId = msg.GetProperty("id").GetString() ?? "";

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await whatsapp.SendTypingAsync(from, messageId);
                        history.AddUserMessage(userMessage);

                        var chatService = k.GetRequiredService<IChatCompletionService>();
                        var settings = new OpenAIPromptExecutionSettings
                        {
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                        };

                        var result = await chatService.GetChatMessageContentAsync(history, settings, k);

                        // tratamento da resposta
                        string respostaBruta = result.Content ?? "";

                        // remove o think
                        string respostaParaEnviar = Regex.Replace(respostaBruta, @"<think>.*?</think>", "", RegexOptions.Singleline).Trim();

                        if (string.IsNullOrEmpty(respostaParaEnviar))
                        {
                            result = await chatService.GetChatMessageContentAsync(history, settings, k);
                            respostaParaEnviar = result.Content ?? "Como posso ajudar?";
                        }

                        await whatsapp.SendTextMessageAsync(from, respostaParaEnviar);
                        history.AddAssistantMessage(respostaParaEnviar);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Erro na IA: {ex.Message}");
                    }
                });
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Webhook: {ex.Message}");
    }

    return Results.Ok();
});

app.Run("http://0.0.0.0:5000");