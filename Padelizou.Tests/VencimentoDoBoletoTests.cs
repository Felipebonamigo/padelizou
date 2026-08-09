using Padelizou.Services;

namespace Padelizou.Tests;

// O boleto vencia AMANHÃ (08/08/2026).
//
// O vencimento era um `AddDays(1)` cravado, igual pros três meios de pagamento. Boleto leva
// 1 dia útil só pra compensar depois de pago: emitido numa sexta com vencimento no sábado,
// nasce morto — e vencido vira PAYMENT_OVERDUE, que o webhook grava como "Cancelado". A
// inscrição simplesmente não acontece, pelo caminho NORMAL do boleto.
public class VencimentoDoBoletoTests
{
    private static readonly DateTime UmaSexta = new(2026, 8, 7);

    [Fact]
    public void Boleto_nao_vence_amanha()
    {
        var vencimento = VencimentoDaCobranca.Para("BOLETO", UmaSexta);

        // O que a tela promete pro jogador é "vence em alguns dias" — era o texto que estava
        // certo e o código que estava errado.
        Assert.Equal(new DateTime(2026, 8, 10), vencimento);
        Assert.True(vencimento > UmaSexta.AddDays(1));
    }

    [Fact]
    public void Pix_e_cartao_seguem_em_um_dia()
    {
        // Não é descuido: nos dois o dinheiro é resolvido na hora, e o vencimento serve só
        // pra não deixar a vaga pendurada em quem desistiu no meio do checkout.
        Assert.Equal(UmaSexta.AddDays(1), VencimentoDaCobranca.Para("PIX", UmaSexta));
        Assert.Equal(UmaSexta.AddDays(1), VencimentoDaCobranca.Para("CREDIT_CARD", UmaSexta));
    }

    [Fact]
    public void Forma_em_aberto_ganha_o_prazo_do_boleto()
    {
        // "UNDEFINED" é o jogador escolhendo lá no meio de pagamento — e o boleto é uma das
        // opções que ele pode abrir. Um dia tiraria a opção sem avisar ninguém.
        Assert.Equal(new DateTime(2026, 8, 10), VencimentoDaCobranca.Para("UNDEFINED", UmaSexta));
    }

    [Fact]
    public void A_hora_do_dia_nao_encurta_o_prazo()
    {
        // Cobrança criada às 23h58 tem que valer o mesmo prazo da criada às 8h — o vencimento
        // é uma DATA, e arrastar a hora junto tiraria um dia de quem se inscreveu à noite.
        var tarde = new DateTime(2026, 8, 7, 23, 58, 0);

        Assert.Equal(new DateTime(2026, 8, 10), VencimentoDaCobranca.Para("BOLETO", tarde));
    }

    // ── OS DOIS RELÓGIOS (09/08/2026) ────────────────────────────────────────────────────────
    // O torneio que aceita "pagar até o fechamento" esticava só o NOSSO relógio; o vencimento
    // que ia pro gateway continuava sendo hoje+1. A tela prometia "Pague até 21/09" e a fatura
    // vencia no dia seguinte — vencida, PAYMENT_OVERDUE, pagamento "Cancelado" aqui.

    [Fact]
    public void O_prazo_do_torneio_manda_no_vencimento_da_fatura()
    {
        var fechamento = new DateTime(2026, 9, 21);

        Assert.Equal(fechamento, VencimentoDaCobranca.Para("PIX", UmaSexta, fechamento));
        Assert.Equal(fechamento, VencimentoDaCobranca.Para("BOLETO", UmaSexta, fechamento));
        Assert.Equal(fechamento, VencimentoDaCobranca.Para("UNDEFINED", UmaSexta, fechamento));
    }

    [Fact]
    public void Sem_prazo_combinado_nada_muda()
    {
        // Quadra, aula e mensalidade não passam prazo nenhum: ali o valor fica reservado e a
        // regra de sempre continua valendo.
        Assert.Equal(new DateTime(2026, 8, 10), VencimentoDaCobranca.Para("BOLETO", UmaSexta, null));
        Assert.Equal(UmaSexta.AddDays(1), VencimentoDaCobranca.Para("PIX", UmaSexta, null));
    }

    [Fact]
    public void O_prazo_combinado_so_ESTICA_nunca_encurta()
    {
        // ⚠️ Fechamento amanhã não pode encurtar o boleto pra amanhã: ele nasceria morto, que é
        // exatamente o defeito que este arquivo existe pra impedir. Custa aceitar pagamento
        // dois dias depois do fechamento — barato perto de emitir papel que o banco não aceita.
        var fechaAmanha = UmaSexta.AddDays(1);

        Assert.Equal(new DateTime(2026, 8, 10), VencimentoDaCobranca.Para("BOLETO", UmaSexta, fechaAmanha));
        Assert.Equal(UmaSexta.AddDays(1), VencimentoDaCobranca.Para("PIX", UmaSexta, fechaAmanha));
    }

    [Fact]
    public void A_hora_do_prazo_combinado_nao_entra_no_vencimento()
    {
        // PrazoParaPagar.Ate devolve o FIM DO DIA (23:59:59.999…) pra quem tem até dia 21 ter o
        // dia 21 inteiro. O gateway quer uma data, e mandar a hora junto arriscaria arredondar
        // pro dia seguinte.
        var fimDoDia21 = new DateTime(2026, 9, 21).AddDays(1).AddTicks(-1);

        Assert.Equal(new DateTime(2026, 9, 21), VencimentoDaCobranca.Para("PIX", UmaSexta, fimDoDia21));
    }
}
