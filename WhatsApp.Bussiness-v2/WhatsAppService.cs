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

    public async Task SendTemplateWithVariablesAsync(
        string to,
        string templateName,
        string languageCode,
        List<string> variables)
    {
        var components = new List<object>();

        // 1. HEADER (Onde está o {{nome}})
        // Importante: Usamos 'parameter_name' para templates com variáveis nomeadas
        components.Add(new
        {
            type = "header",
            parameters = new[]
            {
            new {
                type = "text",
                parameter_name = "nome", // Nome exato que está no painel da Meta
                text = variables[0]
            }
        }
        });

        // 2. BUTTON (O link do iFood que a Meta exigiu parâmetro)
        // Se o botão não tiver nome de variável, ele usa o padrão
        components.Add(new
        {
            type = "button",
            sub_type = "url",
            index = "0",
            parameters = new[]
            {
            new {
                type = "text",
                text = "promocao_sexta" // O que completa a URL do iFood
            }
        }
        });

        var payload = new
        {
            messaging_product = "whatsapp",
            to = to,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components = components
            }
        };

        var response = await _http.PostAsJsonAsync($"{_apiUrl}/{_phoneNumberId}/messages", payload);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"❌ Erro: {content}");
            return;
        }
        Console.WriteLine("✅ MENSAGEM ENVIADA COM SUCESSO!");
    }

}
