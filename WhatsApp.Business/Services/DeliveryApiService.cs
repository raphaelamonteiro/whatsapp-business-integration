namespace chat_with_api.Services;

using RestSharp;
using System.Text.Json;
using chat_with_api.DTO;

public class DeliveryApiService
{
    private readonly RestClient _client;
    public DeliveryApiService(string token)
    {
        _client = new RestClient("http://localhost:5256");

        if (string.IsNullOrEmpty(token))
            throw new Exception("API_TOKEN não foi fornecido ao serviço.");

        // Configura o cabeçalho de autorização usando o token do JSON
        _client.AddDefaultHeader("Authorization", $"Bearer {token}");
    }
    public async Task<List<ProdutoDto>?> BuscarProdutosAsync(string? nome = null, int operador = 1)
    {
        var request = new RestRequest("/Produto/Consultar", Method.Post);

        // Montamos o corpo EXATAMENTE como a API pediu no exemplo
        var body = new
        {
            listPesquisaDefaultDto = new object[] { },
            listPesquisaDto = string.IsNullOrEmpty(nome)
                ? new object[] { }
                : new[] { new { atributoPesquisa = "Descricao", operador = operador, valorPesquisa = nome } },
            comparador = 1,
            ordenacao = 1,
            listOrdenacao = new string[] { },
            listIncludeRelations = new string[] { },
            registrosPorPagina = 10,
            numeroPagina = 1
        };

        request.AddJsonBody(body);

        var response = await _client.ExecuteAsync(request);

        if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
        {
            Console.WriteLine($"❌ Erro API Delivery: {response.StatusCode}");
            return null;
        }

        try
        {
            // Se a sua API retorna a lista direto: [{},{}]
            return JsonSerializer.Deserialize<List<ProdutoDto>>(response.Content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            // Se a sua API retorna um objeto que CONTÉM a lista (ex: { "data": [] })
            using var doc = JsonDocument.Parse(response.Content);
            if (doc.RootElement.TryGetProperty("data", out var dataArray)) // Ajuste "data" para o nome real se for diferente
            {
                return JsonSerializer.Deserialize<List<ProdutoDto>>(dataArray.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            return null;
        }
    }
}