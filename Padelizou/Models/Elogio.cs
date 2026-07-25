namespace Padelizou.Models;

public class Elogio
{
    public int Id { get; set; }

    public int DeJogadorId { get; set; }
    public virtual Jogador DeJogador { get; set; } = null!;

    public int ParaJogadorId { get; set; }
    public virtual Jogador ParaJogador { get; set; } = null!;

    // Código fixo do catálogo (Services/CatalogoElogios.cs), ex: "SmashBom".
    public string Tipo { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
