using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// configuração e Injeção do Serviço
var config = builder.Configuration.AddJsonFile("appsettings.json").Build();

// registra o WhatsAppService pra ser usado nos endpoints
builder.Services.AddHttpClient();
builder.Services.AddSingleton<WhatsAppService>(sp =>
    new WhatsAppService(
        sp.GetRequiredService<HttpClient>(),
        config["WhatsApp:PhoneNumberId"]!,
        config["WhatsApp:AccessToken"]!,
        config["WhatsApp:ApiUrl"]!
    ));

var app = builder.Build();

// endpoints
// GET - Meta valida o túnel (Aperto de mão)
app.MapGet("/webhook", (HttpContext context) =>
{
    var query = context.Request.Query;

    string verifyToken = config["WhatsApp:VerifyToken"] ?? "";

    if (query["hub.mode"] == "subscribe" && query["hub.verify_token"] == verifyToken)
    {
        var challenge = query["hub.challenge"].ToString();
        Console.WriteLine($"✅ Validando Webhook. Challenge: {challenge}");
        return Results.Text(challenge);
    }

    return Results.BadRequest("Token inválido");
});

// POST - Para receber o "oi" e responder "tudo bem?"
app.MapPost("/webhook", async (HttpContext context, WhatsAppService service) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    using var json = JsonDocument.Parse(body);
    var entry = json.RootElement.GetProperty("entry")[0];
    var changes = entry.GetProperty("changes")[0];
    var value = changes.GetProperty("value");

    // só processa se existir a propriedade "messages"
    if (value.TryGetProperty("messages", out var messages))
    {
        var textElement = messages[0].GetProperty("text");
        var textBody = textElement.GetProperty("body").GetString();
        var from = messages[0].GetProperty("from").GetString();

        Console.WriteLine($"📩 Mensagem real de {from}: {textBody}");

        // Só responde se o cara escreveu algo (e não se for eco)
        if (!string.IsNullOrEmpty(from))
        {
            await service.SendTextMessageAsync(from, "Oi, tudo bem?");
        }
    }

    return Results.Ok();
});

Console.WriteLine(" TechChef Online! Ouvindo na porta 5000...");
app.Run("http://0.0.0.0:5000"); // força a porta que o Cloudflare está usando