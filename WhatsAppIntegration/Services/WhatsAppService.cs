using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

public class WhatsAppService
{
    private readonly HttpClient _http;
    private readonly string _phoneNumberId;
    private readonly string _accessToken;
    private readonly string _apiUrl;

    public WhatsAppService(HttpClient http, string phoneNumberId, string accessToken, string apiUrl)
    {
        _http = http;
        _phoneNumberId = phoneNumberId;
        _accessToken = accessToken;
        _apiUrl = apiUrl;
    }

    public async Task SendMessageAsync(string to, string message)
    {
        var url = $"{_apiUrl}/{_phoneNumberId}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            to = to,
            type = "text",
            text = new { body = message }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Authorization", $"Bearer {_accessToken}");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}