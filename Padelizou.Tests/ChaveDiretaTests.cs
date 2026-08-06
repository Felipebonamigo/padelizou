using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Mata-mata puro, sem fase de grupos — e o caso que o motivou: 24 duplas, que NÃO fecham
// quadro. O risco real dessa feature é o bye: quem passa direto não venceu nada, então não
// está na lista de vencedores, e um erro aqui elimina do torneio 8 duplas que nunca perderam.
public class ChaveDiretaTests
{
    // ---- O quadro e os byes ----

    [Fact]
    public void Vinte_e_quatro_duplas_viram_quadro_de_32_com_oito_byes_e_oito_jogos()
    {
        var duplas = Enumerable.Range(1, 24).ToList();

        var rodada = ChaveamentoMataMata.MontarChaveDireta(duplas);

        Assert.Equal(ChaveamentoMataMata.PrimeiraRodada, rodada.Fase);
        Assert.Equal(8, rodada.Confrontos.Count);
        Assert.Equal(8, rodada.Byes.Count);

        // Toda dupla aparece EXATAMENTE uma vez: ou joga, ou passa direto. Ninguém em dois
        // lugares (jogaria duas vezes na mesma rodada) e ninguém esquecido (sumiria da chave).
        var citadas = rodada.Confrontos
            .SelectMany(c => new[] { c.Dupla1Id, c.Dupla2Id })
            .Concat(rodada.Byes)
            .ToList();
        Assert.Equal(24, citadas.Count);
        Assert.Equal(24, citadas.Distinct().Count());
    }

    [Fact]
    public void Quadro_cheio_nao_tem_bye_nenhum()
    {
        var rodada = ChaveamentoMataMata.MontarChaveDireta(Enumerable.Range(1, 16).ToList());

        Assert.Equal("Oitavas de Final", rodada.Fase);
        Assert.Equal(8, rodada.Confrontos.Count);
        Assert.Empty(rodada.Byes);
    }

    [Theory]
    // duplas, jogos na 1ª rodada, byes — a conta é sempre "quadro = menor potência de 2 que cabe"
    [InlineData(24, 8, 8)]
    [InlineData(20, 4, 12)]
    [InlineData(12, 4, 4)]
    [InlineData(6, 2, 2)]
    [InlineData(3, 1, 1)]
    [InlineData(2, 1, 0)]
    public void A_conta_do_quadro_fecha_pra_qualquer_numero_de_duplas(int quantas, int jogos, int byes)
    {
        var rodada = ChaveamentoMataMata.MontarChaveDireta(Enumerable.Range(1, quantas).ToList());

        Assert.Equal(jogos, rodada.Confrontos.Count);
        Assert.Equal(byes, rodada.Byes.Count);

        // Quem sobrevive à primeira rodada sempre fecha uma potência de 2 — é isso que faz a
        // chave terminar em final, e não em três duplas se olhando.
        int sobrevivem = rodada.Confrontos.Count + rodada.Byes.Count;
        Assert.Equal(ChaveamentoMataMata.MenorPotenciaDe2APartirDe(sobrevivem), sobrevivem);
    }

    [Fact]
    public void Chave_direta_recusa_menos_de_duas_e_mais_que_o_teto()
    {
        Assert.NotNull(ChaveamentoMataMata.ProblemaNaChaveDireta(1));
        Assert.NotNull(ChaveamentoMataMata.ProblemaNaChaveDireta(33));
        Assert.Null(ChaveamentoMataMata.ProblemaNaChaveDireta(24));
        Assert.Null(ChaveamentoMataMata.ProblemaNaChaveDireta(2));
    }

    // ---- A ponte: vencedores + byes ----

    [Fact]
    public async Task Fechada_a_primeira_rodada_os_byes_entram_junto_com_os_vencedores()
    {
        using var ctx = TestInfra.NovoContexto();
        var categoria = await MontarChaveDiretaDe24Async(ctx);

        // Os 8 jogos terminam; vence sempre o Dupla1.
        foreach (var partida in ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToList())
        {
            partida.Status = "Finalizada";
            partida.VencedorId = partida.Dupla1Id;
        }
        await ctx.SaveChangesAsync();

        var avancam = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, ChaveamentoMataMata.PrimeiraRodada);

        // 8 vencedores + 8 byes = 16, que é o quadro das Oitavas.
        Assert.Equal(16, avancam.Count);
        Assert.Equal(16, avancam.Distinct().Count());
        Assert.Equal("Oitavas de Final", ChaveamentoMataMata.NomeFase(avancam.Count));

        // Os vencedores vêm ANTES dos byes: é o que faz cada vencedor cruzar com uma dupla
        // que passou direto, em vez de vencedor x vencedor (que deixaria os byes se
        // enfrentando entre si do outro lado da chave).
        var pares = ChaveamentoMataMata.ParearVencedores(avancam);
        var byes = avancam.Skip(8).ToHashSet();
        Assert.All(pares, p =>
            Assert.True(byes.Contains(p.Dupla1Id) ^ byes.Contains(p.Dupla2Id),
                "todo jogo das Oitavas devia ser um vencedor contra um bye"));
    }

    [Fact]
    public async Task Com_jogo_pendente_ninguem_avanca()
    {
        using var ctx = TestInfra.NovoContexto();
        var categoria = await MontarChaveDiretaDe24Async(ctx);

        var jogos = ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToList();
        foreach (var partida in jogos.Take(7))   // 7 de 8: falta uma quadra terminar
        {
            partida.Status = "Finalizada";
            partida.VencedorId = partida.Dupla1Id;
        }
        await ctx.SaveChangesAsync();

        var avancam = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, ChaveamentoMataMata.PrimeiraRodada);

        Assert.Empty(avancam);
    }

    [Fact]
    public async Task Depois_da_primeira_rodada_nao_existe_mais_bye()
    {
        using var ctx = TestInfra.NovoContexto();
        var categoria = await MontarChaveDiretaDe24Async(ctx);

        foreach (var partida in ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToList())
        {
            partida.Status = "Finalizada";
            partida.VencedorId = partida.Dupla1Id;
        }
        await ctx.SaveChangesAsync();

        // As Oitavas acontecem: agora TODA dupla viva tem partida, então a regra do bye
        // ("quem não tem jogo passou direto") precisa se esgotar sozinha.
        var dezesseis = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, ChaveamentoMataMata.PrimeiraRodada);
        foreach (var confronto in ChaveamentoMataMata.ParearVencedores(dezesseis))
        {
            ctx.Partidas.Add(new Partida
            {
                CategoriaId = categoria.Id,
                Fase = "Oitavas de Final",
                Status = "Finalizada",
                Dupla1Id = confronto.Dupla1Id,
                Dupla2Id = confronto.Dupla2Id,
                VencedorId = confronto.Dupla1Id,
                Codigo = Guid.NewGuid().ToString()[..6],
            });
        }
        await ctx.SaveChangesAsync();

        var avancam = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, "Oitavas de Final");

        Assert.Equal(8, avancam.Count);   // 8 vencedores e nenhum bye reaproveitado
        Assert.Equal("Quartas de Final", ChaveamentoMataMata.NomeFase(avancam.Count));
    }

    [Fact]
    public async Task Categoria_comum_nunca_ganha_bye_por_engano()
    {
        // A regra do bye lê "dupla sem partida". Numa categoria NORMAL isso não pode valer:
        // a dupla que ficou de fora do sorteio (sem parceiro) também não tem partida, e
        // entraria no mata-mata de carona.
        using var ctx = TestInfra.NovoContexto();
        var categoria = new Categoria { Nome = "5ª", Codigo = "5", TorneioId = 1, ChaveDireta = false };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        var jogam = NovasDuplas(ctx, categoria.Id, 4);
        var semParceiro = new Dupla { CategoriaId = categoria.Id, Jogador1Id = 999, UltimaFase = "Grupos" };
        ctx.Duplas.Add(semParceiro);
        await ctx.SaveChangesAsync();

        ctx.Partidas.Add(new Partida
        {
            CategoriaId = categoria.Id,
            Fase = "Semifinal",
            Status = "Finalizada",
            Dupla1Id = jogam[0],
            Dupla2Id = jogam[1],
            VencedorId = jogam[0],
            Codigo = "A1",
        });
        ctx.Partidas.Add(new Partida
        {
            CategoriaId = categoria.Id,
            Fase = "Semifinal",
            Status = "Finalizada",
            Dupla1Id = jogam[2],
            Dupla2Id = jogam[3],
            VencedorId = jogam[2],
            Codigo = "A2",
        });
        await ctx.SaveChangesAsync();

        var avancam = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, "Semifinal");

        Assert.Equal(new[] { jogam[0], jogam[2] }, avancam);
        Assert.DoesNotContain(semParceiro.Id, avancam);
    }

    // ---- cenário ----

    private static List<int> NovasDuplas(Padelizou.Models.DbPadelContext ctx, int categoriaId, int quantas)
    {
        var criadas = new List<Dupla>();
        for (int i = 0; i < quantas; i++)
        {
            // Jogadores distintos por dupla: `Completa` exige os dois lados preenchidos, e
            // dupla incompleta é justamente o que NÃO pode virar bye.
            var dupla = new Dupla
            {
                CategoriaId = categoriaId,
                Jogador1Id = 1000 + i * 2,
                Jogador2Id = 1001 + i * 2,
                UltimaFase = "Grupos",
            };
            criadas.Add(dupla);
            ctx.Duplas.Add(dupla);
        }
        ctx.SaveChanges();
        return criadas.Select(d => d.Id).ToList();
    }

    private static async Task<Categoria> MontarChaveDiretaDe24Async(Padelizou.Models.DbPadelContext ctx)
    {
        var categoria = new Categoria { Nome = "Mata-Mata", Codigo = "MM", TorneioId = 1, ChaveDireta = true };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        var ids = NovasDuplas(ctx, categoria.Id, 24);
        var rodada = ChaveamentoMataMata.MontarChaveDireta(ids);

        foreach (var confronto in rodada.Confrontos)
        {
            ctx.Partidas.Add(new Partida
            {
                CategoriaId = categoria.Id,
                Fase = rodada.Fase,
                Status = "Agendada",
                Dupla1Id = confronto.Dupla1Id,
                Dupla2Id = confronto.Dupla2Id,
                Codigo = Guid.NewGuid().ToString()[..6],
            });
        }
        await ctx.SaveChangesAsync();
        return categoria;
    }

    // ---- Fase que já avançou não avança de novo ----

    // O bug do Interno de 05/08/2026: quartas de final montadas com os 8 vencedores da
    // PRIMEIRA RODADA cruzados entre si (1×8, 2×7, 3×6, 4×5), com as oitavas ainda em quadra.
    //
    // Uma fase completa continua completa pra sempre, então todo finalizar posterior refaz
    // esta pergunta — e a resposta MUDA sozinha: os byes só contam enquanto o mata-mata tem
    // uma fase só, e depois que as oitavas nascem os mesmos 16 viram 8. NomeFase(8) é
    // "Quartas de Final", que ainda não existia, então a guarda de duplicidade do chamador
    // deixava passar.
    [Fact]
    public async Task Fase_ja_avancada_nao_avanca_de_novo()
    {
        using var ctx = TestInfra.NovoContexto();
        var categoria = await MontarChaveDiretaDe24Async(ctx);

        foreach (var partida in ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToList())
        {
            partida.Status = "Finalizada";
            partida.VencedorId = partida.Dupla1Id;
        }
        await ctx.SaveChangesAsync();

        var dezesseis = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, ChaveamentoMataMata.PrimeiraRodada);
        Assert.Equal(16, dezesseis.Count);

        // As oitavas nascem e vão pra quadra — nenhuma terminou ainda.
        foreach (var confronto in ChaveamentoMataMata.ParearVencedores(dezesseis))
        {
            ctx.Partidas.Add(new Partida
            {
                CategoriaId = categoria.Id,
                Fase = "Oitavas de Final",
                Status = "Agendada",
                Dupla1Id = confronto.Dupla1Id,
                Dupla2Id = confronto.Dupla2Id,
                Codigo = Guid.NewGuid().ToString()[..6],
            });
        }
        await ctx.SaveChangesAsync();

        // Segundo jogo da primeira rodada finalizado quase junto com o primeiro — ou o
        // organizador reabrindo e finalizando de novo. A pergunta se repete.
        var denovo = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, ChaveamentoMataMata.PrimeiraRodada);

        Assert.Empty(denovo);
    }

    // A guarda não pode travar o avanço legítimo: quando as oitavas de fato terminam, as
    // quartas precisam nascer.
    [Fact]
    public async Task Guarda_nao_impede_o_avanco_da_fase_mais_recente()
    {
        using var ctx = TestInfra.NovoContexto();
        var categoria = await MontarChaveDiretaDe24Async(ctx);

        foreach (var partida in ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToList())
        {
            partida.Status = "Finalizada";
            partida.VencedorId = partida.Dupla1Id;
        }
        await ctx.SaveChangesAsync();

        var dezesseis = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, ChaveamentoMataMata.PrimeiraRodada);
        foreach (var confronto in ChaveamentoMataMata.ParearVencedores(dezesseis))
        {
            ctx.Partidas.Add(new Partida
            {
                CategoriaId = categoria.Id,
                Fase = "Oitavas de Final",
                Status = "Finalizada",
                Dupla1Id = confronto.Dupla1Id,
                Dupla2Id = confronto.Dupla2Id,
                VencedorId = confronto.Dupla1Id,
                Codigo = Guid.NewGuid().ToString()[..6],
            });
        }
        await ctx.SaveChangesAsync();

        var avancam = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, "Oitavas de Final");

        Assert.Equal(8, avancam.Count);
        Assert.Equal("Quartas de Final", ChaveamentoMataMata.NomeFase(avancam.Count));
    }
}
