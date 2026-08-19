using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("PalpitePartida")]
public class PalpitePartida
{
    public int Id { get; set; }

    public int PartidaId { get; set; }
    public virtual Partida Partida { get; set; } = null!;

    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    public int DuplaEscolhidaId { get; set; }
    public virtual Dupla DuplaEscolhida { get; set; } = null!;

    // ── O PLACAR PALPITADO (opcional) ─────────────────────────────────────────────────────
    //
    // Mesmos nomes e MESMA ORIENTAÇÃO das colunas da `Partida`: lado 1 é sempre a Dupla1 do
    // jogo, no palpite e no placar de verdade. Conferir vira comparar coluna com coluna, e
    // "6x4" não muda de significado conforme quem lê.
    //
    // ⚠️ NULO QUER DIZER "NÃO PALPITOU O PLACAR" — nunca "palpitou 0x0", que é placar que não
    // existe. Por isso as quatro colunas são anuláveis e **nascem sem `defaultValue`**: um zero
    // herdado transformaria todo palpite gravado antes de 08/2026 num chute de 0x0, e o
    // ranking passaria a contar um placar que ninguém escreveu.
    //
    // ⚠️ Games OU sets, nunca os dois: o formato do jogo decide qual pergunta cabe (ver
    // Services/PlacaresPossiveis). Num jogo de 2+ sets, `Partida.GamesDupla1` guarda os games
    // do set em andamento — comparar games ali responderia outra pergunta, então lá o palpite
    // é em SETS. Qual das duas duplas de colunas veio preenchida é o que diz, sozinho, em que
    // moeda a pessoa palpitou.
    public int? GamesDupla1 { get; set; }
    public int? GamesDupla2 { get; set; }

    public int? SetsDupla1 { get; set; }
    public int? SetsDupla2 { get; set; }

    public DateTime DataHora { get; set; } = DateTime.Now;
}
