using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// ⚠️ O QUE FAZ A CONCENTRAÇÃO FUNCIONAR DE VERDADE, E QUE NÃO ESTAVA NO PEDIDO: A GRADE
// PRECISA CHEGAR ATÉ O TURNO ESCOLHIDO.
//
// O impedimento tira UMA janela de muitas — sempre sobra grade adiante. A concentração faz o
// contrário: tira TODAS menos uma. Se a lista de vagas acabar ANTES do turno escolhido, o
// `Encaixar` cai no último recurso ("jogo sem horário é pior que jogo fora do turno") e marca
// a dupla onde der — o favor não acontece, e ninguém fica sabendo.
//
// A conta de vagas é `jogos + margem` (VagasDaGrade), e num torneio de fim de semana isso mal
// passa da manhã de sábado: a sexta abre às 18h e come as primeiras rodadas. Ou seja, "os 2
// jogos no sábado à TARDE" — uma das três opções que o Felipe pediu — era a que mais tinha
// chance de sair calada.
public class VagasAlcancamAConcentracaoTests
{
    // Sexta 14/08/2026, 18h — o padrão de torneio de fim de semana descrito em GradeDeJogos.
    private static readonly DateTime Sexta18h = new(2026, 8, 14, 18, 0, 0);
    private static readonly DateTime Sabado = new(2026, 8, 15);

    private static Torneio Torneio() => new()
    {
        Nome = "T", Codigo = "T1",
        DataInicio = Sexta18h,
        QuantidadeQuadras = 4,
        TempoPrevistoPartidaMinutos = 50,
        HoraInicioDoDia = new TimeSpan(18, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
    };

    // A PROVA DE QUE O PROBLEMA É REAL, e ela é mais fina do que parecia: 39 jogos de grupo em
    // 4 quadras (o tamanho do torneio do Er) alcançam a tarde de sábado por UMA RODADA SÓ —
    // 12:10, e acabou.
    //
    // Uma rodada não serve: a dupla joga DOIS jogos de grupo, e os dois não cabem no mesmo
    // horário (o `Encaixar` nunca põe a mesma gente em duas quadras ao mesmo tempo). Com uma
    // rodada só, o segundo jogo cai no último recurso e sai do turno prometido.
    [Fact]
    public void Sem_alcance_a_tarde_de_sabado_nao_tem_rodada_pros_dois_jogos()
    {
        var vagas = VagasDaGrade.Montar(Torneio(), Sexta18h, quantosJogos: 39);

        var rodadasDaTarde = vagas
            .Where(v => v >= Sabado.Add(JanelasDeImpedimento.CorteSabadoManhaTarde) && v < Sabado.AddDays(1))
            .Distinct()
            .Count();

        Assert.True(rodadasDaTarde < 2,
            $"A premissa deste arquivo caiu: a tarde de sábado já tem {rodadasDaTarde} rodadas "
            + "sem pedir alcance nenhum. Se a conta de vagas mudou, reveja se o alcance ainda "
            + "faz falta.");
    }

    [Fact]
    public void Com_alcance_a_tarde_de_sabado_ganha_rodada_de_sobra()
    {
        var vagas = VagasDaGrade.Montar(Torneio(), Sexta18h, quantosJogos: 39,
            peloMenosAte: Sabado.AddDays(1));

        var rodadasDaTarde = vagas
            .Where(v => v >= Sabado.Add(JanelasDeImpedimento.CorteSabadoManhaTarde) && v < Sabado.AddDays(1))
            .Distinct()
            .Count();

        // Duas já bastariam pros 2 jogos da dupla; a tarde inteira dá muito mais, e é isso que
        // deixa o `Encaixar` escolher em vez de ceder.
        Assert.True(rodadasDaTarde >= 2, $"Só {rodadasDaTarde} rodada(s) na tarde de sábado.");
    }

    // ⚠️ E NÃO ENCOLHE: quem já pedia mais vagas que o alcance continua com as vagas que pedia.
    [Fact]
    public void O_alcance_nunca_tira_vaga_de_quem_ja_tinha_mais()
    {
        var semAlcance = VagasDaGrade.Montar(Torneio(), Sexta18h, quantosJogos: 400);
        var comAlcance = VagasDaGrade.Montar(Torneio(), Sexta18h, quantosJogos: 400,
            peloMenosAte: Sabado.AddHours(9));

        Assert.Equal(semAlcance.Count, comAlcance.Count);
    }

    [Fact]
    public void Sem_pedido_de_alcance_a_conta_de_vagas_nao_muda()
    {
        var torneio = Torneio();

        Assert.Equal(VagasDaGrade.Montar(torneio, Sexta18h, quantosJogos: 39).Count,
                     VagasDaGrade.Montar(torneio, Sexta18h, quantosJogos: 39, peloMenosAte: null).Count);
    }

    // O instante que a grade precisa alcançar, dado quem tem concentração. Nulo quando ninguém
    // tem — e aí nada muda pra torneio nenhum.
    [Fact]
    public void Sem_ninguem_concentrado_nao_ha_alcance_a_pedir()
    {
        var torneio = Torneio();

        Assert.Null(ConcentracaoDeJogos.AteQuandoAGradePrecisaIr(torneio, new[] { new Dupla() }));
    }

    [Fact]
    public void O_alcance_pedido_e_o_fim_do_turno_mais_tardio()
    {
        var torneio = Torneio();
        var naSexta = new Dupla { Id = 1, ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite };
        var naTarde = new Dupla { Id = 2, ConcentrarJogosEm = TurnoDeConcentracao.SabadoTarde };

        Assert.Equal(Sabado.AddDays(1),
            ConcentracaoDeJogos.AteQuandoAGradePrecisaIr(torneio, new[] { naSexta, naTarde }));
    }
}
