using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// DESFAZER O SORTEIO DO AMERICANO — pedido do Felipe (06/09/2026): "Elas geraram as chaves sem
// querer do torneio Americano das gurias 2ª edição, deixe sem gerar as chaves ainda, e cria a
// função de reabrir as inscrições e fechar as chaves".
//
// ⚠️ O AMERICANO NÃO TEM A TELA DE APROVAÇÃO DO FORMATO PADRÃO. `GerarRodadasAmericano` e
// `GerarRodadasAmericanoDuplas` vão direto pra "Fase de Grupos" — não existe "Chaves em
// Aprovação" nem `DesfazerSorteio` (aquele só desfaz ENQUANTO pendente, e o Americano nunca
// fica pendente). Um sorteio acidental aqui não tinha caminho de volta: só apagar o torneio
// inteiro e recomeçar, perdendo as inscrições e o link já compartilhado.
//
// `ReabrirInscricoes` JÁ EXISTE (08/08/2026) e já recusa reabrir quando há Partida — é
// `PortaDaInscricao.PorQueNaoPodeAbrir` com `jaSorteou: true`. O que faltava era o botão que
// tira o "jaSorteou" do caminho: apagar as rodadas geradas.
//
// ⚠️ AS DUPLAS SE COMPORTAM DIFERENTE ENTRE OS DOIS FORMATOS DO AMERICANO, e por isso o
// arquivo trava os dois separadamente:
//   • no individual (Americano), a Dupla nasce EFÊMERA a cada rodada — é o par sorteado
//     daquela partida, não a inscrição. Quem inscreveu é InscricaoAmericana, que continua
//     intacta; apagar a Dupla não perde ninguém.
//   • no AmericanoDuplas, a Dupla É A INSCRIÇÃO — a mesma dupla fixa formada lá atrás. Apagar
//     ela junto apagaria quem se inscreveu, não só o sorteio.
public class DesfazerRodadasAmericanoTests
{
    // ── AMERICANO INDIVIDUAL ──────────────────────────────────────────────────────────────

    private static (DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador org)
        MontarAmericano(int inscritos)
    {
        var ctx = TestInfra.NovoContexto();

        var org = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(org);

        var torneio = new Torneio
        {
            Nome = "Americano das gurias", Codigo = "AG2", Status = "Chaves em Sorteio",
            Formato = "Americano", QuantidadeQuadras = 4, TempoPrevistoPartidaMinutos = 20,
            DataInicio = new DateTime(2026, 9, 7, 19, 0, 0),
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "6ª Feminina", Codigo = "F6", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = org.Id });

        for (int i = 1; i <= inscritos; i++)
        {
            var j = new Jogador { Nome = $"Jogadora {i:00}", Cpf = $"777000000{i:00}" };
            ctx.Jogadores.Add(j);
            ctx.SaveChanges();
            ctx.InscricoesAmericanas.Add(new InscricaoAmericana { CategoriaId = categoria.Id, JogadorId = j.Id });
        }
        ctx.SaveChanges();

        return (ctx, torneio, categoria, org);
    }

    private static Task SortearAsync(DbPadelContext ctx, Jogador org, int torneioId) =>
        TestInfra.NovoTorneiosController(ctx, org.Id)
            .GerarRodadasAmericano(torneioId, new DateTime(2026, 9, 7, 19, 0, 0));

    [Fact]
    public async Task Desfaz_as_rodadas_e_devolve_pro_sorteio()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        Assert.True(await ctx.Partidas.AnyAsync(p => p.TorneioId == torneio.Id)); // sorteou de verdade

        await TestInfra.NovoTorneiosController(ctx, org.Id).DesfazerRodadasAmericano(torneio.Id);

        Assert.Equal(PortaDaInscricao.Fechada, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.False(await ctx.Partidas.AnyAsync(p => p.TorneioId == torneio.Id));
    }

    // ⚠️ O CORAÇÃO DA DIFERENÇA ENTRE OS DOIS FORMATOS: no individual, a Dupla é o par daquela
    // rodada — apagável — e quem inscreveu (InscricaoAmericana) continua intacta.
    [Fact]
    public async Task Desfaz_apaga_as_duplas_efemeras_sem_perder_a_inscricao()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        var inscritosAntes = await ctx.InscricoesAmericanas.CountAsync(i => i.CategoriaId == categoria.Id);
        Assert.True(await ctx.Duplas.AnyAsync(d => d.CategoriaId == categoria.Id)); // as duplas da rodada existem

        await TestInfra.NovoTorneiosController(ctx, org.Id).DesfazerRodadasAmericano(torneio.Id);

        Assert.False(await ctx.Duplas.AnyAsync(d => d.CategoriaId == categoria.Id));
        Assert.Equal(inscritosAntes, await ctx.InscricoesAmericanas.CountAsync(i => i.CategoriaId == categoria.Id));
        Assert.All(await ctx.InscricoesAmericanas.Where(i => i.CategoriaId == categoria.Id).ToListAsync(),
            i => Assert.Null(i.Grupo));
    }

    // O PEDIDO INTEIRO: depois de desfazer, o botão que já existe (ReabrirInscricoes) para de
    // ser recusado. Sem isto, "cria a função de reabrir" teria ficado pela metade — a rodada
    // some, mas `jaSorteou` continuaria vendo alguma Partida perdida por aí.
    [Fact]
    public async Task Depois_de_desfazer_da_pra_reabrir_as_inscricoes()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        var controlador = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controlador.DesfazerRodadasAmericano(torneio.Id);
        await controlador.ReabrirInscricoes(torneio.Id);

        Assert.Equal(PortaDaInscricao.Aberta, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // Mesma recusa do DesfazerSorteio (formato Padrão), pelo mesmo motivo: com jogo em
    // andamento ou finalizado, desfazer apagaria placar de verdade — melhor recusar do que
    // arriscar.
    [Fact]
    public async Task Nao_desfaz_se_algum_jogo_ja_tem_resultado()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        var primeiroJogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        primeiroJogo.Status = "Finalizada";
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).DesfazerRodadasAmericano(torneio.Id);

        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.True(await ctx.Partidas.AnyAsync(p => p.Id == primeiroJogo.Id));
    }

    // ⚠️ SEM O `TempData["Erro"]`, ESTE TESTE NÃO PROVA NADA — e foi visto passando com a
    // guarda desligada antes desta versão. Quando não há nada sorteado, os dois caminhos
    // (com e sem a guarda) terminam com o MESMO status: `PortaDaInscricao.Fechada` é
    // literalmente "Chaves em Sorteio", e todo o resto (apagar 0 partidas, apagar 0 duplas,
    // zerar campos que já estavam zerados) é no-op nos dois. O único jeito de distinguir é
    // checar que a recusa foi de verdade avisada, não deduzir da ausência de mudança.
    [Fact]
    public async Task Nada_pra_desfazer_quando_ainda_nao_sorteou()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;

        var controlador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controlador.DesfazerRodadasAmericano(torneio.Id);

        Assert.Equal("Chaves em Sorteio", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.Contains("não tem rodadas geradas", controlador.TempData["Erro"] as string);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_desfaz()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000088" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id).DesfazerRodadasAmericano(torneio.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.True(await ctx.Partidas.AnyAsync(p => p.TorneioId == torneio.Id));
    }

    [Fact]
    public async Task Torneio_de_formato_padrao_nao_atende_essa_acao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        var resultado = await TestInfra.NovoTorneiosController(ctx, org.Id).DesfazerRodadasAmericano(torneio.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(resultado);
    }

    // ── O BOTÃO NA PÁGINA (ViewBag.PodeDesfazerRodadasAmericano) ─────────────────────────
    //
    // A régua que decide se o botão aparece tem que ser IGUAL à do servidor — folgada de mais
    // e o botão convida pra um clique que o POST recusa calado; apertada de mais e some no
    // exato caso em que o organizador precisa dele. `Details` é a tela que ele está olhando
    // quando aperta "Sortear Rodadas do Americano" sem querer.

    [Fact]
    public async Task O_botao_aparece_com_rodadas_sorteadas_e_nada_jogado()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;
        await SortearAsync(ctx, org, torneio.Id);

        var controlador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controlador.Details(torneio.Id, timeFiltroId: null, categoriaFiltroIds: null);

        Assert.Equal(true, controlador.ViewData["PodeDesfazerRodadasAmericano"]);
    }

    [Fact]
    public async Task O_botao_some_assim_que_um_jogo_e_lancado()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        var primeiroJogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        primeiroJogo.Status = "Finalizada";
        await ctx.SaveChangesAsync();

        var controlador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controlador.Details(torneio.Id, timeFiltroId: null, categoriaFiltroIds: null);

        Assert.Equal(false, controlador.ViewData["PodeDesfazerRodadasAmericano"]);
    }

    [Fact]
    public async Task O_botao_nao_aparece_antes_de_sortear()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;

        var controlador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controlador.Details(torneio.Id, timeFiltroId: null, categoriaFiltroIds: null);

        Assert.Equal(false, controlador.ViewData["PodeDesfazerRodadasAmericano"]);
    }

    [Fact]
    public async Task O_botao_some_depois_de_desfazer()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        var controlador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controlador.DesfazerRodadasAmericano(torneio.Id);

        await controlador.Details(torneio.Id, timeFiltroId: null, categoriaFiltroIds: null);

        Assert.Equal(false, controlador.ViewData["PodeDesfazerRodadasAmericano"]);
    }

    // O formato Padrão tem o próprio botão (DesfazerSorteio, só enquanto pendente) — este não
    // pode aparecer duplicado numa chave que já foi aprovada e virou "Fase de Grupos".
    [Fact]
    public async Task O_botao_nao_aparece_pro_formato_padrao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controlador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controlador.GerarChaves(torneio.Id);
        await controlador.AprovarChaves(torneio.Id);
        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);

        await controlador.Details(torneio.Id, timeFiltroId: null, categoriaFiltroIds: null);

        Assert.Equal(false, controlador.ViewData["PodeDesfazerRodadasAmericano"]);
    }

    // ── AMERICANO DE DUPLAS ───────────────────────────────────────────────────────────────

    private static (DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador org, List<Dupla> duplas)
        MontarAmericanoDeDuplas(int quantasDuplas)
    {
        var ctx = TestInfra.NovoContexto();

        var org = new Jogador { Nome = "Organizador", Cpf = "99900000098" };
        ctx.Jogadores.Add(org);

        var torneio = new Torneio
        {
            Nome = "Americano de Duplas", Codigo = "AD2", Status = "Chaves em Sorteio",
            Formato = "AmericanoDuplas", QuantidadeQuadras = 2, TempoPrevistoPartidaMinutos = 20,
            DataInicio = new DateTime(2026, 9, 7, 19, 0, 0),
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "Geral", Codigo = "GER", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = org.Id });

        var duplas = new List<Dupla>();
        for (int i = 1; i <= quantasDuplas; i++)
        {
            var j1 = new Jogador { Nome = $"Jogador {i:00}A", Cpf = $"6660000{i:0000}" };
            var j2 = new Jogador { Nome = $"Jogador {i:00}B", Cpf = $"6670000{i:0000}" };
            ctx.Jogadores.AddRange(j1, j2);
            ctx.SaveChanges();

            var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id };
            ctx.Duplas.Add(dupla);
            duplas.Add(dupla);
        }
        ctx.SaveChanges();

        return (ctx, torneio, categoria, org, duplas);
    }

    // ⚠️ O TESTE QUE PROVA QUE A DIFERENÇA ENTRE OS FORMATOS FOI RESPEITADA. Se a ação apagasse
    // toda `Dupla` da categoria sem checar o formato, este teste apagaria a inscrição de gente
    // que nunca pediu pra sair — o mesmo tipo de erro documentado no comentário do arquivo.
    [Fact]
    public async Task AmericanoDuplas_desfaz_sem_apagar_as_duplas_da_inscricao()
    {
        var (ctx, torneio, categoria, org, duplas) = MontarAmericanoDeDuplas(4);
        using var _ = ctx;
        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .GerarRodadasAmericanoDuplas(torneio.Id, new DateTime(2026, 9, 7, 19, 0, 0));
        Assert.True(await ctx.Partidas.AnyAsync(p => p.TorneioId == torneio.Id));

        await TestInfra.NovoTorneiosController(ctx, org.Id).DesfazerRodadasAmericano(torneio.Id);

        Assert.Equal(PortaDaInscricao.Fechada, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.False(await ctx.Partidas.AnyAsync(p => p.TorneioId == torneio.Id));
        Assert.Equal(duplas.Count, await ctx.Duplas.CountAsync(d => d.CategoriaId == categoria.Id));
    }
}
