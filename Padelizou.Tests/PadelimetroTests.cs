using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A matemática do Padelímetro (RANKING.md) — motor puro, sem banco.
public class PadelimetroMotorTests
{
    [Fact]
    public void Duplas_iguais_tem_expectativa_meio_a_meio()
    {
        Assert.Equal(0.5, Padelimetro.Expectativa(500, 500), precision: 10);
    }

    [Theory]
    [InlineData(100, 0.64)] // 100 pontos de diferença ≈ 64% (a promessa da espec)
    [InlineData(200, 0.76)]
    public void Diferenca_de_nivel_vira_favoritismo(int diferenca, double esperado)
    {
        Assert.Equal(esperado, Padelimetro.Expectativa(500 + diferenca, 500), precision: 2);
    }

    [Theory]
    [InlineData(6, 0, 1.6)]  // passeio
    [InlineData(7, 6, 1.1)]  // no detalhe
    [InlineData(9, 0, 1.6)]  // teto: 9x0 de W.O. disfarçado não explode a conta
    [InlineData(4, 4, 1.0)]  // empate em games não amplifica
    public void Fator_de_games_premia_o_passeio_com_teto(int g1, int g2, double esperado)
    {
        Assert.Equal(esperado, Padelimetro.FatorDeGames(g1, g2), precision: 10);
    }

    [Fact]
    public void K_dobrado_ate_o_decimo_jogo_depois_estavel()
    {
        Assert.Equal(40, Padelimetro.K(0));
        Assert.Equal(40, Padelimetro.K(9));
        Assert.Equal(20, Padelimetro.K(10));
        Assert.Equal(20, Padelimetro.K(500));

        Assert.True(Padelimetro.EmCalibracao(9));
        Assert.False(Padelimetro.EmCalibracao(10));
    }

    [Fact]
    public void Vitoria_esperada_move_pouco_zebra_move_muito()
    {
        // Favorito de 64% ganhou: sobe pouco. Azarão de 36% ganhou: sobe muito.
        int doFavorito = Padelimetro.Variacao(20, 1.0, venceu: true, expectativaDoTime: 0.64);
        int daZebra = Padelimetro.Variacao(20, 1.0, venceu: true, expectativaDoTime: 0.36);

        Assert.Equal(7, doFavorito);   // 20 × 0,36 = 7,2 → 7
        Assert.Equal(13, daZebra);     // 20 × 0,64 = 12,8 → 13
        Assert.True(daZebra > doFavorito);
    }

    [Fact]
    public void Quem_se_arrisca_pra_cima_perde_pouco_e_ganha_muito()
    {
        // A promessa feita ao Felipe: jogador da 5ª (500) contra dupla da 4ª (600).
        double expectativa = Padelimetro.Expectativa(500, 600); // ≈ 0,36

        int derrota = Padelimetro.Variacao(20, 1.0, venceu: false, expectativaDoTime: expectativa);
        int vitoria = Padelimetro.Variacao(20, 1.0, venceu: true, expectativaDoTime: expectativa);

        Assert.Equal(-7, derrota);  // derrota esperada quase não desce
        Assert.Equal(13, vitoria);  // zebra paga bem
    }

    [Fact]
    public void Nivel_da_dupla_e_a_media_e_o_clamp_segura_as_pontas()
    {
        Assert.Equal(550.0, Padelimetro.NivelDaDupla(500, 600), precision: 10);
        Assert.Equal(0, Padelimetro.Acomodar(-5));
        Assert.Equal(1000, Padelimetro.Acomodar(1200));
    }
}

// O mapa categoria → régua (faixas, entradas e a soma da dupla).
public class FaixasDePadelimetroTests
{
    [Theory]
    [InlineData("Categoria Open Masculino", "Open", 850, 1000, 900)]
    [InlineData("2ª Categoria Masculina", "2ª", 750, 849, 800)]
    [InlineData("4ª Categoria Masculina", "4ª", 550, 649, 600)]
    [InlineData("7ª Categoria Masculina", "7ª", 0, 349, 300)]
    [InlineData("Categoria Iniciantes Masculina", "7ª", 0, 349, 300)]
    public void Escada_masculina_por_nome(string nome, string rotulo, int piso, int teto, int entrada)
    {
        var faixa = FaixasDePadelimetro.DaCategoria(nome);

        Assert.NotNull(faixa);
        Assert.Equal(rotulo, faixa!.Rotulo);
        Assert.Equal("masculina", faixa.Escada);
        Assert.Equal(piso, faixa.Piso);
        Assert.Equal(teto, faixa.Teto);
        Assert.Equal(entrada, faixa.Entrada);
    }

    [Theory]
    [InlineData("2ª Categoria Feminina", "2ª", 450, 549, 500)]
    [InlineData("5ª Categoria Feminina", "5ª", 150, 249, 200)]
    [InlineData("Categoria Open Feminina", "Open", 550, 1000, 600)]
    public void Escada_feminina_fica_mais_abaixo_na_MESMA_regua(string nome, string rotulo, int piso, int teto, int entrada)
    {
        var faixa = FaixasDePadelimetro.DaCategoria(nome);

        Assert.NotNull(faixa);
        Assert.Equal(rotulo, faixa!.Rotulo);
        Assert.Equal("feminina", faixa.Escada);
        Assert.Equal(piso, faixa.Piso);
        Assert.Equal(teto, faixa.Teto);
        Assert.Equal(entrada, faixa.Entrada);
    }

    [Fact]
    public void Soma_da_dupla_e_dois_tetos_menos_cinquenta()
    {
        // 4ª masculina: teto 649 → soma 1250. Dois 630 (1260) não cabem juntos nela.
        Assert.Equal(1250, FaixasDePadelimetro.DaCategoria("4ª Categoria Masculina")!.SomaDaDupla);
        // Open e 7ª não precisam de teto de soma.
        Assert.Null(FaixasDePadelimetro.DaCategoria("Categoria Open Masculino")!.SomaDaDupla);
        Assert.Null(FaixasDePadelimetro.DaCategoria("7ª Categoria Masculina")!.SomaDaDupla);
    }

    [Fact]
    public void Mista_nao_tem_faixa_mas_tem_entrada()
    {
        Assert.Null(FaixasDePadelimetro.DaCategoria("Categoria Mista B"));
        Assert.Equal(550, FaixasDePadelimetro.Entrada("Categoria Mista A"));
        Assert.Equal(450, FaixasDePadelimetro.Entrada("Categoria Mista B"));
        Assert.Equal(250, FaixasDePadelimetro.Entrada("Categoria Mista D"));
    }

    [Fact]
    public void Categoria_fora_da_convencao_entra_neutra_no_meio_da_regua()
    {
        Assert.Null(FaixasDePadelimetro.DaCategoria("Categoria 45+"));
        Assert.Equal(500, FaixasDePadelimetro.Entrada("Categoria 45+"));
        Assert.Equal(500, FaixasDePadelimetro.Entrada(null));
    }

    [Fact]
    public void Do_nivel_pra_faixa_e_quanto_falta_pra_subir()
    {
        var faixa = FaixasDePadelimetro.DoNivel(620, feminina: false);
        Assert.Equal("4ª", faixa.Rotulo);

        // Teto da 4ª é 649: com 620, faltam 30 pra cruzar.
        Assert.Equal(30, FaixasDePadelimetro.FaltaPraSubir(620, feminina: false));

        // No topo da escada não existe "subir".
        Assert.Null(FaixasDePadelimetro.FaltaPraSubir(900, feminina: false));

        // O MESMO 500 é 5ª na régua masculina e 2ª na feminina — escala única, faixas diferentes.
        Assert.Equal("5ª", FaixasDePadelimetro.DoNivel(500, feminina: false).Rotulo);
        Assert.Equal("2ª", FaixasDePadelimetro.DoNivel(500, feminina: true).Rotulo);
    }
}

// O serviço: o que conta, o que não conta, seed, idempotência e replay.
public class PadelimetroServiceTests
{
    // Partida finalizada pronta pra contar: 4 jogadores novatos, categoria informada.
    private static (Partida partida, Jogador[] time1, Jogador[] time2) MontarPartidaFinalizada(
        DbPadelContext ctx, string nomeCategoria = "5ª Categoria Masculina",
        bool restrito = false, int games1 = 6, int games2 = 2, int sufixo = 0)
    {
        var torneio = new Torneio
        {
            Nome = $"Torneio {sufixo}",
            Codigo = $"TRN{sufixo:00}",
            Status = "Em Andamento",
            Restrito = restrito,
            DataInicio = new DateTime(2026, 7, 1).AddDays(sufixo),
        };
        var categoria = new Categoria { Nome = nomeCategoria, Codigo = $"C{sufixo:00}", Torneio = torneio };

        var jogadores = Enumerable.Range(1, 4)
            .Select(i => new Jogador { Nome = $"J{sufixo}-{i}", Cpf = $"888{sufixo:000}000{i:02}" })
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
            Codigo = $"P{sufixo:000}",
            Status = "Finalizada",
            Fase = "Fase de Grupos",
            GamesDupla1 = games1,
            GamesDupla2 = games2,
            SetsDupla1 = games1 > games2 ? 1 : 0,
            SetsDupla2 = games2 > games1 ? 1 : 0,
            VencedorId = games1 > games2 ? d1.Id : d2.Id,
            PlacarMarcadoEm = new DateTime(2026, 7, 1).AddDays(sufixo).AddHours(10),
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();

        return (partida, new[] { jogadores[0], jogadores[1] }, new[] { jogadores[2], jogadores[3] });
    }

    [Fact]
    public async Task Novatos_nascem_pela_categoria_e_o_placar_move_os_quatro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, time1, time2) = MontarPartidaFinalizada(ctx, games1: 6, games2: 2);

        await new PadelimetroService(ctx).AplicarAsync(partida.Id);

        // Todos estrearam pela 5ª masculina (entrada 500), expectativa meio a meio,
        // K de calibração 40, fator do 6x2 = 1,4: delta = 40 × 1,4 × 0,5 = 28.
        Assert.All(time1, j => Assert.Equal(528, j.Padelimetro));
        Assert.All(time2, j => Assert.Equal(472, j.Padelimetro));
        Assert.All(time1.Concat(time2), j => Assert.Equal(1, j.JogosDePadelimetro));

        // Extrato: 4 nascimentos + 4 movimentos, com motivo legível.
        Assert.Equal(4, ctx.HistoricosDePadelimetro.Count(h => h.PartidaId == null));
        Assert.Equal(4, ctx.HistoricosDePadelimetro.Count(h => h.PartidaId == partida.Id));
        var doVencedor = ctx.HistoricosDePadelimetro
            .Single(h => h.JogadorId == time1[0].Id && h.PartidaId != null);
        Assert.Contains("Vitória 6x2", doVencedor.Motivo);
        Assert.Equal(500, doVencedor.NivelAntes);
        Assert.Equal(28, doVencedor.Delta);
    }

    [Fact]
    public async Task Torneio_restrito_nao_move_o_padelimetro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, time1, time2) = MontarPartidaFinalizada(ctx, restrito: true);

        await new PadelimetroService(ctx).AplicarAsync(partida.Id);

        Assert.All(time1.Concat(time2), j => Assert.Null(j.Padelimetro));
        Assert.Empty(ctx.HistoricosDePadelimetro);
    }

    [Fact]
    public async Task Partida_de_times_nao_move_ninguem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, time1, time2) = MontarPartidaFinalizada(ctx);

        // Vira jogo de TIMES: o Jogador1Id da dupla-time é o organizador, não atleta.
        var dupla = ctx.Duplas.Find(partida.Dupla1Id)!;
        dupla.NomeTime = "Los Corneteiros";
        ctx.SaveChanges();

        await new PadelimetroService(ctx).AplicarAsync(partida.Id);

        Assert.All(time1.Concat(time2), j => Assert.Null(j.Padelimetro));
        Assert.Empty(ctx.HistoricosDePadelimetro);
    }

    [Fact]
    public async Task Aplicar_duas_vezes_nao_conta_o_jogo_em_dobro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, time1, _) = MontarPartidaFinalizada(ctx);
        var servico = new PadelimetroService(ctx);

        await servico.AplicarAsync(partida.Id);
        await servico.AplicarAsync(partida.Id); // a fila offline da Mesa reentrega

        Assert.Equal(528, time1[0].Padelimetro);
        Assert.Equal(1, time1[0].JogosDePadelimetro);
        Assert.Equal(4, ctx.HistoricosDePadelimetro.Count(h => h.PartidaId == partida.Id));
    }

    [Fact]
    public async Task Estreia_por_categoria_mais_forte_nasce_mais_alto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, time1, time2) = MontarPartidaFinalizada(ctx, nomeCategoria: "2ª Categoria Masculina");

        await new PadelimetroService(ctx).AplicarAsync(partida.Id);

        // Entrada da 2ª masculina é 800 — o 6x2 move a partir dela.
        Assert.All(time1, j => Assert.Equal(828, j.Padelimetro));
        Assert.All(time2, j => Assert.Equal(772, j.Padelimetro));
    }

    [Fact]
    public async Task Replay_reconstroi_o_mesmo_resultado_do_ao_vivo()
    {
        using var ctx = TestInfra.NovoContexto();
        var servico = new PadelimetroService(ctx);

        var (p1, time1, _) = MontarPartidaFinalizada(ctx, sufixo: 1);
        var (p2, _, _) = MontarPartidaFinalizada(ctx, sufixo: 2, games1: 3, games2: 6);

        // Ao vivo, na ordem dos jogos.
        await servico.AplicarAsync(p1.Id);
        await servico.AplicarAsync(p2.Id);
        var aoVivo = ctx.Jogadores.Where(j => j.Padelimetro != null)
            .OrderBy(j => j.Id)
            .Select(j => new { j.Id, j.Padelimetro, j.JogosDePadelimetro })
            .ToList();

        // Replay do zero: mesmos números, e rodar de novo dá no mesmo lugar.
        var contadas = await servico.RecalcularTudoAsync();
        Assert.Equal(2, contadas);

        var doReplay = ctx.Jogadores.Where(j => j.Padelimetro != null)
            .OrderBy(j => j.Id)
            .Select(j => new { j.Id, j.Padelimetro, j.JogosDePadelimetro })
            .ToList();
        Assert.Equal(aoVivo, doReplay);

        // No replay o extrato é datado pela PARTIDA, não por "agora".
        var linhaDeJogo = ctx.HistoricosDePadelimetro.First(h => h.PartidaId == p1.Id);
        Assert.Equal(p1.PlacarMarcadoEm, linhaDeJogo.CriadoEm);
    }

    [Fact]
    public async Task Lista_do_ranking_ordena_por_pdz_e_rotula_a_faixa_pela_escada_certa()
    {
        using var ctx = TestInfra.NovoContexto();
        var servico = new PadelimetroService(ctx);

        // Um jogo masculino (5ª, entrada 500) e um feminino (2ª fem, entrada 500 também —
        // escala única!): os números empatam, mas as faixas saem de escadas diferentes.
        var (pMasc, timeM1, _) = MontarPartidaFinalizada(ctx, "5ª Categoria Masculina", sufixo: 1);
        var (pFem, timeF1, _) = MontarPartidaFinalizada(ctx, "2ª Categoria Feminina", sufixo: 2);
        await servico.AplicarAsync(pMasc.Id);
        await servico.AplicarAsync(pFem.Id);

        var lista = await servico.ListarRankingAsync(null);

        Assert.Equal(8, lista.Count);
        // Ordena do maior pro menor, e todo mundo em calibração aparece marcado.
        Assert.Equal(lista.Select(l => l.Pdz).OrderByDescending(p => p), lista.Select(l => l.Pdz));
        Assert.All(lista, l => Assert.True(l.EmCalibracao));

        var vencedorMasc = lista.Single(l => l.Jogador.Id == timeM1[0].Id);
        Assert.Equal(528, vencedorMasc.Pdz);
        Assert.Equal("5ª", vencedorMasc.FaixaRotulo);
        Assert.Equal("masculina", vencedorMasc.FaixaEscada);

        // O MESMO número na escada feminina rotula outra faixa — escala única, faixas próprias.
        var vencedoraFem = lista.Single(l => l.Jogador.Id == timeF1[0].Id);
        Assert.Equal(528, vencedoraFem.Pdz);
        Assert.Equal("2ª", vencedoraFem.FaixaRotulo);
        Assert.Equal("feminina", vencedoraFem.FaixaEscada);

        // O filtro regional corta a lista (mesmo contrato do hub: conjunto de ids).
        var soUmaPessoa = await servico.ListarRankingAsync(new HashSet<int> { timeM1[0].Id });
        Assert.Single(soUmaPessoa);
        Assert.Equal(timeM1[0].Id, soUmaPessoa[0].Jogador.Id);
    }

    [Fact]
    public async Task Finalizar_pela_mesa_move_o_padelimetro_de_verdade()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Em Andamento");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var partida = new Partida
        {
            CategoriaId = categoria.Id,
            TorneioId = torneio.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Codigo = "MESA01",
            Status = "Em Andamento",
            Fase = "Fase de Grupos",
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await TestInfra.FinalizarComPlacarAsync(ctx, controller, partida, 6, 4);

        // O gancho do FinalizarPartida aplicou: 2ª masculina (entrada 800), 6x4 → fator
        // 1,2, K 40 → delta 24.
        var vencedores = new[] { duplas[0].Jogador1Id, duplas[0].Jogador2Id!.Value };
        foreach (var id in vencedores)
            Assert.Equal(824, ctx.Jogadores.Find(id)!.Padelimetro);
    }
}
