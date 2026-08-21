using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Traduz Dupla.ImpedimentoQuintaNoite/SextaNoite/SabadoManha/SabadoTarde em janelas de
// data/hora reais. Até 21/08/2026 esses quatro campos só alimentavam preço e exibição —
// GradeDeJogos.Encaixar nunca os lia, então a dupla pagava pela flexibilidade e podia ser
// escalada exatamente na janela que pagou pra evitar. Ver GradeDeJogosTests para o lado do
// Encaixar (o que acontece quando a janela existe).
public class JanelasDeImpedimentoTests
{
    // Sábado, 15/08/2026 — mesma data que GradeDeJogosTests usa, pra reaproveitar intuição.
    private static readonly DateTime InicioSabado = new(2026, 8, 15, 8, 0, 0);

    private static Torneio Torneio(DateTime? inicio, bool quintaEhDiaDoTorneio = false) => new()
    {
        DataInicio = inicio,
        PermiteImpedimentoQuintaNoite = quintaEhDiaDoTorneio,
    };

    private static Dupla Dupla(bool quinta = false, bool sexta = false, bool sabManha = false, bool sabTarde = false) => new()
    {
        ImpedimentoQuintaNoite = quinta,
        ImpedimentoSextaNoite = sexta,
        ImpedimentoSabadoManha = sabManha,
        ImpedimentoSabadoTarde = sabTarde,
    };

    [Fact]
    public void Sem_impedimento_nenhum_nao_ha_janela()
    {
        var janelas = JanelasDeImpedimento.Da(Torneio(InicioSabado), Dupla()).ToList();

        Assert.Empty(janelas);
    }

    [Fact]
    public void Torneio_sem_data_nao_gera_janela_mesmo_com_impedimento_marcado()
    {
        var janelas = JanelasDeImpedimento.Da(Torneio(null), Dupla(sexta: true)).ToList();

        Assert.Empty(janelas);
    }

    [Fact]
    public void Sexta_a_noite_bloqueia_a_sexta_inteira()
    {
        var inicioSexta = new DateTime(2026, 8, 14, 18, 0, 0);   // sexta — o torneio ABRE nela

        var janelas = JanelasDeImpedimento.Da(Torneio(inicioSexta), Dupla(sexta: true)).ToList();

        var sexta = new DateTime(2026, 8, 14);
        Assert.Equal((sexta, sexta.AddDays(1)), Assert.Single(janelas));
    }

    [Fact]
    public void Torneio_que_comeca_no_sabado_nao_gera_janela_de_sexta()
    {
        // Sem sexta no calendário do torneio, marcar o impedimento não pode inventar uma
        // janela numa sexta que não existe — nem a de uma semana antes nem a de depois.
        var janelas = JanelasDeImpedimento.Da(Torneio(InicioSabado), Dupla(sexta: true)).ToList();

        Assert.Empty(janelas);
    }

    [Fact]
    public void Sabado_de_manha_bloqueia_ate_o_meio_dia()
    {
        var janelas = JanelasDeImpedimento.Da(Torneio(InicioSabado), Dupla(sabManha: true)).ToList();

        var sabado = new DateTime(2026, 8, 15);
        Assert.Equal((sabado, sabado.Add(JanelasDeImpedimento.CorteSabadoManhaTarde)), Assert.Single(janelas));
    }

    [Fact]
    public void Sabado_a_tarde_bloqueia_do_meio_dia_ate_o_fim_do_dia()
    {
        var janelas = JanelasDeImpedimento.Da(Torneio(InicioSabado), Dupla(sabTarde: true)).ToList();

        var sabado = new DateTime(2026, 8, 15);
        Assert.Equal(
            (sabado.Add(JanelasDeImpedimento.CorteSabadoManhaTarde), sabado.AddDays(1)),
            Assert.Single(janelas));
    }

    [Fact]
    public void Sabado_inteiro_marcado_vira_duas_janelas_que_se_encostam_sem_buraco()
    {
        var janelas = JanelasDeImpedimento.Da(Torneio(InicioSabado), Dupla(sabManha: true, sabTarde: true))
            .OrderBy(j => j.Inicio).ToList();

        Assert.Equal(2, janelas.Count);
        // A manhã termina exatamente onde a tarde começa — nenhum minuto do sábado escapa.
        Assert.Equal(janelas[0].Fim, janelas[1].Inicio);
    }

    [Fact]
    public void Quinta_so_bloqueia_quando_o_torneio_realmente_comeca_na_quinta()
    {
        // Torneio de fim de semana comum (sexta/sábado/domingo): quinta não é dia do
        // torneio, então marcar o impedimento não deveria gerar janela nenhuma — não existe
        // turno de quinta pra bloquear.
        var torneioDeFimDeSemana = Torneio(InicioSabado, quintaEhDiaDoTorneio: false);

        var janelas = JanelasDeImpedimento.Da(torneioDeFimDeSemana, Dupla(quinta: true)).ToList();

        Assert.Empty(janelas);
    }

    [Fact]
    public void Quinta_bloqueia_o_dia_inteiro_quando_o_torneio_comeca_na_quinta()
    {
        var quinta = new DateTime(2026, 8, 13, 18, 0, 0);
        var torneioDaQuinta = Torneio(quinta, quintaEhDiaDoTorneio: true);

        var janelas = JanelasDeImpedimento.Da(torneioDaQuinta, Dupla(quinta: true)).ToList();

        var diaQuinta = new DateTime(2026, 8, 13);
        Assert.Equal((diaQuinta, diaQuinta.AddDays(1)), Assert.Single(janelas));
    }

    [Fact]
    public void PorDupla_ignora_quem_nao_tem_impedimento_nenhum()
    {
        var torneio = Torneio(InicioSabado);
        var comImpedimento = new Dupla { Id = 1, ImpedimentoSabadoManha = true };
        var semImpedimento = new Dupla { Id = 2 };

        var mapa = JanelasDeImpedimento.PorDupla(torneio, new[] { comImpedimento, semImpedimento });

        Assert.True(mapa.ContainsKey(1));
        Assert.False(mapa.ContainsKey(2));
    }
}
