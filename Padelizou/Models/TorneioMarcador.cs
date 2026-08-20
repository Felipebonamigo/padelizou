using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// O MARCADOR do torneio: a pessoa da mesa no dia de jogo — marca placar, dá a largada,
// registra W.O., faz o check-in e remenda a grade (trocar quadra/horário, refazer).
//
// É uma tabela PRÓPRIA, e não um NivelAcesso em TorneioOrganizador, de propósito: toda
// checagem de organizador no sistema pergunta "existe linha em TorneioOrganizadores?" sem
// olhar o nível, e um marcador naquela tabela viraria organizador em TODAS elas — inscrição,
// edição, sorteio — de uma vez, calado. Aqui é o contrário, fail-closed: o marcador só entra
// onde uma checagem o incluiu por escrito (PodeOperarODiaDeJogoAsync e o espelho no
// PartidasController). Dinheiro, inscrições e configuração continuam sendo de organizador.
[Table("TorneioMarcador")]
public class TorneioMarcador
{
    public int TorneioId { get; set; }
    public int JogadorId { get; set; }

    public virtual Torneio Torneio { get; set; } = null!;
    public virtual Jogador Jogador { get; set; } = null!;
}
