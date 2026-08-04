using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A chave direta é montada pelo organizador com gente que JÁ está inscrita (e paga) na
// categoria dela. Se ela contasse dinheiro, um mata-mata paralelo de 24 duplas dobraria o
// arrecadado da tela E dobraria a taxa de 5% cobrada do organizador — a mesma pessoa
// faturada duas vezes pelo mesmo torneio. Mesma regra que já valia pra categoria de TIMES.
public class ChaveDiretaNaoContaDinheiroTests
{
    [Fact]
    public async Task Caderneta_do_por_fora_ignora_as_duplas_da_chave_direta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarTorneioExterno(ctx, duplasNaCategoria: 2, duplasNaChaveDireta: 3);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        var vm = ((await controller.Financeiro(torneio.Id)) as ViewResult)!.Model as FinanceiroTorneioVM;

        Assert.NotNull(vm);
        Assert.Equal(2, vm!.CobrancaPorFora.Count);                       // só as 2 da categoria
        Assert.All(vm.CobrancaPorFora, c => Assert.NotEqual("Mata-Mata", c.Categoria));
        Assert.Equal(240m, vm.ArrecadadoNaTela);                          // 2 duplas x 2 pessoas x R$ 60
    }

    [Fact]
    public async Task Taxa_do_padelizou_nao_cobra_de_novo_por_quem_joga_a_chave_direta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarTorneioExterno(ctx, duplasNaCategoria: 2, duplasNaChaveDireta: 3);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        var view = (await controller.TaxaPlataforma(torneio.Id)) as ViewResult;

        Assert.NotNull(view);
        // 4 pessoas (as 2 duplas da categoria). Sem o filtro seriam 10, e a taxa 2,5x maior.
        Assert.Equal(4, view!.ViewData["Pessoas"]);
    }

    private static (Torneio torneio, Jogador organizador) MontarTorneioExterno(
        DbPadelContext ctx, int duplasNaCategoria, int duplasNaChaveDireta)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Interno",
            Codigo = "INT1",
            Status = "Chaves em Sorteio",
            FormaPagamento = "Externo",
            PrecoInscricao = 60m,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "6ª", Codigo = "C6", Torneio = torneio };
        var chave = new Categoria { Nome = "Mata-Mata", Codigo = "MM", Torneio = torneio, ChaveDireta = true };
        ctx.Categorias.AddRange(categoria, chave);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        int n = 0;
        void Inscrever(Categoria cat, int quantas)
        {
            for (int i = 0; i < quantas; i++)
            {
                var j1 = TestInfra.NovoJogador(++n);
                var j2 = TestInfra.NovoJogador(++n);
                ctx.Jogadores.AddRange(j1, j2);
                ctx.Duplas.Add(new Dupla { Categoria = cat, Jogador1 = j1, Jogador2 = j2, Pago = true });
            }
        }

        Inscrever(categoria, duplasNaCategoria);
        Inscrever(chave, duplasNaChaveDireta);
        ctx.SaveChanges();

        return (torneio, organizador);
    }
}
