using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// O RANKING DE DESAFIOS (fase 2 do DESAFIOS.md).
//
// ⚠️ O que se guarda aqui é o que faz uma tabela de ranking mentir sem quebrar: linha que soma
// duas vezes, empate que troca de ordem entre dois carregamentos, jogo velho que nunca sai, e a
// dupla que aparece duas vezes só porque desafiou numa semana e foi desafiada na outra.
public class DesafiosRankingTests
{
    // Um desafio já fechado. `pontos` são os CONGELADOS na linha — o ranking só soma o que
    // está gravado, nunca recalcula (é a razão de a fórmula poder mudar sem reescrever o
    // passado).
    private static Desafio Fechado(int d1, int d2, int a1, int a2,
        int gamesDesafiante, int gamesDesafiado, int pontosDesafiante, int pontosDesafiado,
        DateTime confirmadoEm, int categoriaId = 1, int clubeId = 1) => new()
        {
            DesafianteJogador1Id = d1, DesafianteJogador2Id = d2,
            DesafiadoJogador1Id = a1, DesafiadoJogador2Id = a2,
            CategoriaPadraoId = categoriaId, ClubeId = clubeId,
            Formato = FormatoDoDesafio.UmSet,
            Status = Desafio.Confirmado,
            DataHora = confirmadoEm.AddHours(-2),
            PropostoEm = confirmadoEm.AddDays(-1),
            GamesDesafiante = gamesDesafiante, GamesDesafiado = gamesDesafiado,
            ConfirmadoEm = confirmadoEm,
            PontosDesafiante = pontosDesafiante, PontosDesafiado = pontosDesafiado,
        };

    private static Dictionary<int, Jogador> Pessoas(params int[] ids) =>
        ids.ToDictionary(i => i, i => new Jogador { Id = i, Nome = $"Jogador {i:00}", Cpf = $"999000000{i:00}" });

    // ── As duas tabelas ───────────────────────────────────────────────────────────────

    [Fact]
    public void A_dupla_e_a_mesma_tenha_ela_desafiado_ou_sido_desafiada()
    {
        // ⚠️ Sem a chave canônica, "1 & 2" desafiando na terça e sendo desafiada no sábado
        // viraria DUAS linhas com metade dos jogos cada.
        var agora = new DateTime(2026, 8, 11, 20, 0, 0);
        var desafios = new[]
        {
            Fechado(1, 2, 3, 4, 6, 3, 11, 1, agora.AddDays(-5)),
            Fechado(3, 4, 2, 1, 2, 6, 1, 11, agora.AddDays(-2)),   // lados trocados
        };

        var tabela = RankingDeDesafios.PorDupla(desafios, Pessoas(1, 2, 3, 4));

        Assert.Equal(2, tabela.Count);

        var lider = tabela[0];
        Assert.Equal(ChaveDaDupla.De(1, 2), lider.Chave);
        Assert.Equal(2, lider.Desafios);
        Assert.Equal(2, lider.Vitorias);
        Assert.Equal(22, lider.Pontos);
        Assert.Equal(100, lider.Aproveitamento);

        var lanterna = tabela[1];
        Assert.Equal(2, lanterna.Desafios);
        Assert.Equal(0, lanterna.Vitorias);
        Assert.Equal(2, lanterna.Pontos);
    }

    [Fact]
    public void Na_tabela_de_jogadores_a_pessoa_soma_com_qualquer_parceiro()
    {
        var agora = new DateTime(2026, 8, 11, 20, 0, 0);
        var desafios = new[]
        {
            Fechado(1, 2, 3, 4, 6, 3, 11, 1, agora.AddDays(-5)),   // 1 joga com 2
            Fechado(1, 5, 3, 4, 6, 1, 11, 1, agora.AddDays(-3)),   // 1 joga com 5
        };

        var porJogador = RankingDeDesafios.PorJogador(desafios, Pessoas(1, 2, 3, 4, 5));
        var eu = porJogador.Single(l => l.Chave == "1");

        Assert.Equal(2, eu.Desafios);
        Assert.Equal(22, eu.Pontos);

        // E na tabela de duplas os mesmos jogos viram DOIS pares diferentes.
        var porDupla = RankingDeDesafios.PorDupla(desafios, Pessoas(1, 2, 3, 4, 5));
        Assert.Equal(11, porDupla.Single(l => l.Chave == ChaveDaDupla.De(1, 2)).Pontos);
        Assert.Equal(11, porDupla.Single(l => l.Chave == ChaveDaDupla.De(1, 5)).Pontos);
    }

    [Fact]
    public void As_duas_tabelas_leem_as_mesmas_partidas_e_por_isso_nao_se_somam()
    {
        // ⚠️ Este teste existe pra travar a tentação de "juntar tudo num ranking só". O total de
        // pontos é IGUAL nas duas — somá-las contaria cada jogo duas vezes, que foi exatamente o
        // erro do Ranking Americano (individual × duplas).
        var agora = new DateTime(2026, 8, 11, 20, 0, 0);
        var desafios = new[]
        {
            Fechado(1, 2, 3, 4, 6, 3, 11, 1, agora.AddDays(-5)),
            Fechado(1, 5, 3, 4, 6, 1, 11, 1, agora.AddDays(-3)),
        };
        var pessoas = Pessoas(1, 2, 3, 4, 5);

        var totalPorDupla = RankingDeDesafios.PorDupla(desafios, pessoas).Sum(l => l.Pontos);
        var totalPorJogador = RankingDeDesafios.PorJogador(desafios, pessoas).Sum(l => l.Pontos);

        Assert.Equal(24, totalPorDupla);
        Assert.Equal(totalPorDupla * 2, totalPorJogador);   // dois jogadores por lado
    }

    // ── A janela de 12 meses ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Desafio_de_mais_de_doze_meses_sai_do_ranking_sozinho()
    {
        // Igual ao ranking anual do RANKING.md: a campanha expira, e é isso que mata o
        // "campeão eterno".
        using var ctx = TestInfra.NovoContexto();
        var agora = new DateTime(2026, 8, 11, 20, 0, 0);

        ctx.Desafios.AddRange(
            Fechado(1, 2, 3, 4, 6, 0, 11, 1, agora.AddMonths(-13)),
            Fechado(1, 2, 3, 4, 6, 0, 11, 1, agora.AddMonths(-11)));
        await ctx.SaveChangesAsync();

        var contam = await RankingDeDesafios.QueContam(ctx.Desafios, agora).ToListAsync();

        Assert.Single(contam);
        Assert.Equal(agora.AddMonths(-11), contam[0].ConfirmadoEm);
    }

    [Fact]
    public async Task Placar_em_disputa_e_jogo_sem_comparecimento_ficam_de_fora()
    {
        using var ctx = TestInfra.NovoContexto();
        var agora = new DateTime(2026, 8, 11, 20, 0, 0);

        var emDisputa = Fechado(1, 2, 3, 4, 6, 0, 11, 1, agora.AddDays(-2));
        emDisputa.Status = Desafio.EmDisputa;

        var semJogo = Fechado(1, 2, 5, 6, 0, 0, 0, 0, agora.AddDays(-3));
        semJogo.Status = Desafio.SemComparecimento;

        var aceito = Fechado(1, 2, 3, 4, 6, 0, 11, 1, agora.AddDays(-1));
        aceito.Status = Desafio.Aceito;
        aceito.ConfirmadoEm = null;

        ctx.Desafios.AddRange(emDisputa, semJogo, aceito,
            Fechado(1, 2, 3, 4, 6, 2, 11, 1, agora.AddDays(-4)));
        await ctx.SaveChangesAsync();

        var contam = await RankingDeDesafios.QueContam(ctx.Desafios, agora).ToListAsync();

        Assert.Single(contam);
        Assert.Equal(Desafio.Confirmado, contam[0].Status);
    }

    // ── A ordenação ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Empate_em_pontos_e_desempatado_de_forma_estavel()
    {
        // ⚠️ Ordenação parcial faz a tabela trocar de ordem entre dois carregamentos da MESMA
        // página — ninguém reporta isso como bug e todo mundo desconfia. Rodar duas vezes com a
        // lista embaralhada tem que dar exatamente a mesma sequência.
        var agora = new DateTime(2026, 8, 11, 20, 0, 0);
        var desafios = new[]
        {
            Fechado(1, 2, 3, 4, 6, 0, 5, 5, agora.AddDays(-5)),
            Fechado(5, 6, 7, 8, 6, 0, 5, 5, agora.AddDays(-4)),
            Fechado(3, 4, 5, 6, 6, 0, 5, 5, agora.AddDays(-3)),
        };
        var pessoas = Pessoas(1, 2, 3, 4, 5, 6, 7, 8);

        var primeira = RankingDeDesafios.PorDupla(desafios, pessoas).Select(l => l.Chave).ToList();
        var segunda = RankingDeDesafios.PorDupla(desafios.Reverse(), pessoas).Select(l => l.Chave).ToList();

        Assert.Equal(primeira, segunda);
        Assert.Equal(Enumerable.Range(1, primeira.Count),
            RankingDeDesafios.PorDupla(desafios, pessoas).Select(l => l.Posicao));
    }

    [Fact]
    public void Quem_ganhou_mais_passa_na_frente_de_quem_so_apareceu()
    {
        var agora = new DateTime(2026, 8, 11, 20, 0, 0);
        var desafios = new[]
        {
            // A dupla 1&2 vence uma vez (11 pts). A 5&6 aparece quatro vezes sem vencer (4 pts).
            Fechado(1, 2, 3, 4, 6, 0, 11, 1, agora.AddDays(-9)),
            Fechado(7, 8, 5, 6, 6, 0, 11, 1, agora.AddDays(-8)),
            Fechado(7, 8, 5, 6, 6, 1, 11, 1, agora.AddDays(-7)),
            Fechado(7, 8, 5, 6, 6, 2, 11, 1, agora.AddDays(-6)),
            Fechado(7, 8, 5, 6, 6, 3, 11, 1, agora.AddDays(-5)),
        };

        var tabela = RankingDeDesafios.PorDupla(desafios, Pessoas(1, 2, 3, 4, 5, 6, 7, 8));

        Assert.Equal(ChaveDaDupla.De(7, 8), tabela[0].Chave);   // 44 pts
        Assert.Equal(ChaveDaDupla.De(1, 2), tabela[1].Chave);   // 11 pts
        Assert.Equal(ChaveDaDupla.De(5, 6), tabela[2].Chave);   // 4 pts, 4 jogos
        Assert.Equal(0, tabela[2].Vitorias);
        Assert.Equal(0, tabela[2].Aproveitamento);
    }

    // ── O resumo do perfil ────────────────────────────────────────────────────────────

    [Fact]
    public void O_resumo_do_perfil_conta_so_os_jogos_da_pessoa()
    {
        var agora = new DateTime(2026, 8, 11, 20, 0, 0);
        var desafios = new[]
        {
            Fechado(1, 2, 3, 4, 6, 3, 11, 1, agora.AddDays(-5)),   // 1 venceu
            Fechado(3, 4, 1, 5, 6, 2, 11, 1, agora.AddDays(-3)),   // 1 perdeu
            Fechado(6, 7, 8, 9, 6, 0, 11, 1, agora.AddDays(-2)),   // sem o 1
        };

        var resumo = RankingDeDesafios.DoJogador(desafios, 1);

        Assert.Equal(2, resumo.Desafios);
        Assert.Equal(1, resumo.Vitorias);
        Assert.Equal(1, resumo.Derrotas);
        Assert.Equal(50, resumo.Aproveitamento);
        Assert.Equal(12, resumo.Pontos);
        Assert.False(resumo.Vazio);
    }

    [Fact]
    public void Quem_nunca_desafiou_tem_resumo_vazio_em_vez_de_zero_por_cento()
    {
        // "0%" num perfil novo faria a pessoa parecer ruim em vez de nova — a tela não mostra
        // a linha quando está vazia.
        var resumo = RankingDeDesafios.DoJogador(Array.Empty<Desafio>(), 1);

        Assert.True(resumo.Vazio);
        Assert.Equal(0, resumo.Aproveitamento);
    }

    // ── A tela ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Em_construcao_o_ranking_nao_responde_pra_jogador_comum()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = TestInfra.NovoJogador(1);
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();

        var fechado = TestInfra.NovoDesafiosController(ctx, jogador.Id, habilitado: false);
        Assert.IsType<NotFoundResult>(await fechado.Ranking(null, null));

        var aberto = TestInfra.NovoDesafiosController(ctx, jogador.Id, habilitado: true);
        Assert.IsType<ViewResult>(await aberto.Ranking(null, null));
    }

    [Fact]
    public async Task Filtro_de_categoria_e_de_clube_recortam_a_tabela()
    {
        using var ctx = TestInfra.NovoContexto();
        var agora = DateTime.Now;

        for (int i = 1; i <= 8; i++) ctx.Jogadores.Add(TestInfra.NovoJogador(i));
        ctx.CategoriasPadrao.AddRange(
            new CategoriaPadrao { Id = 1, Nome = "4ª Categoria", Codigo = "C4M", Tipo = "Masculina" },
            new CategoriaPadrao { Id = 2, Nome = "2ª Categoria", Codigo = "C2M", Tipo = "Masculina" });
        ctx.Clubes.AddRange(
            new Clube { Id = 1, Nome = "Golden Point" },
            new Clube { Id = 2, Nome = "Padel Garden" });
        await ctx.SaveChangesAsync();

        ctx.Desafios.AddRange(
            Fechado(1, 2, 3, 4, 6, 0, 11, 1, agora.AddDays(-5), categoriaId: 1, clubeId: 1),
            Fechado(5, 6, 7, 8, 6, 0, 11, 1, agora.AddDays(-4), categoriaId: 2, clubeId: 2));
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoDesafiosController(ctx, 1);

        var tudo = (RankingDeDesafiosVM)((ViewResult)await controller.Ranking(null, null)).Model!;
        Assert.Equal(4, tudo.Duplas.Count);

        var porCategoria = (RankingDeDesafiosVM)((ViewResult)await controller.Ranking(1, null)).Model!;
        Assert.Equal(2, porCategoria.Duplas.Count);
        Assert.Contains(porCategoria.Duplas, l => l.Chave == ChaveDaDupla.De(1, 2));

        var porClube = (RankingDeDesafiosVM)((ViewResult)await controller.Ranking(null, 2)).Model!;
        Assert.Equal(2, porClube.Duplas.Count);
        Assert.Contains(porClube.Duplas, l => l.Chave == ChaveDaDupla.De(5, 6));

        // O filtro de clube só oferece clubes que JÁ receberam desafio — um filtro que só sabe
        // esvaziar a tela ensina a pessoa a não usar filtro.
        Assert.Equal(2, tudo.CatalogoClubes.Count);
    }

    [Fact]
    public async Task A_tela_sabe_qual_linha_e_minha_nas_duas_tabelas()
    {
        using var ctx = TestInfra.NovoContexto();
        var agora = DateTime.Now;

        for (int i = 1; i <= 4; i++) ctx.Jogadores.Add(TestInfra.NovoJogador(i));
        ctx.CategoriasPadrao.Add(new CategoriaPadrao { Id = 1, Nome = "4ª", Codigo = "C4M", Tipo = "Masculina" });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Golden Point" });
        await ctx.SaveChangesAsync();

        ctx.Desafios.Add(Fechado(1, 2, 3, 4, 6, 0, 11, 1, agora.AddDays(-1)));
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoDesafiosController(ctx, 2);
        var vm = (RankingDeDesafiosVM)((ViewResult)await controller.Ranking(null, null)).Model!;

        // Na de duplas, a linha é minha porque eu sou UM dos dois.
        Assert.True(vm.SouEu(vm.Duplas.Single(l => l.Chave == ChaveDaDupla.De(1, 2))));
        Assert.False(vm.SouEu(vm.Duplas.Single(l => l.Chave == ChaveDaDupla.De(3, 4))));
        Assert.True(vm.SouEu(vm.Jogadores.Single(l => l.Chave == "2")));
    }
}
