using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 🗣️ Felipe, 09/09/2026, olhando a 4ª Masculina do torneio do Er no dev: "por que que toda vez
// q eu gero o sorteio, esta vindo igual, o chaveamento, os horarios dos jogos e tudo mais?"
//
// 🕳️ NÃO ERA SEED TRAVADO — era ausência de sorteio. O ramo de categoria COM GRUPOS do
// `GerarChaves` não embaralhava nada: a ordem saía de `OrderByDescending(pontos)` e todo o
// resto (os grupos de 2 do resto, o zigue-zague, a letra do grupo, os confrontos) é função
// PURA dessa lista. Os `OrderBy(Guid.NewGuid())` que de fato sorteiam só existem nos ramos de
// TIMES e de CHAVE DIRETA, que uma categoria com grupos não usa.
//
// ⚠️ E o `OrderByDescending` do LINQ é ordenação ESTÁVEL: com quase todo mundo em 0 ponto — o
// normal de um torneio de teste — o empate preservava a ordem de carga do EF, ou seja, a ordem
// de INSCRIÇÃO. A chave era "ordem de inscrição → zigue-zague", igual a cada clique.
//
// 🕐 A GRADE VEM DE CARONA: `EncaixarNasLevas` é chamado sem `aPartirDe`, então parte de
// `torneio.AberturaDaGrade` (data fixa, não `DateTime.Now`). Mesma lista de jogos + mesma
// configuração = mesma grade, minuto a minuto. Por isso os horários repetiam junto.
//
// O conserto é o desempate: quem tem ranking continua semeado por ranking; quem empata é
// sorteado. As duas guardas de baixo (estrutura e cabeça de chave) existem pra que o conserto
// não vire embaralhamento puro, que jogaria fora a separação dos favoritos.
public class SorteioNaoRepeteAChaveTests
{
    // O número do print do Felipe: 16 duplas viram 2 grupos de 2 (o ramo `resto == 1`) e
    // 4 grupos de 3.
    private const int DuplasDoPrint = 16;

    // Quantas vezes o organizador aperta "gerar chaves" no teste. Uma rodada só não distingue
    // "sorteou" de "não sorteou" — é preciso ver DUAS saídas diferentes.
    private const int Sorteios = 5;

    [Fact]
    public async Task Gerar_de_novo_nao_repete_a_composicao_dos_grupos()
    {
        var assinaturas = await SortearVariasVezesAsync(DuplasDoPrint, QuemCaiuEmQualGrupo);

        // O que o Felipe vê na aba "Chaves e Grupos": quem está com quem.
        Assert.True(assinaturas.Distinct().Count() > 1,
            $"Os {Sorteios} sorteios devolveram exatamente os mesmos grupos: {assinaturas[0]}");
    }

    [Fact]
    public async Task Gerar_de_novo_nao_repete_os_horarios_dos_jogos()
    {
        var assinaturas = await SortearVariasVezesAsync(DuplasDoPrint, QuemJogaAQueHoras);

        // A grade não muda de FORMATO (os mesmos slots continuam existindo) — o que tem que
        // mudar é QUEM é chamado pra cada horário. Era a segunda metade da reclamação.
        Assert.True(assinaturas.Distinct().Count() > 1,
            $"Os {Sorteios} sorteios marcaram as mesmas duplas nos mesmos horários: {assinaturas[0]}");
    }

    [Fact]
    public async Task A_estrutura_dos_grupos_continua_a_mesma_em_todo_sorteio()
    {
        // ⚠️ GUARDA DO OUTRO LADO: o desempate mexe na ORDEM, nunca no desenho. 16 duplas têm
        // que fechar em 2 grupos de 2 + 4 grupos de 3 em TODA execução — se o sorteio passasse
        // a montar 5 ou 6 grupos conforme a sorte, o organizador perderia o formato que
        // escolheu.
        var tamanhos = await SortearVariasVezesAsync(DuplasDoPrint, ctx =>
            string.Join(",", ctx.Duplas
                .Where(d => d.Grupo != null)
                .AsEnumerable()
                .GroupBy(d => d.Grupo!)
                .OrderBy(g => g.Key)
                .Select(g => g.Count())));

        Assert.All(tamanhos, t => Assert.Equal("2,2,3,3,3,3", t));
    }

    [Fact]
    public async Task O_cabeca_de_chave_por_ranking_sobrevive_ao_desempate()
    {
        // ⚠️ A GUARDA MAIS IMPORTANTE: o desempate NÃO pode virar embaralhamento puro. Com 9
        // duplas (3 grupos de 3) e pontos ESTRITAMENTE diferentes nas três primeiras, o
        // zigue-zague obriga a 1ª a abrir o Grupo A, a 2ª o Grupo B e a 3ª o Grupo C — em toda
        // execução, porque entre elas não há empate pra sortear.
        for (int sorteio = 1; sorteio <= Sorteios; sorteio++)
        {
            using var ctx = TestInfra.NovoContexto();
            var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 9);
            var favoritas = SemearRankingAsync(ctx, categoria);
            var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

            await controller.GerarChaves(torneio.Id);

            var grupoDe = await ctx.Duplas
                .Where(d => d.CategoriaId == categoria.Id)
                .ToDictionaryAsync(d => d.Id, d => d.Grupo);

            Assert.Equal("A", grupoDe[favoritas[0]]);
            Assert.Equal("B", grupoDe[favoritas[1]]);
            Assert.Equal("C", grupoDe[favoritas[2]]);
        }
    }

    // ---- Apoio ----------------------------------------------------------------------------

    // Sorteia, lê a assinatura, DESFAZ e sorteia de novo — o caminho exato do Felipe (o botão
    // "Desfazer sorteio" existe justamente enquanto a chave espera aprovação).
    private static async Task<List<string>> SortearVariasVezesAsync(
        int qtdDuplas, Func<DbPadelContext, string> assinatura)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        var assinaturas = new List<string>();
        for (int sorteio = 1; sorteio <= Sorteios; sorteio++)
        {
            await controller.GerarChaves(torneio.Id);
            Assert.Equal(AprovacaoDeChaves.Pendente, torneio.Status);

            assinaturas.Add(assinatura(ctx));

            if (sorteio < Sorteios) await controller.DesfazerSorteio(torneio.Id);
        }
        return assinaturas;
    }

    private static string QuemCaiuEmQualGrupo(DbPadelContext ctx) =>
        string.Join("|", ctx.Duplas
            .Where(d => d.Grupo != null)
            .AsEnumerable()
            .OrderBy(d => d.Id)
            .Select(d => $"{d.Id}:{d.Grupo}"));

    private static string QuemJogaAQueHoras(DbPadelContext ctx) =>
        string.Join("|", ctx.Partidas
            .AsEnumerable()
            .OrderBy(p => p.HorarioPrevisto).ThenBy(p => p.Dupla1Id)
            .Select(p => $"{p.HorarioPrevisto:dd/MM HH:mm} {p.Dupla1Id}x{p.Dupla2Id}"));

    // Dá ranking REAL às três primeiras duplas da categoria, pelo caminho que o sorteio lê
    // (`EstatisticasService.ObterPontosPorJogadorAsync` soma campanha de torneio JÁ COMEÇADO).
    // Devolve os Ids delas, da mais forte pra menos forte.
    private static int[] SemearRankingAsync(DbPadelContext ctx, Categoria categoria)
    {
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).Take(3).ToList();

        var passado = new Torneio
        {
            Nome = "Etapa anterior",
            Codigo = "ANT123",
            Status = "Finalizado",
            DataInicio = new DateTime(2026, 5, 1, 9, 0, 0),
        };
        ctx.Torneios.Add(passado);
        var categoriaPassada = new Categoria { Nome = "2ª Categoria Masculina", Codigo = "CATANT", Torneio = passado };
        ctx.Categorias.Add(categoriaPassada);
        ctx.SaveChanges();

        // Campeão (100) > vice (60) > semifinal (35): três degraus distintos, sem empate.
        var campanhas = new[] { "Campeao", "Final", "Semifinal" };
        for (int i = 0; i < duplas.Count; i++)
        {
            ctx.Duplas.Add(new Dupla
            {
                CategoriaId = categoriaPassada.Id,
                Jogador1Id = duplas[i].Jogador1Id,
                Jogador2Id = duplas[i].Jogador2Id,
                UltimaFase = campanhas[i],
            });
        }
        ctx.SaveChanges();

        return duplas.Select(d => d.Id).ToArray();
    }
}
