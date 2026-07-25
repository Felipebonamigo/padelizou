namespace Padelizou.ViewModels;

public class MinhaCidadeItem
{
    public int CidadeId { get; set; }
    public string Nome { get; set; } = null!;
    public string? Estado { get; set; }
    public bool Vinculada { get; set; }
}
