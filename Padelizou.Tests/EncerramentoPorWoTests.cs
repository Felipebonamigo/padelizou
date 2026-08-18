using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// W.O. — O JOGO QUE ACABOU SEM SER JOGADO.
//
// Até 18/08/2026 não existia: quem não tinha adversário em quadra digitava 6x0 e seguia. O
// buraco não estava na chave (essa funcionava) — estava no PADELÍMETRO, que mede como a
// pessoa joga e recebia um jogo que não aconteceu.
//
// ⚠️ O QUE ESTES TESTES PROTEGEM, e que é fácil desfazer sem perceber: o W.O. **grava
// placar de verdade**. É contraintuitivo, e a "limpeza" óbvia (deixar games nulos, afinal
// ninguém jogou) quebraria a classificação de grupos em silêncio — `QuemVenceu` lê o placar,
// e 0x0 é empate. O par de testes `Wo_da_a_vitoria...` + `Wo_conta_vitoria_na_tabela...`
// existe pra que essa simplificação não passe.
public class EncerramentoPorWoTests
{
    private static (Partida partida, Dupla d1, Dupla d2, Jogador[] todos) Montar(
        DbPadelContext ctx, string fase = "Fase de Grupos", int gamesDaFase = 6)
    {
        var torneio = new Torneio
        {
            Nome = "Torneio do W.O.",
            Codigo = "TWO1",
            Status = "Em Andamento",
            DataInicio = new DateTime(2026, 8, 18),
            GamesFaseGrupos = gamesDaFase,
            SetsFaseGrupos = 1,
            GamesFaseMataMata = gamesDaFase,
            SetsFaseMataMata = 1,
            GamesFaseFinal = gamesDaFase,
            SetsFaseFinal = 1,
        };
        var categoria = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "C01", Torneio = torneio };

        var jogadores = Enumerable.Range(1, 4)
            .Select(i => new Jogador { Nome = $"Jogador {i}", Cpf = $"7770000000{i}" })
            .ToArray();
        ctx.Jogadores.AddRange(jogadores);

        var d1 = new Dupla { Categoria = categoria, Jogador1 = jogadores[0], Jogador2 = jogadores[1] };
        var d2 = new Dupla { Categoria = categoria, Jogador1 = jogadores[2], Jogador2 = jogadores[3] };
        ctx.Duplas.AddRange(d1, d2);
        ctx.SaveChanges();

        var partida = new Partida
        {
            CategoriaId = categoria.Id,
            TorneioId = torneio.Id,
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            Codigo = "J001",
            Status = "Agendada",
            Fase = fase,
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();

        return (partida, d1, d2, jogadores);
    }

    private static FormatoDaPartida.Formato FormatoDe(DbPadelContext ctx, Partida p)
    {
        var categoria = ctx.Categorias.Find(p.CategoriaId)!;
        var torneio = ctx.Torneios.Find(categoria.TorneioId);
        return FormatoDaPartida.De(torneio, p.Fase);
    }

    [Fact]
    public void Wo_da_a_vitoria_a_quem_compareceu_com_o_placar_da_fase()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, d1, d2, _) = Montar(ctx, gamesDaFase: 6);

        EncerramentoPorWo.Registrar(partida, d1.Id, FormatoDe(ctx, partida));

        Assert.Equal(d2.Id, partida.VencedorId);
        Assert.Equal(0, partida.GamesDupla1);
        Assert.Equal(6, partida.GamesDupla2);
        Assert.Equal("Finalizada", partida.Status);
        Assert.True(EncerramentoPorWo.Foi(partida));
    }

    // O placar do W.O. segue o formato da FASE, não um 6x0 cravado: torneio configurado com
    // jogos até 9 anota W.O. 9x0. Cravar 6 aqui seria a segunda cópia da régua de formato,
    // exatamente o que FormatoDaPartida existe pra impedir.
    [Fact]
    public void O_placar_do_wo_respeita_o_formato_do_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, d2, _) = Montar(ctx, gamesDaFase: 9);

        EncerramentoPorWo.Registrar(partida, d2.Id, FormatoDe(ctx, partida));

        Assert.Equal(9, partida.GamesDupla1);
        Assert.Equal(0, partida.GamesDupla2);
    }

    // ⚠️ `QuemVenceu` olha SETS PRIMEIRO. Um set velho, de um placar que existia antes,
    // apontaria o vencedor contrário ao do W.O. — foi assim que uma final saiu com o campeão
    // errado no Interno de 05/08/2026.
    [Fact]
    public void Set_velho_nao_sobrevive_ao_wo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, d1, d2, _) = Montar(ctx);
        partida.SetsDupla1 = 1;   // a dupla 1 "vencia" por sets
        partida.SetsDupla2 = 0;

        EncerramentoPorWo.Registrar(partida, d1.Id, FormatoDe(ctx, partida));

        Assert.Equal(d2.Id, QuemVenceu.Da(partida));
        Assert.Equal(d2.Id, partida.VencedorId);
    }

    // ── O CORAÇÃO: o W.O. não move o nível de ninguém ────────────────────────────────────
    [Fact]
    public async Task Wo_nao_mexe_no_padelimetro_de_ninguem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, d1, _, todos) = Montar(ctx);

        EncerramentoPorWo.Registrar(partida, d1.Id, FormatoDe(ctx, partida));
        await ctx.SaveChangesAsync();

        await new PadelimetroService(ctx).AplicarAsync(partida.Id);

        Assert.All(todos, j => Assert.Null(j.Padelimetro));
        Assert.Empty(ctx.HistoricosDePadelimetro);
    }

    // ⚠️ O CONTRASTE QUE PROVA QUE É A MARCA, E NÃO O PLACAR. Mesmo 6x0, mesma fase, mesmas
    // pessoas: sem a marca de W.O. o jogo move o nível dos quatro. Sem este teste, um W.O.
    // que parasse de mover por qualquer outro motivo (placar nulo, filtro errado) passaria
    // por conserto funcionando.
    [Fact]
    public async Task O_mesmo_placar_jogado_de_verdade_move_o_padelimetro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, d1, d2, todos) = Montar(ctx);

        EncerramentoPorWo.Registrar(partida, d1.Id, FormatoDe(ctx, partida));
        EncerramentoPorWo.Limpar(partida);   // o jogo aconteceu; só a marca sai
        await ctx.SaveChangesAsync();

        await new PadelimetroService(ctx).AplicarAsync(partida.Id);

        Assert.All(todos, j => Assert.NotNull(j.Padelimetro));
        Assert.Equal(4, ctx.HistoricosDePadelimetro.Count(h => h.PartidaId == partida.Id));
    }

    // O W.O. tem que valer na TABELA DO GRUPO — é o teste que quebra se alguém "simplificar"
    // o placar para nulo achando que jogo não jogado não tem games.
    [Fact]
    public void Wo_conta_vitoria_na_tabela_do_grupo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, d1, d2, _) = Montar(ctx);
        d1.Grupo = "A";
        d2.Grupo = "A";

        EncerramentoPorWo.Registrar(partida, d1.Id, FormatoDe(ctx, partida));
        ctx.SaveChanges();

        var tabela = ClassificacaoDeGrupos.Ordenar(new[] { d1, d2 }, new[] { partida });

        Assert.Equal(d2.Id, tabela[0].Dupla.Id);
        Assert.Equal(1, tabela[0].Vitorias);
        Assert.Equal(1, tabela[1].Derrotas);
    }

    // Corrigir o placar depois desfaz o W.O. — senão o jogo ficaria fora do Padelímetro pra
    // sempre, com placar de verdade e sem nada na tela explicando por quê.
    [Fact]
    public void Corrigir_o_placar_tira_a_marca_de_wo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, d1, _, _) = Montar(ctx);

        EncerramentoPorWo.Registrar(partida, d1.Id, FormatoDe(ctx, partida));
        Assert.True(EncerramentoPorWo.Foi(partida));

        EncerramentoPorWo.Limpar(partida);

        Assert.False(EncerramentoPorWo.Foi(partida));
        Assert.Null(partida.MotivoDoEncerramento);
    }

    [Fact]
    public void Dupla_de_fora_do_jogo_nao_pode_levar_wo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, _, _) = Montar(ctx);

        var motivo = EncerramentoPorWo.MotivoParaNaoRegistrar(partida, duplaQueNaoCompareceuId: 9999);

        Assert.NotNull(motivo);
        Assert.Contains("não é deste jogo", motivo);
    }

    [Fact]
    public void Jogo_normal_nao_e_wo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, _, _) = Montar(ctx);

        Assert.False(EncerramentoPorWo.Foi(partida));
        Assert.Null(EncerramentoPorWo.MotivoParaNaoRegistrar(partida, partida.Dupla1Id));
    }
}
