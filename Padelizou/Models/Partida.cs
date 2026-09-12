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

    // A POSIÇÃO DESTE JOGO DENTRO DO PRÓPRIO HORÁRIO (10/09/2026).
    //
    // 🗣️ Felipe, arrumando o domingo do Er: *"quando eu altero um jogo, no mesmo horario, ele nao
    // esta trocando a ordem na linha, tem q trocar tambem para q eu possa colocar a ordem que eu
    // quiser"*. Num torneio "por ordem de liberação" (Services/OrdemDeLiberacao) a quadra é nula:
    // cinco jogos no mesmo minuto eram cinco linhas sem nenhum critério entre si, e o ⇄ entre dois
    // deles trocava hora por hora igual, quadra nula por quadra nula — não mudava nada.
    //
    // Nulo = AUTOMÁTICO, e o automático é o que ele pediu de padrão: "a Semifinal 1 antes da 2",
    // que é a ordem de Id (a mesma de ReservasDeHorario.NumeroNaFase). Quem tem número gravado
    // vem antes de quem não tem — a numeração é 1..k a partir do topo do horário, então "sem
    // número" só pode significar "abaixo dos numerados". A régua inteira está em
    // Services/OrdemNoHorario, que é quem ordena a lista da tela.
    //
    // ⚠️ Some com o "Recalcular horários": ele refaz a grade do zero e desfaz as trocas na mão —
    // a ordem manual é uma delas.
    public int? OrdemNoHorario { get; set; }

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

    // EM QUE CLUBE É O JOGO — a decisão do motor, gravada (10/09/2026).
    //
    // 🗣️ Felipe: *"nao precisa ter a quadra definida, mas o clube sempre tem q estar definido"*.
    // O motor escolhe a quadra de cada jogo (Services/GradeDeJogos.Encaixar) e a quadra carrega o
    // clube; o modo "por ordem" apaga a quadra porque quem chama é a Mesa
    // (Services/OrdemDeLiberacao), e sem esta coluna o clube ia junto — 97 jogos sem lugar na
    // tela do Er. Carimbado a partir da quadra escolhida, antes de ela ser apagada, no "por
    // ordem" e fora dele. Nulo só no jogo que nunca passou pela grade.
    //
    // ⚠️ É a decisão de QUANDO SORTEOU. A Mesa pode chamar o jogo em outra quadra, e aí quem
    // manda é `NomeQuadra` (Services/LugarDoJogo lê a quadra primeiro, o carimbo depois).
    public int? ClubeId { get; set; }
    public virtual Clube? Clube { get; set; }
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

    // A CONTAGEM DO TIE-BREAK, em pontos (Felipe, 12/09/2026). Nulo = o tie-break nem começou.
    //
    // Existe porque o placar ao vivo é desenhado pelo SERVIDOR e chega ao público por
    // atualização automática: um número que vive só na tela de quem marca não chega a quem está
    // olhando de casa. É o mesmo motivo de `DuplaSacandoId` ser coluna.
    //
    // ⚠️ Estes pontos NÃO decidem a partida: quem fecha o tie-break leva o último game, e são os
    // GAMES que dizem quem venceu (9x8). Ver Services/TieBreakDoJogo — a decisão está escrita lá,
    // e é ela que mantém classificação, saldo de games, Padelímetro e chave fora desta história.
    // O que fica aqui é a memória do que aconteceu na quadra, pro card poder dizer
    // "9 x 8, tie-break 7-5".
    public int? PontosTieBreak1 { get; set; }
    public int? PontosTieBreak2 { get; set; }

    // Qual dupla está SACANDO agora. Nulo = ninguém marcou (jogo que não começou, ou
    // organizador que não usa).
    //
    // No padel o saque é metade da leitura do jogo: quem chega na quadra no meio de um
    // game, ou acompanha de fora, precisa saber de quem é o saque pra entender o que está
    // acontecendo. Quem controla é o organizador, na mesma tela em que marca o placar —
    // ele é quem está olhando pra quadra.
    public int? DuplaSacandoId { get; set; }

    // POR QUE o jogo terminou. Nulo é o caso normal: jogou-se e alguém venceu.
    //
    // Hoje o único valor é "WO" (alguém não compareceu) — ver Services/EncerramentoPorWo,
    // que é quem grava e quem lê. ⚠️ Não dá pra deduzir isso do placar: o W.O. é gravado
    // com o placar convencional da fase, justamente pra que a classificação de grupos e o
    // chaveamento continuem funcionando. Por fora ele fica idêntico a um 6x0 jogado, e esta
    // coluna é a única coisa que separa os dois — é ela que mantém o W.O. fora do
    // Padelímetro, que mede como a pessoa JOGA.
    public string? MotivoDoEncerramento { get; set; }
}
