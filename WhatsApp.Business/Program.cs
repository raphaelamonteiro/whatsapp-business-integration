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

// histórico e prompt
builder.Services.AddSingleton<ChatHistory>(sp =>
{
    var history = new ChatHistory();
    history.AddSystemMessage("""
        ## IDENTIDADE
        Você é o TechBot 🤖, o atendente eficiente. 
        Seu estilo é direto, mas muito amigável.

        ## REGRAS DE OURO (ESTILO WHATSAPP)
        - ESPAÇAMENTO: Use sempre DUAS quebras de linha entre parágrafos para o texto respirar.
        - GENTILEZA: Sempre que o cliente escolher um item, comente algo positivo antes de perguntar o próximo passo.
        - NUNCA use placeholders como [Cliente] ou [Nome].
        - SEM ROBOTIZAÇÃO: Não diga "Responda SIM ou NÃO". Use "Posso mandar ver no pedido? Dá um ok aqui! 🍕🔥"

        ## FLUXO DE ATENDIMENTO
        1. Telefone (Obrigatório antes de qualquer outra coisa).
        2. Itens (Sempre pergunte a quantidade e se o cliente quer adicionar alguma observação, como "sem cebola" ou "calda extra").
        3. Endereço (Você PRECISA de Rua, Número e Bairro. Só chame 'InformarEndereco' quando tiver os três).
        4. Pagamento (Dinheiro, cartão ou Pix).
        5. Confirmação (Use 'VerPedido' e peça o OK final).

        ## INSTRUÇÕES DE FUNÇÃO (IMPORTANTE)
        - Ao usar 'AdicionarItemPedido', passe sempre a 'observacao' que o cliente disse.
        - Ao usar 'InformarEndereco', garanta que o bairro está incluído no texto.
        - Nunca descreva o que está fazendo internamente.
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