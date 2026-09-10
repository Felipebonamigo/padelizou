using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// O HORÁRIO QUE O ORGANIZADOR RESERVOU PRA UM JOGO QUE AINDA NÃO EXISTE (10/09/2026).
//
// 🗣️ Felipe, olhando as finais do Er com o selo "prévia": *"permita também trocar de horário as
// eliminatórias, não apenas as de chave"*. A Semifinal e a Final de um torneio de grupos só nascem
// quando a fase anterior fecha (Partida exige as duas duplas — não existe partida "a definir" no
// banco). Até lá elas são prévia (Services/ProximasFasesDaChave), e a troca de horário só
// alcançava jogo real.
//
// A troca do organizador vira uma linha aqui: "a Final 1 desta categoria é às 22:00, na Quadra
// Central". Quem obedece: a prévia (é onde ele vê a troca), o robô que cria a rodada
// (RoboDoChaveamento.AgendarNaGradeAsync, onde ela vira jogo de verdade) e o reencaixe que o robô
// faz quando outra categoria avança — sem esse último a escolha sumiria calada.
//
// ⚠️ A CHAVE É (categoria, fase, número), a mesma numeração com que a prévia e a tela citam o jogo
// ("Vencedor Quartas de Final 2"): ordem de Id dentro da fase, que é a ordem em que o robô grava
// a rodada. A PK composta é o que segura o clique duplo: reservar duas vezes o mesmo jogo é
// UPDATE, nunca duas linhas.
//
// ⚠️ Reserva antes de a fase anterior da própria categoria terminar NÃO VALE — nem na prévia, nem
// no robô (ver Services/ReservasDeHorario.Vale). A final não pode ser marcada com os finalistas
// ainda em quadra, reserva ou não; e a reserva é aceita na troca justamente porque, naquele
// momento, ela era possível. Se o torneio atrasar e ela deixar de ser, o jogo volta pra grade.
//
// Some com o "Refazer grade" e com o "Desfazer sorteio": a reserva é um remendo por cima da grade,
// e os dois refazem a grade do zero.
[Table("ReservaDeHorario")]
public class ReservaDeHorario
{
    public int CategoriaId { get; set; }

    // "Semifinal", "Final" — o nome da fase como Partida.Fase o grava.
    public string Fase { get; set; } = null!;

    // O número do jogo dentro da fase, começando em 1.
    public int Numero { get; set; }

    public DateTime Horario { get; set; }

    // Nula no torneio sem quadra cadastrada: o jogo nasce com hora e sem quadra, como a grade
    // faria com ele.
    public string? NomeQuadra { get; set; }

    public virtual Categoria Categoria { get; set; } = null!;
}
