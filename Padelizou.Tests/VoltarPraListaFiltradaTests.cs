using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — MARCAR A CHEGADA NÃO PODE TIRAR O ORGANIZADOR DA LISTA QUE ELE ESTAVA OLHANDO.
//
// 🗣️ Felipe, com o check-in por jogador já no ar: *"Ao marcar de confirmar na tela, ele sai da
// tela, ele tem q sempre se manter na tela da alteracao"*.
//
// 🕳️ REPRODUZIDO NO NAVEGADOR, com o app rodando: filtrado numa categoria a aba dizia
// "Agendadas (2)" com 2 jogos na tela; depois de marcar uma chegada, "Agendadas (3)" com 3 — o
// redirect voltava pra `Details` NU, sem a query string, e a lista inteira do torneio voltava por
// cima do recorte. A rolagem até era restaurada no mesmo pixel, o que PIORA: o mesmo lugar da
// tela, com outra lista embaixo. Num torneio de 12 categorias e 97 jogos, é perder o lugar.
//
// ⚠️ O TETO ESTAVA ESCRITO NO CÓDIGO desde a primeira versão ("o filtro da tela NÃO volta junto")
// e era herdado do `VoltarDaLargada`. Escrever o teto não é o mesmo que ele não incomodar.
//
// ⚠️ A LISTA DE CHAVES ACEITAS É FECHADA (Services/FiltrosDaListaDeJogos): o que volta pela URL
// são os SEIS filtros que as ações `Details`/`Jogos` já conhecem, e nada além. Campo de
// formulário não vira rota arbitrária — mesma régua do `voltarPara`.
public class VoltarPraListaFiltradaTests
{
    private static (Torneio torneio, Jogador organizador, Dupla dupla, Partida jogo) Montar(DbPadelContext ctx)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        torneio.UsaCheckIn = true;
        ctx.SaveChanges();
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();
        // A presença é do JOGO desde 12/09/2026 (Models/PresencaNoJogo): sem um jogo não há o que
        // marcar, e o clique que este arquivo mede é o da linha da lista de jogos.
        var jogo = TestInfra.NovoJogo(ctx, categoria, duplas[0], duplas[1]);
        return (torneio, organizador, duplas[0], jogo);
    }

    // ── O QUE VOLTA NA URL ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_filtro_de_categoria_volta_junto_com_o_clique()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, dupla, jogo) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id).MarcarCheckIn(
            dupla.Jogador1Id, jogo.Id, presente: true,
            voltarPara: "Details", filtros: "?categoriaFiltroIds=7&categoriaFiltroIds=9");

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Details", redir.ActionName);
        Assert.Equal(torneio.Id, redir.RouteValues!["id"]);
        Assert.Equal("jogosDoTorneio", redir.Fragment);
        // As DUAS categorias: o filtro é múltipla escolha, e voltar só com a primeira seria
        // outro recorte — não o dele.
        Assert.Equal(new[] { "7", "9" }, Assert.IsAssignableFrom<string[]>(redir.RouteValues["categoriaFiltroIds"]));
    }

    [Fact]
    public async Task Os_outros_filtros_da_tela_voltam_tambem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, dupla, jogo) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id).MarcarCheckIn(
            dupla.Jogador1Id, jogo.Id, presente: true, voltarPara: "Jogos",
            filtros: "?timeFiltroId=3&soMeusJogos=true&clubeFiltroId=5&quadraFiltro=Arena+2&faseFiltro=Semifinal");

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Jogos", redir.ActionName);
        Assert.Equal("3", redir.RouteValues!["timeFiltroId"]);
        Assert.Equal("true", redir.RouteValues["soMeusJogos"]);
        Assert.Equal("5", redir.RouteValues["clubeFiltroId"]);
        Assert.Equal("Arena 2", redir.RouteValues["quadraFiltro"]);
        Assert.Equal("Semifinal", redir.RouteValues["faseFiltro"]);
    }

    [Fact]
    public async Task Sem_filtro_nenhum_a_volta_continua_a_de_sempre()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, dupla, jogo) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Jogador1Id, jogo.Id, presente: true, voltarPara: "Details");

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Details", redir.ActionName);
        var rota = Assert.IsAssignableFrom<Microsoft.AspNetCore.Routing.RouteValueDictionary>(redir.RouteValues);
        Assert.Single(rota);   // só o id
        Assert.Equal(torneio.Id, rota["id"]);
    }

    // ── A LISTA FECHADA ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("?returnUrl=https://exemplo.invalido")]
    [InlineData("?controller=Admin&action=Apagar")]
    [InlineData("?id=999")]
    [InlineData("?area=Admin")]
    public void Chave_que_nao_e_filtro_da_lista_e_jogada_fora(string sujeira)
    {
        // O `filtros` chega por campo de formulário. Aceitar chave qualquer aqui seria deixar o
        // cliente montar a rota do redirect — inclusive trocar o `id` do torneio ou apontar pra
        // outro controller.
        Assert.Empty(FiltrosDaListaDeJogos.Reaproveitar(sujeira));
    }

    [Fact]
    public void O_lixo_convive_com_o_filtro_de_verdade_sem_contaminar()
    {
        var rota = FiltrosDaListaDeJogos.Reaproveitar("?categoriaFiltroIds=7&id=999&action=Apagar");

        // Valor único volta como STRING, não array de um: é o que a geração de URL sabe escrever.
        Assert.Equal("7", rota["categoriaFiltroIds"]);
        Assert.False(rota.ContainsKey("id"));
        Assert.False(rota.ContainsKey("action"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("?")]
    [InlineData("lixo sem interrogação")]
    public void Entrada_vazia_ou_estranha_nao_estoura(string? entrada)
    {
        Assert.Empty(FiltrosDaListaDeJogos.Reaproveitar(entrada));
    }

    [Fact]
    public void O_tamanho_da_query_tem_teto()
    {
        // Campo escondido é campo que dá pra editar. Sem teto, um POST com 200 KB de query
        // viraria uma URL de redirect que nenhum navegador aceita — e o organizador levaria um
        // erro no lugar da lista.
        var gigante = "?quadraFiltro=" + new string('x', 5000);
        Assert.Empty(FiltrosDaListaDeJogos.Reaproveitar(gigante));
    }

    // ── O QUE A TELA MANDA ───────────────────────────────────────────────────────────────

    [Fact]
    public void A_tela_manda_a_query_atual_no_formulario()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.QueryString = new QueryString("?categoriaFiltroIds=7&soMeusJogos=true&nada=1");

        // Só o que a volta sabe reaproveitar — mandar o resto seria carregar peso que o
        // servidor vai jogar fora de qualquer jeito.
        Assert.Equal("?categoriaFiltroIds=7&soMeusJogos=true", FiltrosDaListaDeJogos.Atual(contexto.Request));
    }

    [Fact]
    public void O_botao_do_check_in_leva_o_campo_dos_filtros()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_BotaoDoCheckIn.cshtml"));
        Assert.Contains("name=\"filtros\"", fonte);
        Assert.Contains("FiltrosDaListaDeJogos.Atual", fonte);
    }

    // ── OS VIZINHOS DE BARRA (12/09/2026, segunda rodada) ────────────────────────────────
    //
    // 🗣️ Felipe, depois de a chegada parar de perder o recorte: *"Quando corrigir, publique"* —
    // à pergunta sobre estender pro ▶, 📍, ⇄ e as setas. São os botões que moram na MESMA barra
    // e faziam a MESMA coisa: devolver a grade inteira por cima da categoria filtrada.
    //
    // ⚠️ SÃO DOIS FUNIS, e é por isso que a mudança é pequena: `TorneiosController.VoltarPara`
    // (modais de horário/quadra, setas, ajustar horários, salvar placares) e
    // `PartidasController.VoltarDaLargada` (largada e saque), mais o retorno do ControlePlacar.
    // Trocar o redirect nos três alcança os dez botões.

    [Fact]
    public async Task A_troca_de_quadra_volta_pro_mesmo_recorte()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var jogo = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id,
            Fase = "Fase de Grupos", Status = "Agendada", Codigo = "QQQ111", NomeQuadra = "Quadra 1",
        };
        ctx.Partidas.Add(jogo);
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .TrocarQuadra(torneio.Id, jogo.Id, "Quadra 2", voltarPara: "Details", filtros: "?categoriaFiltroIds=7");

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Details", redir.ActionName);
        Assert.Equal("jogosDoTorneio", redir.Fragment);
        Assert.Equal("7", redir.RouteValues!["categoriaFiltroIds"]);
    }

    [Fact]
    public async Task A_largada_volta_pro_mesmo_recorte()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var jogo = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id,
            Fase = "Fase de Grupos", Status = "Agendada", Codigo = "LLL111", NomeQuadra = "Quadra 1",
        };
        ctx.Partidas.Add(jogo);
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoPartidasController(ctx, organizador.Id)
            .ColocarNoAr(jogo.Id, voltarPara: "Details", filtros: "?quadraFiltro=Quadra+1");

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Details", redir.ActionName);
        Assert.Equal("jogosDoTorneio", redir.Fragment);
        Assert.Equal("Quadra 1", redir.RouteValues!["quadraFiltro"]);
    }

    // ── O GATE ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Todo_formulario_que_volta_pra_lista_leva_os_filtros()
    {
        // ⚠️ ESTE É O TESTE QUE IMPORTA DAQUI A UM MÊS. O botão novo da barra vai nascer copiando
        // o vizinho, e o `filtros` é justamente o campo que se esquece — ele não aparece na tela,
        // então nada denuncia a falta até alguém marcar algo com a lista filtrada e se perder.
        //
        // A régua: formulário que manda `voltarPara` está voltando PRA LISTA, e lista tem
        // recorte. Quem volta pra lista leva o recorte junto.
        var faltando = new List<string>();

        foreach (var arquivo in new[] { "_JogoEmLinha.cshtml", "_JogosDoTorneio.cshtml", "_SetasDaOrdem.cshtml", "_BotaoDoCheckIn.cshtml" })
        {
            var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", arquivo));

            // ⚠️ COMENTÁRIO RAZOR FORA DA CONTA: `@* ... *@` desta pasta cita `<form>` em prosa
            // (o _SetasDaOrdem explica por que o formulário cabe dentro do dropdown), e sem
            // recortar isso o gate acusa um formulário que não existe. Mesma armadilha do gate
            // das seções: varrer HTML com regex exige tirar o que só PARECE HTML.
            var semComentario = System.Text.RegularExpressions.Regex.Replace(
                fonte, @"@\*.*?\*@", m => new string(' ', m.Length),
                System.Text.RegularExpressions.RegexOptions.Singleline);

            // Cada <form ...> ... </form> do arquivo, um por vez.
            foreach (System.Text.RegularExpressions.Match form in
                     System.Text.RegularExpressions.Regex.Matches(semComentario, @"<form\b.*?</form>",
                         System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                bool voltaPraLista = form.Value.Contains("voltarPara", StringComparison.Ordinal);
                bool levaFiltros = form.Value.Contains("_CampoDosFiltros", StringComparison.Ordinal)
                                || form.Value.Contains("name=\"filtros\"", StringComparison.Ordinal);

                if (voltaPraLista && !levaFiltros)
                    faltando.Add($"{arquivo}: {form.Value[..Math.Min(120, form.Value.Length)].ReplaceLineEndings(" ")}");
            }
        }

        Assert.True(faltando.Count == 0,
            "Formulário que volta pra lista sem levar o recorte da tela:\n" + string.Join("\n", faltando));
    }

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
