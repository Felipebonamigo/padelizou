using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "PAGAR ATÉ O FECHAMENTO DAS INSCRIÇÕES" (Felipe, 08/08/2026).
//
// ⚠️ Nasceu de um estrago real. No NATA PADEL TOUR, com "só confirmo depois de pago" ligado,
// OITO pessoas se inscreveram e sumiram: a cobrança tinha 60 minutos de vida, o varredor a
// marcava "Expirado", e nesse arranjo a inscrição só nasce no pagamento — não sobrava NADA,
// nem uma linha na lista. Quem abriu o checkout às 20h15 e voltou às 22h tinha perdido a vaga
// sem nunca ser avisado.
public class PrazoParaPagarTests
{
    private static Torneio Torneio(bool exigePagarNaHora, DateTime? fecha = null, DateTime? prazo = null) => new()
    {
        Nome = "NATA PADEL TOUR", Codigo = "NATA1", PrecoInscricao = 125m,
        PagamentoObrigatorioNaInscricao = exigePagarNaHora,
        PrevisaoEncerramentoInscricoes = fecha,
        PrazoPagamento = prazo,
    };

    [Fact]
    public void Pagar_na_hora_segue_a_regra_de_sempre()
    {
        // Nulo = o serviço não opina, e vale o prazo curto do gateway. É o certo aqui: nesse
        // arranjo o valor prende a vaga, e ela não pode ficar pendurada a noite toda.
        Assert.Null(PrazoParaPagar.Ate(Torneio(exigePagarNaHora: true, fecha: new DateTime(2026, 9, 21))));
    }

    [Fact]
    public void Pagar_depois_vale_ate_o_fim_do_dia_do_fechamento()
    {
        // ⚠️ Fim do DIA, não a meia-noite do começo: quem tem até 21/09 tem o dia 21 inteiro.
        var ate = PrazoParaPagar.Ate(Torneio(exigePagarNaHora: false, fecha: new DateTime(2026, 9, 21)));

        Assert.NotNull(ate);
        Assert.Equal(new DateTime(2026, 9, 21), ate!.Value.Date);
        Assert.True(ate.Value > new DateTime(2026, 9, 21, 23, 59, 0));
    }

    [Fact]
    public void O_prazo_digitado_a_mao_manda_no_encerramento()
    {
        var ate = PrazoParaPagar.Ate(Torneio(
            exigePagarNaHora: false,
            fecha: new DateTime(2026, 9, 21),
            prazo: new DateTime(2026, 9, 15)));

        Assert.Equal(new DateTime(2026, 9, 15), ate!.Value.Date);
    }

    [Fact]
    public void Sem_data_nenhuma_nao_se_promete_prazo()
    {
        // Prometer "até o fechamento" sem ninguém ter marcado o fechamento seria promessa
        // vazia — e a tela pede a data em vez de inventar uma.
        var torneio = Torneio(exigePagarNaHora: false);

        Assert.Null(PrazoParaPagar.Ate(torneio));
        Assert.Null(PrazoParaPagar.Frase(torneio));
    }

    [Fact]
    public void A_frase_da_tela_traz_a_data()
    {
        var frase = PrazoParaPagar.Frase(Torneio(exigePagarNaHora: false, fecha: new DateTime(2026, 9, 21)));

        Assert.Equal("Pague até 21/09", frase);
    }

    // ── O prazo é RECADO PESSOAL ─────────────────────────────────────────────────────────────
    // Felipe, 09/08/2026, olhando a lista do NATA PADEL TOUR: "só mostre esse 'pague até 21/09'
    // para o usuário que deve, não para todos". O selo "Pagamento pendente" é informação do
    // torneio; a ORDEM de pagar é de uma pessoa só.

    [Fact]
    public void Quem_deve_ve_o_prazo()
    {
        var frase = PrazoParaPagar.FraseParaODono(
            Torneio(exigePagarNaHora: false, fecha: new DateTime(2026, 9, 21)),
            souDaInscricao: true, pago: false);

        Assert.Equal("Pague até 21/09", frase);
    }

    [Fact]
    public void Quem_esta_so_olhando_a_lista_NAO_ve_o_prazo_dos_outros()
    {
        // Pendurado no nome alheio, o aviso não informa nada a quem lê e cobra em público quem
        // ainda não pagou.
        Assert.Null(PrazoParaPagar.FraseParaODono(
            Torneio(exigePagarNaHora: false, fecha: new DateTime(2026, 9, 21)),
            souDaInscricao: false, pago: false));
    }

    [Fact]
    public void Quem_ja_pagou_nao_recebe_cobranca_nenhuma()
    {
        Assert.Null(PrazoParaPagar.FraseParaODono(
            Torneio(exigePagarNaHora: false, fecha: new DateTime(2026, 9, 21)),
            souDaInscricao: true, pago: true));
    }

    [Fact]
    public void A_cobranca_de_quem_paga_depois_dura_MUITO_mais_que_os_60_minutos()
    {
        // O teste que amarra a lição: era o prazo de uma hora que matava a inscrição.
        var torneio = Torneio(exigePagarNaHora: false, fecha: DateTime.Today.AddDays(30));

        var ate = PrazoParaPagar.Ate(torneio);

        Assert.NotNull(ate);
        Assert.True(ate!.Value > DateTime.Now.AddHours(1),
            "a cobrança de quem pode pagar depois não pode morrer em uma hora");
    }
}
