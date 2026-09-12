using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — A PRESENÇA PASSA A SER DA PESSOA, E NÃO DA DUPLA.
//
// 🗣️ Felipe, olhando a bolinha por dupla na aba Jogos: *"Mas é tem um check para cada jogador da
// dupla?"* — e, quando a resposta foi não: *"Mude para um check por jogador, por que é assim que
// controla check in"*.
//
// 🕳️ `Dupla.CheckInEm` respondia "a dupla apareceu", que é o que o W.O. precisa e NÃO é o que a
// mesa faz no sábado: quem chega é uma pessoa por vez, e o organizador precisa saber QUAL dos dois
// falta pra ligar pra pessoa certa em vez de pro parceiro que já está no clube.
//
// ⚠️ A CHAVE ERA (TorneioId, JogadorId) E DUROU MEIO DIA. No mesmo 12/09 o Felipe perguntou *"e o
// checkin, ele herda dos outros jogos pra mesma pessoa? pq se sim, nao deveria, tem q ser separado
// jogo a jogo"* — e virou **(PartidaId, JogadorId)**. O que este arquivo guarda é a parte que
// sobreviveu: o check é da PESSOA, não da dupla. Que ele é de UM JOGO mora no
// PresencaSeparadaPorJogoTests.
//
// ⚠️ PK COMPOSTA, e não `bool Presente` numa coluna: é o banco segurando o clique duplo, o mesmo
// molde do `TorneioMarcador` (ver a escada do CLAUDE.md, degrau 4). Linha existe = chegou;
// desfazer é apagar a linha.
//
// ⚠️ "DUPLA PRESENTE" VIRA DERIVADO — todos os jogadores dela marcados —, e por isso mora numa
// régua só (Services/PresencaNoDia). Duas contas de "essa dupla chegou?" fariam o contador da tela
// e o selo do jogo discordarem sobre o mesmo par.
public class PresencaPorJogadorTests
{
    private static (Torneio torneio, Categoria categoria, Jogador organizador, List<Dupla> duplas) Montar(
        DbPadelContext ctx, bool usaCheckIn = true, int qtdDuplas = 2)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas, status: "Fase de Grupos");
        torneio.UsaCheckIn = usaCheckIn;
        ctx.SaveChanges();

        var duplas = ctx.Duplas
            .Include(d => d.Jogador1).Include(d => d.Jogador2)
            .Where(d => d.CategoriaId == categoria.Id).ToList();

        return (torneio, categoria, organizador, duplas);
    }

    private static Partida Jogo(DbPadelContext ctx, Torneio t, Categoria c, Dupla d1, Dupla d2, string status = "Agendada")
    {
        var p = new Partida
        {
            TorneioId = t.Id,
            CategoriaId = c.Id,
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            Fase = "Fase de Grupos",
            Status = status,
            HorarioPrevisto = new DateTime(2026, 9, 12, 8, 0, 0),
            Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
        };
        ctx.Partidas.Add(p);
        ctx.SaveChanges();
        return p;
    }

    // ── O QUE FICA GRAVADO ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Marcar_um_jogador_grava_a_presenca_dele_e_nao_a_do_parceiro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx);
        var dupla = duplas[0];
        var jogo = Jogo(ctx, torneio, categoria, duplas[0], duplas[1]);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Jogador1Id, jogo.Id, presente: true);

        Assert.True(ctx.Presencas.Any(p => p.PartidaId == jogo.Id && p.JogadorId == dupla.Jogador1Id));
        Assert.False(ctx.Presencas.Any(p => p.PartidaId == jogo.Id && p.JogadorId == dupla.Jogador2Id));
    }

    [Fact]
    public async Task Desfazer_apaga_a_linha()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx);
        var jogo = Jogo(ctx, torneio, categoria, duplas[0], duplas[1]);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: true);
        await controller.MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: false);

        Assert.Empty(ctx.Presencas.Where(p => p.PartidaId == jogo.Id));
    }

    [Fact]
    public async Task Marcar_duas_vezes_nao_duplica_a_linha()
    {
        // O dedo escorrega no balcão e o POST sai duas vezes. A PK composta não deixa nascer a
        // segunda linha — e aqui se cobra que o código não ESTOURE tentando.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx);
        var jogo = Jogo(ctx, torneio, categoria, duplas[0], duplas[1]);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: true);
        await controller.MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: true);

        Assert.Single(ctx.Presencas.Where(p => p.PartidaId == jogo.Id));
    }

    // ⚠️ AQUI VIVIA O `Um_check_so_vale_pras_duas_categorias_da_mesma_pessoa`, e ele foi APAGADO
    // no mesmo dia em que nasceu: afirmava que um check valia pros dois jogos da mesma pessoa, que
    // é exatamente o defeito que o Felipe mandou tirar horas depois. O contrário dele está no
    // PresencaSeparadaPorJogoTests — um teste que trava o comportamento errado é pior que teste
    // nenhum, porque a próxima sessão o lê como decisão.

    // ── QUEM PODE ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_nao_opera_o_dia_nao_grava()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, duplas) = Montar(ctx);
        var jogo = Jogo(ctx, torneio, categoria, duplas[0], duplas[1]);
        var estranho = TestInfra.NovoJogador(999);
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoTorneiosController(ctx, estranho.Id)
            .MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: true);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Empty(ctx.Presencas);
    }

    [Fact]
    public async Task Jogador_que_nao_joga_este_torneio_nao_ganha_linha()
    {
        // Regra 0: o `jogadorId` chega por campo de formulário. Sem esta checagem, um
        // organizador podia carimbar presença de gente que não está inscrita — lixo no banco
        // com a cara de dado bom.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx);
        var jogo = Jogo(ctx, torneio, categoria, duplas[0], duplas[1]);
        var deFora = TestInfra.NovoJogador(888);
        ctx.Jogadores.Add(deFora);
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(deFora.Id, jogo.Id, presente: true);

        Assert.IsType<NotFoundResult>(resultado);
        Assert.Empty(ctx.Presencas);
    }

    [Fact]
    public async Task Com_o_check_in_desligado_o_clique_direto_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx, usaCheckIn: false);
        var jogo = Jogo(ctx, torneio, categoria, duplas[0], duplas[1]);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: true);

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Details", redir.ActionName);
        Assert.Empty(ctx.Presencas);
    }

    // ── PRA ONDE O CLIQUE VOLTA (segue valendo, agora por jogador) ───────────────────────

    [Theory]
    [InlineData("Details", "Details", "jogosDoTorneio")]
    [InlineData("Jogos", "Jogos", null)]
    [InlineData(null, "CheckIn", null)]
    [InlineData("https://exemplo.invalido/roubado", "CheckIn", null)]
    public async Task O_clique_volta_pra_tela_de_onde_saiu(string? voltarPara, string acao, string? ancora)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx);
        var jogo = Jogo(ctx, torneio, categoria, duplas[0], duplas[1]);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: true, voltarPara: voltarPara);

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(acao, redir.ActionName);
        Assert.Equal(torneio.Id, redir.RouteValues!["id"]);
        Assert.Equal(ancora, redir.Fragment);
    }

    // ── A RÉGUA DE "ESSA DUPLA CHEGOU?" ──────────────────────────────────────────────────

    [Fact]
    public void A_dupla_so_esta_completa_com_os_DOIS_jogadores()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, _, duplas) = Montar(ctx);
        var dupla = duplas[0];

        Assert.False(PresencaNoDia.DuplaCompleta(JogoDaRegua, dupla, Chegaram()));
        Assert.False(PresencaNoDia.DuplaCompleta(JogoDaRegua, dupla, Chegaram(dupla.Jogador1Id)));
        Assert.True(PresencaNoDia.DuplaCompleta(JogoDaRegua, dupla, Chegaram(dupla.Jogador1Id, dupla.Jogador2Id!.Value)));
    }

    [Fact]
    public void Inscricao_sem_parceiro_esta_completa_com_uma_pessoa_so()
    {
        // A vaga sem parceiro entra na chave desde 09/09: ali quem está inscrito é UMA pessoa,
        // e exigir dois checks deixaria essa dupla eternamente "faltando alguém".
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria, _, _) = Montar(ctx);
        var sozinho = TestInfra.NovoJogador(555);
        ctx.Jogadores.Add(sozinho);
        ctx.SaveChanges();
        var semParceiro = new Dupla { CategoriaId = categoria.Id, Jogador1Id = sozinho.Id, Jogador2Id = null };
        ctx.Duplas.Add(semParceiro);
        ctx.SaveChanges();

        Assert.False(PresencaNoDia.DuplaCompleta(JogoDaRegua, semParceiro, Chegaram()));
        Assert.True(PresencaNoDia.DuplaCompleta(JogoDaRegua, semParceiro, Chegaram(sozinho.Id)));
    }

    [Fact]
    public void A_regua_aguenta_dupla_nula_sem_estourar()
    {
        // A prévia do mata-mata tem lado sem dono, e a linha do jogo chama esta régua pros dois
        // lados sem perguntar.
        Assert.False(PresencaNoDia.DuplaCompleta(JogoDaRegua, null, Chegaram()));
        Assert.Empty(PresencaNoDia.JogadoresDa(null));
    }

    // ── O QUE AS TELAS RECEBEM ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_lista_de_jogos_recebe_quem_ja_chegou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx);
        var jogo = Jogo(ctx, torneio, categoria, duplas[0], duplas[1]);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: true);

        var view = Assert.IsType<ViewResult>(await controller.Jogos(torneio.Id, null, null));
        var chegadas = Assert.IsAssignableFrom<IReadOnlyDictionary<(int PartidaId, int JogadorId), DateTime>>(
            view.ViewData["ChegadasNoTorneio"]);

        Assert.True(chegadas.ContainsKey((jogo.Id, duplas[0].Jogador1Id)));
        Assert.False(chegadas.ContainsKey((jogo.Id, duplas[0].Jogador2Id!.Value)));
    }

    [Fact]
    public async Task A_tela_de_check_in_conta_JOGADORES_e_nao_duplas()
    {
        // Um jogo com 2 duplas = 4 VAGAS de check-in. Com um jogador marcado, a barra diz 1 de 4.
        //
        // ⚠️ O DENOMINADOR PASSOU A SER VAGA (jogo × jogador), e não pessoa (12/09/2026): com a
        // presença por jogo, quem joga duas categorias tem DOIS checks a dar, e contá-lo uma vez
        // faria a barra fechar com gente ainda por marcar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx);
        var jogo = Jogo(ctx, torneio, categoria, duplas[0], duplas[1]);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: true);

        var view = Assert.IsType<ViewResult>(await controller.CheckIn(torneio.Id));

        Assert.Equal(4, view.ViewData["TotalDeJogadores"]);
        Assert.Equal(1, view.ViewData["JogadoresPresentes"]);
    }

    // ── A LINHA DO JOGO ──────────────────────────────────────────────────────────────────

    [Fact]
    public void O_botao_da_linha_do_jogo_recebe_o_JOGADOR_e_nao_a_dupla()
    {
        // Os dois lados desenham o botão DENTRO do laço de jogadores, e o que vai pro POST é o
        // `j.Id` de cada pessoa. É o que separa "um check por dupla" de "um check por jogador" —
        // e o laço é também o que faz a inscrição SEM PARCEIRO desenhar um botão só, sem `if`.
        var fonte = Ler("_JogoEmLinha.cshtml");

        Assert.Equal(2, fonte.Split("PresencaNoDia.JogadoresDa(jogo.Dupla").Length - 1);
        Assert.Equal(2, fonte.Split("BotaoDeCheckInVM(j.Id,").Length - 1);
    }

    [Fact]
    public void O_formulario_que_grava_presenca_mora_num_lugar_so()
    {
        Assert.Contains("asp-action=\"MarcarCheckIn\"", Ler("_BotaoDoCheckIn.cshtml"));
        foreach (var outra in new[] { "_JogoEmLinha.cshtml", "_LinhaDoCheckIn.cshtml", "CheckIn.cshtml", "_JogoNoCheckIn.cshtml" })
            Assert.DoesNotContain("asp-action=\"MarcarCheckIn\"", Ler(outra));
    }

    [Fact]
    public void No_celular_o_par_empilha_e_a_porcentagem_acompanha_a_DUPLA()
    {
        // Medido no Chromium a 390px: dois checks de 30px, dois rostos de 26px e dois nomes não
        // cabem numa linha só. O par empilha — mas dentro de `.pdz-jl-par`, senão as quatro
        // linhas viram quatro entradas soltas e ninguém lê mais "dupla × dupla".
        var css = Css();
        Assert.Matches(new System.Text.RegularExpressions.Regex(
            @"\.pdz-jl-par\s*\{[^}]*min-width:\s*0", System.Text.RegularExpressions.RegexOptions.Singleline), css);
        Assert.Matches(new System.Text.RegularExpressions.Regex(
            @"@media[^{]*575\.98px[^{]*\{(?:[^{}]|\{[^{}]*\})*\.pdz-jl-par\s*\{[^}]*flex-direction:\s*column",
            System.Text.RegularExpressions.RegexOptions.Singleline), css);
    }

    // Açúcar dos testes da régua: quem chegou, com uma hora qualquer — a régua não olha a hora,
    // só a presença da chave.
    private const int JogoDaRegua = 1;

    private static Dictionary<(int PartidaId, int JogadorId), DateTime> Chegaram(params int[] ids) =>
        ids.ToDictionary(id => (PartidaId: JogoDaRegua, JogadorId: id), _ => new DateTime(2026, 9, 12, 8, 12, 0));

    private static string Ler(string view) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", view));

    private static string Css() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "css", "site.css"));

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
