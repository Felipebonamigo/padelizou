namespace Padelizou.Models;

// Uma curtida numa linha do mural do perfil (ComentarioPerfil).
//
// Pedido do Felipe (07/09/2026): "Permita as pessoas curtirem comentário no perfil também" —
// mesma forma do Elogio (Models/Elogio.cs): um por pessoa, trocável só entre curtir/descurtir
// (não tem "tipo" pra trocar, então as duas ações — Curtir/Descurtir — cobrem tudo).
public class CurtidaDoComentario
{
    public int Id { get; set; }

    public int ComentarioId { get; set; }
    public virtual ComentarioPerfil Comentario { get; set; } = null!;

    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
