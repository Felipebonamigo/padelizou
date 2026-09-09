using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A JANELA DA QUADRA PASSOU A VALER TAMBÉM COM UM CLUBE SÓ (09/09/2026).
//
// 🗣️ Felipe, na tela de planejamento de quadras: *"permita adicionar mais quadras, e colocar o
// nome do local e dados necessário, e colocar quais horarios estarão disponiveis nessa(s)
// quadra(s)"*.
//
// 🕳️ O CAMPO EXISTIA E ERA DESCARTADO EM SILÊNCIO. `SedesDoTorneio.Montar` saía por
// `return Nenhuma` assim que todas as quadras caíam no mesmo clube — uma otimização escrita
// quando a janela só servia pro LOCAL ALUGADO de outra sede (08/09). Com o campo oferecido na
// tela, o mesmo atalho vira mentira: o organizador aluga duas quadras no próprio complexo do
// Er das 8h às 14h de sábado, digita a janela, o motor a joga fora e marca jogo lá às 22h.
//
// ⚠️ FALHA MUDA, que é a pior que este projeto conhece: nada reclama, o valor fica gravado no
// banco, e a única forma de descobrir é alguém aparecer numa quadra fechada.
//
// ⚠️ O `Nenhuma` NÃO MORREU — ele continua sendo a resposta do torneio de uma sede SEM janela
// nenhuma, que é a imensa maioria. O que mudou é que a janela sobrevive à saída antecipada, e
// `MaisDeUmClube` continua `false`: janela não é sede, e confundir as duas ligaria a folga pra
// atravessar a cidade e a trava de categoria num torneio que acontece num lugar só.
public class JanelaDeQuadraNoClubeUnicoTests
{
    private const int Clube = 1;
    private static readonly DateTime Sabado = new(2026, 8, 15);

    private static Quadra Q(string nome, DateTime? de = null, DateTime? ate = null) =>
        new() { Nome = nome, ClubeId = null, DisponivelDe = de, DisponivelAte = ate };

    // Duas quadras do próprio clube: a de sempre e uma alugada das 8h às 14h.
    private static SedesDoTorneio UmClubeSo() =>
        SedesDoTorneio.Montar(Clube, 30,
            new[]
            {
                Q("Central"),
                Q("Alugada", Sabado.AddHours(8), Sabado.AddHours(14)),
            },
            Array.Empty<Categoria>());

    [Fact]
    public void Quadra_alugada_no_proprio_clube_fecha_fora_da_janela()
    {
        var sedes = UmClubeSo();

        Assert.True(sedes.QuadraAberta("Alugada", Sabado.AddHours(9)));
        Assert.False(sedes.QuadraAberta("Alugada", Sabado.AddHours(7)));

        // O "até" entra: 14h é a hora do último jogo, não o instante em que já fechou
        // (09/09/2026 — ver JanelaDaQuadraTerminaNoUltimoJogoTests).
        Assert.True(sedes.QuadraAberta("Alugada", Sabado.AddHours(14)));
        Assert.False(sedes.QuadraAberta("Alugada", Sabado.AddHours(15)));
    }

    [Fact]
    public void A_quadra_de_sempre_continua_aberta_o_dia_inteiro()
    {
        var sedes = UmClubeSo();

        // Quem não tem janela não entra no mapa e vale o expediente do torneio — é o que
        // impede o conserto de fechar quadra que ninguém pediu pra fechar.
        Assert.True(sedes.QuadraAberta("Central", Sabado.AddHours(7)));
        Assert.True(sedes.QuadraAberta("Central", Sabado.AddHours(23)));
    }

    // ⚠️ A CONTA DE VAGAS PRECISA ENXERGAR AS DUAS. `VagasDaGrade` pergunta quantas quadras
    // estão abertas em cada horário pra não gastar orçamento com vaga morta; se o mapa de
    // clubes viesse vazio, ela contaria ZERO quadra aberta e a grade não teria onde marcar.
    [Fact]
    public void Conta_quantas_quadras_estao_abertas_em_cada_horario()
    {
        var sedes = UmClubeSo();

        Assert.Equal(2, sedes.QuadrasAbertasEm(Sabado.AddHours(9)));
        Assert.Equal(1, sedes.QuadrasAbertasEm(Sabado.AddHours(20)));
    }

    // ⚠️ JANELA NÃO É SEDE. Sem esta guarda, o conserto ligaria num torneio de um clube só a
    // folga pra atravessar a cidade e a trava de "esta categoria joga só naquele clube" —
    // regras de quem tem DOIS endereços.
    [Fact]
    public void Um_clube_com_janela_continua_sendo_um_clube_so()
    {
        var sedes = UmClubeSo();

        Assert.False(sedes.MaisDeUmClube);
        Assert.Equal(TimeSpan.Zero, sedes.FolgaParaTrocarDeClube);
        Assert.Null(sedes.QuadrasDe(10));
    }

    // O caminho de quase todo torneio do Padelizou: um clube, nenhuma janela. Continua saindo
    // pelo atalho, sem pagar dicionário nenhum.
    [Fact]
    public void Um_clube_sem_janela_nenhuma_continua_sendo_Nenhuma()
    {
        var sedes = SedesDoTorneio.Montar(Clube, 30,
            new[] { Q("Central"), Q("Quadra 2") }, Array.Empty<Categoria>());

        Assert.False(sedes.MaisDeUmClube);
        Assert.Null(sedes.QuadrasAbertasEm(Sabado.AddHours(20)));
        Assert.True(sedes.QuadraAberta("Central", Sabado.AddHours(20)));
    }
}
