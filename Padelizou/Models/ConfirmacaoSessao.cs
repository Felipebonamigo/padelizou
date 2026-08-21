using System.ComponentModel.DataAnnotations.Schema;
using Padelizou.Services;

namespace Padelizou.Models;

// RSVP de um jogador pra uma SessaoGrupo: mensalista do grupo (Avulso=false, criado
// automaticamente quando a sessão é gerada) ou convidado de fora (Avulso=true, criado quando
// alguém do grupo manda um convite por Convidar/ConvidarJogador).
//
// ⚠️ SÃO QUATRO STATUS DESDE 21/08/2026, e a régua mora em Services/PresencaNaSessao — nunca
// compare esta coluna com string crua. O membro NASCE "Presumido" (é da panelinha, logo está na
// lista sem precisar responder); o convidado nasce "Pendente", porque ele foi chamado e não
// combinou nada.
[Table("ConfirmacaoSessao")]
public partial class ConfirmacaoSessao
{
    public int SessaoId { get; set; }
    public int JogadorId { get; set; }

    // Presumido / Confirmado / NaoVai / Pendente — ver Services/PresencaNaSessao.
    public string Status { get; set; } = PresencaNaSessao.Pendente;

    public string? Lado { get; set; } // Esquerda / Direita — default = Jogador.LadoQuadra na criação
    public bool Avulso { get; set; }

    // ⚠️ NULO JUNTO COM Status="NaoVai" QUER DIZER "O ADMIN ME TIROU", e não "eu avisei que não
    // vou" — ver PresencaNaSessao.TiradoPeloAdmin. É o que impede a tela de dizer que alguém
    // avisou algo que essa pessoa nunca disse.
    public DateTime? RespondidoEm { get; set; }

    // Marcado pelo LembreteJogoBackgroundService no DESPACHO do aviso de 24h (não na entrega —
    // a fila é assíncrona e o próprio job diz isso por escrito). Evita reenviar a cada tick.
    public DateTime? LembreteEnviadoEm { get; set; }

    [ForeignKey("SessaoId")]
    public virtual SessaoGrupo Sessao { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
