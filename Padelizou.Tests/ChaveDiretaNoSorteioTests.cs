using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O cenário REAL que originou a chave direta: um torneio com categorias normais rodando E,
// em paralelo, um mata-mata de duplas remontadas — os MESMOS jogadores, embaralhados em
// outros pares. Passa pelo GerarChaves de verdade, que é o botão que o organizador aperta.
public class ChaveDiretaNoSorteioTests
{
    [Fact]
    public async Task Sorteio_gera_primeira_rodada_e_nenhum_grupo_na_chave_direta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, chave) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 24);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.CategoriaId == chave.Id).ToListAsync();

        Assert.Equal(8, jogos.Count);                                   // 24 duplas, quadro de 32
        Assert.All(jogos, j => Assert.Equal(ChaveamentoMataMata.PrimeiraRodada, j.Fase));
        Assert.All(jogos, j => Assert.False(string.IsNullOrEmpty(j.Codigo)));   // NOT NULL no banco
        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));      // entra na grade junto

        // Chave direta não tem grupo — nem GrupoTorneio nem Dupla.Grupo preenchido.
        Assert.Empty(await ctx.Set<GrupoTorneio>().Where(g => g.CategoriaId == chave.Id).ToListAsync());
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == chave.Id).ToListAsync();
        Assert.All(duplas, d => Assert.Null(d.Grupo));

        // 8 jogam, 16 esperam a segunda rodada (8 vencedores virão dos jogos + 8 byes).
        var naPrimeiraRodada = jogos.SelectMany(j => new[] { j.Dupla1Id, j.Dupla2Id }).ToHashSet();
        Assert.Equal(16, naPrimeiraRodada.Count);
        Assert.Equal(8, duplas.Count(d => !naPrimeiraRodada.Contains(d.Id)));
    }

    // ⚠️ 25 sorteios, não um. O chaveamento da chave direta é ALEATÓRIO (`OrderBy(Guid)`),
    // então cada execução monta uma grade diferente: um teste de uma rodada só afirma "esse
    // sorteio deu certo", e passa ou falha por sorte. Foi assim que o furo do encaixe chegou
    // ao CI verde na máquina e vermelho lá — a versão antiga enfiava o jogo no horário mesmo
    // sem vaga livre, e só alguns sorteios caíam nisso.
    [Theory]
    [InlineData(24)]
    [InlineData(20)]
    [InlineData(12)]
    public async Task Ninguem_e_chamado_pra_duas_quadras_no_mesmo_horario(int duplasNaChave)
    {
        // O ponto mais perigoso da feature: cada pessoa está em DUAS duplas do mesmo torneio
        // (a da categoria dela e a da chave direta). A grade compara PESSOA por causa disso.
        for (int sorteio = 1; sorteio <= 25; sorteio++)
        {
            using var ctx = TestInfra.NovoContexto();
            var (torneio, org, _) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave);
            var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

            await controller.GerarChaves(torneio.Id);

            var jogos = await ctx.Partidas
                .Where(p => p.TorneioId == torneio.Id)
                .Include(p => p.Dupla1).Include(p => p.Dupla2)
                .ToListAsync();

            var conflitos = jogos
                .GroupBy(j => j.HorarioPrevisto)
                .SelectMany(porHorario => porHorario
                    .SelectMany(j => new[]
                    {
                        j.Dupla1.Jogador1Id, j.Dupla1.Jogador2Id!.Value,
                        j.Dupla2.Jogador1Id, j.Dupla2.Jogador2Id!.Value,
                    })
                    .GroupBy(jogadorId => jogadorId)
                    .Where(g => g.Count() > 1)
                    .Select(g => $"{porHorario.Key:dd/MM HH:mm} jogador {g.Key}"))
                .ToList();

            Assert.Empty(conflitos);

            // E ninguém pode ficar sem horário por causa da folga de vagas.
            Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
        }
    }

    [Fact]
    public async Task Do_sorteio_ao_campeao_passando_pelos_byes()
    {
        // A chave inteira, jogada até o fim pelo fluxo real: se a ponte dos byes errar, ou o
        // torneio trava numa fase, ou 8 duplas somem sem nunca ter perdido.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, chave) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 24);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var esperado = new[]
        {
            (ChaveamentoMataMata.PrimeiraRodada, 8),
            ("Oitavas de Final", 8),
            ("Quartas de Final", 4),
            ("Semifinal", 2),
            ("Final", 1),
        };

        foreach (var (fase, jogosDaFase) in esperado)
        {
            var jogos = await ctx.Partidas
                .Where(p => p.CategoriaId == chave.Id && p.Fase == fase)
                .ToListAsync();

            Assert.Equal(jogosDaFase, jogos.Count);

            foreach (var jogo in jogos)
                await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 6, 3);   // vence sempre a Dupla1
        }

        // Um campeão só, e ele saiu da Final.
        var final = await ctx.Partidas.SingleAsync(p => p.CategoriaId == chave.Id && p.Fase == "Final");
        Assert.NotNull(final.VencedorId);
        Assert.Equal(23, await ctx.Partidas.CountAsync(p => p.CategoriaId == chave.Id));   // 24 duplas = 23 jogos
    }

    [Fact]
    public async Task Torneio_completo_com_categorias_times_e_chave_direta_na_mesma_grade()
    {
        // A forma real do Interno: 4 categorias comuns + 8 times + o mata-mata paralelo,
        // 5 quadras, 12 min por jogo. É a combinação que o sorteio de verdade vai encontrar,
        // e a única em que TIME e CHAVE DIRETA dividem a mesma grade — os dois tratam
        // conflito de horário por regras diferentes (dupla x pessoa).
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, categorias) = MontarTorneioComoOInterno(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id)
            .Include(p => p.Dupla1).Include(p => p.Dupla2)
            .ToListAsync();

        // Cada categoria comum virou grupos; o mata-mata virou primeira rodada.
        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
        Assert.Equal(8, jogos.Count(j => j.Fase == ChaveamentoMataMata.PrimeiraRodada));
        Assert.All(categorias.comuns, cat =>
            Assert.True(ctx.Set<GrupoTorneio>().Any(g => g.CategoriaId == cat.Id),
                $"{cat.Nome} devia ter grupos sorteados"));
        Assert.True(ctx.Set<GrupoTorneio>().Any(g => g.CategoriaId == categorias.times.Id));
        Assert.Empty(ctx.Set<GrupoTorneio>().Where(g => g.CategoriaId == categorias.chave.Id));

        // O que mais importa no dia: ninguém chamado pra duas quadras no mesmo horário.
        // Time entra pelo Id da dupla (o Jogador1Id dele é o organizador, e comparar por
        // pessoa faria todo time brigar com todo time).
        var conflitos = jogos
            .GroupBy(j => j.HorarioPrevisto)
            .SelectMany(horario => horario
                .SelectMany(j => new[] { j.Dupla1, j.Dupla2 })
                .SelectMany(d => d.EhTime
                    ? new[] { $"time-{d.Id}" }
                    : new[] { $"j-{d.Jogador1Id}", $"j-{d.Jogador2Id}" })
                .GroupBy(quem => quem)
                .Where(g => g.Count() > 1)
                .Select(g => $"{horario.Key:dd/MM HH:mm} {g.Key}"))
            .ToList();

        Assert.Empty(conflitos);
    }

    [Fact]
    public async Task A_chave_direta_ABRE_o_torneio_em_vez_de_esperar_os_grupos()
    {
        // A regra era o contrário — a chave direta ia pro fim da fase de grupos, pra que
        // "fase de grupo primeiro, mata-mata depois" fosse garantia e não preferência.
        //
        // Só que isso vale pro mata-mata que SAI dos grupos, que de fato precisa do resultado
        // deles. A chave direta não espera nada: as duplas já estão definidas, e ela é uma
        // competição paralela ("como se fosse uma outra categoria", nas palavras do
        // organizador). Pior: é ela que tem MAIS FASES pela frente — 24 duplas são cinco
        // rodadas até a final, contra as três de uma categoria de grupos. Empurrá-la pro fim
        // dos grupos empurrava as cinco rodadas junto, e no Interno isso jogou a final da
        // chave geral pras 23h18.
        //
        // Quem impede o embaralhamento perigoso (a mesma pessoa em duas quadras) é o encaixe,
        // que compara PESSOA — ver Ninguem_e_chamado_pra_duas_quadras_no_mesmo_horario.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, chave) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 24);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();

        var primeiroDoMataMata = jogos.Where(j => j.CategoriaId == chave.Id).Min(j => j.HorarioPrevisto);
        var ultimoDeGrupo = jogos
            .Where(j => Padelizou.Services.FasesTorneio.EhFaseDeGrupos(j.Fase))
            .Max(j => j.HorarioPrevisto);

        Assert.Equal(torneio.AberturaDaGrade, primeiroDoMataMata);
        Assert.True(primeiroDoMataMata < ultimoDeGrupo,
            $"a chave direta abriria às {primeiroDoMataMata:HH:mm}, sem ganhar nada em relação " +
            $"ao fim dos grupos ({ultimoDeGrupo:HH:mm})");
    }

    [Fact]
    public async Task Mata_mata_que_sai_dos_grupos_continua_esperando_os_grupos()
    {
        // A contrapartida do teste acima: o que DEPENDE do resultado dos grupos não pode
        // subir pro meio deles. Só a chave direta é independente.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, chave) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 24);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        foreach (var jogo in await ctx.Partidas
                     .Where(p => p.TorneioId == torneio.Id && p.CategoriaId != chave.Id).ToListAsync())
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        var fimDosGrupos = jogos
            .Where(j => Padelizou.Services.FasesTorneio.EhFaseDeGrupos(j.Fase))
            .Max(j => j.HorarioPrevisto);

        var dosGrupos = jogos
            .Where(j => j.CategoriaId != chave.Id && !Padelizou.Services.FasesTorneio.EhFaseDeGrupos(j.Fase))
            .ToList();

        Assert.NotEmpty(dosGrupos);
        Assert.All(dosGrupos, j => Assert.True(j.HorarioPrevisto > fimDosGrupos,
            $"{j.Fase} às {j.HorarioPrevisto:HH:mm} subiu pro meio da fase de grupos, que só " +
            $"acaba {fimDosGrupos:HH:mm} — ela depende de quem classificar"));
    }

    [Fact]
    public async Task O_torneio_inteiro_cabe_em_quase_o_minimo_de_quadra()
    {
        // O piso de um torneio é aritmética, não algoritmo: N jogos em Q quadras ocupam pelo
        // menos ceil(N/Q) levas, e nenhuma ordem de fila vence isso. O que a grade PODE fazer
        // é não desperdiçar quadra — e é isso que este teste tranca.
        //
        // Serve de alarme: se alguém voltar a enfileirar categoria atrás de categoria, ou a
        // empurrar uma chave inteira pro fim, a folga estoura e o teste cai.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, _) = MontarTorneioComoOInterno(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        // Joga o torneio inteiro, fase após fase.
        for (int volta = 0; volta < 12; volta++)
        {
            var pendentes = await ctx.Partidas
                .Where(p => p.TorneioId == torneio.Id && p.Status != "Finalizada").ToListAsync();
            if (pendentes.Count == 0) break;
            foreach (var jogo in pendentes)
                await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);
        }

        var todas = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        int duracao = torneio.TempoPrevistoPartidaMinutos;
        var levas = todas.Select(p => p.HorarioPrevisto).Distinct().Count();
        int piso = (int)Math.Ceiling(todas.Count / (double)torneio.QuantidadeQuadras);

        // Duas levas de folga: entre uma fase e a seguinte existe o descanso, e uma final
        // sozinha ocupa uma leva inteira usando uma quadra só.
        Assert.True(levas <= piso + 2,
            $"{todas.Count} jogos em {torneio.QuantidadeQuadras} quadras cabem em {piso} levas de " +
            $"{duracao} min, e a grade usou {levas} — {(levas - piso) * duracao} min de quadra parada");
    }

    [Fact]
    public async Task Refazer_a_grade_muda_o_horario_e_nao_o_confronto()
    {
        // Sorteio e grade são coisas diferentes. Refazer a grade serve pra quando a regra
        // melhora depois do sorteio (foi o caso: a chave direta passou a abrir o torneio) ou
        // pra quando o organizador mexe em quadras, duração ou horário de início.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, _) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 24);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var antes = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id)
            .Select(p => new { p.Id, p.Dupla1Id, p.Dupla2Id, p.Fase })
            .ToListAsync();

        // Dobra as quadras: a grade tem que encolher, e ninguém pode trocar de adversário.
        torneio.QuantidadeQuadras = 10;
        await ctx.SaveChangesAsync();

        var fimAntes = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).MaxAsync(p => p.HorarioPrevisto);

        await controller.RefazerGrade(torneio.Id);

        var depois = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id)
            .Select(p => new { p.Id, p.Dupla1Id, p.Dupla2Id, p.Fase })
            .ToListAsync();
        var fimDepois = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).MaxAsync(p => p.HorarioPrevisto);

        Assert.Equal(antes.OrderBy(x => x.Id), depois.OrderBy(x => x.Id));
        Assert.True(fimDepois < fimAntes, $"com o dobro de quadras o torneio devia acabar antes de {fimAntes:HH:mm}");
        Assert.Empty(await ctx.Partidas.Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto == null).ToListAsync());
    }

    [Fact]
    public async Task Refazer_a_grade_e_recusado_depois_do_primeiro_jogo()
    {
        // Remarcar jogo que já rolou é apagar história, e mexer no horário de quem já está em
        // quadra é pior ainda.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, _) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 24);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);
        await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);
        var horarios = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .Select(p => new { p.Id, p.HorarioPrevisto }).ToListAsync();

        await controller.RefazerGrade(torneio.Id);

        var depois = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .Select(p => new { p.Id, p.HorarioPrevisto }).ToListAsync();

        Assert.Equal(horarios.OrderBy(x => x.Id), depois.OrderBy(x => x.Id));
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Chave_direta_acima_do_teto_e_recusada_antes_de_sortear()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, _) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 33);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        // Recusa inteira: nada de torneio sorteado pela metade.
        Assert.Empty(await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync());
        Assert.Equal("Chaves em Sorteio", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // ---- cenário ----

    // O Interno como ele é: 4ª (4 duplas), 5ª (9), 6ªM (11), 6ªF (8), 8 times e o
    // Mata-Mata Geral com 24 duplas feitas dos MESMOS 48 homens, remontados.
    private static (Torneio torneio, Jogador organizador,
                    (List<Categoria> comuns, Categoria times, Categoria chave) categorias)
        MontarTorneioComoOInterno(DbPadelContext ctx)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Ensaio do Interno",
            Codigo = "ENSAIO",
            Status = "Chaves em Sorteio",
            DataInicio = new DateTime(2026, 8, 5, 8, 0, 0),
            QuantidadeQuadras = 5,
            TempoPrevistoPartidaMinutos = 12,
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        };
        ctx.Torneios.Add(torneio);

        var quarta = new Categoria { Nome = "4ª Masculina", Codigo = "4M", Torneio = torneio };
        var quinta = new Categoria { Nome = "5ª Masculina", Codigo = "5M", Torneio = torneio };
        var sextaM = new Categoria { Nome = "6ª Masculina", Codigo = "6M", Torneio = torneio };
        var sextaF = new Categoria { Nome = "6ª Feminina", Codigo = "6F", Torneio = torneio };
        var times = new Categoria
        {
            Nome = "Times", Codigo = "TM", Torneio = torneio,
            DeTimes = true, QuantidadeTimes = 8, QuantidadeGrupos = 2, ClassificadosPorGrupo = 2,
        };
        var chave = new Categoria { Nome = "Mata-Mata Geral", Codigo = "MM", Torneio = torneio, ChaveDireta = true };
        ctx.Categorias.AddRange(quarta, quinta, sextaM, sextaF, times, chave);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        // 48 homens e 16 mulheres.
        var homens = Enumerable.Range(1, 48).Select(TestInfra.NovoJogador).ToList();
        var mulheres = Enumerable.Range(49, 16).Select(TestInfra.NovoJogador).ToList();
        ctx.Jogadores.AddRange(homens);
        ctx.Jogadores.AddRange(mulheres);
        ctx.SaveChanges();

        void Inscrever(Categoria cat, List<Jogador> gente, int primeiro, int quantasDuplas)
        {
            for (int i = 0; i < quantasDuplas; i++)
                ctx.Duplas.Add(new Dupla
                {
                    Categoria = cat,
                    Jogador1 = gente[primeiro + i * 2],
                    Jogador2 = gente[primeiro + i * 2 + 1],
                });
        }

        Inscrever(quarta, homens, 0, 4);     // homens 1..8
        Inscrever(quinta, homens, 8, 9);     // homens 9..26
        Inscrever(sextaM, homens, 26, 11);   // homens 27..48
        Inscrever(sextaF, mulheres, 0, 8);   // 8 duplas femininas COMPLETAS

        // O mata-mata remonta os mesmos 48: o homem n joga com o n+24, então ninguém
        // repete o parceiro da categoria dele — que é o caso real.
        for (int i = 0; i < 24; i++)
            ctx.Duplas.Add(new Dupla { Categoria = chave, Jogador1 = homens[i], Jogador2 = homens[i + 24] });

        foreach (var nome in new[] { "ST Led", "Target.it", "Kayser Imóveis", "CredHub",
                                     "Valandro Gestão", "Málaga Seguros", "Pro Tech", "Argentus XP" })
            ctx.Duplas.Add(new Dupla { Categoria = times, Jogador1 = organizador, NomeTime = nome });

        ctx.SaveChanges();

        return (torneio, organizador,
                (new List<Categoria> { quarta, quinta, sextaM, sextaF }, times, chave));
    }

    // Um torneio como o Interno: uma categoria normal com 12 duplas e uma chave direta com
    // N duplas montadas a partir dos MESMOS 24 jogadores, remontados em outros pares.
    private static (Torneio torneio, Jogador organizador, Categoria chaveDireta)
        MontarTorneioComChaveDiretaAsync(DbPadelContext ctx, int duplasNaChave)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Interno de Teste",
            Codigo = "INT123",
            Status = "Chaves em Sorteio",
            DataInicio = new DateTime(2026, 8, 5, 8, 0, 0),
            QuantidadeQuadras = 5,
            TempoPrevistoPartidaMinutos = 12,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "6ª Masculina", Codigo = "C6M", Torneio = torneio };
        var chave = new Categoria { Nome = "Mata-Mata", Codigo = "MM", Torneio = torneio, ChaveDireta = true };
        ctx.Categorias.AddRange(categoria, chave);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        // Gente suficiente pra encher as duas: cada pessoa entra 1x na categoria e 1x na chave.
        int pessoas = Math.Max(24, duplasNaChave * 2);
        var jogadores = Enumerable.Range(1, pessoas).Select(TestInfra.NovoJogador).ToList();
        ctx.Jogadores.AddRange(jogadores);
        ctx.SaveChanges();

        for (int i = 0; i < 12; i++)
            ctx.Duplas.Add(new Dupla
            {
                Categoria = categoria,
                Jogador1 = jogadores[i * 2],
                Jogador2 = jogadores[i * 2 + 1],
            });

        // A remontagem: os pares da chave direta são DESLOCADOS em relação aos da categoria,
        // então quase ninguém joga com o mesmo parceiro — é o caso real.
        for (int i = 0; i < duplasNaChave; i++)
            ctx.Duplas.Add(new Dupla
            {
                Categoria = chave,
                Jogador1 = jogadores[(i * 2 + 1) % pessoas],
                Jogador2 = jogadores[(i * 2 + 2) % pessoas],
            });

        ctx.SaveChanges();
        return (torneio, organizador, chave);
    }
}
