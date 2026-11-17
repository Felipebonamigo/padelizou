using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;

namespace Padelizou.Tests;

// O CO-ORGANIZADOR VÊ E MARCA O PAGO DE CADA JOGADOR NA LISTA DE INSCRITOS (17/11/2026).
//
// 🗣️ Rafael Paim (Wallau), encaminhado pelo Felipe: *"Na tela do vini não aparece a flag pra
// marcar só um da dupla como pago"*. O Vini organiza junto; a tela escondia o selo, o ✓, a parte
// e o "Cobrar" de cada jogador atrás da régua do CAIXA (`PodeVerDinheiro`: só Criador/Total) —
// enquanto o servidor (`AlternarPagamentoJogador`) já aceitava qualquer organizador.
//
// Decisão do Felipe (17/11/2026), entre "sem valores", "tudo" e "como está": *TUDO* da lista de
// inscritos pro co-organizador — selo, ✓, valor da parte e o Cobrar no WhatsApp. O resto do caixa
// (Financeiro: entrou, falta, líquido; relatório CSV; taxa) continua de quem criou.
public class CoOrganizadorMarcaPorJogadorTests
{
    private static (Torneio Torneio, Jogador Criador, Jogador CoOrganizador) Cenario(DbPadelContext ctx)
    {
        var (torneio, _, criador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        torneio.FormaPagamento = "Externo";
        torneio.PrecoInscricao = 60m;
        ctx.TorneioOrganizadores.Single(o => o.TorneioId == torneio.Id).NivelAcesso = "Criador";

        var vini = new Jogador { Nome = "Vini Co-organizador", Cpf = "11144477735" };
        ctx.Jogadores.Add(vini);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = vini.Id, NivelAcesso = "Organizador",
        });
        ctx.SaveChanges();
        return (torneio, criador, vini);
    }

    [Fact]
    public async Task Co_organizador_recebe_o_pago_por_jogador_na_lista_de_inscritos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, vini) = Cenario(ctx);

        var controller = TestInfra.NovoTorneiosController(ctx, vini.Id);
        Assert.IsType<ViewResult>(await controller.Details(torneio.Id, null, null));

        Assert.True((controller.ViewBag.PodeMarcarPorJogador as bool?) == true);
        Assert.NotNull((object?)controller.ViewBag.PagamentoDosJogadores);
        // E o caixa também, desde que o co-organizador passou a ver igual a quem criou
        // (CoOrganizadorVeODinheiroTests).
        Assert.True((controller.ViewBag.PodeVerDinheiro as bool?) == true);
    }

    [Fact]
    public async Task Quem_criou_continua_vendo_tudo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, criador, _) = Cenario(ctx);

        var controller = TestInfra.NovoTorneiosController(ctx, criador.Id);
        Assert.IsType<ViewResult>(await controller.Details(torneio.Id, null, null));

        Assert.True((controller.ViewBag.PodeMarcarPorJogador as bool?) == true);
        Assert.NotNull((object?)controller.ViewBag.PagamentoDosJogadores);
        Assert.True((controller.ViewBag.PodeVerDinheiro as bool?) == true);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_recebe_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = Cenario(ctx);
        var curioso = new Jogador { Nome = "Curioso", Cpf = "52998224725" };
        ctx.Jogadores.Add(curioso);
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, curioso.Id);
        Assert.IsType<ViewResult>(await controller.Details(torneio.Id, null, null));

        Assert.False((controller.ViewBag.PodeMarcarPorJogador as bool?) == true);
        Assert.Null((object?)controller.ViewBag.PagamentoDosJogadores);
    }
}
