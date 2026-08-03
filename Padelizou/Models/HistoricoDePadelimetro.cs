using System.ComponentModel.DataAnnotations.Schema;
using padelizou.Models;

namespace Padelizou.Models;

// O extrato do Padelímetro: uma linha por movimento, com o porquê. É o que desenha o
// gráfico do perfil e responde "por que meu número mudou?" — transparência que o
// concorrente não dá (regras em RANKING.md).
//
// O número ATUAL vive em Jogador.Padelimetro; este histórico é a memória. O replay
// (PadelimetroService.RecalcularTudoAsync) reconstrói os dois do zero a partir das
// partidas finalizadas — mudou a regra, roda de novo e o extrato inteiro se refaz.
[Table("HistoricoDePadelimetro")]
public class HistoricoDePadelimetro
{
    public int Id { get; set; }

    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    // Nulo = linha de nascimento (o seed pela categoria da primeira partida contada),
    // não um jogo. Cascade no jogo: se a partida sumir do banco, a linha dela sai do
    // extrato — e o replay reacerta o total.
    public int? PartidaId { get; set; }
    public virtual Partida? Partida { get; set; }

    public int NivelAntes { get; set; }
    public int Delta { get; set; }

    [NotMapped]
    public int NivelDepois => NivelAntes + Delta;

    // Legível por gente: "Entrou pela 4ª Categoria Masculina", "Vitória 6x2 sobre
    // Fulano & Beltrano". É o texto do extrato, não um código.
    public string Motivo { get; set; } = "";

    // No gancho ao vivo é agora; no replay é a data da PARTIDA — senão o gráfico
    // inteiro nasceria empilhado no dia do recálculo.
    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
