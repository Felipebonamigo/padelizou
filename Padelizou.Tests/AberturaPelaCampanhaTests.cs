using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// FOLGA PROS MELHORES 1ºs, PELA CAMPANHA (27/11/2026).
//
// 🗣️ Rafael (Treta): "como não tem ranking, os escolhidos para ir direto pra semi são os melhores
// 1ºs, e não exatamente o A1". Desde 12/09/2026 o robô grava no sorteio o cruzamento por
// colocação (CruzamentoDoMataMata.Padrao), e num 3 grupos de 3 a folga fica cravada no 1º do A e
// no do B antes de qualquer jogo. Ligada a opção, a categoria espera TODOS os grupos fecharem e a
// abertura sai do motor de campanha (ChaveamentoMataMata.MontarPrimeiraFase sem desenho): os
// melhores 1ºs entre grupos (vitórias → saldo → games) pulam a primeira rodada.
public class AberturaPelaCampanhaTests
{
    private static readonly DateTime Sabado = new(2026, 12, 5);

    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Categoria Cat, Jogador Org,
        Dictionary<string, Dupla> Duplas)
    {
        public RoboDoChaveamento Robo() => new(Ctx, TestInfra.EstatisticasFalsas());
        public int Id(string nome) => Duplas[nome].Id;
    }

    // 3 grupos de 3. A dupla i vence a j (i < j). O perdedor faz 4 games no A, 2 no B e 0 no C:
    // o 1º do C tem a MELHOR campanha, depois o do B; o do A é o pior líder.
    private static Cenario Montar(bool campanha, bool deixarAberto = false)
    {
        var ctx = TestInfra.NovoContexto();
        var org = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(org);
        var torneio = new Torneio
        {
            Nome = "Treta", Codigo = "TRETA", Status = "Fase de Grupos", ForaDoRanking = true,
            DataInicio = Sabado.AddHours(9), DataFim = Sabado.AddDays(1),
            HoraInicioDoDia = new TimeSpan(9, 0, 0), HoraInicioDiasSeguintes = new TimeSpan(9, 0, 0),
            HoraFimDoDia = new TimeSpan(22, 0, 0), QuantidadeQuadras = 3, TempoPrevistoPartidaMinutos = 30,
        };
        ctx.Torneios.Add(torneio);
        var cat = new Categoria
        {
            Nome = "Preto", Codigo = "PT", Torneio = torneio, ClassificadosPorGrupo = 2,
            CruzamentoDoMataMata = campanha ? AberturaPelaCampanha.Marca : null,
        };
        ctx.Categorias.Add(cat);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = org.Id, NivelAcesso = "Criador" });
        foreach (var q in new[] { "Quadra 1", "Quadra 2", "Quadra 3" })
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = q });

        var duplas = new Dictionary<string, Dupla>();
        var partidas = new List<Partida>();
        int n = 1;
        int[] gamesDoPerdedor = { 4, 2, 0 };
        for (int g = 0; g < 3; g++)
        {
            string letra = ((char)('A' + g)).ToString();
            var grupo = new GrupoTorneio { Categoria = cat, Nome = "Grupo " + letra };
            ctx.Add(grupo);
            var doGrupo = new List<Dupla>();
            for (int i = 0; i < 3; i++)
            {
                var j1 = TestInfra.NovoJogador(n++);
                var j2 = TestInfra.NovoJogador(n++);
                ctx.Jogadores.AddRange(j1, j2);
                var d = new Dupla { Categoria = cat, Jogador1 = j1, Jogador2 = j2, GrupoTorneio = grupo, Grupo = letra, Pago = true };
                ctx.Duplas.Add(d);
                doGrupo.Add(d);
                duplas[$"{i + 1}{letra}"] = d;
            }
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                    partidas.Add(new Partida
                    {
                        TorneioId = torneio.Id, Categoria = cat, Dupla1 = doGrupo[i], Dupla2 = doGrupo[j],
                        Codigo = Guid.NewGuid().ToString()[..6], Fase = "Grupo " + letra, Status = "Finalizada",
                        GamesDupla1 = 6, GamesDupla2 = gamesDoPerdedor[g], SetsDupla1 = 1, SetsDupla2 = 0,
                    });
        }
        ctx.Partidas.AddRange(partidas);
        ctx.SaveChanges();
        foreach (var p in partidas) p.VencedorId = p.Dupla1Id;
        if (deixarAberto)
        {
            // O último jogo do grupo C (o grupo do melhor líder) ainda por jogar.
            var ultimo = partidas.Last();
            ultimo.Status = "Agendada";
            ultimo.VencedorId = null;
            ultimo.GamesDupla1 = null;
            ultimo.GamesDupla2 = null;
        }
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return new Cenario(ctx, torneio, cat, org, duplas);
    }

    private static async Task<List<Partida>> MataMataAsync(Cenario c) =>
        await c.Ctx.Partidas.AsNoTracking()
            .Where(p => p.CategoriaId == c.Cat.Id && !p.Fase.StartsWith("Grupo"))
            .ToListAsync();

    private static bool Joga(Partida p, int duplaId) => p.Dupla1Id == duplaId || p.Dupla2Id == duplaId;

    [Fact]
    public async Task Os_dois_melhores_1os_pulam_as_quartas_e_o_pior_lider_joga()
    {
        var c = Montar(campanha: true);
        using var _ = c.Ctx;

        await c.Robo().MontarMataMataDosGruposAsync(c.Cat.Id, c.Torneio.Id);

        var abertura = await MataMataAsync(c);
        Assert.Equal(2, abertura.Count);
        Assert.All(abertura, p => Assert.Equal("Quartas de Final", p.Fase));
        Assert.DoesNotContain(abertura, p => Joga(p, c.Id("1C")) || Joga(p, c.Id("1B")));
        var doPiorLider = Assert.Single(abertura, p => Joga(p, c.Id("1A")));
        // E não reencontra o próprio grupo logo de cara.
        Assert.False(Joga(doPiorLider, c.Id("2A")));
    }

    [Fact]
    public async Task Sem_a_opcao_continua_a_folga_por_letra()
    {
        // Trava o comportamento de hoje: o desenho padrão grava a folga no 1º do A e no do B.
        var c = Montar(campanha: false);
        using var _ = c.Ctx;

        await c.Robo().MontarMataMataDosGruposAsync(c.Cat.Id, c.Torneio.Id);

        var abertura = await MataMataAsync(c);
        Assert.Contains(abertura, p => Joga(p, c.Id("1C")));
    }

    [Fact]
    public async Task Com_grupo_aberto_a_abertura_espera()
    {
        var c = Montar(campanha: true, deixarAberto: true);
        using var _ = c.Ctx;

        await c.Robo().MontarMataMataDosGruposAsync(c.Cat.Id, c.Torneio.Id);

        Assert.Empty(await MataMataAsync(c));
        // E a marca não é trocada pelo desenho por letra.
        Assert.Equal(AberturaPelaCampanha.Marca,
            (await c.Ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == c.Cat.Id)).CruzamentoDoMataMata);
    }

    [Fact]
    public async Task As_semis_juntam_os_dois_melhores_1os_aos_vencedores_das_quartas()
    {
        var c = Montar(campanha: true);
        using var _ = c.Ctx;
        await c.Robo().MontarMataMataDosGruposAsync(c.Cat.Id, c.Torneio.Id);

        foreach (var q in await MataMataAsync(c))
        {
            var p = await c.Ctx.Partidas.FirstAsync(x => x.Id == q.Id);
            p.Status = "Finalizada";
            p.VencedorId = p.Dupla1Id;
            p.GamesDupla1 = 6;
            p.GamesDupla2 = 3;
            await c.Ctx.SaveChangesAsync();
            c.Ctx.ChangeTracker.Clear();
            await c.Robo().AvancarFaseAsync(c.Cat.Id, c.Torneio.Id, "Quartas de Final");
            c.Ctx.ChangeTracker.Clear();
        }

        var semis = (await MataMataAsync(c)).Where(p => p.Fase == "Semifinal").ToList();
        Assert.Equal(2, semis.Count);
        Assert.Contains(semis, p => Joga(p, c.Id("1C")));
        Assert.Contains(semis, p => Joga(p, c.Id("1B")));
        // Os dois melhores 1ºs não se cruzam na semi.
        Assert.DoesNotContain(semis, p => Joga(p, c.Id("1C")) && Joga(p, c.Id("1B")));
    }

    // ── A opção na gestão ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Organizador_liga_e_desliga_mesmo_com_a_chave_aprovada()
    {
        var c = Montar(campanha: false, deixarAberto: true);
        using var _ = c.Ctx;
        // Já aprovada: o cruzamento por letra está gravado.
        var cat = await c.Ctx.Categorias.FirstAsync(x => x.Id == c.Cat.Id);
        cat.CruzamentoDoMataMata = "1A×2B|1B×2A";
        await c.Ctx.SaveChangesAsync();
        c.Ctx.ChangeTracker.Clear();

        await TestInfra.NovoTorneiosController(c.Ctx, c.Org.Id).AberturaPelaCampanhaDaCategoria(c.Torneio.Id, c.Cat.Id, ligar: true);
        Assert.Equal(AberturaPelaCampanha.Marca, (await c.Ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == c.Cat.Id)).CruzamentoDoMataMata);

        await TestInfra.NovoTorneiosController(c.Ctx, c.Org.Id).AberturaPelaCampanhaDaCategoria(c.Torneio.Id, c.Cat.Id, ligar: false);
        Assert.Null((await c.Ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == c.Cat.Id)).CruzamentoDoMataMata);
    }

    [Fact]
    public async Task Com_o_mata_mata_ja_nascido_nao_muda()
    {
        var c = Montar(campanha: false);
        using var _ = c.Ctx;
        await c.Robo().MontarMataMataDosGruposAsync(c.Cat.Id, c.Torneio.Id);
        c.Ctx.ChangeTracker.Clear();
        var antes = (await c.Ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == c.Cat.Id)).CruzamentoDoMataMata;

        await TestInfra.NovoTorneiosController(c.Ctx, c.Org.Id).AberturaPelaCampanhaDaCategoria(c.Torneio.Id, c.Cat.Id, ligar: true);

        Assert.Equal(antes, (await c.Ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == c.Cat.Id)).CruzamentoDoMataMata);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_muda()
    {
        var c = Montar(campanha: false, deixarAberto: true);
        using var _ = c.Ctx;

        var r = await TestInfra.NovoTorneiosController(c.Ctx, c.Duplas["1A"].Jogador1Id)
            .AberturaPelaCampanhaDaCategoria(c.Torneio.Id, c.Cat.Id, ligar: true);

        Assert.IsType<ForbidResult>(r);
        Assert.Null((await c.Ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == c.Cat.Id)).CruzamentoDoMataMata);
    }
}
