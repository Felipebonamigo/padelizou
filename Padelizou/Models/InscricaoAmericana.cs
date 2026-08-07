using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Inscrição individual (não em dupla) de um jogador numa categoria de Torneio Americano —
// os parceiros mudam a cada rodada, então não faz sentido usar Dupla aqui.
[Table("InscricaoAmericana")]
public class InscricaoAmericana
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public virtual Categoria Categoria { get; set; } = null!;
    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    // Mesmo esquema de lista de espera do Dupla — ver Dupla.EmListaDeEspera.
    public bool EmListaDeEspera { get; set; }

    // Em qual grupo esta pessoa caiu no sorteio: "A", "B", "C"... Null = torneio sem divisão
    // (grupo único), ou ainda não sorteado. Ver Services/FaseDoAmericano.NomeDoGrupo.
    //
    // Fica guardado, e não deduzido das partidas, porque a tela de classificação precisa
    // mostrar o grupo de quem AINDA não jogou — e porque deduzir a mesma coisa em dois
    // lugares é como as duas telas do torneio passaram a discordar uma da outra.
    public string? Grupo { get; set; }

    // Nulo = inscrição feita antes de 25/07/2026 (quando a coluna nasceu) — usado nas
    // métricas de uso do admin (inscrições por semana).
    public DateTime? CriadoEm { get; set; } = DateTime.Now;

    // Mesma ideia do Dupla.Pago: vira true sozinho quando o webhook confirma, e o
    // organizador pode virar na mão (muita inscrição é paga em dinheiro na quadra).
    public bool Pago { get; set; }
    public DateTime? PagoEm { get; set; }
}
