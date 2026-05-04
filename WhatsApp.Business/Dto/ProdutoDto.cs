namespace chat_with_api.DTO;

public class ProdutoDto
{
    public Guid Uid { get; set; }
    public required string Descricao { get; set; }
    public required decimal Preco { get; set; }
    public required Guid CategoriaUid { get; set; }

}