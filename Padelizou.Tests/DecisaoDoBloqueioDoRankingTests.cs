using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// O organizador decidindo sobre quem o Ranking RS barrou.
//
// A recusa da inscrição não é a palavra final — é aqui que ela vira decisão. Os testes que mais
// importam são os de PERMISSÃO: o id do bloqueio vem do formulário, e sem amarrar bloqueio e
// torneio daria pra liberar gente no torneio dos outros mandando um número qualquer.
public class DecisaoDoBloqueioDoRankingTests
{
    private static (DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador organizador, BloqueioDoRanking bloqueio) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Interno", Codigo = "INT1", Status = "Inscrições Abertas", ValidarPeloRankingRs = true,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria
        {
            Nome = "6ª Masculina", Codigo = "C6", Torneio = torneio, RankingRsCategoriaId = 112,
        };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        var bloqueio = new BloqueioDoRanking
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Cpf = "11144477735", NomeConsultado = "Silvano Hernandorena",
            CategoriaRsId = 112, CategoriaRsNome = "6ª Masculina",
            Motivo = "pontos na 2ª Masculina", Situacao = SituacaoDoBloqueio.Pendente,
        };
        ctx.BloqueiosDoRanking.Add(bloqueio);
        ctx.SaveChanges();

        return (ctx, torneio, categoria, organizador, bloqueio);
    }

    [Fact]
    public async Task Organizador_libera_e_a_pessoa_passa_a_poder_se_inscrever()
    {
        var (ctx, _, _, organizador, bloqueio) = Montar();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        var r = await controller.LiberarBloqueioRanking(bloqueio.Id);

        Assert.IsType<RedirectToActionResult>(r);
        var salvo = await ctx.BloqueiosDoRanking.FindAsync(bloqueio.Id);
        Assert.Equal(SituacaoDoBloqueio.Liberado, salvo!.Situacao);
        Assert.Equal(organizador.Id, salvo.DecididoPorId);
        Assert.NotNull(salvo.DecididoEm);
    }

    [Fact]
    public async Task Organizador_mantem_a_recusa()
    {
        var (ctx, _, _, organizador, bloqueio) = Montar();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.ManterBloqueioRanking(bloqueio.Id);

        var salvo = await ctx.BloqueiosDoRanking.FindAsync(bloqueio.Id);
        Assert.Equal(SituacaoDoBloqueio.Mantido, salvo!.Situacao);
    }

    // Mudar de ideia é normal, e um clique errado não pode virar beco sem saída — que é
    // justamente o defeito que esta tela existe pra evitar.
    [Fact]
    public async Task Da_pra_mudar_de_ideia_depois_de_decidido()
    {
        var (ctx, _, _, organizador, bloqueio) = Montar();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.ManterBloqueioRanking(bloqueio.Id);
        await controller.LiberarBloqueioRanking(bloqueio.Id);

        var salvo = await ctx.BloqueiosDoRanking.FindAsync(bloqueio.Id);
        Assert.Equal(SituacaoDoBloqueio.Liberado, salvo!.Situacao);
    }

    // ── Permissão ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_nao_organiza_o_torneio_NAO_decide()
    {
        var (ctx, _, _, _, bloqueio) = Montar();
        var estranho = new Jogador { Nome = "Estranho", Cpf = "52998224725" };
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, estranho.Id);

        Assert.IsType<ForbidResult>(await controller.LiberarBloqueioRanking(bloqueio.Id));
        Assert.IsType<ForbidResult>(await controller.ManterBloqueioRanking(bloqueio.Id));

        var salvo = await ctx.BloqueiosDoRanking.FindAsync(bloqueio.Id);
        Assert.Equal(SituacaoDoBloqueio.Pendente, salvo!.Situacao);
    }

    // O ORGANIZADOR DE OUTRO TORNEIO também não. A permissão é por torneio DO BLOQUEIO, não
    // "ser organizador de alguma coisa" — o id vem do formulário.
    [Fact]
    public async Task Organizador_de_OUTRO_torneio_NAO_decide()
    {
        var (ctx, _, _, _, bloqueio) = Montar();

        var outroDono = new Jogador { Nome = "Dono do outro", Cpf = "52998224725" };
        ctx.Jogadores.Add(outroDono);
        var outroTorneio = new Torneio { Nome = "Outro", Codigo = "OUT1", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(outroTorneio);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = outroTorneio.Id, JogadorId = outroDono.Id });
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, outroDono.Id);

        Assert.IsType<ForbidResult>(await controller.LiberarBloqueioRanking(bloqueio.Id));

        var salvo = await ctx.BloqueiosDoRanking.FindAsync(bloqueio.Id);
        Assert.Equal(SituacaoDoBloqueio.Pendente, salvo!.Situacao);
    }

    [Fact]
    public async Task Bloqueio_que_nao_existe_nao_estoura()
    {
        var (ctx, _, _, organizador, _) = Montar();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        Assert.IsType<ForbidResult>(await controller.LiberarBloqueioRanking(99999));
    }

    // ── O de-para das categorias ──────────────────────────────────────────────────────────

    // Aba aberta antes deste deploy manda o formulário SEM os campos do ranking. "Não veio" não
    // pode ser lido como "o organizador desmarcou tudo" — isso desligaria a validação do
    // torneio inteiro sem ninguém pedir.
    [Fact]
    public async Task Formulario_antigo_sem_os_campos_NAO_apaga_o_de_para()
    {
        var (ctx, torneio, categoria, organizador, _) = Montar();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.Editar(
            id: torneio.Id, nome: "Interno", localTorneio: null, dataInicio: null,
            precoInscricao: 0, clubeId: 0, quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null);

        var salva = await ctx.Categorias.FindAsync(categoria.Id);
        Assert.Equal(112, salva!.RankingRsCategoriaId);
    }

    [Fact]
    public async Task Escolher_zero_desliga_a_conferencia_daquela_categoria()
    {
        var (ctx, torneio, categoria, organizador, _) = Montar();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.Editar(
            id: torneio.Id, nome: "Interno", localTorneio: null, dataInicio: null,
            precoInscricao: 0, clubeId: 0, quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            validarPeloRankingRs: true,
            rankingCategoriaId: new[] { categoria.Id }, rankingRsId: new[] { 0 });

        var salva = await ctx.Categorias.FindAsync(categoria.Id);
        Assert.Null(salva!.RankingRsCategoriaId);
    }

    // Id que não existe no catálogo do ranking não pode ser gravado: a requisição sairia daqui
    // já condenada a levar 400 da API.
    [Fact]
    public async Task Id_que_nao_existe_no_ranking_vira_nulo()
    {
        var (ctx, torneio, categoria, organizador, _) = Montar();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.Editar(
            id: torneio.Id, nome: "Interno", localTorneio: null, dataInicio: null,
            precoInscricao: 0, clubeId: 0, quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            validarPeloRankingRs: true,
            rankingCategoriaId: new[] { categoria.Id }, rankingRsId: new[] { 999 });

        var salva = await ctx.Categorias.FindAsync(categoria.Id);
        Assert.Null(salva!.RankingRsCategoriaId);
    }

    // Categoria de OUTRO torneio no meio do formulário é ignorada — o POST se monta à mão, e
    // aqui se decide quem pode se inscrever.
    [Fact]
    public async Task Categoria_de_outro_torneio_e_ignorada()
    {
        var (ctx, torneio, _, organizador, _) = Montar();

        var outroTorneio = new Torneio { Nome = "Outro", Codigo = "OUT1", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(outroTorneio);
        var categoriaAlheia = new Categoria
        {
            Nome = "4ª Masculina", Codigo = "C4", Torneio = outroTorneio, RankingRsCategoriaId = 108,
        };
        ctx.Categorias.Add(categoriaAlheia);
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.Editar(
            id: torneio.Id, nome: "Interno", localTorneio: null, dataInicio: null,
            precoInscricao: 0, clubeId: 0, quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            validarPeloRankingRs: true,
            rankingCategoriaId: new[] { categoriaAlheia.Id }, rankingRsId: new[] { 0 });

        var salva = await ctx.Categorias.FindAsync(categoriaAlheia.Id);
        Assert.Equal(108, salva!.RankingRsCategoriaId);
    }

    [Fact]
    public async Task Ligar_a_conferencia_fica_gravado_no_torneio()
    {
        var (ctx, torneio, categoria, organizador, _) = Montar();
        torneio.ValidarPeloRankingRs = false;
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.Editar(
            id: torneio.Id, nome: "Interno", localTorneio: null, dataInicio: null,
            precoInscricao: 0, clubeId: 0, quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            validarPeloRankingRs: true,
            rankingCategoriaId: new[] { categoria.Id }, rankingRsId: new[] { 112 });

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.True(salvo!.ValidarPeloRankingRs);
    }
}
