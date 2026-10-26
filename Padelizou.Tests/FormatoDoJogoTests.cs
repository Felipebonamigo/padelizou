using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 26/10/2026 — O FORMATO DE UM JOGO SÓ, DECIDIDO NA HORA.
//
// 🗣️ Um organizador: *"tem alguns torneios que deixam para decidir na hora como a final será
// disputada, por exemplo em um set de 9 ou 2 de 6"*. 🗣️ Felipe: *"na hora de por o 'jogo começou'
// na aba de jogos do torneio, uma opção para 'alterar formato do jogo' [...] para que possa trocar
// a cada jogo se quiser"* — e: set de 9 e 2 sets de 6 contam IGUAL no ranking.
//
// O torneio já tinha formato por FASE (grupos, mata-mata, semi+final, e o desvio só da final).
// Aqui o jogo ganha o próprio desvio, no mesmo molde do `GamesSoDaFinal`: o GAMES é o interruptor
// (nulo = segue a fase), e quem traduz é o mesmo `FormatoDaPartida`.
//
// ⚠️ Só antes do primeiro placar: trocar o formato com games marcados reinterpretaria o que já
// foi lançado (um 6x4 que era fim de jogo viraria meio de set).
public class FormatoDoJogoTests
{
    private static Torneio TorneioComFases() => new()
    {
        Id = 1,
        SetsFaseGrupos = 1, GamesFaseGrupos = 6,
        SetsFaseMataMata = 1, GamesFaseMataMata = 9,
        SetsFaseFinal = 3, GamesFaseFinal = 6, PontosTieBreakFinal = TieBreakDoJogo.SuperTieBreak,
    };

    [Fact]
    public void Jogo_sem_formato_proprio_segue_a_fase()
    {
        var torneio = TorneioComFases();
        var final = new Partida { Fase = "Final" };

        Assert.Equal(FormatoDaPartida.De(torneio, "Final"), FormatoDaPartida.De(torneio, final));
    }

    [Fact]
    public void Jogo_com_formato_proprio_vale_por_cima_da_fase()
    {
        var torneio = TorneioComFases();
        var final = new Partida { Fase = "Final", SetsDoJogo = 1, GamesDoJogo = 9, PontosTieBreakDoJogo = TieBreakDoJogo.PontosPadrao };

        var formato = FormatoDaPartida.De(torneio, final);

        Assert.Equal(1, formato.Sets);
        Assert.Equal(9, formato.Games);
        Assert.Equal(TieBreakDoJogo.PontosPadrao, formato.PontosTieBreak);
    }

    [Fact]
    public void O_games_e_o_interruptor_sets_e_tie_break_vazios_caem_no_padrao()
    {
        var torneio = TorneioComFases();
        var final = new Partida { Fase = "Final", GamesDoJogo = 9 };

        var formato = FormatoDaPartida.De(torneio, final);

        Assert.Equal(FormatoDaPartida.SetsPadrao, formato.Sets);
        Assert.Equal(9, formato.Games);
        Assert.Equal(TieBreakDoJogo.Desligado, formato.PontosTieBreak);
    }

    // ── A ação ─────────────────────────────────────────────────────────────────────────────

    private static (DbPadelContext Ctx, Torneio Torneio, Jogador Organizador, Partida Jogo) Montar(
        string status = "Agendada", int? gamesJaMarcados = null)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Em Andamento");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var jogo = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id,
            Codigo = "FIN", Status = status, Fase = "Final", GamesDupla1 = gamesJaMarcados,
        };
        ctx.Partidas.Add(jogo);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return (ctx, torneio, organizador, jogo);
    }

    private static Task<Partida> Recarregar(DbPadelContext ctx, Partida jogo)
    {
        ctx.ChangeTracker.Clear();
        return ctx.Partidas.SingleAsync(p => p.Id == jogo.Id);
    }

    [Fact]
    public async Task Organizador_troca_o_formato_de_um_jogo_ainda_sem_placar()
    {
        var (ctx, torneio, organizador, jogo) = Montar();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.AlterarFormatoDoJogo(torneio.Id, jogo.Id, sets: 1, games: 9, pontosTieBreak: 7);

        var gravado = await Recarregar(ctx, jogo);
        Assert.Equal(1, gravado.SetsDoJogo);
        Assert.Equal(9, gravado.GamesDoJogo);
        Assert.Equal(7, gravado.PontosTieBreakDoJogo);
        Assert.NotNull(controller.TempData["Sucesso"]);
    }

    [Fact]
    public async Task Vale_tambem_com_o_jogo_ja_em_quadra_se_ninguem_marcou_placar()
    {
        // É o "na hora de por o jogo começou": o jogo pode acabar de ir pra quadra.
        var (ctx, torneio, organizador, jogo) = Montar(status: "AoVivo");

        await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .AlterarFormatoDoJogo(torneio.Id, jogo.Id, sets: 3, games: 6, pontosTieBreak: 10);

        Assert.Equal(6, (await Recarregar(ctx, jogo)).GamesDoJogo);
    }

    [Fact]
    public async Task Sem_games_volta_ao_formato_da_fase()
    {
        var (ctx, torneio, organizador, jogo) = Montar();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.AlterarFormatoDoJogo(torneio.Id, jogo.Id, sets: 1, games: 9, pontosTieBreak: 7);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .AlterarFormatoDoJogo(torneio.Id, jogo.Id, sets: null, games: null, pontosTieBreak: null);

        var gravado = await Recarregar(ctx, jogo);
        Assert.Null(gravado.SetsDoJogo);
        Assert.Null(gravado.GamesDoJogo);
        Assert.Null(gravado.PontosTieBreakDoJogo);
    }

    [Fact]
    public async Task Com_placar_marcado_recusa_e_nao_mexe()
    {
        var (ctx, torneio, organizador, jogo) = Montar(status: "AoVivo", gamesJaMarcados: 3);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.AlterarFormatoDoJogo(torneio.Id, jogo.Id, sets: 1, games: 9, pontosTieBreak: 7);

        Assert.Null((await Recarregar(ctx, jogo)).GamesDoJogo);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Jogo_finalizado_recusa()
    {
        var (ctx, torneio, organizador, jogo) = Montar(status: "Finalizada");
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.AlterarFormatoDoJogo(torneio.Id, jogo.Id, sets: 1, games: 9, pontosTieBreak: 7);

        Assert.Null((await Recarregar(ctx, jogo)).GamesDoJogo);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Quem_nao_opera_o_torneio_nao_troca()
    {
        var (ctx, torneio, _, jogo) = Montar();
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99911100011" };
        ctx.Jogadores.Add(intruso);
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .AlterarFormatoDoJogo(torneio.Id, jogo.Id, sets: 1, games: 9, pontosTieBreak: 7);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Null((await Recarregar(ctx, jogo)).GamesDoJogo);
    }

    [Fact]
    public async Task Jogo_de_outro_torneio_e_recusado()
    {
        var (ctx, _, organizador, jogo) = Montar();
        // O organizador opera ESTE outro torneio, e manda o Id de um jogo que não é dele.
        var outro = new Torneio { Nome = "Outro", Codigo = "OUT1", Status = "Em Andamento" };
        ctx.Torneios.Add(outro);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = outro.Id, JogadorId = organizador.Id });
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.AlterarFormatoDoJogo(outro.Id, jogo.Id, sets: 1, games: 9, pontosTieBreak: 7);

        Assert.Null((await Recarregar(ctx, jogo)).GamesDoJogo);
    }

    [Fact]
    public async Task Numero_absurdo_e_contido_e_tie_break_invalido_vira_desligado()
    {
        // POST montado à mão: 99 sets ou tie-break de 3 não podem chegar no placar.
        var (ctx, torneio, organizador, jogo) = Montar();

        await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .AlterarFormatoDoJogo(torneio.Id, jogo.Id, sets: 99, games: 99, pontosTieBreak: 3);

        var gravado = await Recarregar(ctx, jogo);
        Assert.InRange(gravado.SetsDoJogo ?? 0, 1, FormatoDoJogo.MaxSets);
        Assert.InRange(gravado.GamesDoJogo ?? 0, 1, FormatoDoJogo.MaxGames);
        Assert.Equal(TieBreakDoJogo.Desligado, gravado.PontosTieBreakDoJogo);
    }

    // ── A tela ─────────────────────────────────────────────────────────────────────────────

    private static string View(string arquivo) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Padelizou", "Views", "Torneios", arquivo));

    [Fact]
    public void A_linha_do_jogo_tem_o_botao_de_formato_so_enquanto_nao_ha_placar()
    {
        var linha = View("_JogoEmLinha.cshtml");

        Assert.Contains("data-bs-target=\"#modalFormatoDoJogo\"", linha);
        // A mesma régua do servidor: com placar o botão nem aparece (o servidor recusaria).
        Assert.Contains("FormatoDoJogo.TemPlacar(jogo)", linha);
    }

    [Fact]
    public void O_modal_posta_pra_acao_de_trocar_o_formato()
    {
        var aba = View("_JogosDoTorneio.cshtml");

        Assert.Contains("id=\"modalFormatoDoJogo\"", aba);
        Assert.Contains("asp-action=\"AlterarFormatoDoJogo\"", aba);
        // "Voltar ao formato da fase" = mandar games vazio.
        Assert.Contains("formatoVoltarAoDaFase", aba);
    }

    // ── A guarda ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ninguem_resolve_o_formato_de_um_JOGO_so_pela_fase_dele()
    {
        // Com o jogo em mãos, quem chama `De(torneio, p.Fase)` ignora o formato próprio — a Mesa
        // deixaria marcar até 6 numa final trocada pra set de 9. A fase sozinha continua valendo
        // onde a pergunta é da FASE ("qual o formato dos grupos?"), não de um jogo.
        var raiz = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Padelizou");
        var arquivos = Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(raiz, "*.cshtml", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        var padrao = new Regex(@"FormatoDaPartida\.De\([^;]*?,\s*(p|partida)\.Fase\s*\)", RegexOptions.Singleline);
        var achados = arquivos
            .SelectMany(f => padrao.Matches(File.ReadAllText(f)).Select(m => $"{Path.GetFileName(f)}: {m.Value}"))
            .ToList();

        Assert.True(achados.Count == 0, "Resolvendo o formato do jogo só pela fase:\n" + string.Join("\n", achados));
    }
}
