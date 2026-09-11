using Microsoft.AspNetCore.Mvc;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Padelizou.Controllers;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// DAR A LARGADA JÁ ESCOLHENDO A QUADRA (11/09/2026).
//
// 🗣️ Felipe: "ao clicar no verdinho ali, quando nao tem quadra definida, coloque para
// escolher a quadra e deixe a opção de colocar link da transmissão tambem nesse pop up, e ai
// iniciar a partida".
//
// No balcão, o jogo que o robô não conseguiu encaixar chega em quadra SEM quadra: o "por
// ordem de liberação" é assim de propósito — quem joga é quem a quadra que vagou chamar. Até
// agora o organizador apertava o play e o jogo ia pro ar sem lugar nenhum; pra dizer onde era,
// tinha que voltar, achar o jogo na lista e abrir OUTRO modal. Duas telas pra uma decisão só,
// no pior momento possível.
//
// ⚠️ A RÉGUA DA QUADRA NÃO É NOVA: quem manda é Services/TrocaDeQuadra, o mesmo do botão de
// mudar de quadra. Uma segunda régua aqui seria a QUARTA porta do nome de quadra (as outras
// três estão comentadas no ControlePlacar), e é assim que nome fora da lista some da grade.
public class IniciarEscolhendoAQuadraTests
{
    private const string Quadra1 = "Quadra 1";
    private const string Quadra2 = "Quadra 2";
    private static readonly DateTime AsSete = new(2026, 9, 11, 19, 0, 0);

    private static (Torneio torneio, Categoria categoria, Jogador organizador) MontarComQuadras(DbPadelContext ctx)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Fase de Grupos");
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = Quadra1 });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = Quadra2 });
        ctx.SaveChanges();
        return (torneio, categoria, organizador);
    }

    private static Partida NovaPartida(DbPadelContext ctx, Torneio torneio, Categoria categoria,
        int dupla1, int dupla2, string codigo, string? quadra = null, DateTime? horario = null)
    {
        var partida = new Partida
        {
            CategoriaId = categoria.Id,
            TorneioId = torneio.Id,
            Codigo = codigo,
            Fase = "Grupo A",
            Dupla1Id = dupla1,
            Dupla2Id = dupla2,
            Status = "Agendada",
            NomeQuadra = quadra,
            HorarioPrevisto = horario,
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();
        return partida;
    }

    [Fact]
    public async Task O_play_leva_a_quadra_escolhida_junto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = MontarComQuadras(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();
        var partida = NovaPartida(ctx, torneio, categoria, duplas[0].Id, duplas[1].Id, "P1");

        await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .ColocarNoAr(partida.Id, nomeQuadra: Quadra1);

        var salva = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("AoVivo", salva!.Status);
        Assert.Equal(Quadra1, salva.NomeQuadra);
        Assert.NotNull(salva.HorarioInicioReal);
    }

    // O link é opcional e vai no mesmo gesto. `SendoTransmitida` sai dele — é o que a lista
    // de jogos lê pra acender o selo de transmissão.
    [Fact]
    public async Task O_link_da_transmissao_vai_no_mesmo_clique()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = MontarComQuadras(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();
        var partida = NovaPartida(ctx, torneio, categoria, duplas[0].Id, duplas[1].Id, "P1");

        await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .ColocarNoAr(partida.Id, nomeQuadra: Quadra1, linkTransmissao: "https://youtu.be/abc");

        var salva = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("https://youtu.be/abc", salva!.LinkTransmissao);
        Assert.True(salva.SendoTransmitida);
    }

    // ⚠️ NOME FORA DA LISTA NÃO ENTRA, E O JOGO NÃO COMEÇA. Começar sem a quadra que a pessoa
    // acabou de escolher seria o pior dos mundos: ela vê "no ar", vai cuidar de outro jogo, e
    // a quadra nunca esteve lá. Quadra fora da lista some da grade (que só conhece as
    // cadastradas) e ainda atrai o link de uma quadra homônima.
    [Fact]
    public async Task Quadra_que_nao_e_do_torneio_nao_entra_e_o_jogo_nao_comeca()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = MontarComQuadras(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();
        var partida = NovaPartida(ctx, torneio, categoria, duplas[0].Id, duplas[1].Id, "P1");

        await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .ColocarNoAr(partida.Id, nomeQuadra: "Quadra do vizinho");

        var salva = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("Agendada", salva!.Status);
        Assert.Null(salva.NomeQuadra);
        Assert.Null(salva.HorarioInicioReal);
    }

    // ⚠️ QUADRA OCUPADA NO MESMO HORÁRIO TROCA DE LUGAR, e não recusa — é o que o botão de
    // mudar de quadra já faz (Services/TrocaDeQuadra), e pelo mesmo motivo: duas duplas
    // mandadas pro mesmo lugar é a única coisa que a grade não pode produzir.
    [Fact]
    public async Task Quadra_ocupada_no_mesmo_horario_troca_de_lugar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = MontarComQuadras(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(4).ToList();

        var dono = NovaPartida(ctx, torneio, categoria, duplas[0].Id, duplas[1].Id, "DONO", Quadra1, AsSete);
        var chegando = NovaPartida(ctx, torneio, categoria, duplas[2].Id, duplas[3].Id, "NOVO", null, AsSete);

        await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .ColocarNoAr(chegando.Id, nomeQuadra: Quadra1);

        Assert.Equal(Quadra1, (await ctx.Partidas.FindAsync(chegando.Id))!.NomeQuadra);
        // O antigo dono não fica sem lugar nem divide a quadra: recebe a do outro, que era
        // nenhuma. Ele volta pra fila do "por ordem", que é de onde o chegando saiu.
        Assert.Null((await ctx.Partidas.FindAsync(dono.Id))!.NomeQuadra);
    }

    // Jogo que JÁ tem quadra não é remanejado por aqui: com quadra, o botão verde nem abre o
    // modal — ele só pergunta se é pra começar. Um POST montado à mão não pode virar uma
    // segunda porta de trocar quadra, escapando da tela que existe pra isso.
    [Fact]
    public async Task Jogo_que_ja_tem_quadra_ignora_a_quadra_que_vier_no_POST()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = MontarComQuadras(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();
        var partida = NovaPartida(ctx, torneio, categoria, duplas[0].Id, duplas[1].Id, "P1", Quadra1, AsSete);

        await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .ColocarNoAr(partida.Id, nomeQuadra: Quadra2);

        var salva = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("AoVivo", salva!.Status);
        Assert.Equal(Quadra1, salva.NomeQuadra);
    }

    // O play de sempre, sem nada escolhido, continua sendo o play de sempre.
    [Fact]
    public async Task Sem_quadra_no_POST_nada_muda_no_comportamento_de_antes()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = MontarComQuadras(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();
        var partida = NovaPartida(ctx, torneio, categoria, duplas[0].Id, duplas[1].Id, "P1");

        var resultado = await TestInfra.NovoPartidasController(ctx, organizador.Id).ColocarNoAr(partida.Id);

        var salva = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("AoVivo", salva!.Status);
        Assert.Null(salva.NomeQuadra);
        Assert.IsType<RedirectToActionResult>(resultado);
    }

    // Quem não organiza não escolhe quadra de ninguém — a régua de autorização vale igual
    // para a versão com quadra.
    [Fact]
    public async Task Estranho_nao_escolhe_quadra_nem_comeca()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = MontarComQuadras(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();
        var partida = NovaPartida(ctx, torneio, categoria, duplas[0].Id, duplas[1].Id, "P1");

        var estranho = new Jogador { Nome = "Estranho", Cpf = "77777777777" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoPartidasController(ctx, estranho.Id)
            .ColocarNoAr(partida.Id, nomeQuadra: Quadra1);

        Assert.IsType<ForbidResult>(resultado);
        var salva = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("Agendada", salva!.Status);
        Assert.Null(salva.NomeQuadra);
    }

    // ── A TELA ──────────────────────────────────────────────────────────────────────────
    //
    // ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. O COMPORTAMENTO está travado nos
    // testes de controller acima; aqui o que se segura é a AMARRAÇÃO da tela com ele.

    [Fact]
    public void Sem_quadra_o_play_abre_o_modal_em_vez_de_comecar_direto()
    {
        var fonte = JogoEmLinha();

        var play = fonte.IndexOf("modalIniciarJogo", StringComparison.Ordinal);
        Assert.True(play >= 0, "O play precisa abrir o modal de largada quando não há quadra.");

        // O gatilho leva o jogo e a quadra atual, como o de mudar de quadra já faz.
        Assert.Contains("data-jogo-id", fonte);
        Assert.Contains("data-jogo-rotulo", fonte);
    }

    // Com quadra não abre tela nenhuma: é só a pergunta. Quem está no balcão com fila
    // esperando não merece um formulário pra confirmar o que já está decidido.
    [Fact]
    public void Com_quadra_o_play_so_pergunta_se_e_pra_comecar()
    {
        var fonte = JogoEmLinha();

        var acao = fonte.IndexOf("asp-action=\"ColocarNoAr\"", StringComparison.Ordinal);
        Assert.True(acao >= 0);

        // A confirmação viaja no <form>, do jeito que o confirmar.js lê (data-confirmar).
        var trecho = fonte.Substring(acao, 400);
        Assert.Contains("data-confirmar", trecho);
    }

    [Theory]
    [InlineData("name=\"nomeQuadra\"")]
    [InlineData("name=\"linkTransmissao\"")]
    public void O_modal_da_largada_pede_a_quadra_e_oferece_o_link(string campo)
    {
        var fonte = JogosDoTorneio();

        var modal = fonte.IndexOf("id=\"modalIniciarJogo\"", StringComparison.Ordinal);
        Assert.True(modal >= 0, "Falta o modal da largada.");
        Assert.Contains(campo, fonte.Substring(modal, 3000));
    }

    // ⚠️ GUARDRAIL, e não funcionalidade nova: o "voltar pra agendado" JÁ perguntava antes
    // desta mudança, nas duas telas. Se alguém tirar, isto acusa.
    [Theory]
    [InlineData("_JogoEmLinha.cshtml")]
    [InlineData("_JogosDoTorneio.cshtml")]
    public void Voltar_pra_agendado_continua_perguntando(string arquivo)
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDasViews(), arquivo));

        var acao = fonte.IndexOf("asp-action=\"VoltarParaAgendado\"", StringComparison.Ordinal);
        Assert.True(acao >= 0, $"Não achei o VoltarParaAgendado em {arquivo}.");
        Assert.Contains("data-confirmar", fonte.Substring(acao, 400));
    }

    private static string JogoEmLinha() =>
        File.ReadAllText(Path.Combine(PastaDasViews(), "_JogoEmLinha.cshtml"));

    private static string JogosDoTorneio() =>
        File.ReadAllText(Path.Combine(PastaDasViews(), "_JogosDoTorneio.cshtml"));

    private static string PastaDasViews()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views", "Torneios");
            if (Directory.Exists(alvo)) return alvo;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei as views de Torneios.");
    }
}
