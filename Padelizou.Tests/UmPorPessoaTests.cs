using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Controllers;

namespace Padelizou.Tests;

// Um elogio e um comentário por pessoa em cada perfil — trocáveis e editáveis.
//
// Antes dava pra marcar os 18 elogios no mesmo perfil e deixar quantos comentários quisesse.
// O número no badge deixava de dizer "quantas pessoas acham isso" e passava a dizer "quem
// clicou mais": uma pessoa sozinha levava alguém à conquista "Querido da Quadra" (5 elogios).
// Em produção, no dia em que isso foi visto, uma jogadora já tinha passado por três perfis
// dando 3 elogios em cada.
public class UmPorPessoaTests
{
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Quem Elogia", Cpf = "1" },
            new Jogador { Id = 2, Nome = "Quem Recebe", Cpf = "2" },
            new Jogador { Id = 3, Nome = "Outra Pessoa", Cpf = "3" });
        ctx.SaveChanges();
        return ctx;
    }

    // ⚠️ O TestInfra é obrigatório aqui desde que elogio e comentário passaram a avisar a
    // pessoa: montar o controller à mão deixava o `Url` nulo, e `Url.Action` estoura.
    private static JogadoresController Como(DbPadelContext ctx, int jogadorId) =>
        TestInfra.NovoJogadoresController(ctx, jogadorId);

    // ── Elogios ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Primeiro_elogio_entra()
    {
        var ctx = Cenario();

        await Como(ctx, 1).DarElogio(2, "SmashBom");

        var elogio = Assert.Single(ctx.Elogios);
        Assert.Equal("SmashBom", elogio.Tipo);
    }

    [Fact]
    public async Task Clicar_em_outro_elogio_TROCA_em_vez_de_empilhar()
    {
        // O pedido do Felipe: um por pessoa. Antes, os dois ficavam.
        var ctx = Cenario();

        await Como(ctx, 1).DarElogio(2, "SmashBom");
        await Como(ctx, 1).DarElogio(2, "BoaVibra");

        var elogio = Assert.Single(ctx.Elogios);
        Assert.Equal("BoaVibra", elogio.Tipo);
    }

    [Fact]
    public async Task A_troca_e_avisada_com_o_que_saiu_e_o_que_entrou()
    {
        // Trocar calado faria o elogio anterior sumir da tela sem explicação, e a pessoa
        // clicaria de novo achando que perdeu o toque.
        var ctx = Cenario();
        await Como(ctx, 1).DarElogio(2, "SmashBom");

        var controller = Como(ctx, 1);
        await controller.DarElogio(2, "BoaVibra");

        var aviso = (string?)controller.TempData["Sucesso"];
        Assert.NotNull(aviso);
        Assert.Contains("Smash Bom", aviso);
        Assert.Contains("Boa Vibe", aviso);
    }

    [Fact]
    public async Task Clicar_no_mesmo_elogio_de_novo_nao_duplica_nem_avisa_troca()
    {
        var ctx = Cenario();
        await Como(ctx, 1).DarElogio(2, "SmashBom");

        var controller = Como(ctx, 1);
        await controller.DarElogio(2, "SmashBom");

        Assert.Single(ctx.Elogios);
        Assert.Null(controller.TempData["Sucesso"]);
    }

    [Fact]
    public async Task Cada_pessoa_tem_o_seu_elogio_no_mesmo_perfil()
    {
        // O limite é por PESSOA, não por perfil: o badge precisa somar gente diferente.
        var ctx = Cenario();

        await Como(ctx, 1).DarElogio(2, "SmashBom");
        await Como(ctx, 3).DarElogio(2, "BoaVibra");

        Assert.Equal(2, ctx.Elogios.Count());
    }

    [Fact]
    public async Task Remover_tira_o_meu_elogio_seja_qual_for_o_tipo()
    {
        // Casar pelo tipo faria o botão não fazer nada com a página aberta desde antes de
        // uma troca — a pessoa clicaria em "tirar" e o elogio continuaria lá.
        var ctx = Cenario();
        await Como(ctx, 1).DarElogio(2, "SmashBom");
        await Como(ctx, 1).DarElogio(2, "BoaVibra");

        await Como(ctx, 1).RemoverElogio(2);

        Assert.Empty(ctx.Elogios);
    }

    [Fact]
    public async Task Remover_nao_encosta_no_elogio_de_outra_pessoa()
    {
        var ctx = Cenario();
        await Como(ctx, 1).DarElogio(2, "SmashBom");
        await Como(ctx, 3).DarElogio(2, "BoaVibra");

        await Como(ctx, 1).RemoverElogio(2);

        var sobrou = Assert.Single(ctx.Elogios);
        Assert.Equal(3, sobrou.DeJogadorId);
    }

    [Fact]
    public async Task Ninguem_elogia_o_proprio_perfil()
    {
        var ctx = Cenario();

        await Como(ctx, 1).DarElogio(1, "SmashBom");

        Assert.Empty(ctx.Elogios);
    }

    [Fact]
    public async Task Tipo_fora_do_catalogo_e_ignorado_e_nao_apaga_o_que_ja_estava_la()
    {
        // O tipo vem do formulário: um POST na mão com lixo não pode nem gravar nem
        // atropelar a escolha real da pessoa.
        var ctx = Cenario();
        await Como(ctx, 1).DarElogio(2, "SmashBom");

        await Como(ctx, 1).DarElogio(2, "ElogioInventado");

        var elogio = Assert.Single(ctx.Elogios);
        Assert.Equal("SmashBom", elogio.Tipo);
    }

    // ── Comentários ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Primeiro_comentario_entra()
    {
        var ctx = Cenario();

        await Como(ctx, 1).ComentarPerfil(2, "Jogador excelente");

        var comentario = Assert.Single(ctx.ComentariosPerfil);
        Assert.Equal("Jogador excelente", comentario.Texto);
    }

    [Fact]
    public async Task Comentar_de_novo_EDITA_o_mesmo_comentario()
    {
        // Sem isso, corrigir uma frase deixava dois textos parecidos no perfil do outro — e
        // só o dono do perfil (ou um admin) podia apagar o primeiro.
        var ctx = Cenario();

        await Como(ctx, 1).ComentarPerfil(2, "Jogador excelente");
        await Como(ctx, 1).ComentarPerfil(2, "Jogador excelente, e ótimo parceiro");

        var comentario = Assert.Single(ctx.ComentariosPerfil);
        Assert.Equal("Jogador excelente, e ótimo parceiro", comentario.Texto);
    }

    [Fact]
    public async Task Editar_o_texto_limpa_a_denuncia_do_texto_antigo()
    {
        // A denúncia era sobre o que estava escrito antes; mantê-la deixaria o admin
        // julgando uma frase que não existe mais.
        var ctx = Cenario();
        await Como(ctx, 1).ComentarPerfil(2, "texto ruim");
        var comentario = ctx.ComentariosPerfil.Single();
        comentario.DenunciadoEm = DateTime.Now;
        comentario.DenunciadoPorId = 3;
        ctx.SaveChanges();

        await Como(ctx, 1).ComentarPerfil(2, "texto corrigido");

        Assert.Null(comentario.DenunciadoEm);
        Assert.Null(comentario.DenunciadoPorId);
    }

    [Fact]
    public async Task Reenviar_o_mesmo_texto_nao_mexe_na_denuncia()
    {
        // Senão bastaria reenviar o próprio texto pra sair da fila do admin.
        var ctx = Cenario();
        await Como(ctx, 1).ComentarPerfil(2, "texto ruim");
        var comentario = ctx.ComentariosPerfil.Single();
        var carimbo = DateTime.Now;
        comentario.DenunciadoEm = carimbo;
        comentario.DenunciadoPorId = 3;
        ctx.SaveChanges();

        await Como(ctx, 1).ComentarPerfil(2, "texto ruim");

        Assert.Equal(carimbo, comentario.DenunciadoEm);
    }

    [Fact]
    public async Task Cada_pessoa_tem_o_seu_comentario_no_mesmo_perfil()
    {
        var ctx = Cenario();

        await Como(ctx, 1).ComentarPerfil(2, "Muito bom");
        await Como(ctx, 3).ComentarPerfil(2, "Jogão");

        Assert.Equal(2, ctx.ComentariosPerfil.Count());
    }

    [Fact]
    public async Task Comentario_ofensivo_e_recusado_e_o_texto_anterior_fica_de_pe()
    {
        // Recusar tem que ser recusar de verdade: o texto bom que já estava lá não pode
        // ser apagado por uma tentativa ofensiva.
        var ctx = Cenario();
        await Como(ctx, 1).ComentarPerfil(2, "Jogador excelente");

        var controller = Como(ctx, 1);
        await controller.ComentarPerfil(2, "seu merda");

        var comentario = Assert.Single(ctx.ComentariosPerfil);
        Assert.Equal("Jogador excelente", comentario.Texto);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Comentario_vazio_nao_apaga_o_que_ja_estava_escrito()
    {
        var ctx = Cenario();
        await Como(ctx, 1).ComentarPerfil(2, "Jogador excelente");

        await Como(ctx, 1).ComentarPerfil(2, "   ");

        Assert.Equal("Jogador excelente", ctx.ComentariosPerfil.Single().Texto);
    }
}
