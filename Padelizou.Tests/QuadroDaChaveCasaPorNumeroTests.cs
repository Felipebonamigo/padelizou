using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// O QUADRO DA ABA CHAVES CASA A PRÉVIA POR NÚMERO, E NÃO POR POSIÇÃO NA LISTA (10/09/2026).
//
// 🕳️ Achado por revisão adversarial, por TRÊS lentes independentes, horas depois do PR #128 ter
// corrigido a PRIMEIRA metade deste mesmo defeito. A cadeia:
//
//   1. `ProximasFasesDaChave.Agendar` emite `Numero = i + 1` — a ordem da CHAVE.
//   2. Uma RESERVA do organizador grava `quando = reserva.Horario` **sem mover o cursor** da
//      grade (é de propósito: "reservar a Semifinal 1 pras 21h não arrasta a Semifinal 2 junto").
//   3. `ProjetarProximasFasesAsync` devolve `projetados.OrderBy(j => j.Horario)` — ordem de HORA.
//   4. A view casava por POSIÇÃO: `daFase[i]`.
//
// Com a Semifinal 1 reservada pra DEPOIS da 2, o `OrderBy` põe a 2 primeiro e a vaga da
// Semifinal 1 no quadro passa a mostrar a hora e a quadra da Semifinal 2. O jogador lê no quadro
// da chave uma hora diferente da que a mesma prévia mostra na aba Jogos.
//
// ⚠️ O COMENTÁRIO DA VIEW AFIRMAVA A PREMISSA QUE ERA FALSA — "os dois saem do mesmo motor de
// chaveamento, na mesma ordem". Saem do mesmo motor; não chegam na mesma ordem, porque tem um
// `OrderBy` no meio do caminho.
//
// ⚠️ A revisão do PR #128 fechou o recorte por FILTRO (`ProjecaoCompleta` em vez de `JogosQueVem`,
// ver FiltroDeJogosNaoAtrapalhaAsOutrasTelasTests) e deixou a REORDENAÇÃO passar. Casar por
// número fecha as duas de uma vez: não importa quem recortou nem quem reordenou.
public class QuadroDaChaveCasaPorNumeroTests
{
    // ── O QUE TRAVA A CORREÇÃO ───────────────────────────────────────────────────────────
    //
    // Teste de FONTE porque a suíte não renderiza Razor: o casamento mora na view, e nenhum teste
    // de C# alcança `daFase[i]`. É o mesmo recurso que o PR #128 usou pra travar a leitura de
    // `ProjecaoCompleta`, e pelo mesmo motivo.
    [Fact]
    public void O_quadro_previsto_procura_pelo_numero_do_jogo()
    {
        var quadro = QuadroProjetado();

        Assert.Contains("j.Numero == i + 1", quadro, StringComparison.Ordinal);
    }

    [Fact]
    public void O_quadro_previsto_nao_casa_por_posicao_na_lista()
    {
        // A metade que importa: `daFase[i]` é o defeito, e ele volta calado num refactor que
        // "simplifique" a busca. A lista chega ordenada por HORA — posição não é número.
        var quadro = QuadroProjetado();

        Assert.DoesNotContain("daFase[i]", quadro, StringComparison.Ordinal);
    }

    // ── A PROVA DE QUE O PERIGO É REAL ───────────────────────────────────────────────────

    [Fact]
    public async Task Uma_reserva_fora_de_ordem_desalinha_a_lista_do_numero_do_jogo()
    {
        // ⚠️ TESTE DE CARACTERIZAÇÃO: ele descreve o estado do mundo (a lista vem por hora), não
        // a correção. Nasce verde, e por isso foi FALSIFICADO antes de entrar — ver o comentário
        // no fim deste arquivo. O que ele existe pra dizer é: quem casar por posição aqui vai
        // pegar o jogo errado, e não é hipótese.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);

        var (fase, cedo) = await FaseComDoisJogosAsync(ctx, torneio.Id, org.Id, categoria.Nome);

        // O organizador joga a nº 1 pra bem depois — é o que "Trocar horário"/"Definir horário"
        // numa prévia grava.
        ctx.ReservasDeHorario.Add(new ReservaDeHorario
        {
            CategoriaId = categoria.Id,
            Fase = fase,
            Numero = 1,
            Horario = cedo.AddHours(5),
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var daFase = await ProjecaoDaFaseAsync(ctx, torneio.Id, org.Id, categoria.Nome, fase);

        // A lista chega ordenada por HORA, então a nº 2 vem primeiro...
        Assert.Equal(2, daFase[0].Numero);

        // ...e é exatamente por isso que a posição não serve: quem pegasse `daFase[0]` pra
        // preencher a vaga da nº 1 mostraria a hora da nº 2.
        Assert.NotEqual(1, daFase[0].Numero);
        Assert.Equal(1, daFase.Single(j => j.Numero == 1).Numero);
    }

    [Fact]
    public async Task Sem_reserva_nenhuma_a_lista_ja_sai_na_ordem_do_numero()
    {
        // O contraponto, e é ele que explica por que o defeito nunca apareceu antes: sem reserva
        // o cursor da grade só anda pra frente, então posição e número coincidem. Foi essa
        // coincidência que sustentou o `daFase[i]` até o "trocar horário da prévia" existir.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);
        ctx.ChangeTracker.Clear();

        var (fase, _) = await FaseComDoisJogosAsync(ctx, torneio.Id, org.Id, categoria.Nome);
        var daFase = await ProjecaoDaFaseAsync(ctx, torneio.Id, org.Id, categoria.Nome, fase);

        Assert.Equal(Enumerable.Range(1, daFase.Count), daFase.Select(j => j.Numero));
    }

    // ── infra ────────────────────────────────────────────────────────────────────────────

    // A projeção desta categoria e fase, como a aba Chaves a lê (ViewBag.ProjecaoCompleta).
    private static async Task<List<JogoQueVem>> ProjecaoDaFaseAsync(
        DbPadelContext ctx, int torneioId, int organizadorId, string categoria, string fase)
    {
        ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(ctx, organizadorId);
        Assert.IsType<ViewResult>(await controller.Jogos(torneioId, null, null));

        var completa = (List<JogoQueVem>)controller.ViewBag.ProjecaoCompleta;
        return completa.Where(j => j.Categoria == categoria && j.Fase == fase).ToList();
    }

    // A primeira fase projetada que tem DOIS jogos — é o mínimo pra posição e número poderem
    // discordar. Devolve o nome dela e o horário mais cedo que ela ocupa.
    private static async Task<(string Fase, DateTime Cedo)> FaseComDoisJogosAsync(
        DbPadelContext ctx, int torneioId, int organizadorId, string categoria)
    {
        ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(ctx, organizadorId);
        Assert.IsType<ViewResult>(await controller.Jogos(torneioId, null, null));

        var completa = (List<JogoQueVem>)controller.ViewBag.ProjecaoCompleta;
        var fase = completa
            .Where(j => j.Categoria == categoria && j.Horario != null)
            .GroupBy(j => j.Fase)
            .FirstOrDefault(g => g.Count() >= 2);

        Assert.True(fase != null,
            "A projeção precisa ter uma fase com 2+ jogos pro teste significar alguma coisa — "
            + "sem isso posição e número nunca discordariam. Fases vistas: "
            + string.Join(", ", completa.Where(j => j.Categoria == categoria)
                .GroupBy(j => j.Fase).Select(g => $"{g.Key}={g.Count()}")));

        return (fase!.Key, fase.Min(j => j.Horario!.Value));
    }

    // O bloco do quadro previsto na aba Chaves, isolado do resto da view — assim uma ocorrência
    // de `daFase[i]` em qualquer outro lugar do arquivo não faz o teste mentir nos dois sentidos.
    private static string QuadroProjetado()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

        // ⚠️ AS ÂNCORAS MUDARAM EM 11/09/2026, o que elas guardam não: o quadro virou uma ÁRVORE
        // num partial (_ChaveProjetadaArvore), e o casamento com a projeção ficou aqui, no
        // `Details.cshtml`, de propósito — a árvore reordena os jogos (a primeira rodada sai
        // 1, 4, 2, 3), então casar lá dentro, na ordem do desenho, pegaria o jogo errado.
        var inicio = fonte.IndexOf("var previstosDoQuadro", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o casamento da chave prevista (previstosDoQuadro) na página do torneio.");

        var fim = fonte.IndexOf("_ChaveProjetadaArvore", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei o partial que recebe o quadro (_ChaveProjetadaArvore).");

        return fonte[inicio..fim];
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou a partir de " + AppContext.BaseDirectory);
    }

    // ⚠️ COMO OS DOIS DE CARACTERIZAÇÃO FORAM FALSIFICADOS (Regra 1), já que nascem verdes:
    // tirando o `OrderBy(j => j.Horario ?? DateTime.MaxValue)` do fim de
    // `TorneiosController.ProjetarProximasFasesAsync`, o primeiro quebra em
    // `Assert.Equal(2, daFase[0].Numero)` (sem a reordenação a lista sai na ordem de emissão,
    // e a nº 1 volta pra frente mesmo reservada pra depois) — que é a prova de que é o `OrderBy`,
    // e não outra coisa, quem desalinha a lista.
}
