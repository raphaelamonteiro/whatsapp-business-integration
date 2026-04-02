public class WhatsAppHttpClient : IWhatsAppSender
{
    private readonly HttpClient _http;
    private readonly WhatsAppOptions _options;

    public WhatsAppHttpClient(HttpClient http, IOptions<WhatsAppOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);
    }

    public async Task<string> SendMarketingTemplateAsync(SendTemplateRequest request, CancellationToken ct = default)
    {
        var url = $"{_options.ApiUrl}/{_options.PhoneNumberId}/messages";

        // Monta os parâmetros dinâmicos do template
        // Ex: "E aí, {{1}}, pizza hoje?" → variável = nome do cliente
        var body = new
        {
            messaging_product = "whatsapp",
            to = request.RecipientPhone,
            type = "template",
            template = new
            {
                name = request.TemplateName,     // ex: "friday_pizza_promo"
                language = new { code = "pt_BR" },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = request.Variables.Select(v => new
                        {
                            type = "text",
                            text = v
                        }).ToArray()
                    }
                }
            }
        };

        var response = await _http.PostAsJsonAsync(url, body, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new WhatsAppApiException($"Falha ao enviar template: {content}");

        // A Meta retorna o message_id que usamos para rastrear status
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement
                  .GetProperty("messages")[0]
                  .GetProperty("id")
                  .GetString()!;
    }
}