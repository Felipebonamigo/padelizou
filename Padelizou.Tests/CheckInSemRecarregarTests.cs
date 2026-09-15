using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// 15/09/2026 — MARCAR PRESENÇA DEIXA DE RECARREGAR A PÁGINA.
//
// 🗣️ Felipe: *"no checkin, ao clicar para marcar, nao deveria atualizar a pagina inteira, como
// estava acontecendo, isso foi alterado ?"* — não tinha sido — e, em seguida: *"sim, faça. o
// jogo tem q subir na hora"*.
//
// 🕳️ CADA BOLINHA ERA UM POST → 302 → GET DA PÁGINA INTEIRA. No torneio de 97 jogos isso é mais
// de 1MB por clique, e são QUATRO cliques por jogo. O que existia era o `data-manter-posicao`,
// que devolve a rolagem pra mesma altura DEPOIS da recarga: disfarce do sintoma — a página some
// e renasce, o <iframe> da transmissão reinicia junto, e na internet do clube cada check custa a
// espera de uma tela inteira.
//
// ⚠️ O COMPORTAMENTO EM SI mora no conferidor de JS (`conferir-checkin-sem-recarregar.js`), que
// roda o arquivo contra um DOM falso. O que se guarda AQUI é o que só o C# sabe:
//
//   • o **204** — a única resposta que prova pro navegador que gravou. `resposta.ok` não serve:
//     sessão vencida responde 302 pra tela de login, o `fetch` segue o desvio e entrega 200 com
//     o HTML do login, e um check verde sem linha no banco é pior que o recarregamento;
//   • que o caminho **sem JavaScript** continua de pé (redirect de sempre);
//   • que a Regra 0 vem ANTES do atalho: quem não é organizador leva `Forbid` também por fetch;
//   • e o markup que o JS procura — sem `data-pdz-checkin` no formulário, o clique volta a
//     recarregar a página em silêncio, sem erro em lugar nenhum e sem teste vermelho.
public class CheckInSemRecarregarTests
{
    private static (Torneio torneio, Jogador organizador, Partida jogo, Dupla dupla) Montar(DbPadelContext ctx)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, 4, status: "Fase de Grupos");
        torneio.UsaCheckIn = true;
        ctx.SaveChanges();

        var duplas = ctx.Duplas
            .Include(d => d.Jogador1).Include(d => d.Jogador2)
            .Where(d => d.CategoriaId == categoria.Id).ToList();

        var jogo = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id,
            Fase = "Fase de Grupos", Status = "Agendada",
            HorarioPrevisto = new DateTime(2026, 9, 15, 16, 20, 0),
            Codigo = "ABC123",
        };
        ctx.Partidas.Add(jogo);
        ctx.SaveChanges();

        return (torneio, organizador, jogo, duplas[0]);
    }

    private static Controllers.TorneiosController PorFetch(DbPadelContext ctx, int usuarioId)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioId);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        return controller;
    }

    [Fact]
    public async Task Quem_clicou_por_fetch_recebe_204_e_nao_a_pagina_inteira()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, organizador, jogo, dupla) = Montar(ctx);

        var resultado = await PorFetch(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Jogador1Id, jogo.Id, presente: true);

        Assert.IsType<NoContentResult>(resultado);
        Assert.True(await ctx.Presencas.AnyAsync(p => p.PartidaId == jogo.Id && p.JogadorId == dupla.Jogador1Id));
    }

    [Fact]
    public async Task E_desmarcar_por_fetch_tambem_responde_204()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, organizador, jogo, dupla) = Montar(ctx);
        await PorFetch(ctx, organizador.Id).MarcarCheckIn(dupla.Jogador1Id, jogo.Id, presente: true);

        var resultado = await PorFetch(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Jogador1Id, jogo.Id, presente: false);

        Assert.IsType<NoContentResult>(resultado);
        Assert.False(await ctx.Presencas.AnyAsync(p => p.PartidaId == jogo.Id && p.JogadorId == dupla.Jogador1Id));
    }

    [Fact]
    public async Task Sem_o_cabecalho_o_caminho_de_sempre_continua_de_pe()
    {
        // WebView velho, script que não carregou, JavaScript desligado: o formulário posta do
        // jeito de sempre e a pessoa volta pra lista. Trocar o redirect por 204 pra todo mundo
        // deixaria esse clique numa tela em branco.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, jogo, dupla) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Jogador1Id, jogo.Id, presente: true, voltarPara: "Jogos");

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Jogos", redir.ActionName);
        Assert.Equal(torneio.Id, redir.RouteValues!["id"]);
    }

    [Fact]
    public async Task A_Regra_0_vem_antes_do_atalho()
    {
        // ⚠️ O 204 é o ÚLTIMO passo, e não o primeiro: quem não pode operar o dia de jogo leva
        // Forbid mesmo chamando por fetch. Um atalho colocado no topo da ação transformaria a
        // checagem de dono em enfeite.
        using var ctx = TestInfra.NovoContexto();
        var (_, _, jogo, dupla) = Montar(ctx);
        var estranho = TestInfra.NovoJogador(777);
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var resultado = await PorFetch(ctx, estranho.Id)
            .MarcarCheckIn(dupla.Jogador1Id, jogo.Id, presente: true);

        Assert.IsType<ForbidResult>(resultado);
        Assert.False(await ctx.Presencas.AnyAsync(p => p.PartidaId == jogo.Id));
    }

    [Fact]
    public async Task Jogador_que_nao_joga_ESTE_jogo_continua_recusado_por_fetch()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, organizador, jogo, _) = Montar(ctx);
        var deFora = TestInfra.NovoJogador(888);
        ctx.Jogadores.Add(deFora);
        ctx.SaveChanges();

        var resultado = await PorFetch(ctx, organizador.Id)
            .MarcarCheckIn(deFora.Id, jogo.Id, presente: true);

        Assert.IsType<NotFoundResult>(resultado);
    }

    // ── O MARKUP QUE O JAVASCRIPT PROCURA ────────────────────────────────────────────────

    [Fact]
    public void O_formulario_diz_ao_JavaScript_que_ele_e_de_check_in()
    {
        // Sem `data-pdz-checkin` o ouvinte não acha o botão, o clique segue pro POST de sempre e
        // a página volta a recarregar inteira — em silêncio, sem erro no console e sem nenhum
        // outro teste enxergando, porque o caminho antigo FUNCIONA.
        var fonte = Ler("_BotaoDoCheckIn.cshtml");
        var form = Regex.Match(fonte, @"<form[^>]*asp-action=""MarcarCheckIn""[^>]*>", RegexOptions.Singleline);

        Assert.True(form.Success, "Não achei o formulário do check-in.");
        Assert.Contains("data-pdz-checkin", form.Value);

        // E o `data-manter-posicao` fica: é o caminho sem JavaScript, que ainda recarrega.
        Assert.Contains("data-manter-posicao", form.Value);
    }

    [Fact]
    public void O_botao_carrega_o_rotulo_do_OUTRO_estado()
    {
        // A pintura é imediata, então o rótulo (title/aria-label) tem que virar junto — e as
        // palavras são escolhidas AQUI, numa view só, e não copiadas dentro do JavaScript.
        var fonte = Ler("_BotaoDoCheckIn.cshtml");

        Assert.Contains("data-rotulo-outro", fonte);
        Assert.Matches(new Regex(@"data-rotulo-outro=""@rotuloOutro"""), fonte);
    }

    [Theory]
    [InlineData("Details.cshtml")]
    [InlineData("jogos.cshtml")]
    public void As_duas_telas_que_desenham_a_bolinha_carregam_o_script(string arquivo)
    {
        // A bolinha aparece nas duas (a aba Jogos do torneio e a /Torneios/Jogos). Faltando o
        // script numa delas, o check-in daquela tela volta a recarregar a página — e é o tipo de
        // esquecimento que só aparece no sábado, no meio do torneio.
        var fonte = Ler(arquivo);

        Assert.Contains("js/checkin-sem-recarregar.js", fonte);
        // `asp-append-version` obrigatório: o Service Worker guarda script em cache, e sem a
        // versão o organizador fica preso no JS velho indefinidamente.
        Assert.Matches(new Regex(
            @"<script src=""~/js/checkin-sem-recarregar\.js"" asp-append-version=""true""></script>"), fonte);
    }

    [Fact]
    public void A_bolinha_tem_cor_de_erro_no_CSS()
    {
        // Sem a recarga não há nada denunciando o que não gravou: a classe que o JS põe no botão
        // precisa existir no CSS, senão a falha é pintada de nada.
        var css = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

        Assert.Matches(new Regex(@"\.pdz-jl-checkin-erro\s*\{[^}]*color:", RegexOptions.Singleline), css);
    }

    private static string Ler(string arquivo) =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", arquivo));

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Padelizou.csproj")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
