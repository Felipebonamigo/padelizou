using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// TORNEIO FECHADO NÃO SE ANUNCIA SOZINHO (20/09/2026).
//
// 🗣️ Felipe, com o print da vitrine mostrando o "Los Corneteiros | Seletiva QTimes" entre os
// torneios abertos: *"E aqui ele nao deveria aparecer pra todos"*.
//
// Ontem o push parou de sair pra base. A VITRINE continuou anunciando — listagem, Home, páginas
// de cidade, Google. Anunciar pra quem não pode entrar é o mesmo defeito numa superfície maior.
//
// ⚠️ DESCOBERTA NÃO É PERMISSÃO, e a diferença é o coração deste bloco: a PÁGINA continua
// abrindo por link direto. Se ela fechasse como o `Oculto` faz (404), a chave de acesso não
// serviria pra nada — ninguém conseguiria abrir pra digitá-la. `VisibilidadeDoTorneio` fica
// intocado de propósito.
//
// ⚠️ E ISTO REVERTE METADE DE UMA DECISÃO DE 18/08/2026, que separou `Oculto` de `Restrito` (ver
// Criacao.cs:1838). O que aquela decisão protegia era o torneio ABERTO e não divulgado, que
// precisava de um jeito de ficar escondido; isso continua valendo. O que volta a andar junto é
// só o outro lado: fechado não se anuncia.
public class TorneioFechadoNaoSeAnunciaTests
{
    private static Torneio Aberto() => new() { Id = 1, Nome = "Aberto", AprovadoEm = new DateTime(2026, 9, 1), Status = "Inscrições Abertas" };

    [Fact]
    public void Torneio_normal_continua_se_anunciando()
    {
        Assert.True(PermissaoDeOrganizador.SeAnuncia(Aberto()));
        Assert.True(PermissaoDeOrganizador.ApareceNaDescoberta(Aberto()));
    }

    [Theory]
    [InlineData(true, null)]    // só chave
    [InlineData(false, 9)]      // só camisa
    [InlineData(true, 9)]       // as duas
    public void Torneio_restrito_ou_de_time_NAO_se_anuncia(bool restrito, int? timeExclusivoId)
    {
        var torneio = Aberto();
        torneio.Restrito = restrito;
        torneio.TimeExclusivoId = timeExclusivoId;

        Assert.False(PermissaoDeOrganizador.SeAnuncia(torneio));

        // É por aqui que somem de uma vez o sitemap, as páginas de cidade e o seletor do
        // ranking — a régua é uma só, referenciada, nunca copiada.
        Assert.False(PermissaoDeOrganizador.ApareceNaDescoberta(torneio));

        // ⚠️ E CONTINUA SENDO "público": a página abre, o link funciona, a chave destranca.
        // Descoberta não é permissão — se estas duas colassem, a chave de acesso morreria.
        Assert.True(PermissaoDeOrganizador.ApareceParaOPublico(torneio));
    }

    [Fact]
    public void O_PARCEIRO_DO_RANKING_CONTINUA_RECEBENDO_o_torneio_fechado_de_proposito()
    {
        // ⚠️ ESTA É A LINHA QUE SEPARA A VITRINE DO CONTRATO, e ela fica fora da mudança de
        // propósito. Eu quase tirei o torneio fechado da lista do parceiro junto com o resto,
        // com o argumento de que ele dá ZERO ponto no ranking deles
        // (EstatisticasService.ContaNoRanking). O argumento estava mal-informado: a API já
        // manda `InscricaoRestrita`, que cobre restrito E time exclusivo, e o parceiro decide
        // o que faz — ver ApiDeTorneiosDoParceiroTests, que fixa isso com o motivo escrito.
        //
        // Tirar de lá é mudança de CONTRATO (API-TORNEIOS.md, nível architectural) e deixaria
        // `InscricaoRestrita` valendo `false` pra sempre — um campo morto numa API publicada.
        // Está pendente de decisão do Felipe, com o fato certo na mesa.
        var torneio = Aberto();
        torneio.ValidarPeloRankingRs = true;

        torneio.Restrito = true;
        Assert.True(TorneiosParaOParceiroDoRanking.EntraNaLista(torneio));
        Assert.False(EstatisticasService.ContaNoRanking(torneio));
    }

    // ── A listagem, e os quatro escapes ──────────────────────────────────────────────────

    private static async Task<List<Torneio>> AbertosParaAsync(DbPadelContext ctx, int? quem)
    {
        var view = Assert.IsType<ViewResult>(await TestInfra.NovoTorneiosController(ctx, quem ?? 0).Index());
        return (List<Torneio>)view.ViewData["Abertos"]!;
    }

    private static async Task<(Torneio torneio, Jogador organizador)> TorneioDoTimeAsync(DbPadelContext ctx)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        torneio.AprovadoEm = new DateTime(2026, 9, 1);
        ctx.Times.Add(new Time { Id = 9, Nome = "Los Corneteiros" });
        torneio.TimeExclusivoId = 9;
        await ctx.SaveChangesAsync();
        return (torneio, organizador);
    }

    [Fact]
    public async Task Quem_nao_e_do_time_NAO_ve_o_torneio_na_listagem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await TorneioDoTimeAsync(ctx);
        var deFora = TestInfra.NovoJogador(70);
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        Assert.DoesNotContain(await AbertosParaAsync(ctx, deFora.Id), t => t.Id == torneio.Id);
    }

    [Fact]
    public async Task Quem_VESTE_a_camisa_continua_vendo_na_listagem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await TorneioDoTimeAsync(ctx);
        var doTime = TestInfra.NovoJogador(71);
        doTime.TimeId = 9;
        ctx.Jogadores.Add(doTime);
        await ctx.SaveChangesAsync();

        Assert.Contains(await AbertosParaAsync(ctx, doTime.Id), t => t.Id == torneio.Id);
    }

    [Fact]
    public async Task O_ORGANIZADOR_continua_vendo_o_proprio_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await TorneioDoTimeAsync(ctx);

        Assert.Contains(await AbertosParaAsync(ctx, organizador.Id), t => t.Id == torneio.Id);
    }

    [Fact]
    public async Task Quem_JA_ESTA_INSCRITO_nao_perde_o_torneio_de_vista()
    {
        // O escape que NÃO existia: `meusTorneioIds` só olhava quem ORGANIZA. Sem este, alguém
        // que já pagou via o próprio torneio sumir da lista — a mesma lição do escape 3 do
        // VisibilidadeDoTorneio ("não pode trancar do lado de fora quem já pagou").
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.AprovadoEm = new DateTime(2026, 9, 1);
        torneio.Restrito = true;
        await ctx.SaveChangesAsync();

        var inscrito = await ctx.Duplas.Select(d => d.Jogador1Id).FirstAsync();

        Assert.Contains(await AbertosParaAsync(ctx, inscrito), t => t.Id == torneio.Id);
    }
}
