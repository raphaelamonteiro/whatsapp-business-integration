using RestSharp;
using System.Text.Json;
using chat_with_api.DTO;
using chat_with_api.State;

namespace chat_with_api.Services;

public class DeliveryApiService
{
    private readonly RestClient _client;
    public DeliveryApiService(string token, string baseUrl = "http://localhost:5256")
    {
        _client = new RestClient(baseUrl);

        if (string.IsNullOrEmpty(token))
            throw new Exception("API_TOKEN não foi fornecido ao serviço.");

        _client.AddDefaultHeader("Authorization", $"Bearer {token}");
    }

    public async Task<List<ProdutoDto>?> BuscarProdutosAsync(string? nome = null, int operador = 1)
    {
        var request = new RestRequest("/Produto/Consultar", Method.Post);

        // monta o corpo como a API pediu
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
            // se a API retorna a lista direto: [{},{}]
            return JsonSerializer.Deserialize<List<ProdutoDto>>(response.Content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            using var doc = JsonDocument.Parse(response.Content);
            if (doc.RootElement.TryGetProperty("data", out var dataArray))
            {
                return JsonSerializer.Deserialize<List<ProdutoDto>>(dataArray.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            return null;
        }
    }
    public async Task<bool> CriarVendaAsync(PedidoState pedido)
    {
        var valorTotalCalculado = pedido.Itens.Sum(i => i.Preco * i.Quantidade);

        var venda = new VendaDto
        {
            Total = valorTotalCalculado,
            TotalServico = 0,
            TotalAPagar = valorTotalCalculado,
            DataHoraAbertura = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            StatusVenda = 1,
            StatusRegistroEnum = 0,
            ListVendaProdutoServico = pedido.Itens.Select(i => new VendaProdutoServicoDto
            {
                ProdutoUid = i.ProdutoUid,
                Quantidade = i.Quantidade,
                ValorUnitario = i.Preco,
                ValorTotal = i.Preco * i.Quantidade,
                Observacao = i.Observacao ?? "",
                ImprimirCozinha = true,
                StatusRegistroEnum = 0
            }).ToList()
        };

        // DEBUG
        Console.WriteLine($"Itens no pedido: {pedido.Itens.Count}");
        foreach (var item in pedido.Itens)
            Console.WriteLine($"   → {item.ProdutoUid} | {item.Nome} | {item.Quantidade}x R${item.Preco}");

        var jsonDebug = JsonSerializer.Serialize(venda, new JsonSerializerOptions { WriteIndented = true });

        Console.WriteLine($"JSON enviado:\n{jsonDebug}");

        var request = new RestRequest("/Venda/Salvar", Method.Post);
        request.AddJsonBody(venda);

        var response = await _client.ExecuteAsync(request);

        Console.WriteLine($"📬 Status: {response.StatusCode}");
        Console.WriteLine($"📬 Resposta: {response.Content}");

        if (!response.IsSuccessful)
        {
            Console.WriteLine($"❌ Erro ao criar venda: {response.StatusCode} - {response.Content}");
            return false;
        }

        Console.WriteLine("✅ Venda criada no banco com sucesso!");
        return true;
    }

}