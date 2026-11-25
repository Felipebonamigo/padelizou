using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A CHAVE DTN DO TRETA (25/11/2026) — ver TRETA.md.
//
// 🗣️ O PDF do Treta Padel: os 2 primeiros de cada grupo vão pro Principal, o ÚLTIMO vai pro
// DTN, o meio é eliminado. No DTN o mata-mata é INVERTIDO: quem perde avança, e quem perde a
// final leva o DTN. No Preto (3 grupos de 3), os 2 melhores 3ºs fazem a semi e o pior 3º espera
// direto na final.
//
// A DTN é uma CATEGORIA à parte (`ChaveDireta` + `PapelDaChave = "DTN"`), com cópias das
// duplas: o motor inteiro é por categoria, e as cópias não deixam o DTN carimbar o pódio do
// módulo.
public class TretaChaveDtnTests
{
    private static readonly DateTime Sabado = new(2026, 12, 5);

    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Categoria Modulo,
        Dictionary<string, Dupla> Duplas)
    {
        public async Task<Categoria?> DtnAsync() =>
            await Ctx.Categorias.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoriaDeOrigemId == Modulo.Id && c.PapelDaChave == ChaveDtn.Dtn);

        public async Task<List<Partida>> JogosDaAsync(Categoria cat) =>
            await Ctx.Partidas.AsNoTracking().Where(p => p.CategoriaId == cat.Id).OrderBy(p => p.Id).ToListAsync();

        // A cópia da dupla `nome` dentro da DTN (mesmos jogadores).
        public async Task<Dupla?> CopiaAsync(Categoria dtn, string nome)
        {
            var original = Duplas[nome];
            return await Ctx.Duplas.AsNoTracking().FirstOrDefaultAsync(d =>
                d.CategoriaId == dtn.Id && d.Jogador1Id == original.Jogador1Id && d.Jogador2Id == original.Jogador2Id);
        }

        public RoboDoChaveamento Robo() => new(Ctx, TestInfra.EstatisticasFalsas());
    }

    // Grupos pelo TAMANHO de cada um. Dentro do grupo, a dupla i vence a j sempre que i < j —
    // a ordem de criação É a classificação. O placar da derrota cresce com o grupo (6×0 no A,
    // 6×1 no B, 6×2 no C...), então entre grupos o último do A é o PIOR e o do C o melhor.
    // `deixarAberto` = o último jogo de grupo fica por jogar.
    private static Cenario Montar(int[] tamanhos, int duplasNoDtn = 1, bool deixarAberto = false)
    {
        var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio
        {
            Nome = "Treta", Codigo = "TRETA", Status = "Fase de Grupos", ForaDoRanking = true,
            DataInicio = Sabado.AddHours(9), DataFim = Sabado.AddDays(1),
            HoraInicioDoDia = new TimeSpan(9, 0, 0), HoraInicioDiasSeguintes = new TimeSpan(9, 0, 0),
            HoraFimDoDia = new TimeSpan(22, 0, 0), QuantidadeQuadras = 3, TempoPrevistoPartidaMinutos = 30,
        };
        ctx.Torneios.Add(torneio);
        var modulo = new Categoria
        {
            Nome = "Branco", Codigo = "BR", Torneio = torneio,
            ClassificadosPorGrupo = 2, DuplasNoDtnPorGrupo = duplasNoDtn,
        };
        ctx.Categorias.Add(modulo);
        ctx.SaveChanges();
        foreach (var q in new[] { "Quadra 1", "Quadra 2", "Quadra 3" })
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = q });

        var duplas = new Dictionary<string, Dupla>();
        var partidas = new List<Partida>();
        int n = 1;
        for (int g = 0; g < tamanhos.Length; g++)
        {
            string letra = ((char)('A' + g)).ToString();
            var grupo = new GrupoTorneio { Categoria = modulo, Nome = "Grupo " + letra };
            ctx.Add(grupo);
            var doGrupo = new List<Dupla>();
            for (int i = 0; i < tamanhos[g]; i++)
            {
                var j1 = TestInfra.NovoJogador(n++);
                var j2 = TestInfra.NovoJogador(n++);
                ctx.Jogadores.AddRange(j1, j2);
                var d = new Dupla
                {
                    Categoria = modulo, Jogador1 = j1, Jogador2 = j2, GrupoTorneio = grupo, Grupo = letra, Pago = true,
                    LadoJogador1 = "Direita",
                };
                ctx.Duplas.Add(d);
                doGrupo.Add(d);
                duplas[$"{i + 1}{letra}"] = d;
            }
            for (int i = 0; i < doGrupo.Count; i++)
                for (int j = i + 1; j < doGrupo.Count; j++)
                    partidas.Add(new Partida
                    {
                        TorneioId = torneio.Id, Categoria = modulo, Dupla1 = doGrupo[i], Dupla2 = doGrupo[j],
                        Codigo = Guid.NewGuid().ToString()[..6], Fase = "Grupo " + letra, Status = "Finalizada",
                        GamesDupla1 = 6, GamesDupla2 = g, SetsDupla1 = 1, SetsDupla2 = 0,
                    });
        }
        ctx.Partidas.AddRange(partidas);
        ctx.SaveChanges();
        foreach (var p in partidas) p.VencedorId = p.Dupla1Id;
        if (deixarAberto)
        {
            var ultimo = partidas.Last();
            ultimo.Status = "Agendada";
            ultimo.VencedorId = null;
            ultimo.GamesDupla1 = null;
            ultimo.GamesDupla2 = null;
        }
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return new Cenario(ctx, torneio, modulo, duplas);
    }

    private static async Task Finalizar(Cenario c, Partida partida, int vencedorId)
    {
        var p = await c.Ctx.Partidas.FirstAsync(x => x.Id == partida.Id);
        p.Status = "Finalizada";
        p.VencedorId = vencedorId;
        p.GamesDupla1 = vencedorId == p.Dupla1Id ? 6 : 3;
        p.GamesDupla2 = vencedorId == p.Dupla1Id ? 3 : 6;
        await c.Ctx.SaveChangesAsync();
        await TestInfra.NovoEncerramento(c.Ctx).AplicarAsync(
            p, acabouDeTerminar: true, new EncerramentoDaPartida.LinksDoAviso(null, null));
        c.Ctx.ChangeTracker.Clear();
    }

    // ── Branco: 2 grupos de 4 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Branco_quando_os_grupos_fecham_nasce_a_DTN_com_os_quartos_numa_final()
    {
        var c = Montar(new[] { 4, 4 });
        using var _ = c.Ctx;

        await c.Robo().MontarMataMataDosGruposAsync(c.Modulo.Id, c.Torneio.Id);

        var dtn = await c.DtnAsync();
        Assert.NotNull(dtn);
        Assert.True(dtn!.ChaveDireta);
        Assert.Equal(c.Torneio.Id, dtn.TorneioId);
        Assert.Contains("DTN", dtn.Nome);

        var quartoA = await c.CopiaAsync(dtn, "4A");
        var quartoB = await c.CopiaAsync(dtn, "4B");
        Assert.NotNull(quartoA);
        Assert.NotNull(quartoB);
        Assert.Equal(2, await c.Ctx.Duplas.CountAsync(d => d.CategoriaId == dtn.Id));
        // A cópia não é inscrição: nasce paga (não vira cobrança) e leva o lado da original.
        Assert.True(quartoA!.Pago);
        Assert.Equal("Direita", quartoA.LadoJogador1);

        var jogos = await c.JogosDaAsync(dtn);
        var final = Assert.Single(jogos);
        Assert.Equal("Final", final.Fase);
        Assert.Equal(new[] { quartoA.Id, quartoB!.Id }.OrderBy(x => x), new[] { final.Dupla1Id, final.Dupla2Id }.OrderBy(x => x));

        // O Principal segue o de sempre: semifinais no módulo.
        Assert.Equal(2, await c.Ctx.Partidas.CountAsync(p => p.CategoriaId == c.Modulo.Id && p.Fase == "Semifinal"));
    }

    [Fact]
    public async Task O_terceiro_do_grupo_de_quatro_fica_de_fora_das_duas_chaves()
    {
        var c = Montar(new[] { 4, 4 });
        using var _ = c.Ctx;

        await c.Robo().MontarMataMataDosGruposAsync(c.Modulo.Id, c.Torneio.Id);

        var dtn = (await c.DtnAsync())!;
        Assert.Null(await c.CopiaAsync(dtn, "3A"));
        Assert.Null(await c.CopiaAsync(dtn, "3B"));
    }

    [Fact]
    public async Task Com_grupo_aberto_a_DTN_ainda_nao_nasce()
    {
        var c = Montar(new[] { 4, 4 }, deixarAberto: true);
        using var _ = c.Ctx;

        await c.Robo().MontarMataMataDosGruposAsync(c.Modulo.Id, c.Torneio.Id);

        Assert.Null(await c.DtnAsync());
    }

    [Fact]
    public async Task Rodar_de_novo_nao_cria_uma_segunda_DTN()
    {
        var c = Montar(new[] { 4, 4 });
        using var _ = c.Ctx;

        await c.Robo().MontarMataMataDosGruposAsync(c.Modulo.Id, c.Torneio.Id);
        c.Ctx.ChangeTracker.Clear();
        await c.Robo().MontarMataMataDosGruposAsync(c.Modulo.Id, c.Torneio.Id);

        Assert.Equal(1, await c.Ctx.Categorias.CountAsync(x => x.CategoriaDeOrigemId == c.Modulo.Id));
        var dtn = (await c.DtnAsync())!;
        Assert.Single(await c.JogosDaAsync(dtn));
    }

    [Fact]
    public async Task Modulo_sem_DTN_configurado_nao_ganha_DTN()
    {
        var c = Montar(new[] { 4, 4 }, duplasNoDtn: 0);
        using var _ = c.Ctx;

        await c.Robo().MontarMataMataDosGruposAsync(c.Modulo.Id, c.Torneio.Id);

        Assert.Null(await c.DtnAsync());
    }

    // ── Verde: um grupo de 3 e um de 4 ────────────────────────────────────────────────────

    [Fact]
    public async Task Verde_a_final_DTN_e_o_terceiro_do_A_contra_o_quarto_do_B()
    {
        var c = Montar(new[] { 3, 4 });
        using var _ = c.Ctx;

        await c.Robo().MontarMataMataDosGruposAsync(c.Modulo.Id, c.Torneio.Id);

        var dtn = (await c.DtnAsync())!;
        var final = Assert.Single(await c.JogosDaAsync(dtn));
        var ids = new[] { (await c.CopiaAsync(dtn, "3A"))!.Id, (await c.CopiaAsync(dtn, "4B"))!.Id }.OrderBy(x => x);
        Assert.Equal(ids, new[] { final.Dupla1Id, final.Dupla2Id }.OrderBy(x => x));
        Assert.Null(await c.CopiaAsync(dtn, "3B"));
    }

    // ── Preto: 3 grupos de 3 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Preto_os_dois_melhores_terceiros_fazem_a_semi_e_o_pior_espera()
    {
        var c = Montar(new[] { 3, 3, 3 });
        using var _ = c.Ctx;

        await c.Robo().MontarMataMataDosGruposAsync(c.Modulo.Id, c.Torneio.Id);

        var dtn = (await c.DtnAsync())!;
        var semi = Assert.Single(await c.JogosDaAsync(dtn));
        Assert.Equal("Semifinal", semi.Fase);
        // O pior 3º é o do A (perdeu de 6×0); os melhores, B e C.
        var ids = new[] { (await c.CopiaAsync(dtn, "3B"))!.Id, (await c.CopiaAsync(dtn, "3C"))!.Id }.OrderBy(x => x);
        Assert.Equal(ids, new[] { semi.Dupla1Id, semi.Dupla2Id }.OrderBy(x => x));
        Assert.NotNull(await c.CopiaAsync(dtn, "3A"));
    }

    [Fact]
    public async Task Preto_quem_PERDE_a_semi_DTN_enfrenta_o_pior_terceiro_na_final()
    {
        var c = Montar(new[] { 3, 3, 3 });
        using var _ = c.Ctx;
        await c.Robo().MontarMataMataDosGruposAsync(c.Modulo.Id, c.Torneio.Id);
        var dtn = (await c.DtnAsync())!;
        var semi = (await c.JogosDaAsync(dtn)).Single();
        var terceiroC = (await c.CopiaAsync(dtn, "3C"))!;
        var terceiroB = (await c.CopiaAsync(dtn, "3B"))!;
        var terceiroA = (await c.CopiaAsync(dtn, "3A"))!;

        // O 3º do C vence a semi: escapa. O do B perde: avança pra final.
        await Finalizar(c, semi, terceiroC.Id);

        var final = Assert.Single((await c.JogosDaAsync(dtn)).Where(p => p.Fase == "Final"));
        Assert.Equal(new[] { terceiroA.Id, terceiroB.Id }.OrderBy(x => x), new[] { final.Dupla1Id, final.Dupla2Id }.OrderBy(x => x));
    }

    // ── A final invertida ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_perde_a_final_DTN_leva_o_DTN_e_ninguem_vira_campeao()
    {
        var c = Montar(new[] { 4, 4 });
        using var _ = c.Ctx;
        await c.Robo().MontarMataMataDosGruposAsync(c.Modulo.Id, c.Torneio.Id);
        var dtn = (await c.DtnAsync())!;
        var final = (await c.JogosDaAsync(dtn)).Single();
        var quartoA = (await c.CopiaAsync(dtn, "4A"))!;
        var quartoB = (await c.CopiaAsync(dtn, "4B"))!;

        await Finalizar(c, final, quartoA.Id);

        Assert.Equal(ChaveDtn.Perdedor, (await c.CopiaAsync(dtn, "4B"))!.UltimaFase);
        Assert.NotEqual("Campeao", (await c.CopiaAsync(dtn, "4A"))!.UltimaFase);
        Assert.False(await c.Ctx.Duplas.AnyAsync(d => d.CategoriaId == dtn.Id && d.UltimaFase == "Campeao"));
        // E o módulo não foi tocado: os quartos originais seguem "Grupos".
        Assert.Equal("Grupos", (await c.Ctx.Duplas.FindAsync(c.Duplas["4B"].Id))!.UltimaFase);
    }
}
