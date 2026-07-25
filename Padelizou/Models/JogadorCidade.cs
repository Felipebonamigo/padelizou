using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Cidades onde o jogador joga ou aceita jogar — mirror de ProfessorCidade, mas pro jogador
// comum. Usado pra elegibilidade da notificação de "horário vago na região".
[Table("JogadorCidade")]
public partial class JogadorCidade
{
    public int JogadorId { get; set; }
    public int CidadeId { get; set; }

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;

    [ForeignKey("CidadeId")]
    public virtual Cidade Cidade { get; set; } = null!;
}
