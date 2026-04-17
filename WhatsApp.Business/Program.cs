using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.Extensions.DependencyInjection;
using ai.Plugins; // Seus plugins de cardápio
using ai.Services; // Seus serviços de entrega
using ai.State;  // Seu controle de estado
using ai.ToolCall;
using ai.Plugins;

var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURAÇÕES DE INFRA
var config = builder.Configuration.AddJsonFile("appsettings.json").Build();

// 2. CONFIGURAÇÃO DA IA (OLLAMA + SK)
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOllamaChatCompletion(
    modelId: "qwen2.5:3b",
    endpoint: new Uri("http://localhost:11434")
);

// Registrar seus plugins e estados
kernelBuilder.Services.AddSingleton<DeliveryApiService>();
kernelBuilder.Services.AddSingleton<PedidoState>();
kernelBuilder.Services.AddHttpClient();

// Criar o Kernel
var kernel = kernelBuilder.Build();
kernel.ImportPluginFromType<DeliveryPlugin>();

// 3. CONFIGURAÇÃO DO WHATSAPP
builder.Services.AddSingleton<WhatsAppService>(sp =>
    new WhatsAppService(
        sp.GetRequiredService<HttpClient>(),
        config["WhatsApp:PhoneNumberId"]!,
        config["WhatsApp:AccessToken"]!,
        config["WhatsApp:ApiUrl"]!
    ));

// Histórico de Chat (Para a IA lembrar o que o Renato disse na msg anterior)
var history = new ChatHistory();
history.AddSystemMessage("Você é o TechBot... (seu prompt completo)");

var app = builder.Build();

// 4. O WEBHOOK INTELIGENTE
app.MapPost("/webhook", async (HttpContext context, WhatsAppService whatsapp) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    using var json = JsonDocument.Parse(body);

    var value = json.RootElement.GetProperty("entry")[0].GetProperty("changes")[0].GetProperty("value");

    if (value.TryGetProperty("messages", out var messages))
    {
        var userMessage = messages[0].GetProperty("text").GetProperty("body").GetString();
        var from = messages[0].GetProperty("from").GetString();

        // Manda o input do Zap para a IA
        history.AddUserMessage(userMessage!);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var settings = new OllamaPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

        // IA processa e decide se chama função (cardápio, pedido, etc)
        var result = await chatService.GetChatMessageContentAsync(history, settings, kernel);

        // Responde o Renato no WhatsApp
        await whatsapp.SendTextMessageAsync(from!, result.Content!);
        history.AddAssistantMessage(result.Content!);
    }
    return Results.Ok();
});

app.Run("http://0.0.0.0:5000");