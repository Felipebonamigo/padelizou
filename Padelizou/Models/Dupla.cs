using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;
[Table("Dupla")]
public partial class Dupla
{
    public int Id { get; set; }

    public int CategoriaId { get; set; }

    public int Jogador1Id { get; set; }

    // NULO = inscrito sozinho, ainda procurando parceiro. A dupla existe (ocupa vaga,
    // pagou, entra na lista de espera), mas fica de fora do sorteio até ter os dois nomes.
    public int? Jogador2Id { get; set; }

    public string? Codigo { get; set; }

    // Convite de parceiro: o link que fecha a dupla sem ninguém precisar do CPF do outro.
    // Nulo depois de aceito — token usado não volta a valer (ver Services/ConviteDeParceiro).
    public string? ConviteToken { get; set; }
    public DateTime? ConviteCriadoEm { get; set; }

    public virtual Categoria Categoria { get; set; } = null!;

    public virtual Jogador Jogador1 { get; set; } = null!;

    public virtual Jogador? Jogador2 { get; set; }

    // Só entra em chaves/grupos quando a dupla está fechada.
    [NotMapped]
    public bool Completa => Jogador2Id != null;

    // Situação do pagamento desta inscrição. Vira true sozinho quando o webhook do Asaas
    // confirma, e o organizador pode virar na mão a qualquer momento — muita inscrição é
    // paga em dinheiro na quadra, e a última palavra sobre quem pagou é sempre dele.
    public bool Pago { get; set; }
    public DateTime? PagoEm { get; set; }

    public virtual ICollection<Partida> PartidasDupla1 { get; set; } = new List<Partida>();

    public virtual ICollection<Partida> PartidasDupla2 { get; set; } = new List<Partida>(); 
    public bool ImpedimentoSextaNoite { get; set; }
    public bool ImpedimentoSabadoManha { get; set; }
    public bool ImpedimentoSabadoTarde { get; set; }
    public int? GrupoTorneioId { get; set; }
    public virtual GrupoTorneio? GrupoTorneio { get; set; }

    [NotMapped]
    public int Jogos { get; set; }

    [NotMapped]
    public int Vitorias { get; set; }

    [NotMapped]
    public int Derrotas { get; set; }

    [NotMapped]
    public int SaldoGames { get; set; }
    public string UltimaFase { get; set; } = "Grupos";
    public string? Grupo { get; set; } // Vai receber "A", "B", etc.

    // Verdadeiro quando a categoria (ou o torneio) já estava com vagas esgotadas no
    // momento da inscrição. Fica de fora das chaves/contagens até ser promovido.
    public bool EmListaDeEspera { get; set; }

    // Nulo = inscrição feita antes de 25/07/2026 (quando a coluna nasceu) — usado nas
    // métricas de uso do admin (inscrições por semana).
    public DateTime? CriadoEm { get; set; } = DateTime.Now;

    // Check-in no dia do torneio: a dupla apareceu. Nulo = ainda não fez check-in.
    // Serve pro organizador ver quem falta antes de começar, e evitar W.O. surpresa.
    public DateTime? CheckInEm { get; set; }
}
