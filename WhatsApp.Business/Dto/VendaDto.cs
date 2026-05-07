public class VendaProdutoServicoDto
{
    public Guid? VendaUid { get; set; } = null;
    public Guid? ProdutoUid { get; set; }
    public required decimal Quantidade { get; set; }
    public required decimal ValorUnitario { get; set; }
    public required decimal ValorTotal { get; set; }
    public String? Observacao { get; set; } = "";
    public bool ImprimirCozinha { get; set; } = true;
    public Guid? Uid { get; set; } = null;
    public Guid? EmpresaUid { get; set; } = null;
    public string DataHoraInclusao { get; set; } = "";
    public string DataHoraAlteracao { get; set; } = "";
    public int StatusRegistroEnum { get; set; } = 1;
}

public class VendaDto
{
    public Guid CaixaUid { get; set; } = Guid.Parse("019df3f3-8645-7ed0-9ba0-f2814f96318c");
    public Guid? ClienteFornecedorUid { get; set; } = null;
    public Guid? MesaUid { get; set; } = null;
    public Guid? ComandaUid { get; set; } = null;
    public int StatusVenda { get; set; } = 1; // 1 = ABERTA
    public string DataHoraAbertura { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
    public string? DataHoraFinalizacao { get; set; } = null;
    public required decimal TotalServico { get; set; }
    public required decimal Total { get; set; }
    public required decimal TotalAPagar { get; set; }
    public List<VendaProdutoServicoDto> ListVendaProdutoServico { get; set; } = new();
    public Guid? Uid { get; set; } = null;
    public Guid? EmpresaUid { get; set; } = null;
    public string DataHoraInclusao { get; set; } = "";
    public string DataHoraAlteracao { get; set; } = "";
    public int StatusRegistroEnum { get; set; } = 1;
}