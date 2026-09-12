using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O GRUPO QUE TERMINOU NÃO ESPERA OS OUTROS.
//
// 🗣️ Felipe, 11/09/2026: *"quando um grupo finalizar os 3 jogos, já coloque eles para a próxima
// fase conforme a classificação, não precisa necessariamente terminar todos os jogos dos outros
// grupos/chaves para ir avançando"*.
//
// ⚠️ ISSO SÓ DÁ PRA FAZER COM O CRUZAMENTO DESENHADO, e a razão é a régua de hoje, não uma
// limitação técnica. Sem desenho, quem o 1º do Grupo A enfrenta sai da campanha COMPARADA de
// todos os grupos (ChaveamentoMataMata.MontarPrimeiraFase semeia o melhor contra o pior):
// enquanto o Grupo D joga, não dá pra saber se o 1º do A é o melhor ou o pior primeiro
// colocado — e um jogo criado cedo teria que ser desfeito. Com o desenho
// (Services/CruzamentoDoMataMata) a vaga é por COLOCAÇÃO ("1º do A × 2º do C"), então ela fica
// conhecida no instante em que aqueles dois grupos fecham.
//
// Decisão do Felipe em 11/09/2026, escolhendo entre as três saídas: vale só com desenho —
// torneio sem desenho (o Er) continua letra por letra como era.
public class AvancoParcialDosGruposTests
{
    // 8 duplas → 3 grupos (A com 2 duplas, B e C com 3), 6 classificados num quadro de 8:
    // 2 jogos de abertura e 2 byes. O desenho guardado é o PADRÃO — o que a tela abre
    // preenchido, e o mesmo cruzamento que o motor faria.
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria,
                               Padelizou.Controllers.TorneiosController Controller,
                               CruzamentoDoMataMata.Mapa Mapa)>
        SorteadoComDesenhoAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var grupos = await ctx.Set<GrupoTorneio>()
            .Where(g => g.CategoriaId == categoria.Id)
            .Include(g => g.Duplas)
            .OrderBy(g => g.Nome).ToListAsync();

        var mapa = CruzamentoDoMataMata.Padrao(
            grupos.Select(g => g.Nome).ToList(),
            categoria.ClassificadosPorGrupo ?? 2,
            grupos.Select(g => g.Duplas.Count).ToList());
        Assert.NotNull(mapa);
        Assert.Equal(2, mapa!.Confrontos.Count);

        categoria.CruzamentoDoMataMata = mapa.Escrever();
        await ctx.SaveChangesAsync();

        return (ctx, torneio, categoria, controller, mapa);
    }

    // Fecha os jogos dos grupos citados (pela LETRA, que é o que Dupla.Grupo guarda).
    private static async Task FecharGruposAsync(
        DbPadelContext ctx, Padelizou.Controllers.TorneiosController controller,
        Categoria categoria, IEnumerable<string> letras)
    {
        var doGrupo = await ctx.Duplas
            .Where(d => d.CategoriaId == categoria.Id && d.Grupo != null && letras.Contains(d.Grupo))
            .Select(d => d.Id).ToListAsync();

        var jogos = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && FasesTorneio.EhFaseDeGrupos(p.Fase)
                     && p.Status != "Finalizada"
                     && (doGrupo.Contains(p.Dupla1Id) || doGrupo.Contains(p.Dupla2Id)))
            .OrderBy(p => p.Id).ToListAsync();

        foreach (var jogo in jogos)
        {
            bool venceA1 = jogo.Dupla1Id < jogo.Dupla2Id;
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, venceA1 ? 9 : 3, venceA1 ? 3 : 9);
        }
    }

    private static Task<List<Partida>> AberturaAsync(DbPadelContext ctx, int categoriaId) =>
        ctx.Partidas
            .Where(p => p.CategoriaId == categoriaId && !FasesTorneio.EhFaseDeGrupos(p.Fase))
            .OrderBy(p => p.Id).ToListAsync();

    // O pedido: fechados os dois grupos do PRIMEIRO confronto do desenho, o jogo nasce — com o
    // terceiro grupo ainda em quadra.
    [Fact]
    public async Task O_jogo_de_abertura_nasce_quando_os_grupos_dele_fecham()
    {
        var (ctx, _, categoria, controller, mapa) = await SorteadoComDesenhoAsync();
        using var _ctx = ctx;

        var doPrimeiro = new[] { mapa.Confrontos[0].Lado1.Grupo, mapa.Confrontos[0].Lado2.Grupo }
            .Distinct().ToList();
        await FecharGruposAsync(ctx, controller, categoria, doPrimeiro);

        // O torneio NÃO acabou a fase de grupos — é isso que o pedido pede pra não esperar.
        Assert.True(await ctx.Partidas.AnyAsync(p => p.CategoriaId == categoria.Id
            && FasesTorneio.EhFaseDeGrupos(p.Fase) && p.Status != "Finalizada"));

        var abertura = await AberturaAsync(ctx, categoria.Id);
        Assert.NotEmpty(abertura);

        // E o jogo 1 é o que o desenho promete: o 1º/2º de cada grupo citado nele.
        var classificados = ClassificacaoDeGrupos.Calcular(
            await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync(),
            await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id
                && FasesTorneio.EhFaseDeGrupos(p.Fase) && p.Status == "Finalizada").ToListAsync(),
            ClassificacaoDeGrupos.SemPontos, ClassificacaoDeGrupos.VagasPorGrupo(categoria));

        int? Vaga(CruzamentoDoMataMata.Vaga v) => CruzamentoDoMataMata.IdDaVaga(v, classificados);

        Assert.Equal(
            new HashSet<int?> { Vaga(mapa.Confrontos[0].Lado1), Vaga(mapa.Confrontos[0].Lado2) },
            new HashSet<int?> { abertura[0].Dupla1Id, abertura[0].Dupla2Id });
    }

    // ⚠️ A TRAVA DO ER. Sem desenho, nada muda: dois grupos fechados e o mata-mata continua sem
    // nascer, porque a semeadura por campanha ainda não tem como saber quem é o melhor 1º.
    [Fact]
    public async Task Sem_desenho_a_chave_continua_esperando_todos_os_grupos()
    {
        var (ctx, _, categoria, controller, mapa) = await SorteadoComDesenhoAsync();
        using var _ctx = ctx;

        categoria.CruzamentoDoMataMata = null;
        await ctx.SaveChangesAsync();

        await FecharGruposAsync(ctx, controller, categoria,
            new[] { mapa.Confrontos[0].Lado1.Grupo, mapa.Confrontos[0].Lado2.Grupo }.Distinct().ToList());

        Assert.Empty(await AberturaAsync(ctx, categoria.Id));
    }

    // ⚠️ E A PRIMEIRA RODADA PELA METADE NÃO AVANÇA. Com 1 dos 2 jogos criados e ele terminado,
    // a lista de vagas teria 1 vencedor e nenhum bye (a classificação ainda é provisória): o
    // robô batizaria isso de "Final" e criaria a decisão do torneio com um jogo de abertura
    // sobrando. É a mesma família do bug do Interno de 05/08/2026.
    [Fact]
    public async Task A_abertura_pela_metade_nao_gera_a_fase_seguinte()
    {
        var (ctx, _, categoria, controller, mapa) = await SorteadoComDesenhoAsync();
        using var _ctx = ctx;

        await FecharGruposAsync(ctx, controller, categoria,
            new[] { mapa.Confrontos[0].Lado1.Grupo, mapa.Confrontos[0].Lado2.Grupo }.Distinct().ToList());

        var abertura = await AberturaAsync(ctx, categoria.Id);
        Assert.Single(abertura);

        await TestInfra.FinalizarComPlacarAsync(ctx, controller, abertura[0], 9, 3);

        // Continua só o jogo de abertura: nada de Final, Semifinal ou o que for.
        var fases = (await AberturaAsync(ctx, categoria.Id)).Select(p => p.Fase).Distinct().ToList();
        Assert.Single(fases);
        Assert.Empty(await AvancoDaChave.ByesDaCategoriaAsync(ctx, categoria.Id, TestInfra.SemPontosDoRanking));
    }

    // Fechada a fase de grupos inteira, a chave é EXATAMENTE a mesma que o desenho produziria de
    // uma vez só — nascer em duas levas não pode mudar confronto nenhum.
    [Fact]
    public async Task Fechados_todos_os_grupos_a_chave_sai_igual_a_do_desenho()
    {
        var (ctx, _, categoria, controller, mapa) = await SorteadoComDesenhoAsync();
        using var _ctx = ctx;

        await FecharGruposAsync(ctx, controller, categoria,
            new[] { mapa.Confrontos[0].Lado1.Grupo, mapa.Confrontos[0].Lado2.Grupo }.Distinct().ToList());
        Assert.Single(await AberturaAsync(ctx, categoria.Id));   // nasceu pela metade

        var todasAsLetras = await ctx.Duplas
            .Where(d => d.CategoriaId == categoria.Id && d.Grupo != null)
            .Select(d => d.Grupo!).Distinct().ToListAsync();
        await FecharGruposAsync(ctx, controller, categoria, todasAsLetras);

        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        var jogosDeGrupo = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && FasesTorneio.EhFaseDeGrupos(p.Fase)).ToListAsync();
        var classificados = ClassificacaoDeGrupos.Calcular(
            duplas, jogosDeGrupo, ClassificacaoDeGrupos.SemPontos,
            ClassificacaoDeGrupos.VagasPorGrupo(categoria));

        var (faseEsperada, confrontosEsperados, byesEsperados) =
            CruzamentoDoMataMata.Aplicar(mapa, classificados);

        var abertura = await AberturaAsync(ctx, categoria.Id);
        Assert.Equal(confrontosEsperados.Count, abertura.Count);
        for (int i = 0; i < abertura.Count; i++)
        {
            Assert.Equal(faseEsperada, abertura[i].Fase);
            Assert.Equal(
                new HashSet<int> { confrontosEsperados[i].Dupla1Id, confrontosEsperados[i].Dupla2Id },
                new HashSet<int> { abertura[i].Dupla1Id, abertura[i].Dupla2Id });
        }

        // E os byes voltam a ser os do desenho, agora que a classificação é definitiva.
        Assert.Equal(byesEsperados, await AvancoDaChave.ByesDaCategoriaAsync(ctx, categoria.Id, TestInfra.SemPontosDoRanking));
    }

    // A prévia não pode prometer de novo o jogo que já nasceu — ele já está na lista de jogos,
    // com nome e sobrenome. Ela continua prometendo o que FALTA, por colocação.
    [Fact]
    public async Task A_previa_dos_grupos_nao_repete_o_jogo_que_ja_nasceu()
    {
        var (ctx, _, categoria, controller, mapa) = await SorteadoComDesenhoAsync();
        using var _ctx = ctx;

        await FecharGruposAsync(ctx, controller, categoria,
            new[] { mapa.Confrontos[0].Lado1.Grupo, mapa.Confrontos[0].Lado2.Grupo }.Distinct().ToList());

        var grupos = await ctx.Set<GrupoTorneio>()
            .Where(g => g.CategoriaId == categoria.Id).Include(g => g.Duplas)
            .OrderBy(g => g.Nome).ToListAsync();

        var cadeia = ProximasFasesDaChave.MontarDosGrupos(
            grupos.Select(g => g.Nome).ToList(),
            categoria.ClassificadosPorGrupo ?? 2,
            fimDosGrupos: null,
            categoria.Nome,
            categoria.Id,
            grupos.Select(g => g.Duplas.Count).ToList(),
            categoria.CruzamentoDoMataMata,
            jogosJaCriadosNaAbertura: 1);

        var abertura = cadeia.Rodadas[0];
        Assert.Single(abertura.Confrontos);          // só o jogo 2
        Assert.Equal(2, abertura.PrimeiroNumero);    // e ele se chama "2", não "1"
        // Os dois rótulos são escritos por classes diferentes e têm que casar letra por letra:
        // "1º do Grupo A" (CruzamentoDoMataMata.Vaga) e "1º do Grupo A" (ChaveProjetada.Vaga).
        Assert.Equal(mapa.Confrontos[1].Lado1.Rotulo, abertura.Confrontos[0].Lado1.Rotulo);
        Assert.Equal(mapa.Confrontos[1].Lado2.Rotulo, abertura.Confrontos[0].Lado2.Rotulo);
    }

    // E a TELA continua mostrando o caminho inteiro. Este é o teste de fiação: quem decide por
    // qual das duas entradas a categoria é projetada é o controller, e com um jogo de abertura
    // no ar ele escolhia a entrada "a chave já começou" — que lê o quadro pelos jogos que
    // existem. Meia abertura vira uma cadeia de uma vaga só, que não encadeia nada: a Semifinal
    // e a Final SUMIAM da lista justamente pra quem acabou de classificar.
    [Fact]
    public async Task A_lista_de_jogos_mostra_o_caminho_ate_a_final_com_a_abertura_pela_metade()
    {
        var (ctx, torneio, categoria, controller, mapa) = await SorteadoComDesenhoAsync();
        using var _ctx = ctx;

        await FecharGruposAsync(ctx, controller, categoria,
            new[] { mapa.Confrontos[0].Lado1.Grupo, mapa.Confrontos[0].Lado2.Grupo }.Distinct().ToList());
        Assert.Single(await AberturaAsync(ctx, categoria.Id));

        // As chaves precisam estar públicas pra lista sair — e quem olha aqui é o organizador.
        torneio.Status = "Fase de Grupos";
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await controller.Jogos(torneio.Id, null, null);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado);

        var queVem = (List<ProximasFasesDaChave.JogoQueVem>)controller.ViewBag.JogosQueVem;
        var fases = queVem.Select(j => j.FaseNumerada).ToList();

        Assert.Contains("Semifinal 1", fases);
        Assert.Contains("Semifinal 2", fases);
        Assert.Contains("Final", fases);

        // O jogo que JÁ NASCEU não é prometido de novo — ele está na lista de jogos reais.
        Assert.Equal(1, fases.Count(f => f.StartsWith(CruzamentoDoMataMata.NomeDaAbertura(mapa))));
    }
}
