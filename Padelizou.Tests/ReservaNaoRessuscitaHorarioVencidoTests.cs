using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 13/09/2026 — A RESERVA HONRAVA UM HORÁRIO QUE JÁ TINHA PASSADO.
//
// 🗣️ Felipe, com o 2ª Etapa ER PADEL TOUR rolando: *"o chaveamento se perdeu dos horarios pré
// definidos de semi final"* · *"tem 3 jogos no mesmo horario, sendo que só tem 2 quadras"* ·
// *"ele é obrigatoriamente obrigado a respeitar os horarios das quadras dos sorteios, pq o
// pessoal se programa para jogar por esses horarios"*.
//
// 🕳️ O FLAGRANTE: semifinais nascidas no domingo de manhã foram parar em **12/09 13:00 e
// 13:50** — sábado à tarde, ANTES das próprias quartas (jogadas sábado 21:20–23:00), e em dois
// slots que estavam vazios porque o torneio só começou 17:10.
//
// ⚠️ O ENCAIXE NÃO CONSEGUE FAZER ISSO, e é o que aponta pra cá: `LevasDaGrade.Encaixar` começa
// cada leva em `MaisTarde(barreira, PisoDestaCategoria(...))` e gera os slots a partir dali —
// um horário anterior ao piso nunca chega a ser oferecido. Sobra UM caminho que escreve
// `HorarioPrevisto` sem passar pelo encaixe: `ReservasDeHorario.Aplicar`.
//
// 🕳️ E ele tinha dois furos, os dois nesta linha:
//
//     public static bool Vale(DateTime reservado, DateTime? abreARodada) =>
//         abreARodada is not DateTime abre || reservado >= abre;
//
//   1. PISO NULO ERA SALVO-CONDUTO: categoria sem fase anterior marcada aceitava qualquer hora.
//   2. NUNCA COMPARAVA COM AGORA: reserva feita pra ontem era honrada hoje, calada.
//
// ⚠️ A reserva nasce do ⇄ num jogo que ainda é PROJEÇÃO — o organizador não precisa saber que
// criou uma. Por isso o Felipe respondeu, de boa-fé, que não tinha mexido em reserva nenhuma.
public class ReservaNaoRessuscitaHorarioVencidoTests
{
    // O RELÓGIO DO TORNEIO: até onde ele já chegou. No ER, o último jogo em quadra na hora em
    // que as semifinais nasceram era a manhã de domingo.
    private static readonly DateTime JaJogouAte = new(2026, 9, 13, 10, 0, 0);

    [Fact]
    public void Reserva_com_hora_que_ja_passou_nao_vale()
    {
        // O caso do ER: reserva de sábado 13:00, aplicada no domingo de manhã.
        var ontem = new DateTime(2026, 9, 12, 13, 0, 0);

        Assert.False(ReservasDeHorario.Vale(ontem, abreARodada: null, relogioDoTorneio: JaJogouAte));
    }

    [Fact]
    public void Piso_nulo_deixa_de_ser_salvo_conduto()
    {
        // Sem fase anterior marcada não há o que esperar — mas "não há o que esperar" nunca
        // quis dizer "pode ser ontem".
        var ontem = new DateTime(2026, 9, 12, 23, 0, 0);
        var daquiAPouco = JaJogouAte.AddHours(2);

        Assert.False(ReservasDeHorario.Vale(ontem, abreARodada: null, relogioDoTorneio: JaJogouAte));
        Assert.True(ReservasDeHorario.Vale(daquiAPouco, abreARodada: null, relogioDoTorneio: JaJogouAte));
    }

    [Fact]
    public void O_piso_da_categoria_continua_valendo_como_antes()
    {
        // A régua que já existia não muda: reserva antes de a fase anterior acabar não vale.
        var abre = JaJogouAte.AddHours(3);

        Assert.False(ReservasDeHorario.Vale(JaJogouAte.AddHours(1), abre, JaJogouAte));
        Assert.True(ReservasDeHorario.Vale(JaJogouAte.AddHours(4), abre, JaJogouAte));
    }

    [Fact]
    public void Reserva_de_hoje_mais_tarde_continua_valendo()
    {
        // ⚠️ O GUARDA-CORPO: a correção não pode matar a reserva legítima, que é o recurso do
        // organizador pra dizer "esta final é às 18h". Só a VENCIDA cai.
        Assert.True(ReservasDeHorario.Vale(JaJogouAte.AddMinutes(50), abreARodada: null, relogioDoTorneio: JaJogouAte));
    }

    [Fact]
    public void E_o_Aplicar_nao_escreve_o_horario_vencido_no_jogo()
    {
        // A ponta que o jogador vê: mesmo com a reserva gravada, o jogo NÃO nasce ontem.
        var jogo = new Partida
        {
            Id = 0, CategoriaId = 7, Fase = "Semifinal", Status = "Agendada",
            Dupla1Id = 1, Dupla2Id = 2, Codigo = "AAA111",
        };

        var reserva = new ReservaDeHorario
        {
            CategoriaId = 7, Fase = "Semifinal", Numero = 1,
            Horario = new DateTime(2026, 9, 12, 13, 0, 0),
            NomeQuadra = "Arena Loja 7",
        };

        var (reservados, _) = ReservasDeHorario.Aplicar(
            new[] { jogo }, _ => 1, new[] { reserva }, _ => null, relogioDoTorneio: JaJogouAte);

        Assert.Empty(reservados);
        Assert.Null(jogo.HorarioPrevisto);
    }
}
