using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 13/11/2026 — O JOGO QUE FICOU FORA DO PADELÍMETRO ENTRA DEPOIS, SOZINHO (item 3 de PROXIMOS-PASSOS.md).
//
// 🕳️ O Padelímetro é aplicado DEPOIS de o placar ser gravado (`EncerramentoDaPartida`, num
// try/catch — falha ali não pode travar a Mesa). Um restart no meio (o mesmo que deixou a chave
// do ER sem montar em 12/09/2026) ou uma exceção deixava o jogo FORA do número dos quatro, pra
// sempre e em silêncio: nada tentava de novo, e só o replay manual do admin consertava — se
// alguém soubesse que precisava.
//
// ✅ NENHUMA RÉGUA NOVA, igual à VarreduraDaChave: a varredura chama o MESMO
// `PadelimetroService.AplicarAsync`, que já é guardado (partida com linha no extrato não aplica
// de novo) e já decide o que conta. Só o que terminou entre 10 min (não disputar com o
// encerramento em curso) e 48 h atrás (não reescrever história antiga) — e com a trava do torneio.
public class VarreduraDoPadelimetroTests
{
    private static readonly DateTime Agora = new(2026, 11, 13, 20, 0, 0);

    private static Partida Montar(DbPadelContext ctx, DateTime? fim, DateTime? placar = null,
        bool restrito = false, int sufixo = 0)
    {
        var torneio = new Torneio
        {
            Nome = $"Torneio {sufixo}", Codigo = $"TRN{sufixo:00}", Status = "Em Andamento",
            Restrito = restrito, DataInicio = Agora.Date,
        };
        var categoria = new Categoria { Nome = "5ª Categoria Masculina", Codigo = $"C{sufixo:00}", Torneio = torneio };
        var jogadores = Enumerable.Range(1, 4)
            .Select(i => new Jogador { Nome = $"J{sufixo}-{i}", Cpf = $"666{sufixo:000}000{i:02}" })
            .ToArray();
        ctx.Jogadores.AddRange(jogadores);
        var d1 = new Dupla { Categoria = categoria, Jogador1 = jogadores[0], Jogador2 = jogadores[1] };
        var d2 = new Dupla { Categoria = categoria, Jogador1 = jogadores[2], Jogador2 = jogadores[3] };
        ctx.Duplas.AddRange(d1, d2);
        ctx.SaveChanges();

        var partida = new Partida
        {
            CategoriaId = categoria.Id, TorneioId = torneio.Id, Dupla1Id = d1.Id, Dupla2Id = d2.Id,
            Codigo = $"P{sufixo:000}", Status = "Finalizada", Fase = "Fase de Grupos",
            GamesDupla1 = 6, GamesDupla2 = 2, SetsDupla1 = 1, SetsDupla2 = 0, VencedorId = d1.Id,
            HorarioFimReal = fim, PlacarMarcadoEm = placar ?? fim,
        };
        ctx.Partidas.Add(partida);
        ctx.SaveChanges();
        return partida;
    }

    private static VarreduraDoPadelimetro Varredura(DbPadelContext ctx) =>
        new(ctx, new PadelimetroService(ctx), NullLogger<VarreduraDoPadelimetro>.Instance);

    private static int LinhasDa(DbPadelContext ctx, int partidaId) =>
        ctx.HistoricosDePadelimetro.Count(h => h.PartidaId == partidaId);

    [Fact]
    public async Task O_jogo_que_terminou_sem_mexer_no_numero_entra_na_passada()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = Montar(ctx, fim: Agora.AddMinutes(-30));
        Assert.Equal(0, LinhasDa(ctx, partida.Id));

        var aplicadas = await Varredura(ctx).PassarAsync(Agora, CancellationToken.None);

        Assert.Equal(new[] { partida.Id }, aplicadas);
        Assert.Equal(4, LinhasDa(ctx, partida.Id));
        Assert.All(ctx.Jogadores.AsNoTracking().ToList(), j => Assert.NotNull(j.Padelimetro));
    }

    [Fact]
    public async Task Jogo_que_ja_esta_no_numero_nao_entra_de_novo()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = Montar(ctx, fim: Agora.AddMinutes(-30));
        await new PadelimetroService(ctx).AplicarAsync(partida.Id);   // o encerramento fez o trabalho dele

        var aplicadas = await Varredura(ctx).PassarAsync(Agora, CancellationToken.None);

        Assert.Empty(aplicadas);
        Assert.Equal(4, LinhasDa(ctx, partida.Id));
    }

    [Fact]
    public async Task Duas_passadas_aplicam_uma_vez_so()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = Montar(ctx, fim: Agora.AddMinutes(-30));

        await Varredura(ctx).PassarAsync(Agora, CancellationToken.None);
        var segunda = await Varredura(ctx).PassarAsync(Agora.AddMinutes(3), CancellationToken.None);

        Assert.Empty(segunda);
        Assert.Equal(4, LinhasDa(ctx, partida.Id));
    }

    [Fact]
    public async Task Jogo_que_acabou_de_terminar_e_do_encerramento_e_nao_da_varredura()
    {
        // Menos de 10 minutos: o encerramento ainda pode estar aplicando. Disputar com ele seria
        // o caminho pra aplicar duas vezes.
        using var ctx = TestInfra.NovoContexto();
        var partida = Montar(ctx, fim: Agora.AddMinutes(-2));

        var aplicadas = await Varredura(ctx).PassarAsync(Agora, CancellationToken.None);

        Assert.Empty(aplicadas);
        Assert.Equal(0, LinhasDa(ctx, partida.Id));
    }

    [Fact]
    public async Task Jogo_antigo_fica_com_o_replay_do_admin()
    {
        // Mais de 48 horas: aplicar agora poria o jogo DEPOIS de outros que vieram depois dele, e
        // o Elo depende da ordem. Isso é trabalho do replay, que refaz tudo na ordem.
        using var ctx = TestInfra.NovoContexto();
        var partida = Montar(ctx, fim: Agora.AddDays(-3));

        var aplicadas = await Varredura(ctx).PassarAsync(Agora, CancellationToken.None);

        Assert.Empty(aplicadas);
        Assert.Equal(0, LinhasDa(ctx, partida.Id));
    }

    [Fact]
    public async Task Jogo_que_nao_conta_continua_fora()
    {
        using var ctx = TestInfra.NovoContexto();
        var partida = Montar(ctx, fim: Agora.AddMinutes(-30), restrito: true);

        var aplicadas = await Varredura(ctx).PassarAsync(Agora, CancellationToken.None);

        Assert.Empty(aplicadas);
        Assert.Equal(0, LinhasDa(ctx, partida.Id));
    }

    [Fact]
    public async Task Sem_hora_de_fim_vale_a_hora_do_placar()
    {
        // O restart pode cair ANTES de o encerramento carimbar o HorarioFimReal (passo 0 dele).
        using var ctx = TestInfra.NovoContexto();
        var partida = Montar(ctx, fim: null, placar: Agora.AddMinutes(-40));

        var aplicadas = await Varredura(ctx).PassarAsync(Agora, CancellationToken.None);

        Assert.Equal(new[] { partida.Id }, aplicadas);
    }

    [Fact]
    public async Task Varios_jogos_entram_na_ordem_em_que_terminaram()
    {
        // O Elo depende da ordem: quem terminou antes entra antes.
        using var ctx = TestInfra.NovoContexto();
        var depois = Montar(ctx, fim: Agora.AddMinutes(-20), sufixo: 1);
        var antes = Montar(ctx, fim: Agora.AddMinutes(-50), sufixo: 2);

        var aplicadas = await Varredura(ctx).PassarAsync(Agora, CancellationToken.None);

        Assert.Equal(new[] { antes.Id, depois.Id }, aplicadas);
    }

    [Fact]
    public void A_consulta_das_candidatas_traduz_no_Postgres()
    {
        // EF InMemory não valida SQL (CLAUDE.md): `??` entre duas datas, subconsulta no extrato e
        // navegação até o torneio — confere contra o provedor de verdade, apontado pra lugar nenhum.
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        using var ctx = new DbPadelContext(options);

        var sql = Varredura(ctx).Candidatas(Agora).ToQueryString();

        Assert.Contains("SELECT", sql);
        Assert.Contains("NOT EXISTS", sql);
    }

    [Fact]
    public void A_varredura_tem_relogio_e_esta_registrada()
    {
        var program = File.ReadAllText(Path.Combine(Raiz(), "Padelizou", "Program.cs"));

        Assert.Contains("AddScoped<VarreduraDoPadelimetro>()", program);
        Assert.Contains("AddHostedService<VarreduraDoPadelimetroBackgroundService>()", program);
    }

    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a raiz do repositório.");
    }
}
