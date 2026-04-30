namespace chat_with_api.DTO;

public class ConsultaDto
{
    public List<PesquisaDto>? ListPesquisaDto { get; set; }
    public List<string>? ListOrdenacao { get; set; }
    public int RegistrosPorPagina { get; set; } = 10;
    public int NumeroPagina { get; set; } = 1;
}

public class PesquisaDto
{
    public string AtributoPesquisa { get; set; } = string.Empty;
    public int Operador { get; set; }
    public string ValorPesquisa { get; set; } = string.Empty;
}