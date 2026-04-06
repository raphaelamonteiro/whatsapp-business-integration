using Microsoft.Extensions.Configuration;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// rota que envia a mensagem
app.MapGet("/send", async () =>
{
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();

    var service = new WhatsAppService(
        new HttpClient(),
        config["WhatsApp:PhoneNumberId"]!,
        config["WhatsApp:AccessToken"]!,
        config["WhatsApp:ApiUrl"]!
    );
    //Alterar número de telefone
    await service.SendTemplateAsync(to: "+5512XXXXXXXXX");
    return "Mensagem enviada!";
});

app.MapGet("/webhook", (HttpRequest request) =>
{
    var mode = request.Query["hub.mode"].ToString();
    var token = request.Query["hub.verify_token"].ToString();
    var challenge = request.Query["hub.challenge"].ToString();

    if (mode == "subscribe" && token == "XXXXXX")
        return Results.Ok(challenge);

    return Results.StatusCode(403);
});

// rota que chama quando o status muda
app.MapPost("/webhook", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    using var doc = JsonDocument.Parse(body);
    var root = doc.RootElement;

    try
    {
        var statuses = root
            .GetProperty("entry")[0]
            .GetProperty("changes")[0]
            .GetProperty("value")
            .GetProperty("statuses");

        foreach (var status in statuses.EnumerateArray())
        {
            var id = status.GetProperty("id").GetString();
            var st = status.GetProperty("status").GetString();
            Console.WriteLine($"📨 Mensagem {id} → {st}");
        }
    }
    catch
    {
        Console.WriteLine($"Payload recebido: {body}");
    }

    return Results.Ok();
});

app.Run();