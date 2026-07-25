using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// RSVP de um jogador pra uma SessaoGrupo: mensalista do grupo (Avulso=false, criado automaticamente
// como Pendente quando a sessão é gerada) ou convidado de fora (Avulso=true, criado quando o admin
// manda um convite por Convidar/ConvidarJogador).
[Table("ConfirmacaoSessao")]
public partial class ConfirmacaoSessao
{
    public int SessaoId { get; set; }
    public int JogadorId { get; set; }

    public string Status { get; set; } = "Pendente"; // Pendente / Confirmado / NaoVai
    public string? Lado { get; set; } // Esquerda / Direita — default = Jogador.LadoQuadra na criação
    public bool Avulso { get; set; }
    public DateTime? RespondidoEm { get; set; }

    // Marcado pelo LembreteJogoBackgroundService quando o lembrete de 24h é enviado com sucesso —
    // evita reenviar a cada tick do job.
    public DateTime? LembreteEnviadoEm { get; set; }

    [ForeignKey("SessaoId")]
    public virtual SessaoGrupo Sessao { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
