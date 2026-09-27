using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// CADA MARCO PASSA A TER O SEU INTERRUPTOR (27/09/2026). 🗣️ *"muda as notificações das aulas lá
// só pra avisa 1h antes, n quero q me avise um dia antes tbm, ou deixa separado pra escolher
// qual o cara quer ou não, acho q fica melhor"*.
//
// ⚠️ O INTERRUPTOR JÁ EXISTIA — `NotificarLembreteDeAula` —, só que valia pelos DOIS marcos de
// uma vez. Quem achava o da véspera demais só tinha a opção de desligar os dois, e aí perdia
// junto o de 1h, que é o único que ninguém quer perder ("sai de casa"). Por isso a saída não foi
// tirar o de 24h da base inteira: foi separar.
//
// ⚠️ E A VÉSPERA NASCE LIGADA. Desligá-la pra todo mundo por causa de um pedido tiraria, calado,
// um aviso que hoje existe — e quem gostava dele não teria como entender por que parou.
public class EscolherQualLembreteDeAulaTests
{
    private static readonly DateTime Agora = new(2026, 09, 27, 10, 0, 0);

    private static async Task<(DbPadelContext ctx, Jogador prof, Jogador aluno, LocalAula local)> MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var prof = new Jogador { Nome = "Marcio", Cpf = "55500000001", IsProfessor = true };
        var aluno = new Jogador { Nome = "Leonardo", Cpf = "55500000002" };
        ctx.Jogadores.AddRange(prof, aluno);
        await ctx.SaveChangesAsync();

        var local = new LocalAula { ProfessorId = prof.Id, Nome = "Wallau", PrecoPadrao = 100, Ativo = true };
        ctx.LocaisAula.Add(local);
        await ctx.SaveChangesAsync();

        return (ctx, prof, aluno, local);
    }

    private static Aula Marcada(DbPadelContext ctx, Jogador prof, Jogador aluno, LocalAula local, double emHoras)
    {
        var aula = new Aula
        {
            ProfessorId = prof.Id, AlunoId = aluno.Id, LocalAulaId = local.Id,
            DataHora = Agora.AddHours(emHoras), DuracaoMinutos = 60, Preco = 100m,
            Status = PoliticaAula.Confirmada,
        };
        ctx.Aulas.Add(aula);
        ctx.SaveChanges();
        return aula;
    }

    private static async Task<List<int>> AvisadosAsync(DbPadelContext ctx, IPushNotificationService push)
    {
        await LembreteDaAulaBackgroundService.VarrerAsync(ctx, push, Agora);
        return push.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync))
            .Select(c => (int)c.GetArguments()[0]!)
            .ToList();
    }

    // ── O padrão: nada muda pra quem não mexeu ────────────────────────────────────────────

    [Fact]
    public async Task A_vespera_nasce_ligada_pra_todo_mundo()
    {
        var (ctx, prof, aluno, local) = await MontarAsync();
        using var _ = ctx;
        Marcada(ctx, prof, aluno, local, emHoras: 20);   // dentro do marco de 24h

        var push = Substitute.For<IPushNotificationService>();
        var avisados = await AvisadosAsync(ctx, push);

        // ⚠️ O teste do dia do deploy: ninguém perde aviso sem ter pedido.
        Assert.Contains(aluno.Id, avisados);
        Assert.Contains(prof.Id, avisados);
    }

    // ── Separado, que é o pedido ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Desligar_so_a_vespera_cala_o_de_24h_e_MANTEM_o_de_1h()
    {
        var (ctx, prof, aluno, local) = await MontarAsync();
        using var _ = ctx;

        aluno.NotificarVesperaDaAula = false;
        var aula = Marcada(ctx, prof, aluno, local, emHoras: 20);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        Assert.DoesNotContain(aluno.Id, await AvisadosAsync(ctx, push));

        // ⚠️ E O DE 1H CONTINUA CHEGANDO — é o ponto inteiro do pedido. Antes, quem não queria a
        // véspera só podia desligar os dois, e perdia junto o único que ninguém quer perder.
        aula.DataHora = Agora.AddMinutes(40);
        await ctx.SaveChangesAsync();

        var push2 = Substitute.For<IPushNotificationService>();
        Assert.Contains(aluno.Id, await AvisadosAsync(ctx, push2));
    }

    [Fact]
    public async Task Desligar_so_a_ultima_hora_cala_o_de_1h_e_MANTEM_a_vespera()
    {
        // A outra ponta do "escolher qual o cara quer": as duas são independentes de verdade,
        // e não uma escada em que a de baixo depende da de cima.
        var (ctx, prof, aluno, local) = await MontarAsync();
        using var _ = ctx;

        aluno.NotificarLembreteDeAula = false;
        var aula = Marcada(ctx, prof, aluno, local, emHoras: 20);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        Assert.Contains(aluno.Id, await AvisadosAsync(ctx, push));

        aula.DataHora = Agora.AddMinutes(40);
        await ctx.SaveChangesAsync();

        var push2 = Substitute.For<IPushNotificationService>();
        Assert.DoesNotContain(aluno.Id, await AvisadosAsync(ctx, push2));
    }

    [Fact]
    public async Task A_escolha_e_de_CADA_UM_e_nao_da_aula()
    {
        // ⚠️ `UltimoLembreteEnviado` é por AULA e a preferência é por PESSOA. Se o marco fosse
        // consumido só quando alguém quer receber, o professor que desligou a véspera calaria o
        // aviso do aluno dele — e vice-versa.
        var (ctx, prof, aluno, local) = await MontarAsync();
        using var _ = ctx;

        prof.NotificarVesperaDaAula = false;
        Marcada(ctx, prof, aluno, local, emHoras: 20);
        await ctx.SaveChangesAsync();

        var avisados = await AvisadosAsync(ctx, Substitute.For<IPushNotificationService>());

        Assert.Contains(aluno.Id, avisados);
        Assert.DoesNotContain(prof.Id, avisados);
    }

    [Fact]
    public async Task O_marco_e_consumido_mesmo_sem_ninguem_pra_avisar()
    {
        // ⚠️ Senão o varredor tentaria de novo a cada 15 minutos até a aula acontecer — é a
        // razão escrita no próprio serviço, e ela não pode se perder ao separar as preferências.
        var (ctx, prof, aluno, local) = await MontarAsync();
        using var _ = ctx;

        prof.NotificarVesperaDaAula = false;
        aluno.NotificarVesperaDaAula = false;
        var aula = Marcada(ctx, prof, aluno, local, emHoras: 20);
        await ctx.SaveChangesAsync();

        await AvisadosAsync(ctx, Substitute.For<IPushNotificationService>());

        var depois = await ctx.Aulas.FirstAsync();
        Assert.Equal(LembreteDaAula.MarcoDaVespera, depois.UltimoLembreteEnviado);
    }

    // ── A tela ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_tela_de_preferencias_oferece_os_dois_separados()
    {
        var tela = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Auth", "_PreferenciasFields.cshtml"));

        Assert.Contains("notificarVesperaDaAula", tela);

        // ⚠️ O PAR HIDDEN/VALUE=FALSE TAMBÉM NA CAIXA NOVA. A preferência nasce LIGADA, e caixa
        // desmarcada não vai no POST: sem o par, uma aba aberta antes do deploy religaria a
        // véspera de quem desligou a cada salvamento de qualquer outra preferência. É a
        // armadilha que o comentário do arquivo já nomeia pras caixas vizinhas.
        Assert.Contains("name=\"notificarVesperaDaAula\" value=\"false\"", tela);

        // E o rótulo velho mentia sobre o que a caixa faz agora.
        Assert.DoesNotContain("Lembrete das minhas aulas (24h e 1h antes)", tela);
    }

    [Fact]
    public void A_migration_liga_a_vespera_pra_quem_JA_EXISTE()
    {
        // ⚠️ O DEFEITO QUE A SUÍTE NÃO PEGA, E QUE ESTE TESTE EXISTE PRA PEGAR. O `= true` do
        // `Jogador.NotificarVesperaDaAula` é inicializador de PROPRIEDADE: vale pra objeto novo
        // em memória, e NÃO pro backfill das linhas que já estão no banco. O EF gerou
        // `defaultValue: false`, e com ele o deploy desligaria a véspera da base inteira,
        // calado — o oposto da decisão ("ligado; nada muda até a pessoa mexer").
        //
        // Os outros testes daqui passam nos dois casos, porque o InMemory constrói objetos
        // NOVOS, que herdam o `true` do C# e nunca chegam perto do backfill. Ler a migration é
        // grosseiro, mas é a única checagem mecânica possível: o CLAUDE.md já avisa que "EF
        // InMemory nos testes NÃO valida SQL".
        var arquivo = Directory
            .GetFiles(Path.Combine(PastaDoProjeto(), "Migrations"), "*EscolherQualLembreteDeAula.cs")
            .Single();

        var migration = File.ReadAllText(arquivo);
        var trecho = migration[migration.IndexOf("NotificarVesperaDaAula", StringComparison.Ordinal)..];

        Assert.Contains("defaultValue: true", trecho[..Math.Min(400, trecho.Length)]);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
