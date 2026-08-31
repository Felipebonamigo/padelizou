using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A escolha do organizador de quem pode marcar placar (26/08/2026): Organizacao (o padrão
// de sempre), JogadoresEmQuadra (os 4 do jogo) ou Inscritos (qualquer inscrito, mais os
// assistentes do Padelizou). A escolha só abre MARCAR PLACAR e INICIAR JOGO — W.O.,
// reabrir, voltar pra agendado e quadra/transmissão continuam só da organização, e é este
// arquivo que trava isso.
public class QuemMarcaOPlacarTests
{
    // Torneio com 3 duplas: a partida usa as duas primeiras; a terceira é o inscrito que
    // NÃO está em quadra. Devolve os atores que os testes precisam.
    private static (Partida partida, Jogador emQuadra, Jogador inscritoForaDeQuadra, Jogador estranho)
        MontarCenario(DbPadelContext ctx, string modo)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 3, status: "Fase de Grupos");
        torneio.QuemMarcaPlacar = modo;

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();
        var partida = new Partida
        {
            CategoriaId = categoria.Id,
            TorneioId = torneio.Id,
            Codigo = "P1",
            Fase = "Grupo A",
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "AoVivo",
            GamesDupla1 = 0,
            GamesDupla2 = 0,
        };
        ctx.Partidas.Add(partida);

        var estranho = new Jogador { Nome = "Estranho", Cpf = "88888888888" };
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var emQuadra = ctx.Jogadores.Find(duplas[0].Jogador1Id)!;
        var foraDeQuadra = ctx.Jogadores.Find(duplas[2].Jogador1Id)!;
        return (partida, emQuadra, foraDeQuadra, estranho);
    }

    // ── O que cada modo abre ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Jogador_em_quadra_marca_o_placar_no_modo_jogadores_em_quadra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, emQuadra, _, _) = MontarCenario(ctx, QuemMarcaOPlacar.JogadoresEmQuadra);

        var controller = TestInfra.NovoPartidasController(ctx, emQuadra.Id);
        var resultado = await controller.ControlePlacar(partida.Id, "AoVivo", 6, 3, null, null);

        Assert.IsNotType<ForbidResult>(resultado);
        Assert.Equal(6, (await ctx.Partidas.FindAsync(partida.Id))!.GamesDupla1);
    }

    [Fact]
    public async Task Inscrito_fora_de_quadra_nao_marca_no_modo_jogadores_em_quadra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, foraDeQuadra, _) = MontarCenario(ctx, QuemMarcaOPlacar.JogadoresEmQuadra);

        var controller = TestInfra.NovoPartidasController(ctx, foraDeQuadra.Id);
        var resultado = await controller.ControlePlacar(partida.Id, "AoVivo", 6, 3, null, null);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Equal(0, (await ctx.Partidas.FindAsync(partida.Id))!.GamesDupla1);
    }

    [Fact]
    public async Task Inscrito_fora_de_quadra_marca_no_modo_inscritos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, foraDeQuadra, _) = MontarCenario(ctx, QuemMarcaOPlacar.Inscritos);

        var controller = TestInfra.NovoPartidasController(ctx, foraDeQuadra.Id);
        var resultado = await controller.ControlePlacar(partida.Id, "AoVivo", 6, 3, null, null);

        Assert.IsNotType<ForbidResult>(resultado);
        Assert.Equal(6, (await ctx.Partidas.FindAsync(partida.Id))!.GamesDupla1);
    }

    [Theory]
    [InlineData(QuemMarcaOPlacar.Organizacao)]
    [InlineData(QuemMarcaOPlacar.JogadoresEmQuadra)]
    [InlineData(QuemMarcaOPlacar.Inscritos)]
    public async Task Estranho_continua_barrado_em_qualquer_modo(string modo)
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, _, estranho) = MontarCenario(ctx, modo);

        var controller = TestInfra.NovoPartidasController(ctx, estranho.Id);
        var resultado = await controller.ControlePlacar(partida.Id, "AoVivo", 6, 0, null, null);

        Assert.IsType<ForbidResult>(resultado);
    }

    [Fact]
    public async Task Jogador_em_quadra_nao_marca_no_modo_organizacao()
    {
        // O padrão é o comportamento de sempre: jogador nenhum mexe no próprio placar.
        using var ctx = TestInfra.NovoContexto();
        var (partida, emQuadra, _, _) = MontarCenario(ctx, QuemMarcaOPlacar.Organizacao);

        var controller = TestInfra.NovoPartidasController(ctx, emQuadra.Id);
        var resultado = await controller.ControlePlacar(partida.Id, "AoVivo", 6, 3, null, null);

        Assert.IsType<ForbidResult>(resultado);
    }

    [Fact]
    public async Task Assistente_do_padelizou_entra_so_no_modo_inscritos()
    {
        // A flag IsAssistente é de LEITURA por contrato (PoderesNoSistema) — ela só ganha
        // esta escrita no modo em que o organizador abriu pra todo mundo.
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, _, _) = MontarCenario(ctx, QuemMarcaOPlacar.Inscritos);
        var assistente = new Jogador { Nome = "Assistente", Cpf = "77777777777", IsAssistente = true };
        ctx.Jogadores.Add(assistente);
        ctx.SaveChanges();

        var controller = TestInfra.NovoPartidasController(ctx, assistente.Id);
        Assert.IsNotType<ForbidResult>(await controller.ControlePlacar(partida.Id, "AoVivo", 6, 3, null, null));
    }

    [Theory]
    [InlineData(QuemMarcaOPlacar.Organizacao)]
    [InlineData(QuemMarcaOPlacar.JogadoresEmQuadra)]
    public async Task Assistente_do_padelizou_nao_entra_nos_modos_fechados(string modo)
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, _, _) = MontarCenario(ctx, modo);
        var assistente = new Jogador { Nome = "Assistente", Cpf = "77777777777", IsAssistente = true };
        ctx.Jogadores.Add(assistente);
        ctx.SaveChanges();

        var controller = TestInfra.NovoPartidasController(ctx, assistente.Id);
        Assert.IsType<ForbidResult>(await controller.ControlePlacar(partida.Id, "AoVivo", 6, 3, null, null));
    }

    [Fact]
    public async Task Inscrito_em_lista_de_espera_nao_conta_como_inscrito()
    {
        // Lista de espera não joga — e por isso não marca placar de ninguém.
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, foraDeQuadra, _) = MontarCenario(ctx, QuemMarcaOPlacar.Inscritos);

        var duplaDele = ctx.Duplas.First(d =>
            d.Jogador1Id == foraDeQuadra.Id || d.Jogador2Id == foraDeQuadra.Id);
        duplaDele.EmListaDeEspera = true;
        ctx.SaveChanges();

        var controller = TestInfra.NovoPartidasController(ctx, foraDeQuadra.Id);
        Assert.IsType<ForbidResult>(await controller.ControlePlacar(partida.Id, "AoVivo", 6, 3, null, null));
    }

    // ── Iniciar jogo abre junto; o resto da mesa NÃO ──────────────────────────────────

    [Fact]
    public async Task Jogador_em_quadra_coloca_o_proprio_jogo_no_ar_no_modo_aberto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, emQuadra, _, _) = MontarCenario(ctx, QuemMarcaOPlacar.JogadoresEmQuadra);
        partida.Status = "Agendada";
        ctx.SaveChanges();

        var controller = TestInfra.NovoPartidasController(ctx, emQuadra.Id);
        var resultado = await controller.ColocarNoAr(partida.Id);

        Assert.IsNotType<ForbidResult>(resultado);
        Assert.Equal("AoVivo", (await ctx.Partidas.FindAsync(partida.Id))!.Status);
    }

    [Fact]
    public async Task Wo_reabrir_e_voltar_pra_agendado_continuam_so_da_organizacao()
    {
        // Mesmo no modo mais aberto: jogador registrando W.O. contra o adversário, ou
        // reabrindo o jogo que perdeu, é briga na certa. A rede de segurança do modo aberto
        // é justamente a organização ser a única que desfaz.
        using var ctx = TestInfra.NovoContexto();
        var (partida, emQuadra, _, _) = MontarCenario(ctx, QuemMarcaOPlacar.Inscritos);

        var controller = TestInfra.NovoPartidasController(ctx, emQuadra.Id);

        Assert.IsType<ForbidResult>(await controller.RegistrarWo(partida.Id, partida.Dupla2Id));
        Assert.IsType<ForbidResult>(await controller.VoltarParaAgendado(partida.Id));
        Assert.IsType<ForbidResult>(await controller.ReabrirPartida(partida.Id));
    }

    [Fact]
    public async Task Quadra_e_transmissao_de_quem_nao_e_da_mesa_sao_ignoradas()
    {
        // Jogador marca o JOGO (games, saque, status). Quadra e transmissão são operação da
        // mesa: o POST pode até trazer os campos (formulário montado à mão), mas eles não
        // gravam — vale o que a partida já tinha.
        using var ctx = TestInfra.NovoContexto();
        var (partida, emQuadra, _, _) = MontarCenario(ctx, QuemMarcaOPlacar.JogadoresEmQuadra);
        partida.NomeQuadra = "Quadra 1";
        partida.LinkTransmissao = "https://youtube.com/original";
        ctx.SaveChanges();

        var controller = TestInfra.NovoPartidasController(ctx, emQuadra.Id);
        var resultado = await controller.ControlePlacar(
            partida.Id, "AoVivo", 6, 3, "Quadra Hackeada", "https://youtube.com/troll", aplicarLinkNaQuadra: true);

        Assert.IsNotType<ForbidResult>(resultado);
        var depois = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal(6, depois!.GamesDupla1);
        Assert.Equal("Quadra 1", depois.NomeQuadra);
        Assert.Equal("https://youtube.com/original", depois.LinkTransmissao);
    }

    // ── A régua pura (a mesma que as telas usam pra desenhar o lápis) ─────────────────

    [Fact]
    public void Torneio_novo_nasce_no_modo_organizacao()
    {
        Assert.Equal(QuemMarcaOPlacar.Organizacao, new Torneio().QuemMarcaPlacar);
    }

    [Fact]
    public void Modo_desconhecido_nao_existe_e_nao_abre_nada()
    {
        Assert.False(QuemMarcaOPlacar.Existe(null));
        Assert.False(QuemMarcaOPlacar.Existe(""));
        Assert.False(QuemMarcaOPlacar.Existe("TodoMundo"));
        Assert.True(QuemMarcaOPlacar.Existe(QuemMarcaOPlacar.Organizacao));
        Assert.True(QuemMarcaOPlacar.Existe(QuemMarcaOPlacar.JogadoresEmQuadra));
        Assert.True(QuemMarcaOPlacar.Existe(QuemMarcaOPlacar.Inscritos));

        var jogo = new Partida();
        Assert.False(QuemMarcaOPlacar.Liberado("TodoMundo", 1, jogo, ehInscritoOuAssistente: true));
        Assert.False(QuemMarcaOPlacar.Liberado(QuemMarcaOPlacar.Organizacao, 1, jogo, true));
    }

    [Fact]
    public void Em_quadra_precisa_das_navegacoes_carregadas_e_reconhece_os_4()
    {
        var jogo = new Partida
        {
            Dupla1 = new Dupla { Jogador1Id = 1, Jogador2Id = 2 },
            Dupla2 = new Dupla { Jogador1Id = 3, Jogador2Id = 4 },
        };

        Assert.True(QuemMarcaOPlacar.EstaEmQuadra(1, jogo));
        Assert.True(QuemMarcaOPlacar.EstaEmQuadra(4, jogo));
        Assert.False(QuemMarcaOPlacar.EstaEmQuadra(5, jogo));
        Assert.False(QuemMarcaOPlacar.EstaEmQuadra(null, jogo));
        // Navegação não carregada nunca libera — devolve false em vez de estourar.
        Assert.False(QuemMarcaOPlacar.EstaEmQuadra(1, new Partida()));
    }

    [Fact]
    public void No_modo_inscritos_quem_esta_em_quadra_entra_mesmo_sem_inscricao_propria()
    {
        // Dupla de TIME e chave direta jogam sem inscrição pelo site — em quadra é em quadra.
        var jogo = new Partida
        {
            Dupla1 = new Dupla { Jogador1Id = 1, Jogador2Id = 2 },
            Dupla2 = new Dupla { Jogador1Id = 3, Jogador2Id = 4 },
        };

        Assert.True(QuemMarcaOPlacar.Liberado(QuemMarcaOPlacar.Inscritos, 1, jogo, ehInscritoOuAssistente: false));
        Assert.False(QuemMarcaOPlacar.Liberado(QuemMarcaOPlacar.Inscritos, 9, jogo, ehInscritoOuAssistente: false));
        Assert.True(QuemMarcaOPlacar.Liberado(QuemMarcaOPlacar.Inscritos, 9, jogo, ehInscritoOuAssistente: true));
    }
}

// AS CONSULTAS DA RÉGUA REALMENTE VIRAM SQL? O InMemory do resto da suíte não traduz nada —
// ver TraducaoDasConsultasDePalpiteTests, de onde vem o padrão (e a lição de 19/08/2026).
// As formas abaixo espelham as de PartidasController.LiberadoPeloTorneioAsync; mudou lá,
// muda aqui.
public class TraducaoDasConsultasDeQuemMarcaTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    private static void Traduz(Func<DbPadelContext, IQueryable> consulta)
    {
        using var ctx = ContextoPostgres();
        Assert.Contains("SELECT", consulta(ctx).ToQueryString());
    }

    [Fact]
    public void O_modo_do_torneio_vira_SQL() =>
        Traduz(ctx => ctx.Torneios.Where(t => t.Id == 1).Select(t => t.QuemMarcaPlacar));

    [Fact]
    public void Estar_em_quadra_vira_SQL() =>
        Traduz(ctx => ctx.Duplas
            .Where(d => d.Id == 1 || d.Id == 2)
            .Where(d => d.Jogador1Id == 7 || d.Jogador2Id == 7));

    [Fact]
    public void Ser_inscrito_por_dupla_vira_SQL() =>
        Traduz(ctx => ctx.Duplas.Where(d =>
            d.Categoria.TorneioId == 1 && !d.EmListaDeEspera
            && (d.Jogador1Id == 7 || d.Jogador2Id == 7)));

    [Fact]
    public void Ser_inscrito_no_americano_vira_SQL() =>
        Traduz(ctx => ctx.InscricoesAmericanas.Where(i =>
            i.Categoria.TorneioId == 1 && !i.EmListaDeEspera && i.JogadorId == 7));
}
