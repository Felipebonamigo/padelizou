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

    // Se true, o LembreteJogoBackgroundService manda um WhatsApp automático (via Z-API) pros
    // mensalistas que ainda não confirmaram, 24h antes do horário fixo.
    public bool EnviarLembrete24h { get; set; } = false;

    // Relacionamento com o Jogador (Admin)
    public virtual Jogador Administrador { get; set; } = null!;

    public virtual Clube? Clube { get; set; }
    public virtual CategoriaPadrao? CategoriaPadrao { get; set; }
}