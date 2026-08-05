using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using System.ComponentModel.DataAnnotations.Schema;
[Table("Partida")]
public partial class Partida
{
    public int Id { get; set; }

    public int CategoriaId { get; set; }

    public int Dupla1Id { get; set; }

    public int Dupla2Id { get; set; }

    public string Codigo { get; set; } = null!;

    public int? SetsDupla1 { get; set; }

    public int? SetsDupla2 { get; set; }

    public int? GamesDupla1 { get; set; }

    public int? GamesDupla2 { get; set; }

    public virtual Categoria Categoria { get; set; } = null!;

    public virtual Dupla Dupla1 { get; set; } = null!;

    public virtual Dupla Dupla2 { get; set; } = null!;
    public int? TorneioId { get; set; } = null!;
    public bool SendoTransmitida { get; set; } = false;
    public string Status { get; set; } = null!;
    public int? VencedorId { get; set; }
    public string Fase { get; set; } = "Fase de Grupos";

    public DateTime? HorarioPrevisto { get; set; }
    public DateTime? HorarioInicioReal { get; set; }
    public DateTime? HorarioFimReal { get; set; }

    [NotMapped]
    public int MinutosDecorridos
    {
        get
        {
            if (HorarioInicioReal == null) return 0;
            if (HorarioFimReal != null) return (int)(HorarioFimReal.Value - HorarioInicioReal.Value).TotalMinutes;
            return (int)(DateTime.Now - HorarioInicioReal.Value).TotalMinutes;
        }
    }
    public string? NomeQuadra { get; set; } // Ex: "Quadra Central", "Quadra 1"
    public string? LinkTransmissao { get; set; } // Ex: "https://youtube.com/live/..."

    // Quando saiu o aviso "seu jogo é o próximo" pros jogadores desta partida.
    // Existe pra ele sair UMA vez: o organizador finalizar sem querer e desfazer é comum
    // no meio do torneio, e sem esta marca cada desfazer mandaria o push de novo.
    public DateTime? AvisoProximoEnviadoEm { get; set; }

    // Push de "sua quadra está atrasada" já saiu? (um por partida — aviso repetido vira ruído)
    public DateTime? AvisoAtrasoEnviadoEm { get; set; }

    // Quando o placar foi marcado NA MESA (relógio do aparelho do organizador). É o que deixa
    // a sincronização offline ser "o último estado vence": um placar guardado no celular sem
    // internet não pode atropelar um mais novo vindo de outro aparelho.
    public DateTime? PlacarMarcadoEm { get; set; }

    // Qual dupla está SACANDO agora. Nulo = ninguém marcou (jogo que não começou, ou
    // organizador que não usa).
    //
    // No padel o saque é metade da leitura do jogo: quem chega na quadra no meio de um
    // game, ou acompanha de fora, precisa saber de quem é o saque pra entender o que está
    // acontecendo. Quem controla é o organizador, na mesma tela em que marca o placar —
    // ele é quem está olhando pra quadra.
    public int? DuplaSacandoId { get; set; }
}
