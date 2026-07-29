using System.Timers;

namespace Padelizou.Models;

public class Clube
{
    public int Id { get; set; }
    // Os três são NOT NULL no banco (IsRequired), então string vazia é o padrão honesto: o EF
    // sobrescreve ao ler, e quem cria um clube sem endereço grava "" em vez de estourar no insert.
    public string Nome { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Contato { get; set; } = string.Empty;

    // Dono do clube — atribuído só por um administrador do sistema (AdminController).
    public int? DonoId { get; set; }
    public virtual Jogador? Dono { get; set; }

    // Cidade do clube — usada pra bater com JogadorCidade nas notificações de "Marcar Jogo".
    public int? CidadeId { get; set; }
    public virtual Cidade? Cidade { get; set; }

    // "Marcar Jogo" — dono/admin ativa pra permitir que o Padelizou administre a agenda de
    // quadras do clube (ver QuadraClube/HorarioMarcacaoDisponivel/MarcacaoJogo).
    public bool MarcacaoHorariosAtiva { get; set; }
    public bool NotificarHorariosDiariamente { get; set; }

    // ---- Política de cancelamento / no-show ----
    // Mesma ideia da política do professor: horas mínimas de aviso e se cobra quem não
    // avisa. 0 = pode desmarcar até a última hora.
    public int HorasMinimasCancelamento { get; set; } = 12;
    public bool CobraNoShow { get; set; }
    public string? PoliticaCancelamentoTexto { get; set; }

    // Relacionamentos
    public ICollection<Torneio> Torneios { get; set; } = new List<Torneio>();
    public ICollection<Time> Times { get; set; } = new List<Time>();
}