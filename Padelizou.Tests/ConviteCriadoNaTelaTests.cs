using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// 09/11/2026 — "CONVIDAR POR LINK" GERAVA O LINK FORA DA TELA.
//
// 🗣️ Deivid (inscrito sozinho, pagamento pendente): *"o botão de convidar por link não tá
// funcionando"* · *"vai lá pra cima e nada"* · *"tentei no app e no chrome"*.
//
// 🔬 REPRODUZIDO (Chromium, 390×844, modo Produção): o link ERA criado. A página voltava pro topo,
// e o quadro "Link do convite criado" mora DEPOIS do bloco do Pix do organizador — que aparece
// justamente pra quem deve, como o Deivid. Com Pix e recado, o quadro caía em y=830 numa tela de
// 844: a pessoa via o Pix, não via link nenhum, e concluía que o botão não fazia nada.
//
// A saída: o redirect leva a âncora do próprio quadro, e o navegador abre a página já nele.
public class ConviteCriadoNaTelaTests
{
    [Fact]
    public async Task Gerar_o_convite_volta_ja_rolado_ate_o_link()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        var sozinho = TestInfra.NovoJogador(40);
        ctx.Jogadores.Add(sozinho);
        ctx.SaveChanges();
        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = sozinho.Id };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoDuplasController(ctx, sozinho.Id).GerarConvite(dupla.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("conviteCriado", redirect.Fragment);
        Assert.NotNull((await ctx.Duplas.SingleAsync(d => d.Id == dupla.Id)).ConviteToken);
    }

    [Fact]
    public void O_quadro_do_link_tem_a_ancora()
    {
        var details = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Torneios", "Details.cshtml"));
        var quadro = details.IndexOf("Link do convite criado", StringComparison.Ordinal);
        var ancora = details.LastIndexOf("id=\"conviteCriado\"", quadro, StringComparison.Ordinal);

        Assert.True(quadro > 0 && ancora > 0 && quadro - ancora < 400,
            "O id=\"conviteCriado\" tem que estar no quadro \"Link do convite criado\".");
    }
}
