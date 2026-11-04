using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// O tamanho da camiseta de brinde que UM atleta escolheu num torneio (04/11/2026 — ver
// Services/CamisetaDeBrinde).
//
// ⚠️ Por PESSOA e por torneio, não por dupla: quem joga duas categorias leva UMA camiseta, e a
// PK composta (TorneioId, JogadorId) é o que garante isso — trocar o tamanho atualiza a mesma
// linha, e o clique duplo não vira duas camisetas.
[Table("CamisetaDoAtleta")]
public class CamisetaDoAtleta
{
    public int TorneioId { get; set; }
    public int JogadorId { get; set; }

    // Código de CamisetaDeBrinde.Tamanhos ("M", "BLP"...).
    [StringLength(10)]
    public string Tamanho { get; set; } = null!;

    public DateTime EscolhidaEm { get; set; } = DateTime.Now;

    public virtual Torneio Torneio { get; set; } = null!;
    public virtual Jogador Jogador { get; set; } = null!;
}
