using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// TRETA (25/11/2026): categoria com papel (DTN ou Finalíssima) é montada pelo robô com gente
// que JÁ está inscrita e paga no módulo. Ela não pode virar dinheiro (arrecadado, taxa de 5%),
// nem contagem de inscritos, nem porta de inscrição — e o "Campeao" dela, se algum dia for
// gravado, não pode virar candidato a MVP. Mesmo tratamento da chave direta.
public class TretaForaDoDinheiroEDaInscricaoTests
{
    private static (Torneio torneio, Jogador organizador, Categoria modulo, Categoria finalissima) MontarTorneioExterno(
        DbPadelContext ctx, string status = "Chaves em Sorteio")
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Treta", Codigo = "TRETA1", Status = status,
            FormaPagamento = "Externo", PrecoInscricao = 60m,
        };
        ctx.Torneios.Add(torneio);

        var modulo = new Categoria { Nome = "Branco", Codigo = "BR", Torneio = torneio };
        var finalissima = new Categoria
        {
            Nome = "Finalíssima DTN", Codigo = "FDTN", Torneio = torneio, PapelDaChave = ChaveDtn.Finalissima,
        };
        ctx.Categorias.AddRange(modulo, finalissima);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = organizador.Id, NivelAcesso = "Criador",
        });

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

        Inscrever(modulo, 2);
        Inscrever(finalissima, 2);
        ctx.SaveChanges();
        return (torneio, organizador, modulo, finalissima);
    }

    [Fact]
    public async Task Caderneta_do_por_fora_ignora_as_duplas_da_finalissima()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, _, _) = MontarTorneioExterno(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        var vm = ((await controller.Financeiro(torneio.Id)) as ViewResult)!.Model as FinanceiroTorneioVM;

        Assert.NotNull(vm);
        Assert.Equal(2, vm!.CobrancaPorFora.Count);
        Assert.Equal(240m, vm.ArrecadadoNaTela);
    }

    [Fact]
    public async Task Taxa_do_padelizou_nao_cobra_pela_finalissima()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, _, _) = MontarTorneioExterno(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        var view = (await controller.TaxaPlataforma(torneio.Id)) as ViewResult;

        Assert.NotNull(view);
        Assert.Equal(4, view!.ViewData["Pessoas"]);
    }

    [Fact]
    public async Task Contagem_de_inscritos_ignora_a_finalissima()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _, _) = MontarTorneioExterno(ctx);
        var categorias = await ctx.Categorias.Include(c => c.Duplas).Where(c => c.TorneioId == torneio.Id).ToListAsync();

        var contagem = QuantosInscritos.Contar(torneio, categorias, Array.Empty<InscricaoAmericana>());

        Assert.Equal(2, contagem.Duplas);
    }

    [Fact]
    public async Task Ninguem_se_inscreve_como_dupla_numa_categoria_com_papel()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _, finalissima) = MontarTorneioExterno(ctx, status: "Inscrições Abertas");
        var a = new Jogador { Nome = "Felipe Bonamigo", Cpf = "11144477735" };
        var b = new Jogador { Nome = "Maickel Klein", Cpf = "52998224725" };
        ctx.Jogadores.AddRange(a, b);
        await ctx.SaveChangesAsync();
        int antes = await ctx.Duplas.CountAsync(d => d.CategoriaId == finalissima.Id);

        var controller = TestInfra.NovoDuplasController(ctx, a.Id);
        await controller.Create(
            torneioId: torneio.Id, categoriaId: finalissima.Id,
            nome1: a.Nome, cpf1: a.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false);

        Assert.Equal(antes, await ctx.Duplas.CountAsync(d => d.CategoriaId == finalissima.Id));
        Assert.Contains("montada pelo organizador", (string?)controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Ninguem_se_inscreve_sozinho_numa_categoria_com_papel()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _, finalissima) = MontarTorneioExterno(ctx, status: "Inscrições Abertas");
        var a = new Jogador { Nome = "Felipe Bonamigo", Cpf = "11144477735" };
        ctx.Jogadores.Add(a);
        await ctx.SaveChangesAsync();
        int antes = await ctx.Duplas.CountAsync(d => d.CategoriaId == finalissima.Id);

        var controller = TestInfra.NovoTorneiosController(ctx, a.Id);
        await controller.InscreverIndividual(torneio.Id, finalissima.Id, a.Nome, a.Cpf);

        Assert.Equal(antes, await ctx.Duplas.CountAsync(d => d.CategoriaId == finalissima.Id));
        Assert.Contains("montada pelo organizador", (string?)controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Campeao_de_categoria_com_papel_nao_entra_na_cedula_do_mvp()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, modulo, finalissima) = MontarTorneioExterno(ctx);
        var campeaDoModulo = await ctx.Duplas.FirstAsync(d => d.CategoriaId == modulo.Id);
        var vencedoraDaFinalissima = await ctx.Duplas.FirstAsync(d => d.CategoriaId == finalissima.Id);
        campeaDoModulo.UltimaFase = "Campeao";
        vencedoraDaFinalissima.UltimaFase = "Campeao";
        await ctx.SaveChangesAsync();

        var candidatos = await MvpDoTorneio.CandidatosAsync(ctx, torneio.Id);

        Assert.Contains(candidatos, c => c.JogadorId == campeaDoModulo.Jogador1Id);
        Assert.DoesNotContain(candidatos, c => c.JogadorId == vencedoraDaFinalissima.Jogador1Id);
    }
}
