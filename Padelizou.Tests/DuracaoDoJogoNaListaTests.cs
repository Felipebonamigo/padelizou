using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — QUANDO O JOGO ACABOU, E QUANTO ELE DUROU.
//
// 🗣️ Felipe, com as Finalizadas do 2ª Etapa ER PADEL TOUR na tela: *"as finalizadas tem q
// ficar em ordem da mais recente finalizada para mais tempo atras"* e, logo depois: *"coloque
// também o tempo de duração da partida, em baixo do horario previsto, coloque que horario
// começou e que horario terminou mas nao pode ocupar muito a tela, nao podemos poluir muito"*.
//
// 🕳️ OS DOIS PEDIDOS SÃO O MESMO DEFEITO, VISTO DE FORA. A lista JÁ ordenava pelo fim real
// (08/08/2026) — mas o card mostra o HORÁRIO PREVISTO, e só ele. Quem varre a tela lê
// "21:20 · 20:30 · 19:40 · 22:05" e conclui que a ordem está quebrada, porque a grandeza que
// ordena não está escrita em lugar nenhum. Mostrar começo e fim é o que torna a ordem legível.
//
// ⚠️ E a régua do "quando aconteceu" tinha DOIS passos (`HorarioFimReal ?? HorarioPrevisto`),
// enquanto o resto do sistema (EstatisticasService, MvpDoTorneio, EnqueteDoTorneio) usa TRÊS.
// A diferença aparece no jogo que entrou em quadra e teve o placar lançado sem carimbo de fim:
// ele voltava pro horário do SORTEIO — num torneio por ordem de liberação, uma hora que nunca
// existiu — e caía no meio da lista. Agora a régua é uma só: Services/DuracaoDoJogo.
public class DuracaoDoJogoNaListaTests
{
    // ---- 1. A RÉGUA: QUANDO ESTE JOGO ACONTECEU ----

    [Fact]
    public void O_fim_real_manda_em_tudo()
    {
        var jogo = new Partida
        {
            HorarioPrevisto = new DateTime(2026, 9, 11, 19, 0, 0),
            HorarioInicioReal = new DateTime(2026, 9, 11, 21, 30, 0),
            HorarioFimReal = new DateTime(2026, 9, 11, 22, 19, 0),
        };

        Assert.Equal(new DateTime(2026, 9, 11, 22, 19, 0), DuracaoDoJogo.Quando(jogo));
    }

    [Fact]
    public void Sem_carimbo_de_fim_vale_a_LARGADA_real_e_nao_o_horario_do_sorteio()
    {
        // O passo do meio, que faltava. O jogo entrou em quadra às 21:30; dizer que ele
        // aconteceu às 19:00 é repetir a grade em vez de contar o que houve.
        var jogo = new Partida
        {
            HorarioPrevisto = new DateTime(2026, 9, 11, 19, 0, 0),
            HorarioInicioReal = new DateTime(2026, 9, 11, 21, 30, 0),
        };

        Assert.Equal(new DateTime(2026, 9, 11, 21, 30, 0), DuracaoDoJogo.Quando(jogo));
    }

    [Fact]
    public void Sem_carimbo_nenhum_sobra_o_horario_previsto()
    {
        var jogo = new Partida { HorarioPrevisto = new DateTime(2026, 9, 11, 19, 0, 0) };
        Assert.Equal(new DateTime(2026, 9, 11, 19, 0, 0), DuracaoDoJogo.Quando(jogo));
    }

    [Fact]
    public void Torneio_por_ordem_de_liberacao_sem_hora_nenhuma_devolve_nulo()
    {
        Assert.Null(DuracaoDoJogo.Quando(new Partida()));
    }

    // ---- 2. O RÓTULO: COMEÇOU, TERMINOU, DUROU ----

    [Fact]
    public void Comecou_e_terminou_viram_uma_linha_so_com_a_duracao()
    {
        var jogo = new Partida
        {
            HorarioInicioReal = new DateTime(2026, 9, 11, 19, 42, 0),
            HorarioFimReal = new DateTime(2026, 9, 11, 20, 31, 0),
        };

        Assert.Equal("19:42–20:31 · 49 min", DuracaoDoJogo.Rotulo(jogo));
    }

    [Fact]
    public void So_a_largada_carimbada_diz_so_a_largada()
    {
        // Duração não se inventa: sem o fim não há conta a fazer.
        var jogo = new Partida { HorarioInicioReal = new DateTime(2026, 9, 11, 19, 42, 0) };
        Assert.Equal("começou 19:42", DuracaoDoJogo.Rotulo(jogo));
    }

    [Fact]
    public void So_o_fim_carimbado_diz_so_o_fim()
    {
        var jogo = new Partida { HorarioFimReal = new DateTime(2026, 9, 11, 20, 31, 0) };
        Assert.Equal("terminou 20:31", DuracaoDoJogo.Rotulo(jogo));
    }

    [Fact]
    public void Jogo_que_nunca_entrou_em_quadra_nao_diz_nada()
    {
        // O MESMO card desenha as Agendadas. Rótulo vazio é o que mantém a lista limpa lá —
        // o pedido era não poluir.
        Assert.Null(DuracaoDoJogo.Rotulo(new Partida
        {
            HorarioPrevisto = new DateTime(2026, 9, 11, 19, 0, 0),
        }));
    }

    [Fact]
    public void Fim_ANTES_do_comeco_mostra_os_dois_carimbos_sem_duracao_negativa()
    {
        // Correção na mão pode deixar os carimbos trocados. "-12 min" na tela é pior que
        // silêncio sobre a duração; os dois horários continuam sendo verdade.
        var jogo = new Partida
        {
            HorarioInicioReal = new DateTime(2026, 9, 11, 20, 31, 0),
            HorarioFimReal = new DateTime(2026, 9, 11, 20, 19, 0),
        };

        Assert.Equal("20:31–20:19", DuracaoDoJogo.Rotulo(jogo));
    }

    [Fact]
    public void A_duracao_sai_do_MinutosDecorridos_do_proprio_jogo()
    {
        // Uma conta só: o cronômetro do card AO VIVO já usa esta propriedade. Duas contas de
        // "quanto durou" é como a tela do fim discorda da tela do meio.
        var jogo = new Partida
        {
            HorarioInicioReal = new DateTime(2026, 9, 11, 19, 0, 0),
            HorarioFimReal = new DateTime(2026, 9, 11, 20, 18, 0),
        };

        Assert.Equal(78, jogo.MinutosDecorridos);
        Assert.Contains($"{jogo.MinutosDecorridos} min", DuracaoDoJogo.Rotulo(jogo));
    }

    // ---- 3. A ORDEM DAS FINALIZADAS, PELA TELA ----

    [Fact]
    public async Task A_lista_vai_da_mais_recente_finalizada_para_a_mais_antiga()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = MontarTresJogos(ctx);

        // Finalizados FORA da ordem do sorteio e fora da ordem da grade: o do meio acabou
        // por último. É o caso do organizador lançando placar atrasado.
        Finalizar(ctx, c.Jogos[0], fim: new DateTime(2026, 9, 11, 22, 05, 0));
        Finalizar(ctx, c.Jogos[2], fim: new DateTime(2026, 9, 11, 22, 12, 0));
        Finalizar(ctx, c.Jogos[1], fim: new DateTime(2026, 9, 11, 22, 30, 0));
        await ctx.SaveChangesAsync();

        Assert.Equal(new[] { c.Jogos[1].Id, c.Jogos[2].Id, c.Jogos[0].Id }, await FinalizadasAsync(ctx, c));
    }

    [Fact]
    public async Task Jogo_sem_carimbo_de_fim_entra_pela_LARGADA_real_e_nao_pelo_horario_previsto()
    {
        using var ctx = TestInfra.NovoContexto();
        var c = MontarTresJogos(ctx);

        // O jogo das 19:40 foi o ÚLTIMO a entrar em quadra (21:30) e teve o placar lançado
        // sem carimbo de fim. Pela régua de dois passos ele voltava pras 19:40 e afundava
        // pro fim da lista, abaixo de jogos que acabaram antes dele começar.
        Finalizar(ctx, c.Jogos[0], fim: null);
        c.Jogos[0].HorarioInicioReal = new DateTime(2026, 9, 11, 21, 30, 0);

        Finalizar(ctx, c.Jogos[1], fim: new DateTime(2026, 9, 11, 20, 25, 0));
        Finalizar(ctx, c.Jogos[2], fim: new DateTime(2026, 9, 11, 21, 15, 0));
        await ctx.SaveChangesAsync();

        Assert.Equal(new[] { c.Jogos[0].Id, c.Jogos[2].Id, c.Jogos[1].Id }, await FinalizadasAsync(ctx, c));
    }

    // ---- 4. O QUE SÓ EXISTE NA VIEW (a suíte não renderiza Razor) ----

    [Fact]
    public void O_card_da_lista_mostra_o_rotulo_da_duracao()
    {
        var fonte = TestInfra.SemComentarios(File.ReadAllText(
            Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", "_JogoEmLinha.cshtml")));

        Assert.Contains("DuracaoDoJogo.Rotulo", fonte, StringComparison.Ordinal);
        Assert.Contains("pdz-jl-durou", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void A_linha_da_duracao_nasce_DEBAIXO_do_horario_previsto()
    {
        // A linha ocupa as DUAS colunas da segunda faixa do grid — é isso que a põe embaixo
        // em vez de ao lado. Sem isso ela entra na MESMA linha da hora e empurra as etiquetas
        // de categoria/fase/quadra pra baixo — o oposto de "não poluir".
        //
        // ⚠️ E o container é GRID, não flex com quebra: medido no Chromium, `flex-wrap` mais um
        // filho de `flex-basis: 100%` inflava a caixa da hora de 119px pra 237px (o max-content
        // soma os dois numa linha só), e a 600px de tela isso jogava as etiquetas pra uma
        // segunda linha que antes não existia.
        var css = TestInfra.SemComentarios(File.ReadAllText(
            Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css")));

        var regra = TrechoDaRegra(css, ".pdz-jl-quando .pdz-jl-durou");
        Assert.Contains("grid-column: 1 / -1", regra, StringComparison.Ordinal);

        var container = TrechoDaRegra(css, ".pdz-jl-quando");
        Assert.Contains("display: grid", container, StringComparison.Ordinal);
        Assert.DoesNotContain("flex-wrap", container, StringComparison.Ordinal);
    }

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }

    private static string TrechoDaRegra(string css, string seletor)
    {
        var i = css.IndexOf(seletor + " {", StringComparison.Ordinal);
        Assert.True(i >= 0, $"Não achei a regra `{seletor}` no site.css.");
        var fim = css.IndexOf('}', i);
        return css[i..fim];
    }

    // ---- infra do cenário ----

    private sealed record Cenario(Torneio Torneio, Jogador Organizador, List<Partida> Jogos);

    private static Cenario MontarTresJogos(DbPadelContext ctx)
    {
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6, status: "Em Andamento");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();

        var jogos = new List<Partida>();
        var horarios = new[]
        {
            new DateTime(2026, 9, 11, 19, 40, 0),
            new DateTime(2026, 9, 11, 20, 30, 0),
            new DateTime(2026, 9, 11, 21, 20, 0),
        };

        for (int i = 0; i < 3; i++)
        {
            var jogo = new Partida
            {
                TorneioId = torneio.Id,
                CategoriaId = categoria.Id,
                Dupla1Id = duplas[i * 2].Id,
                Dupla2Id = duplas[i * 2 + 1].Id,
                Fase = "Grupo A",
                Status = "Agendada",
                NomeQuadra = "Quadra 1",
                HorarioPrevisto = horarios[i],
                Codigo = $"JOG{i}",
            };
            ctx.Partidas.Add(jogo);
            jogos.Add(jogo);
        }

        ctx.SaveChanges();
        return new Cenario(torneio, org, jogos);
    }

    private static void Finalizar(DbPadelContext ctx, Partida jogo, DateTime? fim)
    {
        jogo.Status = "Finalizada";
        jogo.GamesDupla1 = 9;
        jogo.GamesDupla2 = 5;
        jogo.VencedorId = jogo.Dupla1Id;
        jogo.HorarioFimReal = fim;
    }

    private static async Task<List<int>> FinalizadasAsync(DbPadelContext ctx, Cenario c)
    {
        ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(ctx, c.Organizador.Id);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await controller.Jogos(c.Torneio.Id, null, null));
        return ((List<Partida>)controller.ViewBag.Finalizadas).Select(p => p.Id).ToList();
    }
}
