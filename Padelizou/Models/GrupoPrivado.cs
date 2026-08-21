using Padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models; // Use o namespace que já está no seu projeto

[Table("GrupoPrivado")]
public partial class GrupoPrivado
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string CodigoConvite { get; set; } = null!;
    public int AdministradorId { get; set; }

    // Configuração do jogo fixo semanal — null até o admin configurar em Configuracoes.
    public int? ClubeId { get; set; }
    public int? DiaSemanaFixo { get; set; } // 0=Domingo ... 6=Sábado, igual DayOfWeek
    public TimeSpan? HorarioFixo { get; set; }
    public int? CategoriaPadraoId { get; set; }
    public decimal? ValorMensalidade { get; set; }
    public decimal? ValorAvulso { get; set; }
    public int VagasMaximas { get; set; } = 4;

    // Se true, o LembreteJogoBackgroundService avisa PELO APP (push + caixa de avisos + e-mail;
    // não é WhatsApp desde 09/08/2026) todo mundo que está na lista, 24h antes do horário fixo.
    //
    // ⚠️ Nasce `true` só pra grupos NOVOS, desde 21/08/2026: com a presença presumida o aviso
    // deixou de ser cobrança e virou a única chance de quem não vai avisar a tempo. Os grupos
    // que JÁ existem não foram ligados em massa — isso seria uma rajada de push pra base
    // inteira num deploy, disfarçada de melhoria. Quem quiser, liga em Configurações.
    public bool EnviarLembrete24h { get; set; } = true;

    // Relacionamento com o Jogador (Admin)
    public virtual Jogador Administrador { get; set; } = null!;

    public virtual Clube? Clube { get; set; }
    public virtual CategoriaPadrao? CategoriaPadrao { get; set; }
}