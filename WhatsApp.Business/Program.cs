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
using System.Text.RegularExpressions; // Adicionado para o Regex funcionar

var builder = WebApplication.CreateBuilder(args);

// 1. CARREGA AS CONFIGURAÇÕES
var config = builder.Configuration.AddJsonFile("appsettings.json").Build();

// 2. REGISTRO DE INFRAESTRUTURA
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PedidoState>();

// 3. SERVIÇOS DE NEGÓCIO
builder.Services.AddSingleton<DeliveryApiService>(sp =>
    new DeliveryApiService(config["WhatsApp:API_TOKEN"] ?? ""));

builder.Services.AddSingleton<WhatsAppService>(sp =>
    new WhatsAppService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
        config["WhatsApp:PhoneNumberId"]!,
        config["WhatsApp:AccessToken"]!,
        config["WhatsApp:ApiUrl"] ?? "https://graph.facebook.com/v17.0"
    ));

// 4. CONFIGURAÇÃO DO SEMANTIC KERNEL
builder.Services.AddScoped<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    // Lendo as configurações do JSON
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

// 5. HISTÓRICO
builder.Services.AddSingleton<ChatHistory>(sp =>
{
    var history = new ChatHistory();
    history.AddSystemMessage("Você é o TechBot, atendente de delivery da pizzaria.");
    return history;
});

var app = builder.Build();

// --- ENDPOINTS (Webhook Meta) ---

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

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await whatsapp.SendTypingAsync(from);
                        history.AddUserMessage(userMessage);

                        var chatService = k.GetRequiredService<IChatCompletionService>();
                        var settings = new OpenAIPromptExecutionSettings
                        {
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                        };

                        var result = await chatService.GetChatMessageContentAsync(history, settings, k);

                        // TRATAMENTO DA RESPOSTA (Limpando o Thinking)
                        string respostaBruta = result.Content ?? "";

                        // Remove as tags <think>...</think> para o cliente não ver a IA "pensando"
                        string respostaParaEnviar = Regex.Replace(respostaBruta, @"<think>.*?</think>", "", RegexOptions.Singleline).Trim();

                        // Se a IA chamou função e não deu resposta de texto, tentamos pegar o resultado final
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