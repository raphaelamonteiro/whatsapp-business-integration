namespace chat_with_api.State;

public class PedidoState
{
    public string? Telefone { get; set; }
    public string? ClienteNome { get; set; }
    public string? Endereco { get; set; }
    public string? FormaPagamento { get; set; }

    public List<ItemPedido> Itens { get; set; } = new();

    public EtapaPedido EtapaAtual { get; set; } = EtapaPedido.Inicio;

    public bool PedidoFinalizado { get; set; } = false;
}

public enum EtapaPedido
{
    Inicio,
    AguardandoTelefone,
    EscolhendoItens,
    AguardandoEndereco,
    AguardandoPagamento,
    ConfirmacaoFinal,
    Finalizado
}

public class ItemPedido
{
    public string Nome { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal Preco { get; set; }
}