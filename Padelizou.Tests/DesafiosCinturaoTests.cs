using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 🥊 O CINTURÃO (fase 3 do DESAFIOS.md).
//
// ⚠️ O que se guarda aqui é o que faz um cinturão morrer: o dono que some e continua campeão
// para sempre, a dupla que troca de parceiro e leva o cinturão junto, e a recusa antiga que
// derruba alguém que acabou de ganhar.
public class DesafiosCinturaoTests
{
    private const int Categoria = 1;

    private static Desafio Confirmado(int d1, int d2, int a1, int a2,
        int gamesDesafiante, int gamesDesafiado, DateTime quando, int categoriaId = Categoria) => new()
        {
            DesafianteJogador1Id = d1, DesafianteJogador2Id = d2,
            DesafiadoJogador1Id = a1, DesafiadoJogador2Id = a2,
            CategoriaPadraoId = categoriaId, ClubeId = 1,
            Formato = FormatoDoDesafio.UmSet,
            Status = Desafio.Confirmado,
            DataHora = quando.AddHours(-2), PropostoEm = quando.AddDays(-1),
            GamesDesafiante = gamesDesafiante, GamesDesafiado = gamesDesafiado,
            ConfirmadoEm = quando,
            PontosDesafiante = 11, PontosDesafiado = 1,
        };

    private static ReinadoNoCinturao Dono(int j1, int j2, DateTime desde, int defesas = 0) => new()
    {
        CategoriaPadraoId = Categoria,
        Jogador1Id = Math.Min(j1, j2), Jogador2Id = Math.Max(j1, j2),
        ComecouEm = desde, Defesas = defesas,
    };

    // ── Quem leva o cinturão ──────────────────────────────────────────────────────────

    [Fact]
    public void Cinturao_vago_vai_pro_vencedor_do_primeiro_desafio()
    {
        // Sem isto o cinturão nunca nasceria: não há dono pra ninguém tomar.
        var jogo = Confirmado(1, 2, 3, 4, 6, 3, new DateTime(2026, 8, 10));

        Assert.Equal(EfeitoNoCinturao.PegouVago, Cinturao.Efeito(dono: null, jogo));
        Assert.Equal((1, 2), Cinturao.DuplaVencedora(jogo, 1));
    }

    [Fact]
    public void Vencer_o_dono_toma_o_cinturao_venha_o_desafio_de_qual_lado_vier()
    {
        var dono = Dono(1, 2, new DateTime(2026, 8, 1));

        // O dono é o DESAFIADO e perde.
        var tomadaA = Confirmado(3, 4, 1, 2, 6, 2, new DateTime(2026, 8, 10));
        Assert.Equal(EfeitoNoCinturao.Tomou, Cinturao.Efeito(dono, tomadaA));

        // O dono é o DESAFIANTE e perde. Continua sendo tomada — o cinturão não protege quem
        // resolveu atacar.
        var tomadaB = Confirmado(1, 2, 3, 4, 2, 6, new DateTime(2026, 8, 11));
        Assert.Equal(EfeitoNoCinturao.Tomou, Cinturao.Efeito(dono, tomadaB));
    }

    [Fact]
    public void Dono_que_vence_defende_e_dois_estranhos_nao_mexem_em_nada()
    {
        var dono = Dono(1, 2, new DateTime(2026, 8, 1));

        Assert.Equal(EfeitoNoCinturao.Defendeu,
            Cinturao.Efeito(dono, Confirmado(1, 2, 3, 4, 6, 1, new DateTime(2026, 8, 10))));

        Assert.Equal(EfeitoNoCinturao.Nada,
            Cinturao.Efeito(dono, Confirmado(5, 6, 7, 8, 6, 1, new DateTime(2026, 8, 10))));
    }

    [Fact]
    public void Trocar_de_parceiro_nao_leva_o_cinturao_junto()
    {
        // ⚠️ É da DUPLA, não do jogador — é o que torna a dupla um personagem. O 1 tem o
        // cinturão com o 2; jogando com o 9, ele é um desafiante como outro qualquer.
        var dono = Dono(1, 2, new DateTime(2026, 8, 1));

        var comOutroParceiro = Confirmado(1, 9, 3, 4, 6, 1, new DateTime(2026, 8, 10));

        Assert.Equal(EfeitoNoCinturao.Nada, Cinturao.Efeito(dono, comOutroParceiro));
    }

    [Fact]
    public void Placar_empatado_ou_desafio_nao_confirmado_nao_mexe_no_cinturao()
    {
        var dono = Dono(1, 2, new DateTime(2026, 8, 1));

        var empate = Confirmado(3, 4, 1, 2, 6, 6, new DateTime(2026, 8, 10));
        Assert.Equal(EfeitoNoCinturao.Nada, Cinturao.Efeito(dono, empate));

        var emDisputa = Confirmado(3, 4, 1, 2, 6, 2, new DateTime(2026, 8, 10));
        emDisputa.Status = Desafio.EmDisputa;
        Assert.Equal(EfeitoNoCinturao.Nada, Cinturao.Efeito(dono, emDisputa));
    }

    // ── A defesa obrigatória ──────────────────────────────────────────────────────────

    private static Desafio Recusado(int desafiante1, int desafiante2, int dono1, int dono2,
        DateTime propostoEm, DateTime respondidoEm) => new()
        {
            DesafianteJogador1Id = desafiante1, DesafianteJogador2Id = desafiante2,
            DesafiadoJogador1Id = dono1, DesafiadoJogador2Id = dono2,
            CategoriaPadraoId = Categoria, ClubeId = 1,
            Formato = FormatoDoDesafio.UmSet,
            Status = Desafio.Recusado,
            DataHora = propostoEm.AddDays(2), PropostoEm = propostoEm, RespondidoEm = respondidoEm,
        };

    private static Desafio Ignorado(int desafiante1, int desafiante2, int dono1, int dono2,
        DateTime propostoEm) => new()
        {
            DesafianteJogador1Id = desafiante1, DesafianteJogador2Id = desafiante2,
            DesafiadoJogador1Id = dono1, DesafiadoJogador2Id = dono2,
            CategoriaPadraoId = Categoria, ClubeId = 1,
            Formato = FormatoDoDesafio.UmSet,
            Status = Desafio.Proposto,        // nunca respondido: morre nas 48h
            DataHora = propostoEm.AddDays(3), PropostoEm = propostoEm,
        };

    [Fact]
    public void Tres_recusas_em_catorze_dias_custam_o_cinturao()
    {
        var agora = new DateTime(2026, 8, 20, 12, 0, 0);
        var dono = Dono(1, 2, agora.AddDays(-30));

        var desafios = new[]
        {
            Recusado(3, 4, 1, 2, agora.AddDays(-10), agora.AddDays(-10)),
            Ignorado(5, 6, 1, 2, agora.AddDays(-6)),
            Recusado(7, 8, 1, 2, agora.AddDays(-2), agora.AddDays(-2)),
        };

        Assert.Equal(0, Cinturao.QuantasFaltamParaPerder(dono, desafios, agora));

        var herdeiro = Cinturao.QuemHerdaPorOmissao(dono, desafios, agora);

        // O PRIMEIRO que chamou e não foi atendido — quem tentou antes esperou mais.
        Assert.NotNull(herdeiro);
        Assert.Equal(3, herdeiro!.DesafianteJogador1Id);
    }

    [Fact]
    public void Duas_recusas_ainda_nao_custam_e_a_tela_sabe_quantas_faltam()
    {
        var agora = new DateTime(2026, 8, 20, 12, 0, 0);
        var dono = Dono(1, 2, agora.AddDays(-30));

        var desafios = new[]
        {
            Recusado(3, 4, 1, 2, agora.AddDays(-5), agora.AddDays(-5)),
            Recusado(5, 6, 1, 2, agora.AddDays(-3), agora.AddDays(-3)),
        };

        Assert.Equal(1, Cinturao.QuantasFaltamParaPerder(dono, desafios, agora));
        Assert.Null(Cinturao.QuemHerdaPorOmissao(dono, desafios, agora));
    }

    [Fact]
    public void Recusa_de_mais_de_catorze_dias_sai_da_conta_sozinha()
    {
        var agora = new DateTime(2026, 8, 20, 12, 0, 0);
        var dono = Dono(1, 2, agora.AddDays(-60));

        var desafios = new[]
        {
            Recusado(3, 4, 1, 2, agora.AddDays(-20), agora.AddDays(-20)),   // fora da janela
            Recusado(5, 6, 1, 2, agora.AddDays(-5), agora.AddDays(-5)),
            Recusado(7, 8, 1, 2, agora.AddDays(-3), agora.AddDays(-3)),
        };

        Assert.Equal(1, Cinturao.QuantasFaltamParaPerder(dono, desafios, agora));
        Assert.Null(Cinturao.QuemHerdaPorOmissao(dono, desafios, agora));
    }

    [Fact]
    public void Recusa_de_antes_do_reinado_nao_derruba_quem_acabou_de_ganhar()
    {
        // ⚠️ Recusar quando não se tinha o cinturão não é fuga de defesa: é uma dupla qualquer
        // dizendo que não pode jogar. Sem este corte, quem ganhasse o cinturão hoje poderia
        // perdê-lo amanhã por recusas de antes de ser dono.
        var agora = new DateTime(2026, 8, 20, 12, 0, 0);
        var dono = Dono(1, 2, agora.AddDays(-1));

        var desafios = new[]
        {
            Recusado(3, 4, 1, 2, agora.AddDays(-9), agora.AddDays(-9)),
            Recusado(5, 6, 1, 2, agora.AddDays(-7), agora.AddDays(-7)),
            Recusado(7, 8, 1, 2, agora.AddDays(-5), agora.AddDays(-5)),
        };

        Assert.Equal(Cinturao.NaoAtendidosQueCustamOCinturao,
            Cinturao.QuantasFaltamParaPerder(dono, desafios, agora));
        Assert.Null(Cinturao.QuemHerdaPorOmissao(dono, desafios, agora));
    }

    [Fact]
    public void Cancelar_um_jogo_ja_aceito_nao_custa_o_cinturao()
    {
        // Desmarcar tem motivo legítimo demais (chuva, lesão) pra custar um cinturão. A espec
        // fala em "recusou ou ignorou" — e é só isso que conta.
        var agora = new DateTime(2026, 8, 20, 12, 0, 0);
        var dono = Dono(1, 2, agora.AddDays(-30));

        var cancelados = Enumerable.Range(3, 3).Select(i =>
        {
            var d = Recusado(i * 10, i * 10 + 1, 1, 2, agora.AddDays(-i), agora.AddDays(-i));
            d.Status = Desafio.Cancelado;
            return d;
        }).ToList();

        Assert.Equal(Cinturao.NaoAtendidosQueCustamOCinturao,
            Cinturao.QuantasFaltamParaPerder(dono, cancelados, agora));
    }

    [Fact]
    public void Recusa_de_outra_categoria_nao_conta_contra_este_cinturao()
    {
        var agora = new DateTime(2026, 8, 20, 12, 0, 0);
        var dono = Dono(1, 2, agora.AddDays(-30));

        var desafios = new[]
        {
            Recusado(3, 4, 1, 2, agora.AddDays(-5), agora.AddDays(-5)),
            Recusado(5, 6, 1, 2, agora.AddDays(-4), agora.AddDays(-4)),
            Recusado(7, 8, 1, 2, agora.AddDays(-3), agora.AddDays(-3)),
        };
        desafios[2].CategoriaPadraoId = 99;

        Assert.Equal(1, Cinturao.QuantasFaltamParaPerder(dono, desafios, agora));
    }

    // ── No banco, pelo caminho de verdade ─────────────────────────────────────────────

    private static (DbPadelContext ctx, FechamentoDoDesafio fechamento) Cenario()
    {
        var ctx = TestInfra.NovoContexto();

        for (int i = 1; i <= 8; i++) ctx.Jogadores.Add(TestInfra.NovoJogador(i));
        ctx.CategoriasPadrao.Add(new CategoriaPadrao
        {
            Id = Categoria, Nome = "4ª Categoria Masculina", Codigo = "C4M", Tipo = "Masculina",
        });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Golden Point" });
        ctx.SaveChanges();

        var movimentacao = new MovimentacaoDoCinturao(ctx, Substitute.For<IPushNotificationService>());
        return (ctx, new FechamentoDoDesafio(ctx, movimentacao));
    }

    [Fact]
    public async Task Fechar_o_placar_move_o_cinturao_no_mesmo_caminho_do_ponto()
    {
        // ⚠️ O cinturão entra pelo FechamentoDoDesafio, que é o caminho único dos DOIS modos de
        // fechar (botão da outra dupla e relógio das 72h). No controller, ele ficaria parado em
        // todo desafio fechado pelo prazo — e o defeito seria mudo.
        var (ctx, fechamento) = Cenario();
        using var _ = ctx;
        var agora = DateTime.Now;

        var primeiro = Confirmado(1, 2, 3, 4, 6, 2, agora);
        primeiro.Status = Desafio.PlacarLancado;
        ctx.Desafios.Add(primeiro);
        await ctx.SaveChangesAsync();

        Assert.Equal(EfeitoNoCinturao.PegouVago, await fechamento.FecharAsync(primeiro, 3, agora));

        var dono = await ctx.ReinadosNoCinturao.SingleAsync(r => r.TerminouEm == null);
        Assert.Equal(1, dono.Jogador1Id);
        Assert.Equal(2, dono.Jogador2Id);
        Assert.Equal(0, dono.Defesas);
        Assert.Equal(primeiro.Id, dono.DesafioDeConquistaId);
    }

    [Fact]
    public async Task Defender_soma_e_perder_encerra_o_reinado_deixando_o_historico()
    {
        var (ctx, fechamento) = Cenario();
        using var _ = ctx;
        var agora = DateTime.Now;

        var conquista = Confirmado(1, 2, 3, 4, 6, 2, agora.AddDays(-3));
        conquista.Status = Desafio.PlacarLancado;
        ctx.Desafios.Add(conquista);
        await ctx.SaveChangesAsync();
        await fechamento.FecharAsync(conquista, 3, agora.AddDays(-3));

        var defesa = Confirmado(1, 2, 5, 6, 6, 4, agora.AddDays(-2));
        defesa.Status = Desafio.PlacarLancado;
        ctx.Desafios.Add(defesa);
        await ctx.SaveChangesAsync();
        Assert.Equal(EfeitoNoCinturao.Defendeu, await fechamento.FecharAsync(defesa, 5, agora.AddDays(-2)));
        Assert.Equal(1, (await ctx.ReinadosNoCinturao.SingleAsync(r => r.TerminouEm == null)).Defesas);

        var tomada = Confirmado(7, 8, 1, 2, 6, 1, agora);
        tomada.Status = Desafio.PlacarLancado;
        ctx.Desafios.Add(tomada);
        await ctx.SaveChangesAsync();
        Assert.Equal(EfeitoNoCinturao.Tomou, await fechamento.FecharAsync(tomada, 1, agora));

        // Um dono atual, e o reinado antigo virou histórico — sem apagar nada.
        var novoDono = await ctx.ReinadosNoCinturao.SingleAsync(r => r.TerminouEm == null);
        Assert.Equal(7, novoDono.Jogador1Id);

        var antigo = await ctx.ReinadosNoCinturao.SingleAsync(r => r.TerminouEm != null);
        Assert.Equal(1, antigo.Jogador1Id);
        Assert.Equal(1, antigo.Defesas);
        Assert.Equal(ReinadoNoCinturao.PerdeuNaQuadra, antigo.ComoTerminou);
        Assert.Equal(tomada.Id, antigo.DesafioDaPerdaId);
    }

    [Fact]
    public async Task Nunca_existe_mais_de_um_dono_na_mesma_categoria()
    {
        var (ctx, fechamento) = Cenario();
        using var _ = ctx;
        var agora = DateTime.Now;

        // Quatro trocas seguidas de mão.
        var pares = new[] { (1, 2, 3, 4), (5, 6, 1, 2), (7, 8, 5, 6), (3, 4, 7, 8) };
        var quando = agora.AddDays(-10);

        foreach (var (v1, v2, p1, p2) in pares)
        {
            var jogo = Confirmado(v1, v2, p1, p2, 6, 1, quando);
            jogo.Status = Desafio.PlacarLancado;
            ctx.Desafios.Add(jogo);
            await ctx.SaveChangesAsync();
            await fechamento.FecharAsync(jogo, p1, quando);
            quando = quando.AddDays(1);
        }

        Assert.Equal(1, await ctx.ReinadosNoCinturao.CountAsync(r => r.TerminouEm == null));
        Assert.Equal(4, await ctx.ReinadosNoCinturao.CountAsync());
        Assert.Equal(3, (await ctx.ReinadosNoCinturao.SingleAsync(r => r.TerminouEm == null)).Jogador1Id);
    }

    [Fact]
    public async Task O_vigia_transfere_o_cinturao_de_quem_nao_defende()
    {
        var (ctx, _fechamento) = Cenario();
        using var _ = ctx;
        var agora = DateTime.Now;

        ctx.ReinadosNoCinturao.Add(Dono(1, 2, agora.AddDays(-30)));
        ctx.Desafios.AddRange(
            Recusado(3, 4, 1, 2, agora.AddDays(-10), agora.AddDays(-10)),
            Ignorado(5, 6, 1, 2, agora.AddDays(-6)),
            Recusado(7, 8, 1, 2, agora.AddDays(-2), agora.AddDays(-2)));
        await ctx.SaveChangesAsync();

        var movimentacao = new MovimentacaoDoCinturao(ctx, Substitute.For<IPushNotificationService>());
        var dono = await ctx.ReinadosNoCinturao.SingleAsync(r => r.TerminouEm == null);

        var novo = await movimentacao.TransferirPorOmissaoAsync(dono, agora);

        Assert.NotNull(novo);
        // Herda o PRIMEIRO que chamou e não foi atendido.
        Assert.Equal(3, novo!.Jogador1Id);
        Assert.Equal(4, novo.Jogador2Id);
        // E não foi ganho na quadra: não há desafio de conquista.
        Assert.Null(novo.DesafioDeConquistaId);

        var antigo = await ctx.ReinadosNoCinturao.SingleAsync(r => r.TerminouEm != null);
        Assert.Equal(ReinadoNoCinturao.PerdeuPorNaoDefender, antigo.ComoTerminou);

        // E rodar de novo não tira o cinturão de quem acabou de ganhar.
        Assert.Null(await movimentacao.TransferirPorOmissaoAsync(novo, agora));
        Assert.Equal(1, await ctx.ReinadosNoCinturao.CountAsync(r => r.TerminouEm == null));
    }
}
