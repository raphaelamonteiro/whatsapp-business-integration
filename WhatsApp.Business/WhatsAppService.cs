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

    public async Task SendTextMessageAsync(string to, string message)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = to,
            type = "text",
            text = new
            {
                body = message
            }
        };

        var response = await _http.PostAsJsonAsync(
            $"{_apiUrl}/{_phoneNumberId}/messages",
            payload
        );

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"❌ Erro ao enviar mensagem: {content}");
            return;
        }

        Console.WriteLine("✅ Mensagem de texto enviada com sucesso!");
    }
}
