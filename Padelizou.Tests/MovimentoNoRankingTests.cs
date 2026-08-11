using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// QUANTAS POSIÇÕES O ÚLTIMO TORNEIO FEZ GANHAR OU PERDER (Felipe, 08/08/2026).
//
// Antes o hub comparava com "~1 mês atrás", em duas abas só. Um mês é recorte de calendário, e
// este ranking não se move por calendário — ele se move em saltos, quando um torneio acaba.
public class MovimentoNoRankingTests
{
    private sealed class Linha
    {
        public int Id { get; init; }
        public int? Movimento { get; set; }
    }

    private static List<Linha> Lista(params int[] ids) => ids.Select(i => new Linha { Id = i }).ToList();

    private static void Aplicar(List<Linha> agora, params int[] ordemAntes) =>
        MovimentoNoRanking.Aplicar(agora, ordemAntes, l => l.Id, (l, mov) => l.Movimento = mov);

    [Fact]
    public void Quem_sobe_ganha_positivo_e_quem_desce_ganha_negativo()
    {
        // Antes: 1º=10, 2º=20, 3º=30.  Agora: 30 subiu pra 1º e 10 caiu pro 3º.
        var agora = Lista(30, 20, 10);

        Aplicar(agora, 10, 20, 30);

        Assert.Equal(2, agora[0].Movimento);    // 30: era 3º, virou 1º
        Assert.Equal(0, agora[1].Movimento);    // 20: continua 2º
        Assert.Equal(-2, agora[2].Movimento);   // 10: era 1º, virou 3º
    }

    [Fact]
    public void Quem_nao_estava_na_lista_antes_e_NOVO___e_nao_zero()
    {
        // ⚠️ "novo" e "+0" são coisas diferentes: um entrou agora (não havia posição pra
        // comparar), o outro jogou e ficou onde estava. A tela mostra selos diferentes, e
        // trocar um pelo outro é a mentira pequena que ninguém percebe no meio da tabela.
        var agora = Lista(10, 99);

        Aplicar(agora, 10);

        Assert.Equal(0, agora[0].Movimento);
        Assert.Null(agora[1].Movimento);
    }

    [Fact]
    public void Sem_base_de_comparacao_ninguem_ganha_selo()
    {
        // Primeiro torneio da história daquele ranking: anunciar 40 estreias não informa nada.
        var agora = Lista(1, 2, 3);

        Aplicar(agora);   // lista "antes" vazia

        Assert.All(agora, l => Assert.Equal(0, l.Movimento));
    }

    [Fact]
    public void Quem_sumiu_do_ranking_nao_atrapalha_a_conta_de_quem_ficou()
    {
        // O 20 não aparece mais (saiu do filtro regional, por exemplo). O 30 estava em 3º e
        // agora é 2º: subiu 1, e não 2 — a posição antiga é a que era, não a recalculada.
        var agora = Lista(10, 30);

        Aplicar(agora, 10, 20, 30);

        Assert.Equal(0, agora[0].Movimento);
        Assert.Equal(1, agora[1].Movimento);
    }

    // ── A JANELA: por quanto tempo o selo fica na tela ────────────────────────────────────

    private static async Task<(DbPadelContext ctx, Torneio torneio)> MontarTorneioTerminadoAsync(
        DateTime fimDoUltimoJogo, string formato = FormatoDoTorneio.Padrao)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        torneio.Formato = formato;
        torneio.DataInicio = fimDoUltimoJogo.AddHours(-6);

        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Fase = "Final",
            Codigo = "MOV1",
            Status = "Finalizada",
            HorarioFimReal = fimDoUltimoJogo,
        });
        await ctx.SaveChangesAsync();
        return (ctx, torneio);
    }

    [Fact]
    public async Task Torneio_que_acabou_ontem_abre_a_janela()
    {
        var agora = new DateTime(2026, 8, 10, 20, 0, 0);
        var (ctx, torneio) = await MontarTorneioTerminadoAsync(agora.AddDays(-1));
        using var _ = ctx;

        var janela = await MovimentoNoRanking.DoOficialAsync(ctx, agora);

        Assert.NotNull(janela);
        Assert.Equal(torneio.Id, janela!.TorneioId);
        // O corte é ANTES do torneio começar: é ele que se manda pro cálculo do "antes".
        Assert.True(janela.Corte < torneio.DataInicio);
    }

    [Fact]
    public async Task Passados_os_7_dias_a_janela_FECHA()
    {
        // ⚠️ A decisão do prazo (Felipe delegou): 7 dias. Um selo que fica pra sempre deixa de
        // significar "olha o que ACABOU de acontecer" — e "+2" de um sábado de um mês atrás
        // não diz nada sobre hoje. Fora da janela a coluna some inteira da tela.
        var agora = new DateTime(2026, 8, 10, 20, 0, 0);
        var (ctx, _) = await MontarTorneioTerminadoAsync(agora.AddDays(-MovimentoNoRanking.DiasNaTela - 1));
        using var _d = ctx;

        Assert.Null(await MovimentoNoRanking.DoOficialAsync(ctx, agora));
    }

    [Fact]
    public async Task No_ultimo_dia_da_janela_o_selo_ainda_esta_la()
    {
        // A borda importa: sem isto, "7 dias" poderia virar 6 sem ninguém notar.
        var agora = new DateTime(2026, 8, 10, 20, 0, 0);
        var (ctx, _) = await MontarTorneioTerminadoAsync(agora.AddDays(-MovimentoNoRanking.DiasNaTela).AddMinutes(1));
        using var _d = ctx;

        Assert.NotNull(await MovimentoNoRanking.DoOficialAsync(ctx, agora));
    }

    [Fact]
    public async Task O_AMERICANO_nao_abre_a_janela_do_ranking_oficial()
    {
        // ⚠️ São dois rankings que não se somam desde 07/08/2026. O rodízio de sábado não pode
        // carimbar "movimento" no ranking de torneio de chave — daria a entender que a posição
        // de alguém mudou por causa de um evento que nem entra naquela conta.
        var agora = new DateTime(2026, 8, 10, 20, 0, 0);
        var (ctx, _) = await MontarTorneioTerminadoAsync(agora.AddDays(-1), FormatoDoTorneio.Americano);
        using var _d = ctx;

        Assert.Null(await MovimentoNoRanking.DoOficialAsync(ctx, agora));
    }

    [Fact]
    public async Task Torneio_RESTRITO_tambem_fica_de_fora()
    {
        // Mesma régua do ContaNoRanking: evento fechado não move ranking, então também não
        // pode anunciar movimento.
        var agora = new DateTime(2026, 8, 10, 20, 0, 0);
        var (ctx, torneio) = await MontarTorneioTerminadoAsync(agora.AddDays(-1));
        using var _d = ctx;
        torneio.Restrito = true;
        await ctx.SaveChangesAsync();

        Assert.Null(await MovimentoNoRanking.DoOficialAsync(ctx, agora));
    }

    [Fact]
    public async Task Torneio_em_ANDAMENTO_nao_abre_janela()
    {
        // Enquanto o torneio corre a posição ainda muda: um selo que aparece e se corrige
        // sozinho é pior do que um que demora.
        var agora = new DateTime(2026, 8, 10, 20, 0, 0);
        var (ctx, torneio) = await MontarTorneioTerminadoAsync(agora.AddHours(-2));
        using var _d = ctx;
        torneio.Status = "Fase de Grupos";
        await ctx.SaveChangesAsync();

        Assert.Null(await MovimentoNoRanking.DoOficialAsync(ctx, agora));
    }

    [Fact]
    public async Task A_janela_conta_do_ULTIMO_JOGO_e_nao_da_data_de_inicio()
    {
        // Num torneio de sexta a domingo, contar da largada erraria a validade em dois dias —
        // o selo sumiria antes de a segunda-feira chegar.
        var agora = new DateTime(2026, 8, 10, 20, 0, 0);
        var (ctx, torneio) = await MontarTorneioTerminadoAsync(agora.AddDays(-6));
        using var _d = ctx;
        torneio.DataInicio = agora.AddDays(-9);     // começou 3 dias antes de acabar
        await ctx.SaveChangesAsync();

        var janela = await MovimentoNoRanking.DoOficialAsync(ctx, agora);

        Assert.NotNull(janela);
        Assert.Equal(agora.AddDays(-6), janela!.TerminouEm);
    }
}
