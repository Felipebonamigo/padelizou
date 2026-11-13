using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 13/11/2026 — QUEM TEM O JOGO MUDADO DE HORÁRIO FICA SABENDO (item 4 de PROXIMOS-PASSOS.md).
//
// 🗣️ Felipe, depois da análise: *"pode começar pelo item 1, 2, 3, 4"*. Item 4: hoje NENHUMA troca
// de horário avisa ninguém — trocar, setas, definir na mão, adicionar horário, aplicar o
// planejamento com a chave pública. O jogador se programa pelo horário que viu quando as chaves
// saíram, e horário mudado sem aviso é W.O. na certa.
//
// 🔑 O DESENHO:
//   - As ações anotam o que mudou (lido do ChangeTracker ANTES de gravar: o horário de antes é o
//     do banco), e o aviso sai depois de 3 MINUTOS SEM MUDANÇA naquele jogo. Arrumar a grade com
//     as setas é uma rajada de cliques: aviso por clique encheria o celular de todo mundo e
//     queimaria a cota de e-mail (100/dia). Jogo que volta pro horário de antes não avisa nada.
//   - Um aviso por PESSOA, com todos os jogos dela que mudaram.
//   - Só com a chave PUBLICADA (antes disso ninguém tem horário pra perder) e só jogo AGENDADO.
//   - Canal: app + e-mail, sem WhatsApp — o mesmo do "Chaves saíram", pelo mesmo motivo (rajada
//     de texto quase igual é a assinatura de spam que a Meta lê).
//   - Em memória, como a FilaDeAvisos: aviso é perecível; um restart perde o que estava esperando.
public class AvisoDeHorarioMudadoTests
{
    private static readonly DateTime Quinta = new(2026, 11, 12);
    private static readonly DateTime Sexta = Quinta.AddDays(1);
    private static readonly DateTime Agora = new(2026, 11, 12, 15, 0, 0);

    // ── 1. A espera: só sai depois de 3 minutos quietos ───────────────────────────────────────

    [Fact]
    public void Mudanca_recente_ainda_espera()
    {
        var pendentes = new AvisosDeHorarioPendentes();
        pendentes.Anotar(1, new[] { new AvisoDeHorarioMudado.Mudanca(10, Quinta.AddHours(18), Quinta.AddHours(19)) }, Agora);

        Assert.Empty(pendentes.Tirar(Agora.AddMinutes(2)));
    }

    [Fact]
    public void Depois_de_3_minutos_quietos_sai_uma_vez_so()
    {
        var pendentes = new AvisosDeHorarioPendentes();
        pendentes.Anotar(1, new[] { new AvisoDeHorarioMudado.Mudanca(10, Quinta.AddHours(18), Quinta.AddHours(19)) }, Agora);

        var vencidos = pendentes.Tirar(Agora.AddMinutes(3));

        Assert.Equal(new AvisosDeHorarioPendentes.Vencido(1, 10, Quinta.AddHours(18)), Assert.Single(vencidos));
        Assert.Empty(pendentes.Tirar(Agora.AddMinutes(10)));
    }

    [Fact]
    public void Varias_mudancas_no_mesmo_jogo_guardam_o_horario_de_antes_da_PRIMEIRA_e_esperam_a_ULTIMA()
    {
        var pendentes = new AvisosDeHorarioPendentes();
        pendentes.Anotar(1, new[] { new AvisoDeHorarioMudado.Mudanca(10, Quinta.AddHours(18), Quinta.AddHours(19)) }, Agora);
        pendentes.Anotar(1, new[] { new AvisoDeHorarioMudado.Mudanca(10, Quinta.AddHours(19), Quinta.AddHours(20)) }, Agora.AddMinutes(2));

        Assert.Empty(pendentes.Tirar(Agora.AddMinutes(4)));     // a última foi há 2 min
        var vencido = Assert.Single(pendentes.Tirar(Agora.AddMinutes(5)));
        Assert.Equal(Quinta.AddHours(18), vencido.Antes);        // o que o jogador sabia
    }

    private static AvisoDeHorarioMudado.Mudanca Mexeu(int partidaId) =>
        new(partidaId, Quinta.AddHours(18), Quinta.AddHours(19));

    // A espera é do TORNEIO: arrumar a grade é mexer em vários jogos, e quem está em dois deles
    // recebe UM aviso com os dois — não um por jogo, com minutos de diferença.
    [Fact]
    public void A_espera_e_do_TORNEIO_mexer_em_outro_jogo_dele_adia_tudo_e_outro_torneio_nao()
    {
        var pendentes = new AvisosDeHorarioPendentes();
        pendentes.Anotar(1, new[] { Mexeu(10) }, Agora);
        pendentes.Anotar(2, new[] { Mexeu(20) }, Agora);
        pendentes.Anotar(1, new[] { Mexeu(11) }, Agora.AddMinutes(2));

        Assert.Equal(new[] { 20 }, pendentes.Tirar(Agora.AddMinutes(3)).Select(v => v.PartidaId));
        Assert.Equal(new[] { 10, 11 }, pendentes.Tirar(Agora.AddMinutes(5)).Select(v => v.PartidaId).OrderBy(x => x));
    }

    // No dia do torneio o organizador remarca o tempo todo — um clique a cada 2 minutos nunca dá
    // 3 minutos quietos, e o jogo que foi pra mais cedo não pode esperar ele parar.
    [Fact]
    public void Grade_mexida_sem_parar_nao_segura_o_aviso_pra_sempre_sai_em_10_minutos()
    {
        var pendentes = new AvisosDeHorarioPendentes();
        for (int minuto = 0; minuto <= 8; minuto += 2)
            pendentes.Anotar(1, new[] { Mexeu(10 + minuto) }, Agora.AddMinutes(minuto));

        Assert.Empty(pendentes.Tirar(Agora.AddMinutes(9)));
        Assert.Equal(5, pendentes.Tirar(Agora.AddMinutes(10)).Count);
    }

    // ── 2. O envio ────────────────────────────────────────────────────────────────────────────

    // Quinta 18:00–20:30 e sexta 18:00–22:10, uma quadra, 9 duplas, chave APROVADA (pública).
    private static async Task<(DbPadelContext ctx, Torneio torneio, Jogador org)> PublicadoAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, 9);
        torneio.Nome = "Last Dance";
        torneio.DataInicio = Quinta;
        torneio.DataFim = Sexta.AddDays(1);
        torneio.HoraInicioDoDia = new TimeSpan(18, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(18, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(22, 10, 0);
        torneio.HorariosPorDia = "2026-11-12=18:00-20:30";
        torneio.QuantidadeQuadras = 1;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra A" });
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id, sortearMesmoSemCaber: true);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id, avisarJogadores: false);
        ctx.ChangeTracker.Clear();
        var publicado = await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id);
        Assert.True(AprovacaoDeChaves.ChavePublicada(publicado));
        return (ctx, publicado, org);
    }

    // A grade deste torneio dá hora a todo jogo de grupo; um sem hora é a montagem que quebrou.
    private static DateTime Hora(Partida jogo) =>
        jogo.HorarioPrevisto ?? throw new InvalidOperationException($"O jogo {jogo.Id} nasceu sem horário.");

    private static async Task<List<int>> JogadoresDoJogoAsync(DbPadelContext ctx, int partidaId)
    {
        var p = await ctx.Partidas.AsNoTracking().Include(x => x.Dupla1).Include(x => x.Dupla2).SingleAsync(x => x.Id == partidaId);
        return new int?[] { p.Dupla1.Jogador1Id, p.Dupla1.Jogador2Id, p.Dupla2.Jogador1Id, p.Dupla2.Jogador2Id }
            .OfType<int>().Distinct().ToList();
    }

    private static async Task<Partida> MoverAsync(DbPadelContext ctx, Partida jogo, DateTime novo)
    {
        var rastreado = await ctx.Partidas.SingleAsync(p => p.Id == jogo.Id);
        rastreado.HorarioPrevisto = novo;
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        return rastreado;
    }

    [Fact]
    public async Task Os_quatro_do_jogo_recebem_o_horario_novo_e_o_de_antes()
    {
        var (ctx, torneio, _) = await PublicadoAsync();
        var jogo = await ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneio.Id && p.Status == "Agendada")
            .OrderBy(p => p.HorarioPrevisto).FirstAsync();
        var antes = Hora(jogo);
        await MoverAsync(ctx, jogo, antes.AddMinutes(50));
        var push = Substitute.For<IPushNotificationService>();

        int enviados = await new AvisoDeHorarioMudado(ctx, push).EnviarAsync(new[] { new AvisosDeHorarioPendentes.Vencido(torneio.Id, jogo.Id, antes) });

        var jogadores = await JogadoresDoJogoAsync(ctx, jogo.Id);
        Assert.Equal(jogadores.Count, enviados);
        foreach (var jogadorId in jogadores)
            await push.Received(1).EnviarParaJogadorAsync(jogadorId, "Seu jogo mudou de horário",
                Arg.Is<string>(c => c != null && c.Contains("Last Dance") && c.Contains($"{antes:HH:mm}") && c.Contains($"{antes.AddMinutes(50):HH:mm}")),
                Arg.Any<string?>(), AlcanceDoAviso.SoApp);
    }

    // A mesma régua do "Chaves saíram" (AvisarChavesPublicadasAsync): na linha de TIME o
    // Jogador1Id é o organizador, e "seu jogo mudou" no celular dele seria o jogo de um time.
    [Fact]
    public async Task Dupla_de_TIME_fica_de_fora_o_Jogador1_dela_e_o_organizador()
    {
        var (ctx, torneio, _) = await PublicadoAsync();
        var jogo = await ctx.Partidas.Include(p => p.Dupla1).Include(p => p.Dupla2)
            .FirstAsync(p => p.TorneioId == torneio.Id && p.Status == "Agendada");
        jogo.Dupla1.NomeTime = "Os Fortes";
        var antes = Hora(jogo);
        jogo.HorarioPrevisto = antes.AddMinutes(50);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var push = Substitute.For<IPushNotificationService>();

        await new AvisoDeHorarioMudado(ctx, push).EnviarAsync(new[] { new AvisosDeHorarioPendentes.Vencido(torneio.Id, jogo.Id, antes) });

        await push.DidNotReceive().EnviarParaJogadorAsync(jogo.Dupla1.Jogador1Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.Received(1).EnviarParaJogadorAsync(jogo.Dupla2.Jogador1Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Quem_teve_dois_jogos_mudados_recebe_UM_aviso_com_os_dois()
    {
        var (ctx, torneio, _) = await PublicadoAsync();
        var jogos = await ctx.Partidas.AsNoTracking().Include(p => p.Dupla1).Include(p => p.Dupla2)
            .Where(p => p.TorneioId == torneio.Id && p.Status == "Agendada").ToListAsync();
        // Uma dupla com dois jogos: a do primeiro jogo, no segundo jogo dela.
        var primeiro = jogos.OrderBy(p => p.HorarioPrevisto).First();
        var dupla = primeiro.Dupla1Id;
        var segundo = jogos.First(p => p.Id != primeiro.Id && (p.Dupla1Id == dupla || p.Dupla2Id == dupla));
        var vencidos = new List<AvisosDeHorarioPendentes.Vencido>();
        foreach (var jogo in new[] { primeiro, segundo })
        {
            vencidos.Add(new(torneio.Id, jogo.Id, jogo.HorarioPrevisto));
            await MoverAsync(ctx, jogo, Hora(jogo).AddMinutes(5));
        }
        var push = Substitute.For<IPushNotificationService>();

        await new AvisoDeHorarioMudado(ctx, push).EnviarAsync(vencidos);

        var jogadorDaDupla = primeiro.Dupla1.Jogador1Id;
        await push.Received(1).EnviarParaJogadorAsync(jogadorDaDupla, "Seus jogos mudaram de horário",
            Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.SoApp);
        await push.DidNotReceive().EnviarParaJogadorAsync(jogadorDaDupla, "Seu jogo mudou de horário",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Jogo_que_voltou_pro_horario_de_antes_nao_avisa_ninguem()
    {
        var (ctx, torneio, _) = await PublicadoAsync();
        var jogo = await ctx.Partidas.AsNoTracking().FirstAsync(p => p.TorneioId == torneio.Id && p.Status == "Agendada");
        var push = Substitute.For<IPushNotificationService>();

        // Anotado com o horário de antes IGUAL ao de agora: mexeu e desfez dentro da espera.
        int enviados = await new AvisoDeHorarioMudado(ctx, push).EnviarAsync(new[] { new AvisosDeHorarioPendentes.Vencido(torneio.Id, jogo.Id, jogo.HorarioPrevisto) });

        Assert.Equal(0, enviados);
        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Chave_ainda_nao_publicada_nao_avisa()
    {
        var (ctx, torneio, _) = await PublicadoAsync();
        var t = await ctx.Torneios.SingleAsync(x => x.Id == torneio.Id);
        t.Status = AprovacaoDeChaves.Pendente;
        await ctx.SaveChangesAsync();
        var jogo = await ctx.Partidas.AsNoTracking().FirstAsync(p => p.TorneioId == torneio.Id && p.Status == "Agendada");
        var antes = jogo.HorarioPrevisto;
        await MoverAsync(ctx, jogo, Hora(jogo).AddMinutes(50));
        var push = Substitute.For<IPushNotificationService>();

        int enviados = await new AvisoDeHorarioMudado(ctx, push).EnviarAsync(new[] { new AvisosDeHorarioPendentes.Vencido(torneio.Id, jogo.Id, antes) });

        Assert.Equal(0, enviados);
    }

    [Fact]
    public async Task Jogo_que_ja_comecou_ou_acabou_nao_avisa()
    {
        var (ctx, torneio, _) = await PublicadoAsync();
        var jogo = await ctx.Partidas.SingleAsync(p => p.Id == ctx.Partidas.Where(x => x.TorneioId == torneio.Id).Min(x => x.Id));
        var antes = jogo.HorarioPrevisto;
        jogo.HorarioPrevisto = Hora(jogo).AddMinutes(50);
        jogo.Status = "Em Andamento";
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var push = Substitute.For<IPushNotificationService>();

        int enviados = await new AvisoDeHorarioMudado(ctx, push).EnviarAsync(new[] { new AvisosDeHorarioPendentes.Vencido(torneio.Id, jogo.Id, antes) });

        Assert.Equal(0, enviados);
    }

    // ── 3. As ações anotam o que mudou ────────────────────────────────────────────────────────

    private static List<AvisosDeHorarioPendentes.Vencido> TudoQueFoiAnotado(AvisosDeHorarioPendentes pendentes) =>
        pendentes.Tirar(DateTime.Now.AddHours(1));

    [Fact]
    public async Task Trocar_horario_anota_os_dois_jogos_com_o_horario_de_antes()
    {
        var (ctx, torneio, org) = await PublicadoAsync();
        var dois = await ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneio.Id && p.Status == "Agendada")
            .OrderBy(p => p.HorarioPrevisto).Take(2).ToListAsync();
        var pendentes = new AvisosDeHorarioPendentes();

        await TestInfra.NovoTorneiosController(ctx, org.Id, horarios: pendentes)
            .TrocarHorario(torneio.Id, dois[0].Id.ToString(), dois[1].Id.ToString());

        var anotados = TudoQueFoiAnotado(pendentes);
        Assert.Equal(dois.Select(p => (p.Id, p.HorarioPrevisto)).OrderBy(x => x.Id),
            anotados.Select(v => (v.PartidaId, v.Antes)).OrderBy(x => x.PartidaId));
        Assert.All(anotados, v => Assert.Equal(torneio.Id, v.TorneioId));
    }

    // Antes de publicar, o jogador nunca viu horário nenhum: "passou de 18:00 pra 18:50" seria de
    // uma hora que ele não conhecia. Anotar e conferir só na hora de mandar não bastaria — a chave
    // publicada DENTRO da espera de 3 minutos mandaria a mudança feita ainda em aprovação.
    [Fact]
    public async Task Mexer_na_grade_antes_de_publicar_nao_anota_nada()
    {
        var (ctx, torneio, org) = await PublicadoAsync();
        var emAprovacao = await ctx.Torneios.SingleAsync(x => x.Id == torneio.Id);
        emAprovacao.Status = AprovacaoDeChaves.Pendente;
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        var dois = await ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneio.Id && p.Status == "Agendada")
            .OrderBy(p => p.HorarioPrevisto).Take(2).ToListAsync();
        var pendentes = new AvisosDeHorarioPendentes();

        await TestInfra.NovoTorneiosController(ctx, org.Id, horarios: pendentes)
            .TrocarHorario(torneio.Id, dois[0].Id.ToString(), dois[1].Id.ToString());

        // A troca ACONTECEU (senão o teste passaria por não ter mudado nada) — só não foi anotada.
        ctx.ChangeTracker.Clear();
        var trocado = await ctx.Partidas.AsNoTracking().SingleAsync(p => p.Id == dois[0].Id);
        Assert.Equal(dois[1].HorarioPrevisto, trocado.HorarioPrevisto);
        Assert.Empty(TudoQueFoiAnotado(pendentes));
    }

    [Fact]
    public async Task Definir_horario_na_mao_anota_o_jogo()
    {
        var (ctx, torneio, org) = await PublicadoAsync();
        var ultimo = await ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneio.Id && p.Status == "Agendada")
            .OrderByDescending(p => p.HorarioPrevisto).FirstAsync();
        var pendentes = new AvisosDeHorarioPendentes();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, horarios: pendentes);

        await controller.DefinirHorario(torneio.Id, ultimo.Id.ToString(), Hora(ultimo).AddMinutes(10));

        Assert.Null(controller.TempData["Erro"]);
        var anotado = Assert.Single(TudoQueFoiAnotado(pendentes));
        Assert.Equal((ultimo.Id, ultimo.HorarioPrevisto), (anotado.PartidaId, anotado.Antes));
    }

    [Fact]
    public async Task Adicionar_horario_no_meio_anota_os_jogos_que_andaram()
    {
        var (ctx, torneio, org) = await PublicadoAsync();
        var corte = Quinta.AddHours(18).AddMinutes(50);
        var queAndam = await ctx.Partidas.AsNoTracking()
            .Where(p => p.TorneioId == torneio.Id && p.Status == "Agendada" && p.HorarioPrevisto >= corte && p.HorarioPrevisto < Sexta)
            .Select(p => p.Id).ToListAsync();
        Assert.NotEmpty(queAndam);
        var pendentes = new AvisosDeHorarioPendentes();

        await TestInfra.NovoTorneiosController(ctx, org.Id, horarios: pendentes)
            .InserirHorario(torneio.Id, Quinta, corte.TimeOfDay, "meio", "esticar");

        Assert.Equal(queAndam.OrderBy(x => x), TudoQueFoiAnotado(pendentes).Select(v => v.PartidaId).OrderBy(x => x));
    }

    [Fact]
    public async Task Aplicar_o_planejamento_com_a_chave_publica_anota_os_jogos_remarcados()
    {
        var (ctx, torneio, org) = await PublicadoAsync();
        var antes = await ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneio.Id)
            .ToDictionaryAsync(p => p.Id, p => p.HorarioPrevisto);
        var pendentes = new AvisosDeHorarioPendentes();

        // A quinta passa a ir até 22:10 como a sexta: a grade é refeita e jogos da sexta descem pra quinta.
        await TestInfra.NovoTorneiosController(ctx, org.Id, horarios: pendentes)
            .AplicarPlanejamento(torneio.Id, limites: "", porDia: true);

        ctx.ChangeTracker.Clear();
        var depois = await ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneio.Id)
            .ToDictionaryAsync(p => p.Id, p => p.HorarioPrevisto);
        var mudaram = antes.Where(a => depois[a.Key] != a.Value).Select(a => a.Key).OrderBy(x => x).ToList();
        Assert.NotEmpty(mudaram);
        Assert.Equal(mudaram, TudoQueFoiAnotado(pendentes).Select(v => v.PartidaId).OrderBy(x => x));
    }

    [Fact]
    public void O_aviso_tem_relogio_e_esta_registrado()
    {
        var program = File.ReadAllText(Path.Combine(Raiz(), "Padelizou", "Program.cs"));

        Assert.Contains("AddSingleton<AvisosDeHorarioPendentes>()", program);
        Assert.Contains("AddScoped<AvisoDeHorarioMudado>()", program);
        Assert.Contains("AddHostedService<AvisoDeHorarioMudadoBackgroundService>()", program);
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
