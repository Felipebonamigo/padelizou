using Padelizou.Models;

namespace Padelizou.Tests;

// Quem pagou e quem não pagou, por inscrição. O organizador vira o status na mão porque
// muita inscrição é acertada em Pix direto ou dinheiro na quadra — o site não sabe disso.
public class StatusPagoInscricaoTests
{
    [Fact]
    public void Inscricao_nasce_nao_paga()
    {
        var dupla = new Dupla();
        var americano = new InscricaoAmericana();

        Assert.False(dupla.Pago);
        Assert.Null(dupla.PagoEm);
        Assert.False(americano.Pago);
    }

    [Fact]
    public void Torneio_novo_exige_pagamento_na_inscricao()
    {
        // É o comportamento que já existia, e o que a migração grava nos torneios antigos:
        // sem pagar, sem vaga. Mudar o padrão viraria a regra de quem já estava cobrando.
        var torneio = new Torneio { Nome = "T", Codigo = "T1" };

        Assert.True(torneio.PagamentoObrigatorioNaInscricao);
        Assert.Null(torneio.PrazoPagamento);
        Assert.False(torneio.ExcluirSeNaoPagar);
    }

    [Fact]
    public void Tirar_alguem_do_torneio_nunca_e_o_padrao()
    {
        // Excluir uma inscrição é grave demais pra acontecer sem o organizador ter pedido.
        Assert.False(new Torneio { Nome = "T", Codigo = "T1" }.ExcluirSeNaoPagar);
    }

    [Fact]
    public void Organizador_marca_e_desmarca_o_pagamento()
    {
        using var ctx = TestInfra.NovoContexto();
        var dupla = new Dupla { CategoriaId = 1, Jogador1Id = 1 };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();

        // Mesma lógica das ações AlternarPagamento* do TorneiosController.
        dupla.Pago = !dupla.Pago;
        dupla.PagoEm = dupla.Pago ? new DateTime(2026, 7, 27, 10, 0, 0) : null;
        ctx.SaveChanges();

        Assert.True(dupla.Pago);
        Assert.NotNull(dupla.PagoEm);

        dupla.Pago = !dupla.Pago;
        dupla.PagoEm = dupla.Pago ? DateTime.Now : null;
        ctx.SaveChanges();

        Assert.False(dupla.Pago);
        Assert.Null(dupla.PagoEm);   // desmarcar limpa a data, senão fica mentindo
    }
}
