using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — O CHECK-IN PASSA A CABER NA ABA JOGOS, UMA BOLINHA POR DUPLA.
//
// 🗣️ Felipe, num print da aba Jogos do 2ª Etapa ER aberta no celular: *"Temos q por uma forma de
// fazer o checkin nessa tela por jogo"*. Perguntado sobre o formato, escolheu *"uma bolinha do
// lado de cada nome, apenas o organizador ve"*, e só nos jogos que ainda não começaram.
//
// 🕳️ O BURACO É DE CAMINHO, não de dado: a presença já existia e já era por jogo (a tela de
// Check-in virou fila de jogos em 10/09), mas quem está na aba Jogos — chamando quadra, dando a
// largada, marcando placar — precisava SAIR dela, marcar na outra tela e voltar. No sábado de
// manhã as duas perguntas ("quem joga agora?" e "essa dupla chegou?") são feitas no mesmo minuto,
// olhando a mesma linha.
//
// ⚠️ O FORMULÁRIO QUE GRAVA PRESENÇA CONTINUA MORANDO NUM LUGAR SÓ — agora em
// `_BotaoDoCheckIn`, de onde a linha da tela de Check-in e a bolinha da lista de jogos saem.
// Duas cópias dele divergiriam na primeira mudança, e é ele que escreve no banco.
//
// ⚠️ E A RÉGUA DE QUEM VÊ É A DO DIA DE JOGO (`PodeOperarODiaDeJogoAsync`) — a mesma que já liga
// o ▶, o ✏️ e o 📍 da linha, e a mesma que a tela de Check-in usa. Inventar uma segunda régua aqui
// era como o marcador ganhar o play e perder a chamada.
public class CheckInNaListaDeJogosTests
{
    private static (Torneio torneio, Jogador organizador, Dupla dupla, Partida jogo) Montar(
        DbPadelContext ctx, bool usaCheckIn = true)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        torneio.UsaCheckIn = usaCheckIn;
        ctx.SaveChanges();

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var jogo = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Fase = "Fase de Grupos",
            Status = "Agendada",
            HorarioPrevisto = new DateTime(2026, 9, 12, 8, 0, 0),
            Codigo = "ABC123",
        };
        ctx.Partidas.Add(jogo);
        ctx.SaveChanges();

        return (torneio, organizador, duplas[0], jogo);
    }

    // ── PRA ONDE O CLIQUE VOLTA ──────────────────────────────────────────────────────────
    //
    // A marcação é um POST → redirect → GET como toda ação do organizador. Sem dizer de onde
    // veio, ela larga quem clicou na TELA DE CHECK-IN — ou seja, marcar uma chegada tirava o
    // organizador da lista de jogos que ele estava operando. É o destino que muda, não a
    // gravação.

    [Fact]
    public async Task Marcado_da_pagina_do_torneio_o_clique_volta_pra_aba_jogos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, dupla, _) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Id, presente: true, voltarPara: "Details");

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Details", redir.ActionName);
        Assert.Equal(torneio.Id, redir.RouteValues!["id"]);
        // Sem a âncora a página volta pelo topo e a aba Jogos nem está aberta.
        Assert.Equal("jogosDoTorneio", redir.Fragment);
    }

    [Fact]
    public async Task Marcado_da_tela_de_jogos_o_clique_volta_pra_ela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, dupla, _) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Id, presente: true, voltarPara: "Jogos");

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Jogos", redir.ActionName);
        Assert.Equal(torneio.Id, redir.RouteValues!["id"]);
    }

    [Fact]
    public async Task Sem_dizer_de_onde_veio_continua_caindo_na_tela_de_check_in()
    {
        // A tela de Check-in não passa `voltarPara`, e o botão dela não pode mudar de destino
        // por causa desta mudança.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, dupla, _) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Id, presente: true);

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("CheckIn", redir.ActionName);
        Assert.Equal(torneio.Id, redir.RouteValues!["id"]);
    }

    [Fact]
    public async Task Destino_inventado_cai_no_check_in_em_vez_de_obedecer()
    {
        // ⚠️ LISTA FECHADA: `voltarPara` chega por campo de formulário, e campo de formulário
        // nunca vira redirecionamento pra onde o cliente pedir. É o mesmo molde do
        // PartidasController.VoltarDaLargada.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, dupla, _) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Id, presente: true, voltarPara: "https://exemplo.invalido/roubado");

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("CheckIn", redir.ActionName);
        Assert.Equal(torneio.Id, redir.RouteValues!["id"]);
    }

    [Fact]
    public async Task O_destino_novo_nao_abre_a_porta_pra_quem_nao_opera_o_dia()
    {
        // Regra 0: o `voltarPara` é enfeite de navegação, e a checagem de quem pode gravar
        // continua sendo a primeira coisa que acontece.
        using var ctx = TestInfra.NovoContexto();
        var (_, _, dupla, _) = Montar(ctx);
        var estranho = TestInfra.NovoJogador(999);
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoTorneiosController(ctx, estranho.Id)
            .MarcarCheckIn(dupla.Id, presente: true, voltarPara: "Details");

        Assert.IsType<ForbidResult>(resultado);
        Assert.Null(ctx.Duplas.Single(d => d.Id == dupla.Id).CheckInEm);
    }

    [Fact]
    public async Task Com_o_check_in_desligado_o_clique_direto_continua_sendo_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, dupla, _) = Montar(ctx, usaCheckIn: false);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Id, presente: true, voltarPara: "Details");

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Details", redir.ActionName);
        Assert.Equal(torneio.Id, redir.RouteValues!["id"]);
        Assert.Null(ctx.Duplas.Single(d => d.Id == dupla.Id).CheckInEm);
    }

    // ── O QUE A LISTA DE JOGOS PRECISA SABER ─────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_lista_de_jogos_sabe_se_o_torneio_usa_check_in(bool ligado)
    {
        // Sem isto a bolinha apareceria nos torneios que desligaram a chamada — e o servidor
        // recusaria o clique depois, que é fazer o organizador descobrir a regra pelo erro.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, _) = Montar(ctx, usaCheckIn: ligado);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        var view = Assert.IsType<ViewResult>(await controller.Jogos(torneio.Id, null, null));

        Assert.Equal(ligado, view.ViewData["UsaCheckIn"]);
    }

    // ── A LINHA DO JOGO ──────────────────────────────────────────────────────────────────

    [Fact]
    public void A_bolinha_de_presenca_esta_na_linha_da_lista_de_jogos()
    {
        Assert.Contains("<partial name=\"_BotaoDoCheckIn\"", Ler("_JogoEmLinha.cshtml"));
    }

    [Fact]
    public void O_formulario_que_grava_presenca_mora_num_lugar_so()
    {
        // Ele é desenhado em dois lugares com caras diferentes (o botão "Chegou" da tela de
        // Check-in e a bolinha da lista de jogos), e o que muda é a APARÊNCIA — nunca o que
        // é gravado. Duas cópias divergiriam na primeira mudança.
        Assert.Contains("asp-action=\"MarcarCheckIn\"", Ler("_BotaoDoCheckIn.cshtml"));
        Assert.DoesNotContain("asp-action=\"MarcarCheckIn\"", Ler("_JogoEmLinha.cshtml"));
        Assert.DoesNotContain("asp-action=\"MarcarCheckIn\"", Ler("_LinhaDoCheckIn.cshtml"));
        Assert.Contains("<partial name=\"_BotaoDoCheckIn\"", Ler("_LinhaDoCheckIn.cshtml"));
    }

    [Fact]
    public void A_bolinha_fica_do_lado_do_nome_da_dupla()
    {
        // 🗣️ *"uma bolinha do lado de cada nome"*: ela nasce ANTES dos rostos, na ponta
        // esquerda da linha — é assim que as duas viram uma coluna que se varre de cima a
        // baixo, como na tela de Check-in. Empurrada pro fim da linha, ela brigaria com a
        // porcentagem do palpitrômetro, que já mora lá.
        var fonte = Ler("_JogoEmLinha.cshtml");

        int bolinha1 = fonte.IndexOf("<partial name=\"_BotaoDoCheckIn\"", StringComparison.Ordinal);
        int nome1 = fonte.IndexOf("@LadoDaDupla(jogo.Dupla1)", StringComparison.Ordinal);
        int bolinha2 = fonte.IndexOf("<partial name=\"_BotaoDoCheckIn\"", bolinha1 + 1, StringComparison.Ordinal);
        int nome2 = fonte.IndexOf("@LadoDaDupla(jogo.Dupla2)", StringComparison.Ordinal);

        Assert.True(bolinha1 >= 0 && bolinha2 >= 0, "As DUAS duplas precisam da bolinha — uma só é meia chamada.");
        Assert.True(bolinha1 < nome1, "A bolinha da primeira dupla tem que vir antes do nome dela.");
        Assert.True(bolinha2 < nome2, "A bolinha da segunda dupla tem que vir antes do nome dela.");
    }

    [Fact]
    public void A_bolinha_so_nasce_pra_quem_opera_o_dia_com_a_chamada_ligada_e_jogo_por_vir()
    {
        // As três condições juntas, numa guarda só: quem NÃO opera o dia não vê (pedido do
        // Felipe: *"apenas o organizador ve"*), torneio sem chamada não ganha bolinha nenhuma,
        // e jogo que já começou ou acabou já respondeu a pergunta por outra via.
        var fonte = Ler("_JogoEmLinha.cshtml");
        int guarda = fonte.IndexOf("bool bolinhaDoCheckIn", StringComparison.Ordinal);
        Assert.True(guarda >= 0, "Não achei a guarda da bolinha do check-in.");

        var expressao = fonte[guarda..fonte.IndexOf(';', guarda)];
        Assert.Contains("Model.EhOrganizador", expressao);
        Assert.Contains("UsaCheckIn", expressao);
        Assert.Contains("ehAgendado", expressao);
    }

    [Fact]
    public void Marcar_da_lista_nao_joga_a_tela_de_volta_pro_topo()
    {
        // Numa lista de 97 jogos, cada POST redesenha a página do começo — a mesma queixa que
        // criou o js/manter-posicao-na-lista.js. O opt-in vive no formulário, que é um só.
        Assert.Contains("data-manter-posicao", Ler("_BotaoDoCheckIn.cshtml"));
    }

    private static string Ler(string view) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", view));

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
