namespace Padelizou.Models;

public class ComentarioPerfil
{
    public int Id { get; set; }

    public int AutorId { get; set; }
    public virtual Jogador Autor { get; set; } = null!;

    public int PerfilId { get; set; }
    public virtual Jogador Perfil { get; set; } = null!;

    public string Texto { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
