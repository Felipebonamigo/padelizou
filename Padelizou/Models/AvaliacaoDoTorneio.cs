using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// A enquete de depois do torneio: quem jogou dá nota pro CLUBE (estrutura, quadras) e pra
// ORGANIZAÇÃO (como o torneio rodou). Uma resposta por jogador por torneio — índice único no
// banco, e trocar a nota enquanto a janela está aberta atualiza a mesma linha.
//
// Existe por causa de 2027: o "Melhor Clube do ano" precisa de um ano de avaliações, e esse
// relógio só começa quando a coleta começa (ver Services/EnqueteDoTorneio).
[Table("AvaliacaoDoTorneio")]
public class AvaliacaoDoTorneio
{
    public int Id { get; set; }

    public int TorneioId { get; set; }
    public int JogadorId { get; set; }

    // De 1 a 5 estrelas, como as avaliações de professor — sem meia-estrela, sem 0-10.
    public int NotaClube { get; set; }
    public int NotaOrganizacao { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    [ForeignKey("TorneioId")]
    public virtual Torneio Torneio { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
