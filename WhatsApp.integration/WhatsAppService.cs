using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public class WhatsAppService
{
    private readonly HttpClient _http;
    private readonly string _phoneNumberId;
    private readonly string _apiUrl;

    public WhatsAppService(HttpClient http, string phoneNumberId,
                           string accessToken, string apiUrl)
    {
        _http = http;
        _phoneNumberId = phoneNumberId;
        _apiUrl = apiUrl;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<string> SendTemplateAsync(
        string to,
        string templateName,
        string languageCode,
        List<string> variables)
    {
        var parameters = variables.Select(v => new
        {
            type = "text",
            text = v
        }).ToArray();

        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components = new[]
                {
                    new { type = "body", parameters }
                }
            }
        };

        var response = await _http.PostAsJsonAsync(
            $"{_apiUrl}/{_phoneNumberId}/messages", payload);

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Erro da Meta: {content}");
            response.EnsureSuccessStatusCode();
        }

        using var doc = JsonDocument.Parse(content);
        var messageId = doc.RootElement
            .GetProperty("messages")[0]
            .GetProperty("id")
            .GetString();

        Console.WriteLine($"Mensagem enviada! ID: {messageId}");
        return messageId!;
    }
}