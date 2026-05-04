namespace chat_with_api.DTO;

public class ProdutoDto
{
    public required string Descricao { get; set; }
    public required decimal Preco { get; set; }
    public required Guid CategoriaUid { get; set; }

}