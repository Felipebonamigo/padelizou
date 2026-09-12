using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A CHAVE REAL TEM QUE SAIR COMO A PRÉVIA PROMETEU.
//
// 🗣️ Felipe, 12/09/2026, com o ER PADEL TOUR rolando e os jogadores cobrando: *"acho que o
// chaveamento se perdeu, por que ontem eu tinha visto e estava diferente"* · *"era primeiro da F
// contra o segundo da E ou algo assim"* · *"ele tem q respeitar o q estava previsto"*.
//
// 🕳️ O DEFEITO. A prévia (Services/ChaveProjetada) alimenta o motor com `Vitorias: 0, Saldo: 0`
// pra todo mundo — ninguém jogou ainda —, então o desempate entre os 2ºs cai no `ThenBy(c =>
// c.Grupo)`, alfabético. A chave de verdade alimenta o MESMO motor com a campanha real
// (ChaveamentoMataMata.cs:122). Mesmo motor, entradas diferentes, saída diferente: na 4ª
// Masculina o jogo 1 prometia `1ºE × 2ºF` e saiu `1ºE × 2ºB`.
//
// ⚠️ OS BYES NÃO DIVERGEM, e é isso que tornou o defeito difícil de ver: `OrdemDosByes` ordena
// por posição, jogos no grupo e nome do grupo — nenhum deles é campanha. As quartas do ER
// saíram idênticas à prévia; só a primeira rodada embaralhou.
//
// ✅ A SAÍDA É CONGELAR, NÃO RECALCULAR. O cruzamento previsto já sabe se escrever
// (`CruzamentoDoMataMata.Padrao`, que sai da própria ChaveProjetada) e o motor já sabe obedecê-lo
// (`MontarPrimeiraFase` lê `categoria.CruzamentoDoMataMata` antes de semear). Faltava gravá-lo no
// instante em que a prévia deixa de ser rascunho e vira promessa pública: a APROVAÇÃO da chave.
public class ChaveRespeitaOPrevistoTests
{
    // 12 duplas → 4 grupos de 3 → 8 classificados, quadro de 8, sem bye.
    private static async Task<(Torneio torneio, Categoria categoria, Jogador org)> TorneioSorteadoAsync(
        DbPadelContext ctx, int qtdDuplas = 12)
    {
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 30;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        return (torneio, categoria, org);
    }

    // O que a PRÉVIA prometia, em rótulos de colocação ("1º do Grupo A × 2º do Grupo D").
    private static List<string> PrevistoAsync(Categoria categoria)
    {
        var grupos = categoria.GruposTorneio.OrderBy(g => g.Nome).Select(g => g.Nome).ToList();
        var tamanhos = categoria.GruposTorneio.OrderBy(g => g.Nome).Select(g => g.Duplas.Count).ToList();
        var (_, confrontos, _) = ChaveProjetada.Montar(
            grupos, ClassificacaoDeGrupos.VagasPorGrupo(categoria), tamanhos);
        return confrontos.Select(c => $"{c.Lado1.Rotulo} × {c.Lado2.Rotulo}").ToList();
    }

    // O que a chave REAL entregou, traduzido pros mesmos rótulos.
    private static async Task<List<string>> RealAsync(DbPadelContext ctx, Categoria categoria)
    {
        var duplas = categoria.GruposTorneio.SelectMany(g => g.Duplas).ToList();
        var finalizadas = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id
                     && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo "))
                     && p.Status == "Finalizada")
            .ToListAsync();

        var pontos = await ClassificacaoDeGrupos.PontosSePrecisarAsync(
            duplas, finalizadas, TestInfra.SemPontosDoRanking);
        var classificados = ClassificacaoDeGrupos.Calcular(
            duplas, finalizadas, pontos, ClassificacaoDeGrupos.VagasPorGrupo(categoria));

        string Rotulo(int duplaId)
        {
            var c = classificados.First(x => x.DuplaId == duplaId);
            var grupo = c.Grupo.StartsWith("Grupo ") ? c.Grupo : $"Grupo {c.Grupo}";
            return $"{c.Posicao}º do {grupo}";
        }

        var mataMata = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id
                     && !(p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")))
            .OrderBy(p => p.Id)
            .ToListAsync();

        return mataMata.Select(p => $"{Rotulo(p.Dupla1Id)} × {Rotulo(p.Dupla2Id)}").ToList();
    }

    // Encerra a fase de grupos com placares VARIADOS — é a campanha desigual que reordena os
    // 2ºs e faz a chave real discordar da prévia.
    private static async Task EncerrarOsGruposAsync(DbPadelContext ctx, Torneio torneio, Categoria categoria, int orgId)
    {
        var jogos = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id)
            .OrderBy(p => p.Id)
            .ToListAsync();

        for (int i = 0; i < jogos.Count; i++)
        {
            var controller = TestInfra.NovoTorneiosController(ctx, orgId);
            // Saldos bem diferentes entre si: 9x0, 9x7, 9x2, 9x5, ...
            int perdedor = new[] { 0, 7, 2, 5, 1, 6 }[i % 6];
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogos[i], 9, perdedor);
        }
    }

    [Fact]
    public async Task Aprovar_congela_o_cruzamento_previsto_na_categoria()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = await TorneioSorteadoAsync(ctx);

        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);

        var salva = await ctx.Categorias.AsNoTracking().FirstAsync(c => c.Id == categoria.Id);
        var comGrupos = await ctx.Categorias
            .Include(c => c.GruposTorneio).ThenInclude(g => g.Duplas)
            .AsNoTracking().FirstAsync(c => c.Id == categoria.Id);

        var esperado = CruzamentoDoMataMata.Padrao(
            comGrupos.GruposTorneio.OrderBy(g => g.Nome).Select(g => g.Nome).ToList(),
            ClassificacaoDeGrupos.VagasPorGrupo(comGrupos),
            comGrupos.GruposTorneio.OrderBy(g => g.Nome).Select(g => g.Duplas.Count).ToList());

        Assert.NotNull(esperado);
        Assert.Equal(esperado!.Escrever(), salva.CruzamentoDoMataMata);
    }

    [Fact]
    public async Task A_chave_real_sai_exatamente_como_a_previa_prometeu()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = await TorneioSorteadoAsync(ctx);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);

        var comGrupos = await ctx.Categorias
            .Include(c => c.GruposTorneio).ThenInclude(g => g.Duplas)
            .FirstAsync(c => c.Id == categoria.Id);
        var previsto = PrevistoAsync(comGrupos);

        await EncerrarOsGruposAsync(ctx, torneio, categoria, org.Id);

        var real = await RealAsync(ctx, comGrupos);

        Assert.NotEmpty(previsto);
        Assert.Equal(previsto, real);
    }
    // ══ O CONSERTO DO TORNEIO QUE JÁ ESTÁ RODANDO ═══════════════════════════════════════════
    //
    // Congelar na aprovação resolve o PRÓXIMO torneio. O ER já estava aprovado e com as partidas
    // do mata-mata criadas quando o defeito apareceu — e com gente cobrando na quadra. Daí a
    // ação que reescreve as duplas das partidas que já existem, seguindo o previsto.
    //
    // ⚠️ Aprovado ANTES da correção = categoria sem desenho. É esse o estado que estes testes
    // montam: sorteia, publica na mão (sem passar pelo AprovarChaves novo) e deixa
    // `CruzamentoDoMataMata` nulo, que é exatamente como o ER acordou hoje.
    private static async Task PublicarComoAntesDaCorrecaoAsync(DbPadelContext ctx, Torneio torneio)
    {
        torneio.Status = "Fase de Grupos";
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Refazer_como_previsto_conserta_a_chave_ja_criada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = await TorneioSorteadoAsync(ctx);
        await PublicarComoAntesDaCorrecaoAsync(ctx, torneio);

        var comGrupos = await ctx.Categorias
            .Include(c => c.GruposTorneio).ThenInclude(g => g.Duplas)
            .FirstAsync(c => c.Id == categoria.Id);
        var previsto = PrevistoAsync(comGrupos);

        await EncerrarOsGruposAsync(ctx, torneio, categoria, org.Id);

        // O estado do ER: a chave saiu diferente do prometido.
        Assert.NotEqual(previsto, await RealAsync(ctx, comGrupos));

        var antes = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final")
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.HorarioPrevisto, p.NomeQuadra, p.Codigo })
            .ToListAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .RefazerMataMataComoPrevisto(torneio.Id, categoria.Id);

        Assert.Equal(previsto, await RealAsync(ctx, comGrupos));

        // ⚠️ HORÁRIO, QUADRA E NÚMERO DO JOGO FICAM. O organizador montou a grade em cima
        // deles e o jogador já se organizou pro horário: só as duplas mudam de lugar.
        var depois = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final")
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.HorarioPrevisto, p.NomeQuadra, p.Codigo })
            .ToListAsync();
        Assert.Equal(antes, depois);
    }

    [Fact]
    public async Task Refazer_grava_o_desenho_pra_nao_acontecer_de_novo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = await TorneioSorteadoAsync(ctx);
        await PublicarComoAntesDaCorrecaoAsync(ctx, torneio);
        await EncerrarOsGruposAsync(ctx, torneio, categoria, org.Id);

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .RefazerMataMataComoPrevisto(torneio.Id, categoria.Id);

        var salva = await ctx.Categorias.AsNoTracking().FirstAsync(c => c.Id == categoria.Id);
        Assert.Equal("1A×2C|1B×2D|1C×2A|1D×2B", salva.CruzamentoDoMataMata);
    }

    [Fact]
    public async Task Refazer_recusa_se_a_bola_ja_rolou_em_algum_jogo_do_mata_mata()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = await TorneioSorteadoAsync(ctx);
        await PublicarComoAntesDaCorrecaoAsync(ctx, torneio);
        await EncerrarOsGruposAsync(ctx, torneio, categoria, org.Id);

        var quartas = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final")
            .OrderBy(p => p.Id)
            .ToListAsync();
        quartas[0].Status = "Em Andamento";
        await ctx.SaveChangesAsync();

        var comoEstava = quartas.Select(p => (p.Id, p.Dupla1Id, p.Dupla2Id)).ToList();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .RefazerMataMataComoPrevisto(torneio.Id, categoria.Id);

        // ⚠️ NADA muda — nem os jogos que ainda não começaram. Reescrever metade da chave
        // deixaria uma dupla em dois jogos e outra em nenhum.
        var agora = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final")
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Dupla1Id, p.Dupla2Id })
            .ToListAsync();
        Assert.Equal(comoEstava, agora.Select(p => (p.Id, p.Dupla1Id, p.Dupla2Id)).ToList());
    }

    [Fact]
    public async Task Refazer_apaga_os_palpites_dos_jogos_que_mudaram()
    {
        // Palpite aponta pra DUPLA ESCOLHIDA (Models/PalpitePartida.DuplaEscolhidaId). Trocada a
        // dupla do jogo, o palpite passaria a apontar pra quem não está mais nele — e o
        // palpitômetro contaria voto de um confronto que deixou de existir.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = await TorneioSorteadoAsync(ctx);
        await PublicarComoAntesDaCorrecaoAsync(ctx, torneio);
        await EncerrarOsGruposAsync(ctx, torneio, categoria, org.Id);

        var jogo = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final")
            .OrderBy(p => p.Id).FirstAsync();
        ctx.PalpitesPartida.Add(new PalpitePartida
        {
            PartidaId = jogo.Id,
            JogadorId = org.Id,
            DuplaEscolhidaId = jogo.Dupla1Id,
        });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .RefazerMataMataComoPrevisto(torneio.Id, categoria.Id);

        Assert.Empty(await ctx.PalpitesPartida.Where(p => p.PartidaId == jogo.Id).ToListAsync());
    }

    [Fact]
    public async Task So_organizador_refaz()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = await TorneioSorteadoAsync(ctx);
        await PublicarComoAntesDaCorrecaoAsync(ctx, torneio);
        await EncerrarOsGruposAsync(ctx, torneio, categoria, org.Id);

        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000088" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .RefazerMataMataComoPrevisto(torneio.Id, categoria.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
    }
    [Fact]
    public async Task Refazer_diz_QUAIS_jogos_mudaram_e_o_que_eles_eram()
    {
        // 🗣️ Felipe: *"me passe uma lista dos jogos q estavam errados e foram corrigidos"* — pra
        // avisar o pessoal. Um contador ("3 jogos") não serve: ele precisa dos nomes, e depois do
        // clique não há mais como saber o que era antes.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = await TorneioSorteadoAsync(ctx);
        await PublicarComoAntesDaCorrecaoAsync(ctx, torneio);
        await EncerrarOsGruposAsync(ctx, torneio, categoria, org.Id);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.RefazerMataMataComoPrevisto(torneio.Id, categoria.Id);

        var recado = $"{controller.TempData["Sucesso"]}";
        Assert.Contains("Jogo 1:", recado);
        Assert.Contains("era", recado);
        // O nome de quem saiu do jogo 1, e não só um número.
        Assert.Matches(@"Jogo 1: .+ × .+ \(era .+ × .+\)", recado);
    }
}
