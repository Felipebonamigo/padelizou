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
    private static (Torneio torneio, Jogador organizador, Dupla dupla) Montar(DbPadelContext ctx)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        torneio.UsaCheckIn = true;
        ctx.SaveChanges();
        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        return (torneio, organizador, dupla);
    }

    // ── O QUE VOLTA NA URL ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_filtro_de_categoria_volta_junto_com_o_clique()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, dupla) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id).MarcarCheckIn(
            dupla.Jogador1Id, torneio.Id, presente: true,
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
        var (torneio, organizador, dupla) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id).MarcarCheckIn(
            dupla.Jogador1Id, torneio.Id, presente: true, voltarPara: "Jogos",
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
        var (torneio, organizador, dupla) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Jogador1Id, torneio.Id, presente: true, voltarPara: "Details");

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
