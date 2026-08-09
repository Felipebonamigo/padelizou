using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "VOCÊ ESTÁ INSCRITO E AINDA NÃO PAGOU" (Felipe, 09/08/2026).
//
// ⚠️ O lembrete que já existia não servia: o do PagamentoExpiradoBackgroundService parte de uma
// FATURA pendente, e quem escolheu "pago depois" não tem fatura nenhuma. As 8 pessoas devendo no
// NATA PADEL TOUR ficariam em silêncio até o dia do fechamento.
public class LembreteDeInscricaoNaoPagaTests
{
    private static readonly DateTime Prazo = new(2026, 9, 21, 23, 59, 59);

    private static Torneio Torneio(
        string forma = FormaDePagamentoDoTorneio.TodasAsFormas,
        bool exigePagarNaHora = false,
        decimal preco = 125m,
        DateTime? cancelado = null,
        DateTime? fecha = null) => new()
    {
        Nome = "NATA PADEL TOUR", Codigo = "NATA1",
        FormaPagamento = forma,
        PrecoInscricao = preco,
        PagamentoObrigatorioNaInscricao = exigePagarNaHora,
        PrevisaoEncerramentoInscricoes = fecha ?? new DateTime(2026, 9, 21),
        CanceladoEm = cancelado,
    };

    // ── Quem entra na varredura ──────────────────────────────────────────────────────────────

    [Fact]
    public void Vigia_o_torneio_que_cobra_pelo_site_e_aceita_pagar_depois()
    {
        Assert.True(LembreteDeInscricaoNaoPaga.VigiaEsteTorneio(Torneio()));
    }

    [Fact]
    public void Nao_vigia_quem_exige_pagamento_na_inscricao()
    {
        // Ali não existe inscrição devendo: ela nasce paga.
        Assert.False(LembreteDeInscricaoNaoPaga.VigiaEsteTorneio(Torneio(exigePagarNaHora: true)));
    }

    [Fact]
    public void Nao_vigia_quem_recebe_por_fora()
    {
        // O site não conhece o combinado — cobrar ali seria falar de um acerto que não é nosso.
        Assert.False(LembreteDeInscricaoNaoPaga.VigiaEsteTorneio(Torneio(forma: FormaDePagamentoDoTorneio.Externo)));
    }

    [Fact]
    public void Nao_vigia_torneio_de_graca_nem_cancelado()
    {
        Assert.False(LembreteDeInscricaoNaoPaga.VigiaEsteTorneio(Torneio(preco: 0m)));
        Assert.False(LembreteDeInscricaoNaoPaga.VigiaEsteTorneio(Torneio(cancelado: DateTime.Now)));
    }

    [Fact]
    public void Sem_data_de_fechamento_nao_ha_prazo_pra_cobrar()
    {
        var semData = Torneio();
        semData.PrevisaoEncerramentoInscricoes = null;
        semData.PrazoPagamento = null;

        Assert.False(LembreteDeInscricaoNaoPaga.VigiaEsteTorneio(semData));
    }

    // ── Quando avisa ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Longe_do_prazo_nao_se_avisa_ninguem()
    {
        // 43 dias antes: a pessoa acabou de se inscrever, cobrar agora é cobrar por cobrar.
        Assert.Null(LembreteDeInscricaoNaoPaga.MarcoDevido(Prazo, new DateTime(2026, 8, 9, 10, 0, 0), null));
    }

    [Fact]
    public void A_sete_dias_sai_o_primeiro_aviso()
    {
        Assert.Equal(7, LembreteDeInscricaoNaoPaga.MarcoDevido(Prazo, new DateTime(2026, 9, 14, 10, 0, 0), null));
    }

    [Fact]
    public void O_mesmo_marco_nao_sai_duas_vezes()
    {
        // ⚠️ O varredor roda de hora em hora. Sem esta guarda, o aviso de 7 dias sairia 12 vezes
        // no mesmo dia — e a pessoa desligaria as notificações, que é pior que não ter avisado.
        var seteDiasAntes = new DateTime(2026, 9, 14, 10, 0, 0);

        Assert.Null(LembreteDeInscricaoNaoPaga.MarcoDevido(Prazo, seteDiasAntes, ultimoMarcoEnviado: 7));
        Assert.Null(LembreteDeInscricaoNaoPaga.MarcoDevido(Prazo, seteDiasAntes.AddHours(1), ultimoMarcoEnviado: 7));
        Assert.Null(LembreteDeInscricaoNaoPaga.MarcoDevido(Prazo, new DateTime(2026, 9, 16, 9, 0, 0), 7));
    }

    [Fact]
    public void A_dois_dias_sai_o_segundo_aviso()
    {
        Assert.Equal(2, LembreteDeInscricaoNaoPaga.MarcoDevido(Prazo, new DateTime(2026, 9, 19, 10, 0, 0), 7));
    }

    [Fact]
    public void Depois_do_ultimo_marco_o_silencio_volta()
    {
        // Dois avisos e pronto. Insistir todo dia até o prazo é perseguição, não lembrete.
        Assert.Null(LembreteDeInscricaoNaoPaga.MarcoDevido(Prazo, new DateTime(2026, 9, 21, 10, 0, 0), 2));
    }

    [Fact]
    public void Quem_se_inscreve_em_cima_da_hora_leva_UM_aviso_so()
    {
        // ⚠️ O caso que quebraria a versão ingênua: faltando 1 dia, os marcos de 7 e de 2 estão
        // os dois vencidos. Pegando o primeiro da lista, a pessoa levaria o de 7 num tick e o de
        // 2 no seguinte — dois avisos em uma hora. Vale o MAIS URGENTE, e os outros são dados
        // por cumpridos.
        var vespera = new DateTime(2026, 9, 20, 10, 0, 0);

        Assert.Equal(2, LembreteDeInscricaoNaoPaga.MarcoDevido(Prazo, vespera, null));
        Assert.Null(LembreteDeInscricaoNaoPaga.MarcoDevido(Prazo, vespera.AddHours(1), ultimoMarcoEnviado: 2));
    }

    [Fact]
    public void Passado_o_prazo_o_robo_cala_a_boca()
    {
        // Cobrar depois do prazo é conversa do organizador com a pessoa, não de um robô às 9h.
        Assert.Null(LembreteDeInscricaoNaoPaga.MarcoDevido(Prazo, new DateTime(2026, 9, 22, 10, 0, 0), null));
    }

    // ── Educação básica ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ninguem_e_acordado_de_madrugada_pra_ser_cobrado()
    {
        Assert.False(LembreteDeInscricaoNaoPaga.HoraCivilizada(new DateTime(2026, 9, 14, 3, 0, 0)));
        Assert.False(LembreteDeInscricaoNaoPaga.HoraCivilizada(new DateTime(2026, 9, 14, 23, 0, 0)));
        Assert.True(LembreteDeInscricaoNaoPaga.HoraCivilizada(new DateTime(2026, 9, 14, 9, 0, 0)));
        Assert.True(LembreteDeInscricaoNaoPaga.HoraCivilizada(new DateTime(2026, 9, 14, 20, 59, 0)));
    }

    [Fact]
    public void O_texto_conta_os_dias_de_VERDADE_nunca_o_numero_do_marco()
    {
        // ⚠️ Quem se inscreve na véspera entra pelo marco de 7. Dizer "faltam 7 dias" seria uma
        // mentira com hora marcada — e a pessoa deixaria pra depois um prazo que acaba amanhã.
        var frase = LembreteDeInscricaoNaoPaga.Frase("NATA PADEL TOUR", 125m, Prazo, new DateTime(2026, 9, 20, 10, 0, 0));

        Assert.Contains("Falta 1 dia", frase);
        Assert.DoesNotContain("7", frase);
        Assert.Contains("21/09", frase);
        Assert.Contains("NATA PADEL TOUR", frase);
    }

    [Fact]
    public void No_ultimo_dia_o_texto_diz_que_e_o_ultimo_dia()
    {
        var frase = LembreteDeInscricaoNaoPaga.Frase("NATA PADEL TOUR", 125m, Prazo, new DateTime(2026, 9, 21, 10, 0, 0));

        Assert.Contains("último dia", frase);
    }

    [Fact]
    public void O_texto_traz_o_valor_em_dinheiro_de_verdade()
    {
        var frase = LembreteDeInscricaoNaoPaga.Frase("NATA PADEL TOUR", 125m, Prazo, new DateTime(2026, 9, 14, 10, 0, 0));

        Assert.Contains("125", frase);
        Assert.Contains("Faltam 7 dias", frase);
    }
}
