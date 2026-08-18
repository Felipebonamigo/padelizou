using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// Duas coisas que só dava pra decidir na CRIAÇÃO e agora se editam: o torneio ser restrito
// (chave de acesso) e a estrutura da categoria de times. Antes, "esqueci de marcar restrito"
// ou "achei que davam 8 times, vieram 6" obrigavam a refazer o torneio inteiro.
public class EditarTorneioRestritoETimesTests
{
    // ---- Torneio restrito no Editar ----

    private static async Task<(Torneio torneio, Jogador organizador)> MontarAsync(
        DbPadelContext ctx, bool restrito = false, string? chave = null)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        torneio.Restrito = restrito;
        torneio.ChaveAcesso = chave;
        await ctx.SaveChangesAsync();
        return (torneio, organizador);
    }

    private static Task<IActionResult> EditarAsync(
        DbPadelContext ctx, Torneio torneio, int organizadorId, bool restrito, string? chave = null)
        => TestInfra.NovoTorneiosController(ctx, organizadorId).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null, dataInicio: torneio.DataInicio,
            precoInscricao: torneio.PrecoInscricao, clubeId: torneio.ClubeId,
            quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            restrito: restrito, chaveAcessoEscolhida: chave);

    [Fact]
    public async Task Ligar_restrito_no_editar_gera_chave_sozinho()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx);

        await EditarAsync(ctx, torneio, organizador.Id, restrito: true);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.True(salvo!.Restrito);
        Assert.False(string.IsNullOrWhiteSpace(salvo.ChaveAcesso));
    }

    [Fact]
    public async Task Campo_de_chave_vazio_MANTEM_a_chave_que_ja_existe()
    {
        // A regra que protege o organizador: ele abre o editar pra mudar o horário, não
        // digita nada no campo da chave e salva. Sortear uma nova aqui derrubaria todo mundo
        // que já recebeu "virgili10" no grupo do WhatsApp.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx, restrito: true, chave: "virgili10");

        await EditarAsync(ctx, torneio, organizador.Id, restrito: true, chave: null);

        Assert.Equal("virgili10", (await ctx.Torneios.FindAsync(torneio.Id))!.ChaveAcesso);
    }

    [Fact]
    public async Task Digitar_uma_chave_nova_troca_a_antiga()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx, restrito: true, chave: "virgili10");

        await EditarAsync(ctx, torneio, organizador.Id, restrito: true, chave: "corneteiros");

        Assert.Equal("corneteiros", (await ctx.Torneios.FindAsync(torneio.Id))!.ChaveAcesso);
    }

    [Fact]
    public async Task Desligar_restrito_apaga_a_chave()
    {
        // Chave guardada num torneio aberto é senha que não tranca nada — e voltaria a valer
        // sozinha se ele religasse o restrito.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx, restrito: true, chave: "virgili10");

        await EditarAsync(ctx, torneio, organizador.Id, restrito: false);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.False(salvo!.Restrito);
        Assert.Null(salvo.ChaveAcesso);
    }

    [Fact]
    public async Task Chave_invalida_e_recusada_sem_gravar_o_resto()
    {
        // A recusa vem ANTES de qualquer gravação: salvar metade do formulário e recusar a
        // outra metade deixaria o torneio num estado que o organizador não pediu.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx);
        var nomeAntes = torneio.Nome;

        await EditarAsync(ctx, torneio, organizador.Id, restrito: true, chave: "ab");   // curta demais

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.False(salvo!.Restrito);
        Assert.Equal(nomeAntes, salvo.Nome);
    }

    [Fact]
    public async Task Desligar_o_restrito_NAO_publica_o_torneio_que_estava_escondido()
    {
        // ⚠️ ESTE TESTE TROCOU DE LADO EM 18/08/2026. Ele era o "Oculto_so_vale_dentro_do
        // _restrito": esconder dependia de o torneio ser restrito, e desligar o restrito
        // publicava o torneio junto. A regra caiu porque quem precisa esconder é quase sempre
        // o torneio ABERTO que ainda não foi divulgado (ver TorneioOcultoTests).
        //
        // O que ficou no lugar é o contrário: o Editar não encosta na visibilidade. Mexer na
        // chave de acesso não pode revelar ao mundo um torneio que o organizador escondeu.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx, restrito: true, chave: "virgili10");
        torneio.Oculto = true;
        await ctx.SaveChangesAsync();

        await EditarAsync(ctx, torneio, organizador.Id, restrito: false);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.False(salvo!.Restrito);
        Assert.True(salvo.Oculto);
    }

    // ---- Estrutura da categoria de times no Editar ----

    private static async Task<(Torneio torneio, Categoria categoria, Jogador organizador)> MontarTimesAsync(
        DbPadelContext ctx, int timesCadastrados = 0, string status = "Inscrições Abertas")
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: status);

        var categoria = new Categoria
        {
            TorneioId = torneio.Id, Nome = "Times", Codigo = "TIMES1",
            DeTimes = true, QuantidadeTimes = 8, QuantidadeGrupos = 2, ClassificadosPorGrupo = 2,
        };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        for (int i = 1; i <= timesCadastrados; i++)
        {
            ctx.Duplas.Add(new Dupla
            {
                CategoriaId = categoria.Id, Jogador1Id = organizador.Id, NomeTime = $"Time {i}", Pago = true,
            });
        }
        await ctx.SaveChangesAsync();

        return (torneio, categoria, organizador);
    }

    [Fact]
    public async Task Da_pra_mudar_times_grupos_e_classificados_depois_de_criada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = await MontarTimesAsync(ctx);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).EditarCategoriaDeTimes(
            torneio.Id, categoria.Id, "Times da firma", quantidadeTimes: 4, quantidadeGrupos: 2, classificadosPorGrupo: 1);

        var salva = await ctx.Categorias.FindAsync(categoria.Id);
        Assert.Equal("Times da firma", salva!.Nome);
        Assert.Equal(4, salva.QuantidadeTimes);
        Assert.Equal(1, salva.ClassificadosPorGrupo);
    }

    [Fact]
    public async Task A_conta_que_nao_fecha_quadro_e_recusada_na_edicao_tambem()
    {
        // 3 grupos × 2 = 6 vagas: o pior 2º "se classificaria" e ficaria de fora da chave.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = await MontarTimesAsync(ctx);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).EditarCategoriaDeTimes(
            torneio.Id, categoria.Id, "Times", quantidadeTimes: 9, quantidadeGrupos: 3, classificadosPorGrupo: 2);

        Assert.Equal(8, (await ctx.Categorias.FindAsync(categoria.Id))!.QuantidadeTimes);   // não mudou
    }

    [Fact]
    public async Task Nao_da_pra_prometer_menos_vagas_do_que_os_times_ja_cadastrados()
    {
        // 6 times dentro e o organizador baixa pra 4: dois ficariam de fora sem ninguém
        // saber quais. Ele remove primeiro, e aí baixa.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = await MontarTimesAsync(ctx, timesCadastrados: 6);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).EditarCategoriaDeTimes(
            torneio.Id, categoria.Id, "Times", quantidadeTimes: 4, quantidadeGrupos: 2, classificadosPorGrupo: 2);

        Assert.Equal(8, (await ctx.Categorias.FindAsync(categoria.Id))!.QuantidadeTimes);
    }

    [Fact]
    public async Task Depois_do_sorteio_a_estrutura_nao_muda_mais()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = await MontarTimesAsync(ctx, status: "Fase de Grupos");

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).EditarCategoriaDeTimes(
            torneio.Id, categoria.Id, "Times", quantidadeTimes: 4, quantidadeGrupos: 2, classificadosPorGrupo: 1);

        Assert.Equal(8, (await ctx.Categorias.FindAsync(categoria.Id))!.QuantidadeTimes);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_edita_a_categoria()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = await MontarTimesAsync(ctx);
        var estranho = new Jogador { Nome = "Estranho", Cpf = "99900000077" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, estranho.Id).EditarCategoriaDeTimes(
            torneio.Id, categoria.Id, "Times", quantidadeTimes: 4, quantidadeGrupos: 2, classificadosPorGrupo: 1);

        Assert.IsType<ForbidResult>(resultado);
    }

    [Fact]
    public async Task Categoria_vazia_pode_ser_removida_mas_com_time_dentro_nao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = await MontarTimesAsync(ctx, timesCadastrados: 2);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).RemoverCategoriaDeTimes(torneio.Id, categoria.Id);
        Assert.NotNull(await ctx.Categorias.FindAsync(categoria.Id));   // recusou: tem time dentro

        ctx.Duplas.RemoveRange(ctx.Duplas.Where(d => d.CategoriaId == categoria.Id));
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).RemoverCategoriaDeTimes(torneio.Id, categoria.Id);
        Assert.Null(await ctx.Categorias.FindAsync(categoria.Id));
    }
}
