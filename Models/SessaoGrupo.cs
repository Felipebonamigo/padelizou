using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// A ocorrência de uma semana específica do jogo fixo de um GrupoPrivado (dia/horário configurados
// em GrupoPrivado.DiaSemanaFixo/HorarioFixo). Criada sob demanda (lazy) quando alguém acessa a tela
// da semana — não existe job de background no projeto, então não há geração antecipada automática.
[Table("SessaoGrupo")]
public partial class SessaoGrupo
{
    public int Id { get; set; }
    public int GrupoId { get; set; }
    public DateTime DataHora { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("GrupoId")]
    public virtual padelizou.Models.GrupoPrivado Grupo { get; set; } = null!;

    public virtual ICollection<ConfirmacaoSessao> Confirmacoes { get; set; } = new List<ConfirmacaoSessao>();
}
