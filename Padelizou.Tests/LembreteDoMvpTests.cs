using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// "AINDA FALTA O SEU VOTO NO MVP" — o lembrete antes de a votação fechar.
//
// 🗣️ Felipe, 13/10/2026: *"temos que relembrar também quem ainda não votou, pra votar, por que é
// apenas permitido 24h após o último jogo"*. Desenho aprovado por ele: UM lembrete por torneio,
// 6h antes de fechar (18h depois do último jogo), só pelo app, só pra quem jogou e ainda não
// votou. Na cédula dividida, quem votou numa só é lembrado da outra, pelo nome.
//
// ⚠️ Madrugada não: se a hora cair entre 1h e 7h, ele sai às 7h (MvpDoTorneio.HoraDeAvisar) —
// e ainda antes de fechar, porque a janela inteira é de 24h e o lembrete abre 6h antes.
public class LembreteDoMvpTests
{
    // Último jogo domingo 11/10 às 13h → fecha segunda 12/10 às 13h → lembrete a partir das 7h.
    private static readonly DateTime UltimoJogo = new(2026, 10, 11, 13, 0, 0);
    private static DateTime HoraDoLembrete => UltimoJogo.AddHours(MvpDoTorneio.HorasParaVotar - MvpDoTorneio.HorasAntesDeFecharParaLembrar);

    private static long _cpf = 71000000000;

    private static Jogador Pessoa(DbPadelContext ctx, string nome)
    {
        var j = new Jogador { Nome = nome, Cpf = (++_cpf).ToString("00000000000") };
        ctx.Jogadores.Add(j);
        return j;
    }

    private static (Dupla campea, Dupla vice) Categoria(DbPadelContext ctx, Torneio torneio, string nome)
    {
        var categoria = new Categoria { Nome = nome, Codigo = nome[..2] + torneio.Id, Torneio = torneio };
        ctx.Categorias.Add(categoria);
        var campea = new Dupla { Categoria = categoria, UltimaFase = "Campeao", Jogador1 = Pessoa(ctx, nome + " C1"), Jogador2 = Pessoa(ctx, nome + " C2") };
        var vice = new Dupla { Categoria = categoria, UltimaFase = "Final", Jogador1 = Pessoa(ctx, nome + " V1"), Jogador2 = Pessoa(ctx, nome + " V2") };
        ctx.Duplas.AddRange(campea, vice);
        ctx.SaveChanges();
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Dupla1Id = campea.Id, Dupla2Id = vice.Id,
            VencedorId = campea.Id, Status = "Finalizada", HorarioFimReal = UltimoJogo, Fase = "Final", Codigo = "F" + categoria.Id,
        });
        ctx.SaveChanges();
        return (campea, vice);
    }

    // Torneio que já recebeu o aviso de "vote no MVP" no fim do último jogo.
    private static (Torneio t, (Dupla c, Dupla v) f, (Dupla c, Dupla v) m) Torneio(DbPadelContext ctx, bool comFeminina = true)
    {
        var torneio = new Torneio
        {
            Nome = "Etapa Teste", Codigo = "ET", Status = MvpDoTorneio.StatusFinalizado,
            UsaVotacaoDeMvp = true, AvisoDeMvpEnviadoEm = UltimoJogo.AddMinutes(5),
        };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();
        var m = Categoria(ctx, torneio, "5ª Categoria Masculina");
        var f = comFeminina ? Categoria(ctx, torneio, "5ª Categoria Feminina") : default;
        return (torneio, f, m);
    }

    private static List<(int Quem, string Corpo)> Lembrados(IPushNotificationService push) =>
        push.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync)
                     && (string)c.GetArguments()[1]! == MvpDoTorneio.TituloDoLembrete)
            .Select(c => ((int)c.GetArguments()[0]!, (string)c.GetArguments()[2]!))
            .ToList();

    [Fact]
    public async Task Antes_da_hora_nao_lembra_ninguem()
    {
        // Último jogo às 20h → lembrete abre às 14h do dia seguinte. As duas checagens caem de
        // DIA de propósito: de madrugada nada sai de qualquer jeito, e um "antes da hora" às 6h50
        // passaria pela regra da madrugada sem nunca testar esta (foi o primeiro erro deste teste).
        using var ctx = TestInfra.NovoContexto();
        Torneio(ctx);
        foreach (var p in ctx.Partidas) p.HorarioFimReal = new DateTime(2026, 10, 11, 20, 0, 0);
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        await AvisoDoMvpBackgroundService.LembrarAsync(ctx, push, new DateTime(2026, 10, 12, 13, 50, 0));
        Assert.Empty(Lembrados(push));

        // O controle: dez minutos depois da hora, sai.
        await AvisoDoMvpBackgroundService.LembrarAsync(ctx, push, new DateTime(2026, 10, 12, 14, 1, 0));
        Assert.NotEmpty(Lembrados(push));
    }

    [Fact]
    public async Task Na_hora_lembra_SO_quem_ainda_nao_votou_e_uma_vez_so()
    {
        using var ctx = TestInfra.NovoContexto();
        var (t, _, m) = Torneio(ctx, comFeminina: false);
        // O V1 já votou; os outros três que jogaram, não.
        await MvpDoTorneio.VotarAsync(ctx, t.Id, m.v.Jogador1Id, m.c.Jogador1Id, UltimoJogo.AddHours(1));
        var push = Substitute.For<IPushNotificationService>();

        await AvisoDoMvpBackgroundService.LembrarAsync(ctx, push, HoraDoLembrete.AddMinutes(1));
        await AvisoDoMvpBackgroundService.LembrarAsync(ctx, push, HoraDoLembrete.AddMinutes(10));

        var quem = Lembrados(push).Select(x => x.Quem).OrderBy(x => x).ToList();
        Assert.Equal(new[] { m.c.Jogador1Id, m.c.Jogador2Id!.Value, m.v.Jogador2Id!.Value }.OrderBy(x => x), quem);
        Assert.Contains("13h", Lembrados(push)[0].Corpo);   // diz QUANDO fecha
        Assert.NotNull((await ctx.Torneios.FindAsync(t.Id))!.LembreteDeMvpEnviadoEm);
    }

    [Fact]
    public async Task De_madrugada_espera_as_7h()
    {
        // Último jogo às 9h → lembrete às 3h da madrugada → sai só às 7h, ainda antes de fechar (9h).
        using var ctx = TestInfra.NovoContexto();
        var (t, _, _) = Torneio(ctx, comFeminina: false);
        foreach (var p in ctx.Partidas) p.HorarioFimReal = new DateTime(2026, 10, 11, 9, 0, 0);
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        await AvisoDoMvpBackgroundService.LembrarAsync(ctx, push, new DateTime(2026, 10, 12, 3, 30, 0));
        Assert.Empty(Lembrados(push));

        await AvisoDoMvpBackgroundService.LembrarAsync(ctx, push, new DateTime(2026, 10, 12, 7, 0, 0));
        Assert.NotEmpty(Lembrados(push));
    }

    [Fact]
    public async Task Depois_que_fechou_nao_lembra()
    {
        using var ctx = TestInfra.NovoContexto();
        Torneio(ctx);
        var push = Substitute.For<IPushNotificationService>();

        await AvisoDoMvpBackgroundService.LembrarAsync(ctx, push, UltimoJogo.AddHours(MvpDoTorneio.HorasParaVotar + 1));

        Assert.Empty(Lembrados(push));
    }

    [Fact]
    public async Task Votacao_desligada_nao_lembra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (t, _, _) = Torneio(ctx);
        t.UsaVotacaoDeMvp = false;
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        await AvisoDoMvpBackgroundService.LembrarAsync(ctx, push, HoraDoLembrete.AddMinutes(1));

        Assert.Empty(Lembrados(push));
    }

    [Fact]
    public async Task Na_cedula_dividida_quem_votou_numa_so_e_lembrado_da_outra_pelo_nome()
    {
        using var ctx = TestInfra.NovoContexto();
        var (t, f, m) = Torneio(ctx);
        int soFeminino = m.v.Jogador1Id;    // votou só no MVP Feminino
        int votouNasDuas = m.v.Jogador2Id!.Value;
        await MvpDoTorneio.VotarAsync(ctx, t.Id, soFeminino, f.c.Jogador1Id, UltimoJogo.AddHours(1));
        await MvpDoTorneio.VotarAsync(ctx, t.Id, votouNasDuas, f.c.Jogador1Id, UltimoJogo.AddHours(1));
        await MvpDoTorneio.VotarAsync(ctx, t.Id, votouNasDuas, m.c.Jogador1Id, UltimoJogo.AddHours(1));
        var push = Substitute.For<IPushNotificationService>();

        await AvisoDoMvpBackgroundService.LembrarAsync(ctx, push, HoraDoLembrete.AddMinutes(1));

        var lembrados = Lembrados(push);
        Assert.DoesNotContain(lembrados, x => x.Quem == votouNasDuas);
        var dele = Assert.Single(lembrados, x => x.Quem == soFeminino);
        Assert.Contains("MVP Masculino", dele.Corpo);
        Assert.DoesNotContain("MVP Feminino", dele.Corpo);
    }

    [Fact]
    public async Task Torneio_antigo_cujo_aviso_saiu_ha_dias_nao_entra_na_varredura()
    {
        // Sem este recorte, a primeira passada depois do deploy percorreria todo torneio antigo do
        // banco. Aqui ele nem chega a ser olhado — e nada é gravado nele.
        using var ctx = TestInfra.NovoContexto();
        var (t, _, _) = Torneio(ctx);
        t.AvisoDeMvpEnviadoEm = UltimoJogo.AddDays(-30);
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        await AvisoDoMvpBackgroundService.LembrarAsync(ctx, push, HoraDoLembrete.AddMinutes(1));

        Assert.Empty(Lembrados(push));
        Assert.Null((await ctx.Torneios.FindAsync(t.Id))!.LembreteDeMvpEnviadoEm);
    }
}
