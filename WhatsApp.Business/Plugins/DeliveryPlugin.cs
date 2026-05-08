using Microsoft.SemanticKernel;
using chat_with_api.State;
using chat_with_api.Services;
using System.ComponentModel;
using System.Text;

namespace chat_with_api.Plugins;

public class DeliveryPlugin
{
    private readonly DeliveryApiService _service;
    private readonly PedidoState _state;
    public DeliveryPlugin(DeliveryApiService service, PedidoState state)
    {
        _service = service;
        _state = state;
    }

    public int DebugStateId() => _state.GetHashCode();
    [KernelFunction, Description("Registra o telefone do cliente para iniciar o atendimento.")]
    public string InformarTelefone(
        [Description("Número de telefone do cliente")] string telefone)
    {
        if (!string.IsNullOrEmpty(_state.Telefone))
            return $"Telefone já registrado como {_state.Telefone}.";

        _state.Telefone = telefone;
        _state.EtapaAtual = EtapaPedido.EscolhendoItens;
        return "Telefone registrado com sucesso.";
    }

    [KernelFunction, Description("Lista todos os produtos disponíveis no cardápio.")]
    public async Task<string> ListarProdutos()
    {
        if (string.IsNullOrEmpty(_state.Telefone))
            return "ERRO: telefone não registrado. Não mostre cardápio.";

        var produtos = await _service.BuscarProdutosAsync();

        if (produtos == null || produtos.Count == 0)
            return "Nenhum produto disponível no momento.";

        var sb = new StringBuilder();
        foreach (var p in produtos.Take(15))
            sb.AppendLine($"{p.Descricao}|R${p.Preco:F2}");

        return sb.ToString();
    }

    [KernelFunction, Description("Busca produtos no cardápio pelo nome.")]
    public async Task<string> BuscarProdutos(
        [Description("Nome ou parte do nome do produto")] string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return "Informe o nome do produto.";

        var produtos = await _service.BuscarProdutosAsync(nome);

        if (produtos == null || produtos.Count == 0)
            return $"'{nome}' não encontrado.";

        var sb = new StringBuilder();
        // Máximo 10 resultados
        foreach (var p in produtos.Take(10))
            sb.AppendLine($"{p.Descricao}|R${p.Preco:F2}");

        return sb.ToString();
    }
    [KernelFunction, Description("Adiciona um item ao pedido. Use o nome exato retornado pelo cardápio.")]
    public async Task<string> AdicionarItemPedido(
        [Description("Nome EXATO do produto conforme listado no cardápio")] string nome,
        [Description("Quantidade")] int quantidade,
        [Description("Observação do cliente sobre o item, ex: sem cebola, queijo extra. Se não houver, deixe vazio.")] string? observacao = null)
    {

        if (string.IsNullOrEmpty(_state.Telefone))
        {
            _state.EtapaAtual = EtapaPedido.AguardandoTelefone;
            return "Preciso do telefone antes.";
        }

        var produtos = await _service.BuscarProdutosAsync(nome);

        var produtosFiltrados = produtos?
                .Where(p => ScoreSimilaridade(p.Descricao, nome) > 0)
                .OrderByDescending(p => ScoreSimilaridade(p.Descricao, nome))
                .ToList();

        if (produtosFiltrados == null || produtosFiltrados.Count == 0)
            return $"'{nome}' não encontrado no cardápio.";

        var produto = produtosFiltrados.First();
        var existente = _state.Itens.FirstOrDefault(i => i.ProdutoUid == produto.Uid);

        if (existente != null)
        {
            existente.Quantidade += quantidade;
            if (!string.IsNullOrEmpty(observacao)) existente.Observacao = observacao;
        }
        else
        {
            _state.Itens.Add(new ItemPedido
            {
                ProdutoUid = produto.Uid,
                Nome = produto.Descricao,
                Quantidade = quantidade,
                Preco = produto.Preco,
                Observacao = observacao ?? ""
            });
        }

        // DEBUG
        Console.WriteLine($"✅ Produto escolhido: {produto.Uid} | {produto.Descricao} | R${produto.Preco}");
        Console.WriteLine($"   Score: {ScoreSimilaridade(produto.Descricao, nome)}");

        _state.EtapaAtual = EtapaPedido.AguardandoEndereco;
        return $"OK: {quantidade}x {produto.Descricao} (R${produto.Preco:F2}) adicionado.";
    }

    [KernelFunction, Description("Adiciona ou atualiza a observação de um item já adicionado ao pedido.")]
    public string AdicionarObservacao(
    [Description("Nome do produto para adicionar observação")] string nomeProduto,
    [Description("Texto da observação, ex: sem cebola, bem passado")] string observacao)
    {
        var item = _state.Itens.FirstOrDefault(i =>
            i.Nome.ToLower().Contains(nomeProduto.ToLower()));

        if (item == null)
            return $"Produto '{nomeProduto}' não encontrado no pedido.";

        item.Observacao = observacao;
        return $"Observação '{observacao}' adicionada para {item.Nome}.";
    }

    // similaridade: conta quantas palavras do nome buscado aparecem no produto
    private static int ScoreSimilaridade(string descricao, string nomeBuscado)
    {
        var descLower = descricao.ToLower();
        var palavras = nomeBuscado.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return palavras.Count(p => descLower.Contains(p));
    }

    [KernelFunction, Description("Retorna o resumo atual do pedido com itens e total.")]
    public string VerPedido()
    {
        if (!_state.Itens.Any())
            return "Seu pedido está vazio.";

        var sb = new StringBuilder("Seu pedido:\n");
        decimal total = 0;

        foreach (var item in _state.Itens)
        {
            var subtotal = item.Preco * item.Quantidade;
            total += subtotal;
            sb.AppendLine($"- {item.Quantidade}x {item.Nome}: R$ {subtotal:F2}");
        }

        sb.AppendLine($"\nTotal: R$ {total:F2}");
        return sb.ToString();
    }

    [KernelFunction, Description("Registra o endereço de entrega do cliente.")]
    public string InformarEndereco(
        [Description("Endereço completo de entrega")] string endereco)
    {
        if (!_state.Itens.Any())
            return "Adicione itens ao pedido antes de informar o endereço.";

        _state.Endereco = endereco;
        _state.EtapaAtual = EtapaPedido.AguardandoPagamento;
        return "Endereço registrado.";
    }

    [KernelFunction, Description("Registra a forma de pagamento escolhida pelo cliente.")]
    public string InformarPagamento(
        [Description("Forma de pagamento: dinheiro, cartão, pix, etc.")] string formaPagamento)
    {
        if (string.IsNullOrEmpty(_state.Endereco))
            return "Preciso do endereço antes de registrar o pagamento.";

        _state.FormaPagamento = formaPagamento;
        _state.EtapaAtual = EtapaPedido.ConfirmacaoFinal;
        return "Pagamento registrado.";
    }


    [KernelFunction, Description("Finaliza e confirma o pedido do cliente.")]
    public async Task<string> FinalizarPedido()
    {
        if (!_state.Itens.Any()) return "Seu carrinho está vazio!";
        if (string.IsNullOrEmpty(_state.Endereco)) return "Por favor, me informe o endereço antes.";

        var sucesso = await _service.CriarVendaAsync(_state);

        if (sucesso)
        {
            _state.Itens.Clear();
            _state.Endereco = "";
            _state.FormaPagamento = "";
            _state.Telefone = "";
            _state.EtapaAtual = EtapaPedido.Finalizado;

            return "Pedido finalizado com sucesso! Ele já apareceu no nosso sistema e está indo para a cozinha.";
        }

        return "Ops! Tive um problema técnico ao enviar seu pedido para o sistema. Pode tentar confirmar novamente?";
    }

    [KernelFunction, Description("Limpa todos os itens do pedido atual.")]
    public string LimparPedido()
    {
        _state.Itens.Clear();
        _state.EtapaAtual = EtapaPedido.EscolhendoItens;
        return "Pedido limpo. O que você gostaria de pedir?";
    }
}