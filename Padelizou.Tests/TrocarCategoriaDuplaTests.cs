using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// TROCAR A DUPLA DE CATEGORIA — pedido do Felipe (06/09/2026): "Crie a opção também, do
// organizador trocar a dupla de categoria".
//
// Pra quem inscreveu na 4ª e devia ter ido na 3ª, ou trocou de ideia antes do sorteio. Até
// aqui a única saída era o organizador remover a dupla (RemoverDupla) e inscrevê-la de novo à
// mão na categoria certa — dois cliques que perdiam o `Pago`/`PagoEm` no meio do caminho.
//
// ⚠️ MESMA JANELA DO SORTEIO, e pela mesma razão de sempre neste projeto: uma vez que existe
// Partida, tem gente vendo contra quem joga e em qual grupo — mudar a categoria por baixo
// desfaria isso silenciosamente. A régua é `jaSorteou` (Partida existindo), a mesma de
// `ReabrirInscricoes`/`DesfazerSorteio`/`DesfazerRodadasAmericano`.
//
// ⚠️ AS REGRAS DE QUEM PODE ENTRAR NA CATEGORIA NÃO SOMEM NA TROCA. Mista e Casal exigem um
// de cada sexo (`SexoDoJogador`); a categoria pode ter vaga limitada (`LimiteDuplas`) — e
// trocar pra uma categoria cheia não fura a fila, vira lista de espera, como na inscrição
// normal. Ignorar essas réguas na troca seria abrir uma porta lateral pras mesmas regras que a
// inscrição já tranca na porta da frente.
public class TrocarCategoriaDuplaTests
{
    private static Categoria NovaCategoria(DbPadelContext ctx, Torneio torneio, string nome = "3ª Categoria Masculina",
        int? limiteDuplas = null, bool deTimes = false, bool chaveDireta = false)
    {
        var categoria = new Categoria
        {
            Nome = nome, Codigo = nome.Replace(" ", "").ToUpper()[..Math.Min(6, nome.Length)],
            TorneioId = torneio.Id, LimiteDuplas = limiteDuplas, DeTimes = deTimes, ChaveDireta = chaveDireta,
        };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();
        return categoria;
    }

    [Fact]
    public async Task Troca_a_dupla_de_categoria()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var destino = NovaCategoria(ctx, torneio);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoriaOrigem.Id);

        await TestInfra.NovoTorneiosController(ctx, org.Id).TrocarCategoriaDupla(dupla.Id, destino.Id);

        Assert.Equal(destino.Id, (await ctx.Duplas.FindAsync(dupla.Id))!.CategoriaId);
    }

    [Fact]
    public async Task So_organizador_troca()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var destino = NovaCategoria(ctx, torneio);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoriaOrigem.Id);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000088" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id).TrocarCategoriaDupla(dupla.Id, destino.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Equal(categoriaOrigem.Id, (await ctx.Duplas.FindAsync(dupla.Id))!.CategoriaId);
    }

    // A tela esconde a opção, mas um POST feito à mão poderia mandar o id de categoria de
    // OUTRO torneio (até de outro organizador) — e o servidor tem que recusar sozinho.
    [Fact]
    public async Task Nao_troca_pra_categoria_de_outro_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var (outroTorneio, categoriaDeOutroTorneio, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoriaOrigem.Id);

        var resultado = await TestInfra.NovoTorneiosController(ctx, org.Id)
            .TrocarCategoriaDupla(dupla.Id, categoriaDeOutroTorneio.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(resultado);
        Assert.Equal(categoriaOrigem.Id, (await ctx.Duplas.FindAsync(dupla.Id))!.CategoriaId);
    }

    [Fact]
    public async Task Nao_troca_depois_do_sorteio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        var destino = NovaCategoria(ctx, torneio);
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoriaOrigem.Id).OrderBy(d => d.Id).ToListAsync();
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoriaOrigem.Id, Codigo = "P1",
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, Status = "Agendada",
        });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).TrocarCategoriaDupla(duplas[0].Id, destino.Id);

        Assert.Equal(categoriaOrigem.Id, (await ctx.Duplas.FindAsync(duplas[0].Id))!.CategoriaId);
    }

    // ── A REGRA DE SEXO NÃO SOME NA TROCA ─────────────────────────────────────────────────

    [Fact]
    public async Task Recusa_mover_dupla_do_mesmo_sexo_pra_categoria_mista()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var mista = NovaCategoria(ctx, torneio, nome: "Categoria Mista");
        var j1 = new Jogador { Nome = "João", Cpf = "11144477735", Sexo = SexoDoJogador.Masculino };
        var j2 = new Jogador { Nome = "Pedro", Cpf = "22233344456", Sexo = SexoDoJogador.Masculino };
        ctx.Jogadores.AddRange(j1, j2);
        ctx.SaveChanges();
        var dupla = new Dupla { CategoriaId = categoriaOrigem.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();

        var controlador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controlador.TrocarCategoriaDupla(dupla.Id, mista.Id);

        Assert.Equal(categoriaOrigem.Id, (await ctx.Duplas.FindAsync(dupla.Id))!.CategoriaId);
        Assert.NotNull(controlador.TempData["Erro"]);
    }

    [Fact]
    public async Task Aceita_mover_dupla_de_um_homem_e_uma_mulher_pra_categoria_mista()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var mista = NovaCategoria(ctx, torneio, nome: "Categoria Mista");
        var j1 = new Jogador { Nome = "João", Cpf = "11144477735", Sexo = SexoDoJogador.Masculino };
        var j2 = new Jogador { Nome = "Ana", Cpf = "22233344456", Sexo = SexoDoJogador.Feminino };
        ctx.Jogadores.AddRange(j1, j2);
        ctx.SaveChanges();
        var dupla = new Dupla { CategoriaId = categoriaOrigem.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).TrocarCategoriaDupla(dupla.Id, mista.Id);

        Assert.Equal(mista.Id, (await ctx.Duplas.FindAsync(dupla.Id))!.CategoriaId);
    }

    // ── VAGA E LISTA DE ESPERA NA CATEGORIA DE DESTINO ────────────────────────────────────

    [Fact]
    public async Task Vai_pra_lista_de_espera_quando_a_categoria_de_destino_esta_cheia()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var cheia = NovaCategoria(ctx, torneio, limiteDuplas: 1);
        var j1 = TestInfra.NovoJogador(101);
        var j2 = TestInfra.NovoJogador(102);
        ctx.Jogadores.AddRange(j1, j2);
        ctx.SaveChanges();
        ctx.Duplas.Add(new Dupla { CategoriaId = cheia.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id }); // ocupa a única vaga
        await ctx.SaveChangesAsync();
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoriaOrigem.Id);

        await TestInfra.NovoTorneiosController(ctx, org.Id).TrocarCategoriaDupla(dupla.Id, cheia.Id);

        var movida = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.Equal(cheia.Id, movida!.CategoriaId);
        Assert.True(movida.EmListaDeEspera);
    }

    [Fact]
    public async Task Entra_confirmada_quando_a_categoria_de_destino_tem_vaga()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var comVaga = NovaCategoria(ctx, torneio, limiteDuplas: 5);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoriaOrigem.Id);

        await TestInfra.NovoTorneiosController(ctx, org.Id).TrocarCategoriaDupla(dupla.Id, comVaga.Id);

        Assert.False((await ctx.Duplas.FindAsync(dupla.Id))!.EmListaDeEspera);
    }

    // Sair de uma categoria confirmada libera vaga lá — a fila de espera DAQUELA categoria
    // avança sozinha, mesmo comportamento de RemoverDupla/Desistir (TirarDuplaDoTorneioAsync).
    [Fact]
    public async Task Sair_confirmada_promove_a_fila_de_espera_da_categoria_de_origem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        categoriaOrigem.LimiteDuplas = 1;
        var destino = NovaCategoria(ctx, torneio);
        var confirmada = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoriaOrigem.Id);
        var j1 = TestInfra.NovoJogador(201);
        var j2 = TestInfra.NovoJogador(202);
        ctx.Jogadores.AddRange(j1, j2);
        ctx.SaveChanges();
        var naEspera = new Dupla { CategoriaId = categoriaOrigem.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id, EmListaDeEspera = true };
        ctx.Duplas.Add(naEspera);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).TrocarCategoriaDupla(confirmada.Id, destino.Id);

        Assert.False((await ctx.Duplas.FindAsync(naEspera.Id))!.EmListaDeEspera);
    }

    // Quem já estava na lista de espera não tira vaga de ninguém ao sair — a fila da
    // categoria de origem não tem por que se mexer.
    [Fact]
    public async Task Sair_da_lista_de_espera_nao_promove_ninguem_na_origem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var destino = NovaCategoria(ctx, torneio);
        var j1 = TestInfra.NovoJogador(301);
        var j2 = TestInfra.NovoJogador(302);
        var j3 = TestInfra.NovoJogador(303);
        var j4 = TestInfra.NovoJogador(304);
        ctx.Jogadores.AddRange(j1, j2, j3, j4);
        ctx.SaveChanges();
        var naEspera1 = new Dupla { CategoriaId = categoriaOrigem.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id, EmListaDeEspera = true };
        var naEspera2 = new Dupla { CategoriaId = categoriaOrigem.Id, Jogador1Id = j3.Id, Jogador2Id = j4.Id, EmListaDeEspera = true };
        ctx.Duplas.AddRange(naEspera1, naEspera2);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).TrocarCategoriaDupla(naEspera1.Id, destino.Id);

        Assert.True((await ctx.Duplas.FindAsync(naEspera2.Id))!.EmListaDeEspera);
    }

    // ── NÃO CRUZA A FRONTEIRA ESTRUTURAL DE TIMES / CHAVE DIRETA ──────────────────────────
    //
    // Categoria de times e de chave direta são cadastradas pelo ORGANIZADOR (jogador não se
    // inscreve) e carregam suposições diferentes sobre o que a Dupla representa — misturar
    // com uma categoria comum bagunçaria as duas.

    [Fact]
    public async Task Nao_move_dupla_comum_pra_categoria_de_times()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var deTimes = NovaCategoria(ctx, torneio, nome: "Geral (Times)", deTimes: true);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoriaOrigem.Id);

        await TestInfra.NovoTorneiosController(ctx, org.Id).TrocarCategoriaDupla(dupla.Id, deTimes.Id);

        Assert.Equal(categoriaOrigem.Id, (await ctx.Duplas.FindAsync(dupla.Id))!.CategoriaId);
    }

    [Fact]
    public async Task Nao_move_dupla_comum_pra_categoria_de_chave_direta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var chaveDireta = NovaCategoria(ctx, torneio, nome: "Chave Geral", chaveDireta: true);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoriaOrigem.Id);

        await TestInfra.NovoTorneiosController(ctx, org.Id).TrocarCategoriaDupla(dupla.Id, chaveDireta.Id);

        Assert.Equal(categoriaOrigem.Id, (await ctx.Duplas.FindAsync(dupla.Id))!.CategoriaId);
    }

    [Fact]
    public async Task Mover_pra_mesma_categoria_e_no_op()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoriaOrigem, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoriaOrigem.Id);

        var resultado = await TestInfra.NovoTorneiosController(ctx, org.Id)
            .TrocarCategoriaDupla(dupla.Id, categoriaOrigem.Id);

        Assert.Equal(categoriaOrigem.Id, (await ctx.Duplas.FindAsync(dupla.Id))!.CategoriaId);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(resultado);
    }
}
