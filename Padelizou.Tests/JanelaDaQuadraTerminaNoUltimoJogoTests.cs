using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O "ATÉ" DA QUADRA PASSOU A SER A HORA DO ÚLTIMO JOGO (09/09/2026).
//
// 🗣️ Felipe, descrevendo o combinado do Er: *"no radar, 2 quadras — 08h, 08:50, 09:40, 10:30,
// 11:20, 12:10. Vão ser 12 jogos"*. Ele preencheu `08:00` e `12:10` na tela e contou 6 rodadas
// × 2 quadras.
//
// 🕳️ A TELA RESPONDIA 10. A janela nascera MEIO ABERTA ([De, Ate)) — "mesmo formato de
// JanelasDeImpedimento", diz o comentário de origem —, então o jogo que começa às 12:10 em
// ponto estava FORA de uma janela que termina 12:10. Duas rodadas de quadra alugada somem da
// conta, e o organizador aluga de menos.
//
// ⚠️ A SIMETRIA ERRADA ERA COM O IMPEDIMENTO, e a certa é com `Torneio.HoraFimDoDia`:
//
//   • O impedimento é um PERÍODO em que a pessoa não pode jogar — "sábado à tarde" —, e aí
//     meio aberto é o certo: o fim de um turno é o começo do outro, e nenhum instante pode
//     pertencer aos dois.
//   • A janela da quadra responde ATÉ QUE HORAS DÁ PRA COMEÇAR UM JOGO, que é exatamente o
//     que `HoraFimDoDia` sempre respondeu — e aquele campo é INCLUSIVO desde sempre ("a hora
//     limite pra COMEÇAR um jogo, não pra terminar"). O rodapé da tela de planejamento já
//     prometia isso com todas as letras; era a conta que estava fora do combinado.
//
// Os dois campos são preenchidos pelo mesmo organizador, na mesma tela, com a mesma cabeça.
public class JanelaDaQuadraTerminaNoUltimoJogoTests
{
    private const int Clube = 1;
    private static readonly DateTime Sabado = new(2026, 9, 12);

    // O combinado do Er, literal: Radar com 2 quadras, das 8h às 12:10.
    private static SedesDoTorneio DoRadar() =>
        SedesDoTorneio.Montar(Clube, 30,
            new[]
            {
                new Quadra { Nome = "Radar 1", ClubeId = null,
                             DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12).AddMinutes(10) },
                new Quadra { Nome = "Radar 2", ClubeId = null,
                             DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12).AddMinutes(10) },
            },
            Array.Empty<Categoria>());

    [Fact]
    public void O_jogo_que_comeca_na_hora_do_ate_esta_dentro()
    {
        var sedes = DoRadar();

        Assert.True(sedes.QuadraAberta("Radar 1", Sabado.AddHours(8)));
        Assert.True(sedes.QuadraAberta("Radar 1", Sabado.AddHours(11).AddMinutes(20)));

        // A LINHA DESTE CONSERTO: 12:10 é a hora do último jogo, não o instante em que a
        // quadra já fechou.
        Assert.True(sedes.QuadraAberta("Radar 1", Sabado.AddHours(12).AddMinutes(10)));
    }

    [Fact]
    public void Depois_do_ate_continua_fechada()
    {
        var sedes = DoRadar();

        Assert.False(sedes.QuadraAberta("Radar 1", Sabado.AddHours(7).AddMinutes(59)));
        Assert.False(sedes.QuadraAberta("Radar 1", Sabado.AddHours(12).AddMinutes(11)));
        Assert.False(sedes.QuadraAberta("Radar 1", Sabado.AddHours(13)));
    }

    // O número que o Felipe contou na mão.
    [Fact]
    public void O_radar_do_Er_cabe_12_jogos()
    {
        Assert.Equal(12, SedesDoTorneio.JogosQueCabemNaJanela(
            2, Sabado.AddHours(8), Sabado.AddHours(12).AddMinutes(10), 50));
    }

    // Uma janela de tamanho ZERO passou a ser UMA rodada, e não nenhuma: "disponível das 8h
    // às 8h" quer dizer "dá pra começar um jogo às 8h". É a leitura inevitável depois de o
    // "até" virar a hora do último jogo.
    [Fact]
    public void Janela_de_um_instante_cabe_uma_rodada()
    {
        Assert.Equal(2, SedesDoTorneio.JogosQueCabemNaJanela(
            2, Sabado.AddHours(8), Sabado.AddHours(8), 50));
    }

    // Invertida continua sendo zero — não há hora nenhuma pra começar.
    [Fact]
    public void Janela_invertida_continua_cabendo_zero()
    {
        Assert.Equal(0, SedesDoTorneio.JogosQueCabemNaJanela(
            2, Sabado.AddHours(12), Sabado.AddHours(8), 50));
    }

    // ⚠️ O IMPEDIMENTO NÃO MUDOU, e esta guarda é o que impede alguém de "uniformizar" as duas
    // mais tarde: lá o fim de um turno é o começo do outro (sábado de manhã acaba ao meio-dia,
    // sábado à tarde começa ao meio-dia), e um instante que pertencesse aos dois bloquearia a
    // dupla no turno que ela liberou. As janelas continuam sendo pares [Início, Fim) que se
    // encostam sem se sobrepor.
    [Fact]
    public void Os_turnos_do_impedimento_continuam_se_encostando_sem_sobrepor()
    {
        var torneio = new Torneio
        {
            DataInicio = new DateTime(2026, 9, 11),   // sexta
            PermiteImpedimentos = true,
        };
        var dupla = new Dupla
        {
            ImpedimentoSextaNoite = true,
            ImpedimentoSabadoManha = true,
            ImpedimentoSabadoTarde = true,
        };

        var janelas = JanelasDeImpedimento.Da(torneio, dupla).OrderBy(j => j.Inicio).ToList();

        Assert.True(janelas.Count >= 2);
        for (int i = 1; i < janelas.Count; i++)
        {
            // Encostam ou têm intervalo — o que não pode é o fim de um passar do começo do
            // seguinte, que é o que aconteceria se alguém tornasse o `Fim` inclusivo aqui.
            Assert.True(janelas[i - 1].Fim <= janelas[i].Inicio);
        }
    }

    // A grade do Er inteira, com o Radar entrando só na janela dele: sábado ganha 6 rodadas
    // de 2 quadras extras.
    [Fact]
    public void O_sabado_do_Er_ganha_12_vagas_com_o_radar()
    {
        var plano = PlanejamentoDeQuadras.Montar(
            inicio: new DateTime(2026, 9, 11, 18, 0, 0),
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0),
            limitePadrao: new TimeSpan(23, 50, 0),
            quadras: new[]
            {
                new Quadra { Nome = "Arena Nclass" },
                new Quadra { Nome = "Arena Loja 7" },
                new Quadra { Nome = "Radar 1", DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12).AddMinutes(10) },
                new Quadra { Nome = "Radar 2", DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12).AddMinutes(10) },
            },
            duracaoMinutos: 50, totalDeJogos: 87,
            ate: new DateTime(2026, 9, 13),
            limitesPorDia: PlanejamentoDeQuadras.LerLimites(null));

        Assert.Equal(16, plano.Dias[0].Vagas);        // sexta: só as duas de casa
        Assert.Equal(40 + 12, plano.Dias[1].Vagas);   // sábado: 20 rodadas × 2 + 6 × 2
        Assert.Equal(40, plano.Dias[2].Vagas);        // domingo: o radar já foi embora
    }
}
