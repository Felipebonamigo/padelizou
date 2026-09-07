using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// RELATÓRIO EM CSV DAS DUPLAS — aba Pagamentos. Pedido do Felipe (07/09/2026): "crie um
// botão com um relatório em excel, com nome completo, telefone, se pagou ou não, se tem
// impedimento e quando".
//
// Uma linha por DUPLA (não por jogador) — nome dos dois juntos, telefone de um deles (o
// Jogador1, mesmo critério que o botão "Cobrar" já usa).
//
// ⚠️ ATRÁS DE PodeVerDinheiro, e não de PodeGerenciar: é o telefone que muda a régua. Hoje só
// quem vê dinheiro tem acesso ao número de qualquer jogador — o link "Cobrar" o usa por
// baixo, mas nunca IMPRIME o dígito em tela pra quem não vê dinheiro (ver
// AbaPagamentosNaPaginaDoTorneioTests). Um relatório com telefone em texto puro pra qualquer
// ajudante seria expor mais do que a própria tela já expõe hoje.
public class RelatorioDuplasCsvTests
{
    // `MontarTorneio` cria o organizador com o NivelAcesso padrão do modelo ("Ajudante") — os
    // testes daqui precisam de alguém que VÊ dinheiro de verdade, então promove pra "Criador"
    // (mesmo idioma de AjudanteNaoVeDinheiroTests.Cenario).
    private static (Torneio torneio, Categoria categoria, Jogador criador) Cenario(DbPadelContext ctx, int qtdDuplas = 1)
    {
        var (torneio, categoria, criador) = TestInfra.MontarTorneio(ctx, qtdDuplas: qtdDuplas);
        ctx.TorneioOrganizadores.Single(o => o.TorneioId == torneio.Id).NivelAcesso = "Criador";
        ctx.SaveChanges();
        return (torneio, categoria, criador);
    }

    [Fact]
    public async Task So_quem_ve_dinheiro_baixa_o_relatorio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, criador) = Cenario(ctx);
        var ajudante = new Jogador { Nome = "Ajudante", Cpf = "88877766655" };
        ctx.Jogadores.Add(ajudante);
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = ajudante.Id, NivelAcesso = "Organizador",
        });
        await ctx.SaveChangesAsync();

        var doCriador = await TestInfra.NovoTorneiosController(ctx, criador.Id).RelatorioDuplasCsv(torneio.Id);
        var doAjudante = await TestInfra.NovoTorneiosController(ctx, ajudante.Id).RelatorioDuplasCsv(torneio.Id);

        Assert.IsType<FileContentResult>(doCriador);
        Assert.IsType<ForbidResult>(doAjudante);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_baixa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = Cenario(ctx);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "77766655544" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id).RelatorioDuplasCsv(torneio.Id);

        Assert.IsType<ForbidResult>(resultado);
    }

    [Fact]
    public async Task Traz_nome_telefone_pago_e_impedimento_de_cada_dupla()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, criador) = Cenario(ctx);
        torneio.PrecoInscricao = 100m;
        var dupla = await ctx.Duplas.Include(d => d.Jogador1).FirstAsync(d => d.CategoriaId == categoria.Id);
        dupla.Jogador1.Celular = "51992395650";
        dupla.Pago = true;
        dupla.ImpedimentoSextaNoite = true;
        dupla.ImpedimentoAlteradoEm = new DateTime(2026, 9, 1, 14, 30, 0);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, criador.Id).RelatorioDuplasCsv(torneio.Id);

        var arquivo = Assert.IsType<FileContentResult>(resultado);
        Assert.Equal("text/csv", arquivo.ContentType);
        var texto = Encoding.UTF8.GetString(arquivo.FileContents);

        Assert.Contains(dupla.NomeDeExibicao, texto);
        Assert.Contains("(51) 99239-5650", texto);
        Assert.Contains("Sim", texto);
        Assert.Contains("Sexta à noite", texto);
        Assert.Contains("01/09/2026 14:30", texto);
    }

    [Fact]
    public async Task Sem_impedimento_a_coluna_de_quando_fica_vazia()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, criador) = Cenario(ctx);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        // Uma dupla que já teve impedimento marcado e depois voltou pra "Nenhum" ainda carrega
        // o registro histórico de QUANDO foi a última alteração — o relatório não pode
        // confundir isso com "tem impedimento agora".
        dupla.ImpedimentoAlteradoEm = new DateTime(2026, 8, 20, 10, 0, 0);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, criador.Id).RelatorioDuplasCsv(torneio.Id);

        var arquivo = Assert.IsType<FileContentResult>(resultado);
        var texto = Encoding.UTF8.GetString(arquivo.FileContents);
        Assert.Contains("Sem impedimento", texto);
        Assert.DoesNotContain("20/08/2026", texto);
    }

    [Fact]
    public async Task Torneio_gratuito_nao_mostra_pago_ou_nao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, criador) = Cenario(ctx);
        torneio.PrecoInscricao = 0;
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, criador.Id).RelatorioDuplasCsv(torneio.Id);

        var arquivo = Assert.IsType<FileContentResult>(resultado);
        var texto = Encoding.UTF8.GetString(arquivo.FileContents);
        Assert.DoesNotContain(";Sim;", texto);
        Assert.DoesNotContain(";Não;", texto);
    }

    [Fact]
    public async Task Time_nao_entra_no_relatorio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, criador) = Cenario(ctx, qtdDuplas: 0);
        var dono = TestInfra.NovoJogador(90);
        ctx.Jogadores.Add(dono);
        ctx.Duplas.Add(new Dupla { Categoria = categoria, Jogador1 = dono, NomeTime = "Time Fantasma" });
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, criador.Id).RelatorioDuplasCsv(torneio.Id);

        var arquivo = Assert.IsType<FileContentResult>(resultado);
        var texto = Encoding.UTF8.GetString(arquivo.FileContents);
        Assert.DoesNotContain("Time Fantasma", texto);
    }

    [Fact]
    public async Task Torneio_que_nao_existe_da_404()
    {
        using var ctx = TestInfra.NovoContexto();
        var organizador = TestInfra.NovoJogador(1);
        ctx.Jogadores.Add(organizador);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id).RelatorioDuplasCsv(999);

        Assert.IsType<NotFoundResult>(resultado);
    }
}
