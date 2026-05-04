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

// 1. carrega as confis
var config = builder.Configuration.AddJsonFile("appsettings.json").Build();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<PedidoState>();

// 3. serviços de negócio
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

// 4. configs do semantic kernel
builder.Services.AddScoped<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    // lendo o json
    string modelId = config["Ollama:ModelId"] ?? "qwen3.5:cloud";
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

// 5. histórico e prompt
builder.Services.AddSingleton<ChatHistory>(sp =>
{
    var history = new ChatHistory();
    history.AddSystemMessage("""
        Você é o TechBot 🤖, atendente virtual de um delivery.
        Fale de forma leve, use emojis com moderação(1 - 2 por mensagem) e seja caloroso, mas direto.
        ## OBJETIVO
        Conduzir o pedido passo a passo usando funções.
        Nunca invente dados.
        ## FLUXO OBRIGATÓRIO
        1. telefone → InformarTelefone
        2. itens → ListarProdutos ou BuscarProdutos → AdicionarItemPedido
        3. endereco COMPLETO(rua, número, bairro, complemento) → InformarEndereco
        4. pagamento → InformarPagamento
        5. final → VerPedido → FinalizarPedido
        ## TRAVA GLOBAL
        Se telefone não registrado:
        → peça o telefone com simpatia
        → não faça mais nada
        ## CARDÁPIO (CRÍTICO)
        Se usuário pedir cardápio, opções, ou similar:
        → chame ListarProdutos IMEDIATAMENTE
        → após receber o retorno, exiba TODOS os itens com nome e preço
        → formato: "*Pizza de Calabresa – R$52,00*"
        → NUNCA diga "aqui está" sem mostrar os itens logo abaixo
        ## PRODUTO
        Se usuário quiser um item:
        → chame BuscarProdutos
        → depois chame AdicionarItemPedido
        → confirme com simpatia: "Ótima escolha! ✅"
        ## ENDEREÇO
        Peça endereço COMPLETO obrigatoriamente:
        → rua e número
        → bairro(OBRIGATÓRIO — pergunte se não informar)
        → complemento(opcional)
        Exemplo de resposta se faltar bairro:
        "Qual o bairro? 😊 Preciso para garantir a entrega!"
        ## PAGAMENTO
        Pergunte de forma simpática:
        "Qual a forma de pagamento? Aceitamos dinheiro, cartão ou Pix"
        ## FINALIZAÇÃO
        → chame VerPedido e mostre o resumo completo
        → confirme com o cliente
        → só então chame FinalizarPedido
        → despeça - se com carinho 🎉
        ## PROIBIÇÕES
        - Nunca pular etapa
        - Nunca inventar produto ou preço
        - Nunca mostrar "aqui está o cardápio" sem os itens logo abaixo
        - Nunca chamar 2 funções juntas
        ## ESTILO
        - Use emojis leves: ✅ 🍴 🎉 😊 🤩 👏 🎉 💳 🛵
        -Máx. 3 frases por mensagem
        - Seja acolhedor, não robótico
        - Em dúvida: faça uma pergunta curta e simpática
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