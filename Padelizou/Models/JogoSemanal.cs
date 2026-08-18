using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Jogo casual de um grupo (não é torneio) — usado pro ranking semanal/mensal interno do grupo.
[Table("JogoSemanal")]
public partial class JogoSemanal
{
    public int Id { get; set; }
    public int GrupoId { get; set; }
    public DateTime DataJogo { get; set; }

    // Onde o jogo aconteceu. Nasce do clube fixo do grupo, mas fica gravado no JOGO porque a
    // panelinha muda de quadra: sem isso, trocar o clube do grupo reescreveria o passado —
    // e é este campo que permite ranking/estatística por clube.
    public int? ClubeId { get; set; }

    public int Dupla1Jogador1Id { get; set; }
    public int Dupla1Jogador2Id { get; set; }
    public int Dupla2Jogador1Id { get; set; }
    public int Dupla2Jogador2Id { get; set; }

    // ⚠️ NULO = jogo registrado SEM placar, e isso é normal desde 18/08/2026: registrar um
    // jogo de panelinha passou a exigir só as duplas e quem venceu. Os dois andam juntos —
    // meio placar não existe (ver ResultadoDoJogoSemanal.MotivoParaNaoSalvar).
    public int? GamesDupla1 { get; set; }
    public int? GamesDupla2 { get; set; }

    // QUEM VENCEU: 0 = empate, 1 = dupla 1, 2 = dupla 2.
    //
    // ⚠️ Isto era uma propriedade CALCULADA a partir dos games, e virou coluna. Enquanto o
    // placar era obrigatório, deduzir bastava; com placar opcional, deduzir de um placar
    // ausente daria 0 x 0, que é EMPATE — e o ranking da panelinha se encheria de empates que
    // ninguém jogou, sem erro nenhum na tela. Quem grava é ResultadoDoJogoSemanal.Aplicar.
    public int VencedorLado { get; set; }

    public int RegistradoPorId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("GrupoId")]
    public virtual padelizou.Models.GrupoPrivado Grupo { get; set; } = null!;

    [ForeignKey("ClubeId")]
    public virtual Clube? Clube { get; set; }

    [ForeignKey("Dupla1Jogador1Id")]
    public virtual Jogador Dupla1Jogador1 { get; set; } = null!;

    [ForeignKey("Dupla1Jogador2Id")]
    public virtual Jogador Dupla1Jogador2 { get; set; } = null!;

    [ForeignKey("Dupla2Jogador1Id")]
    public virtual Jogador Dupla2Jogador1 { get; set; } = null!;

    [ForeignKey("Dupla2Jogador2Id")]
    public virtual Jogador Dupla2Jogador2 { get; set; } = null!;

    [ForeignKey("RegistradoPorId")]
    public virtual Jogador RegistradoPor { get; set; } = null!;

}
