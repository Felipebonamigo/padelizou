using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 🗣️ Felipe, 09/09/2026: "permita também, que o organizador, troque a dupla de lugar no grupo,
// e ao trocar, verifique os horarios com impedimentos novamente, se nao vai atrapalhar algum".
//
// O sorteio passou a sortear de verdade no mesmo dia — e aí o ajuste fino em cima do resultado
// vira necessidade: o organizador olha a chave que saiu e quer mover UMA dupla, sem torrar o
// sorteio inteiro no "Desfazer".
//
// 🔀 A TROCA É UM SWAP, não uma mudança: duas duplas trocam de grupo entre si. Mover uma só
// desbalancearia os grupos (o "2,2,3,3,3,3" da categoria de 16 viraria "1,2,3,3,3,4") e
// desmancharia o desenho que o organizador escolheu na criação.
//
// 🕐 E A GRADE É REFEITA DEPOIS, pelo MESMO EncaixarNasLevas do "Refazer grade" — é ele que já
// conhece impedimento pago, concentração, noite de sábado, quadra preferida e sede. Reencaixar
// à mão aqui seria a segunda cópia da regra que o comentário do EncaixarNasLevas já proíbe.
public class TrocaDeDuplaEntreGruposTests
{
    // 03/07/2026 é SEXTA — o único formato em que as duas janelas de impedimento que importam
    // existem de verdade: a sexta (dia inteiro, porque só tem turno da noite) e o sábado (que
    // cai no dia seguinte, dentro dos 3 dias que JanelasDeImpedimento.DiaDoTorneio varre).
    private static readonly DateTime SextaDeAbertura = new(2026, 7, 3, 9, 0, 0);

    [Fact]
    public async Task As_duas_duplas_trocam_de_grupo_e_os_tamanhos_ficam_de_pe()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, controller) = await SortearAsync(ctx, qtdDuplas: 16);

        var (a, b) = await DuasDuplasDeGruposDiferentesAsync(ctx, categoria);
        var (grupoDeA, grupoDeB) = (a.Grupo, b.Grupo);

        await controller.TrocarDuplasDeGrupo(torneio.Id, a.Id, b.Id);

        await ctx.Entry(a).ReloadAsync();
        await ctx.Entry(b).ReloadAsync();
        Assert.Equal(grupoDeB, a.Grupo);
        Assert.Equal(grupoDeA, b.Grupo);

        // ⚠️ O GrupoTorneioId anda junto com a letra. Deixar os dois fora de sincronia é o que
        // já quebrou a Mesa de Controle antes: metade das telas lê a letra, metade lê a FK.
        var gruposPorLetra = await ctx.Set<GrupoTorneio>()
            .Where(g => g.CategoriaId == categoria.Id)
            .ToDictionaryAsync(g => g.Nome, g => g.Id);
        Assert.Equal(gruposPorLetra[$"Grupo {grupoDeB}"], a.GrupoTorneioId);
        Assert.Equal(gruposPorLetra[$"Grupo {grupoDeA}"], b.GrupoTorneioId);

        // O desenho da categoria não mudou: 16 duplas continuam em 2 grupos de 2 e 4 de 3.
        var tamanhos = await ctx.Duplas
            .Where(d => d.CategoriaId == categoria.Id && d.Grupo != null)
            .GroupBy(d => d.Grupo!)
            .Select(g => g.Count())
            .ToListAsync();
        Assert.Equal("2,2,3,3,3,3", string.Join(",", tamanhos.OrderBy(t => t)));
    }

    // 10/09/2026, ensaio do Er: a mensagem dizia "Dupla 11 foi pro Grupo B e Dupla 16 pro
    // Grupo A" — o controller carregava as duplas sem os jogadores, e NomeDeExibicao caía no
    // número. Ninguém confere na mão uma troca que não diz QUEM trocou.
    [Fact]
    public async Task A_mensagem_diz_quem_trocou_pelo_nome_e_nao_pelo_numero()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, controller) = await SortearAsync(ctx, qtdDuplas: 16);
        var (a, b) = await DuasDuplasDeGruposDiferentesAsync(ctx, categoria);
        var nomeDeA = (await ctx.Jogadores.SingleAsync(j => j.Id == a.Jogador1Id)).Nome;
        var nomeDeB = (await ctx.Jogadores.SingleAsync(j => j.Id == b.Jogador1Id)).Nome;

        // Como em produção: a requisição nasce sem nada rastreado. Sem isto o InMemory
        // preenche Jogador1/Jogador2 de graça e o teste passaria com a consulta errada.
        ctx.ChangeTracker.Clear();
        await controller.TrocarDuplasDeGrupo(torneio.Id, a.Id, b.Id);

        var mensagem = (string?)(controller.TempData["Sucesso"] ?? controller.TempData["Erro"]) ?? "";
        Assert.Contains(nomeDeA, mensagem);
        Assert.Contains(nomeDeB, mensagem);
        Assert.DoesNotContain($"Dupla {a.Id} ", mensagem);
    }

    [Fact]
    public async Task Os_confrontos_do_grupo_passam_a_ser_com_a_dupla_que_entrou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, controller) = await SortearAsync(ctx, qtdDuplas: 16);

        var (a, b) = await DuasDuplasDeGruposDiferentesAsync(ctx, categoria);
        var adversariosDeA = await AdversariosAsync(ctx, a.Id);
        var adversariosDeB = await AdversariosAsync(ctx, b.Id);

        await controller.TrocarDuplasDeGrupo(torneio.Id, a.Id, b.Id);

        // Cada uma herda EXATAMENTE os adversários da outra — é o que "trocar de lugar"
        // significa num grupo de todos-contra-todos.
        Assert.Equal(adversariosDeB, await AdversariosAsync(ctx, a.Id));
        Assert.Equal(adversariosDeA, await AdversariosAsync(ctx, b.Id));

        // E ninguém joga contra si mesmo nem ficou órfão.
        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.All(jogos, j => Assert.NotEqual(j.Dupla1Id, j.Dupla2Id));
    }

    [Fact]
    public async Task Ao_trocar_a_grade_e_refeita_e_o_impedimento_pago_continua_valendo()
    {
        // 🎯 O CORAÇÃO DO PEDIDO. O cenário é montado pra ser uma violação GARANTIDA sem o
        // recálculo: a dupla que joga na SEXTA troca de lugar com uma que joga só no SÁBADO, e
        // a que vem do sábado é justamente a que pagou pra não jogar sexta. Sem refazer a
        // grade, ela herda o horário de sexta da outra e cai dentro da própria janela.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, controller) = await SortearAsync(ctx, qtdDuplas: 16);

        var sexta = SextaDeAbertura.Date;
        var daSexta = await UmaDuplaQueJogaEmAsync(ctx, categoria, sexta);
        var doSabado = await UmaDuplaQueJogaEmAsync(ctx, categoria, sexta.AddDays(1), exceto: daSexta.Grupo);

        // O impedimento é PAGO na inscrição — o sistema vendeu essa garantia.
        doSabado.ImpedimentoSextaNoite = true;
        await ctx.SaveChangesAsync();

        await controller.TrocarDuplasDeGrupo(torneio.Id, daSexta.Id, doSabado.Id);

        Assert.Empty(await FurosDeImpedimentoAsync(ctx, torneio));
    }

    [Fact]
    public async Task A_troca_nao_atrapalha_o_impedimento_de_quem_nao_pediu_nada()
    {
        // 🎯 "se nao vai atrapalhar algum" — a pergunta do Felipe vale pro torneio INTEIRO, e
        // não só pras duas duplas mexidas: remanejar a grade move o horário de quem não pediu
        // nada, então o furo poderia nascer em qualquer lugar.
        //
        // ⚠️ 8 DE 16 IMPEDIDAS — METADE DA CATEGORIA, e esse volume só passou a ser testável
        // quando o motor de grade foi consertado, no mesmo dia. A primeira versão deste teste
        // rodava com 6 e era FLAKY: o `VagasDaGrade` garantia chegar ao fim da janela mais
        // `max(quadras,1)*3` vagas, margem por QUADRA e não pelo volume de jogos que a janela
        // empurrava — então quem cedia era o motor, não a troca, e o teste passava ou falhava
        // por sorte do sorteio. Com `VagasDaGrade.JogosComJanela` dimensionando o alcance, o
        // invariante virou absoluto e este teste pôde subir pro volume que interessa.
        //
        // A guarda do motor mora em ImpedimentoNaGradeCheiaTests (varre 1, 2, 4 e 6 quadras
        // contra 4 a 16 duplas impedidas). Aqui o que se testa é a TROCA: que refazer a grade
        // depois de mexer nos grupos reavalia as janelas de todo mundo, e não só das duas
        // duplas mexidas.
        for (int rodada = 1; rodada <= 8; rodada++)
        {
            using var ctx = TestInfra.NovoContexto();
            var (torneio, categoria, _, controller) = await SortearAsync(ctx, qtdDuplas: 16,
                antesDeSortear: async c =>
                {
                    // Marcado ANTES do sorteio, que é o fluxo real: a dupla escolhe (e paga) a
                    // janela na inscrição, muito antes de existir chave.
                    var todas = await ctx.Duplas.Where(d => d.CategoriaId == c.Id)
                        .OrderBy(d => d.Id).ToListAsync();
                    for (int i = 0; i < todas.Count; i += 2) todas[i].ImpedimentoSextaNoite = true;
                    await ctx.SaveChangesAsync();
                });

            // A grade que o sorteio entregou já respeita as janelas — é a linha de base.
            Assert.Empty(await FurosDeImpedimentoAsync(ctx, torneio));

            var (a, b) = await DuasDuplasDeGruposDiferentesAsync(ctx, categoria);
            await controller.TrocarDuplasDeGrupo(torneio.Id, a.Id, b.Id);

            // E continua respeitando depois de a troca refazer tudo.
            Assert.Empty(await FurosDeImpedimentoAsync(ctx, torneio));

            // Ninguém chamado pra dois jogos no mesmo horário: isso é impossível de cumprir,
            // então aqui o invariante nunca cede.
            Assert.Empty(await ChoquesDePessoaAsync(ctx, torneio));
        }
    }

    [Fact]
    public async Task Todo_jogo_continua_com_hora_depois_da_troca()
    {
        // Guarda de canto: o recálculo devolve `HorarioPrevisto` nulo pro que não coube no
        // expediente. Um jogo sem hora depois de uma troca seria a dupla sumindo da grade.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, controller) = await SortearAsync(ctx, qtdDuplas: 16);

        var (a, b) = await DuasDuplasDeGruposDiferentesAsync(ctx, categoria);
        await controller.TrocarDuplasDeGrupo(torneio.Id, a.Id, b.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
    }

    [Fact]
    public async Task So_troca_enquanto_a_chave_espera_aprovacao()
    {
        // Depois de aprovada a chave é pública: tem gente que já viu contra quem joga e se
        // organizou pra um horário. É a mesma régua do "Desfazer sorteio".
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, controller) = await SortearAsync(ctx, qtdDuplas: 16);
        var (a, b) = await DuasDuplasDeGruposDiferentesAsync(ctx, categoria);
        var (grupoDeA, grupoDeB) = (a.Grupo, b.Grupo);

        await controller.AprovarChaves(torneio.Id);
        await controller.TrocarDuplasDeGrupo(torneio.Id, a.Id, b.Id);

        await ctx.Entry(a).ReloadAsync();
        await ctx.Entry(b).ReloadAsync();
        Assert.Equal(grupoDeA, a.Grupo);
        Assert.Equal(grupoDeB, b.Grupo);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_troca()
    {
        // Regra 0: ação que grava dado precisa de dono conferido. O gate mecânico cobre o
        // [Authorize]; a checagem de dono é trabalho deste teste.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, _) = await SortearAsync(ctx, qtdDuplas: 16);

        var intruso = new Jogador { Nome = "Intruso", Cpf = "11100000011" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var (a, b) = await DuasDuplasDeGruposDiferentesAsync(ctx, categoria);
        var (grupoDeA, grupoDeB) = (a.Grupo, b.Grupo);

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .TrocarDuplasDeGrupo(torneio.Id, a.Id, b.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        await ctx.Entry(a).ReloadAsync();
        await ctx.Entry(b).ReloadAsync();
        Assert.Equal(grupoDeA, a.Grupo);
        Assert.Equal(grupoDeB, b.Grupo);
    }

    [Fact]
    public async Task Duas_duplas_do_mesmo_grupo_nao_sao_troca()
    {
        // Trocar duas duplas do MESMO grupo não muda confronto nenhum (todos-contra-todos é o
        // mesmo conjunto) — mas refaria a grade inteira à toa, mexendo no horário de todo mundo.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, controller) = await SortearAsync(ctx, qtdDuplas: 16);

        var doMesmoGrupo = await ctx.Duplas
            .Where(d => d.CategoriaId == categoria.Id && d.Grupo != null)
            .ToListAsync();
        var par = doMesmoGrupo.GroupBy(d => d.Grupo!).First(g => g.Count() >= 2).Take(2).ToList();

        var antes = await GradeAsync(ctx, torneio);
        await controller.TrocarDuplasDeGrupo(torneio.Id, par[0].Id, par[1].Id);

        Assert.Equal(antes, await GradeAsync(ctx, torneio));
    }

    [Fact]
    public void A_tela_oferece_a_troca_so_enquanto_a_chave_espera_aprovacao()
    {
        // ⚠️ Esconder o botão NÃO é autorização (a ação recusa de novo do lado de lá) — mas
        // oferecer a troca numa chave já pública seria prometer o que o servidor vai negar, e
        // pior: convidar o organizador a remontar grupo de gente que já se organizou.
        var fonte = Details();
        int form = fonte.IndexOf("asp-action=\"TrocarDuplasDeGrupo\"", StringComparison.Ordinal);

        Assert.True(form > 0, "O formulário de trocar duplas de grupo sumiu da aba de grupos.");

        var guardaAcima = fonte[Math.Max(0, form - 1500)..form];
        Assert.Contains("AprovacaoDeChaves.Pendente", guardaAcima);
        Assert.Contains("PodeAprovarChaves", guardaAcima);
    }

    // ---- Apoio ------------------------------------------------------------------------------

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }


    private static async Task<(Torneio, Categoria, Jogador, Padelizou.Controllers.TorneiosController)>
        SortearAsync(DbPadelContext ctx, int qtdDuplas, Func<Categoria, Task>? antesDeSortear = null)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas);
        torneio.DataInicio = SextaDeAbertura;
        await ctx.SaveChangesAsync();

        // O gancho serve pra marcar impedimento ANTES do sorteio — que é o fluxo real (a dupla
        // escolhe a janela na inscrição) e o único em que o sorteio já nasce conhecendo-a.
        if (antesDeSortear != null) await antesDeSortear(categoria);

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.GerarChaves(torneio.Id);
        return (torneio, categoria, organizador, controller);
    }

    private static async Task<(Dupla, Dupla)> DuasDuplasDeGruposDiferentesAsync(
        DbPadelContext ctx, Categoria categoria)
    {
        var duplas = await ctx.Duplas
            .Where(d => d.CategoriaId == categoria.Id && d.Grupo != null)
            .OrderBy(d => d.Id)
            .ToListAsync();
        var a = duplas[0];
        var b = duplas.First(d => d.Grupo != a.Grupo);
        return (a, b);
    }

    private static async Task<Dupla> UmaDuplaQueJogaEmAsync(
        DbPadelContext ctx, Categoria categoria, DateTime dia, string? exceto = null)
    {
        var jogos = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.HorarioPrevisto != null)
            .ToListAsync();
        var duplas = await ctx.Duplas
            .Where(d => d.CategoriaId == categoria.Id && d.Grupo != null)
            .ToListAsync();

        return duplas.First(d =>
            d.Grupo != exceto &&
            jogos.Where(j => j.Dupla1Id == d.Id || j.Dupla2Id == d.Id)
                 .All(j => j.HorarioPrevisto!.Value.Date == dia));
    }

    private static async Task<string> AdversariosAsync(DbPadelContext ctx, int duplaId) =>
        string.Join(",", (await ctx.Partidas
            .Where(p => p.Dupla1Id == duplaId || p.Dupla2Id == duplaId)
            .ToListAsync())
            .Select(p => p.Dupla1Id == duplaId ? p.Dupla2Id : p.Dupla1Id)
            .OrderBy(id => id));

    private static async Task<string> GradeAsync(DbPadelContext ctx, Torneio torneio) =>
        string.Join("|", (await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync())
            .OrderBy(p => p.Id)
            .Select(p => $"{p.Id}:{p.Dupla1Id}x{p.Dupla2Id}@{p.HorarioPrevisto:dd/MM HH:mm}/{p.NomeQuadra}"));

    // Todo jogo marcado DENTRO de uma janela que a dupla pagou pra evitar.
    private static async Task<List<string>> FurosDeImpedimentoAsync(DbPadelContext ctx, Torneio torneio)
    {
        var cheio = await ctx.Torneios
            .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
            .FirstAsync(t => t.Id == torneio.Id);
        var janelas = JanelasDeImpedimento.PorDupla(cheio);
        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto != null).ToListAsync();

        return (from jogo in jogos
                from duplaId in new[] { jogo.Dupla1Id, jogo.Dupla2Id }
                where janelas.TryGetValue(duplaId, out var janelasDaDupla)
                      && janelasDaDupla.Any(j => jogo.HorarioPrevisto >= j.Inicio && jogo.HorarioPrevisto < j.Fim)
                select $"dupla {duplaId} em {jogo.HorarioPrevisto:dd/MM HH:mm}").ToList();
    }

    // A mesma pessoa chamada pra dois jogos no mesmo horário.
    private static async Task<List<string>> ChoquesDePessoaAsync(DbPadelContext ctx, Torneio torneio)
    {
        var jogos = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto != null)
            .Include(p => p.Dupla1).Include(p => p.Dupla2)
            .ToListAsync();

        return jogos
            .GroupBy(j => j.HorarioPrevisto)
            .SelectMany(porHorario => porHorario
                .SelectMany(j => new[]
                {
                    j.Dupla1.Jogador1Id, j.Dupla1.Jogador2Id!.Value,
                    j.Dupla2.Jogador1Id, j.Dupla2.Jogador2Id!.Value,
                })
                .GroupBy(jogadorId => jogadorId)
                .Where(g => g.Count() > 1)
                .Select(g => $"{porHorario.Key:dd/MM HH:mm} jogador {g.Key}"))
            .ToList();
    }
}
