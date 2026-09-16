using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A VARREDURA do lembrete de aula — a ligação, não a régua (essa está em LembreteDaAulaTests).
//
// Aqui se exercita o percurso inteiro: achar a aula certa, avisar as pessoas certas, gravar o
// marco pra não repetir. É onde moram os erros que régua nenhuma pega — o `Include` esquecido,
// a turma mandando três avisos idênticos pro mesmo professor, a aula "Pendente" prometendo o
// que o professor ainda não aceitou.
//
// ⚠️ DOIS CONTEXTOS SOBRE O MESMO BANCO, sempre. Com um só, o EF InMemory costura
// `aula.Aluno` e `aula.LocalAula` sozinho pelo rastreador — o que foi semeado já está na
// memória do contexto — e a varredura passaria VERDE sem os `Include`. Em produção o mesmo
// código quebraria no primeiro `NullReferenceException`, ou pior, deixaria de enxergar a
// preferência do aluno e avisaria quem desligou. (Lição de 16/09/2026.)
public class VarreduraDoLembreteDeAulaTests
{
    private const int ProfessorId = 1;
    private const int AlunoId = 2;
    private const int LocalId = 1;
    private const int CategoriaId = 1;

    // Sábado, 19/09/2026, 19h. A véspera cai às 19h de sexta — hora civilizada.
    private static readonly DateTime QuandoEhAAula = new(2026, 9, 19, 19, 0, 0);
    private static readonly DateTime Vespera = QuandoEhAAula.AddHours(-24);
    private static readonly DateTime EmCimaDaHora = QuandoEhAAula.AddMinutes(-50);

    private static DbContextOptions<DbPadelContext> Banco() =>
        new DbContextOptionsBuilder<DbPadelContext>()
            .UseInMemoryDatabase("lembrete_da_aula_" + Guid.NewGuid())
            .Options;

    private static void SemearElenco(DbContextOptions<DbPadelContext> opcoes,
        bool alunoQuerLembrete = true, bool professorQuerLembrete = true, DateTime? alunoExcluidoEm = null)
    {
        using var ctx = new DbPadelContext(opcoes);
        ctx.Jogadores.Add(new Jogador
        {
            Id = ProfessorId, Nome = "Marcio", Cpf = "55500000101", IsProfessor = true,
            NotificarLembreteDeAula = professorQuerLembrete,
        });
        ctx.Jogadores.Add(new Jogador
        {
            Id = AlunoId, Nome = "Leonardo", Cpf = "55500000102",
            NotificarLembreteDeAula = alunoQuerLembrete, ExcluidoEm = alunoExcluidoEm,
        });
        ctx.LocaisAula.Add(new LocalAula { Id = LocalId, ProfessorId = ProfessorId, Nome = "Wallau", PrecoPadrao = 100m });
        ctx.CategoriasPadrao.Add(new CategoriaPadrao { Id = CategoriaId, Nome = "4ª Masculina", Codigo = "4M", Tipo = "Masculina" });
        ctx.SaveChanges();
    }

    private static void SemearAula(DbContextOptions<DbPadelContext> opcoes, DateTime quando,
        string status = PoliticaAula.Confirmada, int? alunoId = AlunoId, int? marcoJaEnviado = null)
    {
        using var ctx = new DbPadelContext(opcoes);
        ctx.Aulas.Add(new Aula
        {
            ProfessorId = ProfessorId, AlunoId = alunoId, LocalAulaId = LocalId,
            DataHora = quando, Preco = 100m, Status = status,
            NomeAlunoAvulso = alunoId == null ? "Medina" : null,
            UltimoLembreteEnviado = marcoJaEnviado,
        });
        ctx.SaveChanges();
    }

    private static async Task<(int avisos, IPushNotificationService push)> VarrerAsync(
        DbContextOptions<DbPadelContext> opcoes, DateTime agora)
    {
        using var ctx = new DbPadelContext(opcoes);
        var push = Substitute.For<IPushNotificationService>();
        var avisos = await LembreteDaAulaBackgroundService.VarrerAsync(ctx, push, agora);
        return (avisos, push);
    }

    // ── A aula marcada com professor ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Avisa_o_aluno_E_o_professor_e_grava_o_marco()
    {
        var banco = Banco();
        SemearElenco(banco);
        SemearAula(banco, QuandoEhAAula);

        var (avisos, push) = await VarrerAsync(banco, Vespera);

        Assert.Equal(2, avisos);
        await push.Received(1).EnviarParaJogadorAsync(AlunoId, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.Received(1).EnviarParaJogadorAsync(ProfessorId, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());

        using var ctx = new DbPadelContext(banco);
        Assert.Equal(24, ctx.Aulas.Single().UltimoLembreteEnviado);
    }

    [Fact]
    public async Task Sai_SO_pelo_app_sem_email_e_sem_WhatsApp()
    {
        // Decisão do Felipe (16/09/2026), respondendo ao pedido do Maickel: é push e caixa de
        // avisos, e nada mais. A aula é compromisso que a própria pessoa marcou — não vale a
        // cota de e-mail (que já morreu uma vez) nem o chip do WhatsApp (que já foi restrito).
        var banco = Banco();
        SemearElenco(banco);
        SemearAula(banco, QuandoEhAAula);

        var (_, push) = await VarrerAsync(banco, Vespera);

        await push.Received(2).EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
    }

    [Fact]
    public async Task O_aviso_do_aluno_leva_pra_tela_que_tem_o_botao_de_desmarcar()
    {
        // Aviso que cai numa tela sem o botão que ele mesmo sugere não é aviso (lição de 05/08).
        var banco = Banco();
        SemearElenco(banco);
        SemearAula(banco, QuandoEhAAula);

        var (_, push) = await VarrerAsync(banco, Vespera);

        await push.Received(1).EnviarParaJogadorAsync(AlunoId, Arg.Any<string>(), Arg.Any<string>(),
            "/Aulas/MinhasAulas", Arg.Any<AlcanceDoAviso>());
        await push.Received(1).EnviarParaJogadorAsync(ProfessorId, Arg.Any<string>(), Arg.Any<string>(),
            "/Aulas/MinhaAgenda", Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task A_varredura_seguinte_nao_avisa_de_novo()
    {
        // ⚠️ O varredor passa de 15 em 15 minutos e roda de novo a cada deploy. Quem decide se
        // já avisou é a coluna, não o relógio.
        var banco = Banco();
        SemearElenco(banco);
        SemearAula(banco, QuandoEhAAula);

        await VarrerAsync(banco, Vespera);
        var (segunda, push) = await VarrerAsync(banco, Vespera.AddMinutes(15));

        Assert.Equal(0, segunda);
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task Depois_do_de_24h_ainda_sai_o_de_1h()
    {
        var banco = Banco();
        SemearElenco(banco);
        SemearAula(banco, QuandoEhAAula, marcoJaEnviado: 24);

        var (avisos, _) = await VarrerAsync(banco, EmCimaDaHora);

        Assert.Equal(2, avisos);
        using var ctx = new DbPadelContext(banco);
        Assert.Equal(1, ctx.Aulas.Single().UltimoLembreteEnviado);
    }

    [Fact]
    public async Task Aula_de_daqui_a_tres_dias_nem_entra_na_consulta()
    {
        var banco = Banco();
        SemearElenco(banco);
        SemearAula(banco, QuandoEhAAula);

        var (avisos, _) = await VarrerAsync(banco, QuandoEhAAula.AddDays(-3));

        Assert.Equal(0, avisos);
    }

    [Theory]
    [InlineData(PoliticaAula.Pendente)]
    [InlineData(PoliticaAula.Cancelada)]
    [InlineData(PoliticaAula.Recusada)]
    [InlineData(PoliticaAula.ARecuperar)]
    [InlineData(PoliticaAula.Realizada)]
    public async Task So_a_aula_CONFIRMADA_e_lembrada(string status)
    {
        // ⚠️ `PoliticaAula.ContaComoAtiva` inclui "Pendente", e aqui isso seria um erro: pendente
        // é "o professor ainda não aceitou". Mandar "sua aula é amanhã" pra uma aula que pode
        // ser recusada é prometer o que o sistema não tem.
        var banco = Banco();
        SemearElenco(banco);
        SemearAula(banco, QuandoEhAAula, status: status);

        var (avisos, push) = await VarrerAsync(banco, Vespera);

        Assert.Equal(0, avisos);
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task Turma_de_tres_alunos_manda_UM_aviso_so_pro_professor()
    {
        // ⚠️ Turma são TRÊS linhas de Aula no mesmo horário (cada aluno com a cobrança dele).
        // Sem agrupar, o professor levaria três avisos idênticos no mesmo minuto — e é assim
        // que alguém desliga as notificações.
        var banco = Banco();
        SemearElenco(banco);

        using (var ctx = new DbPadelContext(banco))
        {
            var turma = Guid.NewGuid();
            for (var i = 0; i < 3; i++)
            {
                ctx.Jogadores.Add(new Jogador { Id = 10 + i, Nome = $"Aluno {i}", Cpf = $"5550000020{i}" });
                ctx.Aulas.Add(new Aula
                {
                    ProfessorId = ProfessorId, AlunoId = 10 + i, LocalAulaId = LocalId,
                    DataHora = QuandoEhAAula, Preco = 50m, Status = PoliticaAula.Confirmada,
                    TurmaId = turma, QuantidadeAlunos = 3,
                });
            }
            ctx.SaveChanges();
        }

        var (avisos, push) = await VarrerAsync(banco, Vespera);

        // Três alunos + UM professor.
        Assert.Equal(4, avisos);
        await push.Received(1).EnviarParaJogadorAsync(ProfessorId, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());

        using var conferencia = new DbPadelContext(banco);
        Assert.All(conferencia.Aulas.ToList(), a => Assert.Equal(24, a.UltimoLembreteEnviado));
    }

    [Fact]
    public async Task O_aviso_da_turma_diz_quantos_alunos_vem()
    {
        var banco = Banco();
        SemearElenco(banco);

        using (var ctx = new DbPadelContext(banco))
        {
            var turma = Guid.NewGuid();
            for (var i = 0; i < 3; i++)
            {
                ctx.Jogadores.Add(new Jogador { Id = 10 + i, Nome = $"Aluno {i}", Cpf = $"5550000021{i}" });
                ctx.Aulas.Add(new Aula
                {
                    ProfessorId = ProfessorId, AlunoId = 10 + i, LocalAulaId = LocalId,
                    DataHora = QuandoEhAAula, Preco = 50m, Status = PoliticaAula.Confirmada,
                    TurmaId = turma, QuantidadeAlunos = 3,
                });
            }
            ctx.SaveChanges();
        }

        var (_, push) = await VarrerAsync(banco, Vespera);

        await push.Received(1).EnviarParaJogadorAsync(ProfessorId, Arg.Any<string>(),
            Arg.Is<string>(corpo => corpo != null && corpo.Contains("3 alunos")), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Aluno_avulso_nao_tem_como_receber_e_o_professor_recebe_assim_mesmo()
    {
        // Aluno sem conta não tem push, não tem caixa de avisos e não tem preferência. Quem
        // cobre esse caso é o botão manual do professor (Services/ConviteDaAulaMarcada).
        var banco = Banco();
        SemearElenco(banco);
        SemearAula(banco, QuandoEhAAula, alunoId: null);

        var (avisos, push) = await VarrerAsync(banco, Vespera);

        Assert.Equal(1, avisos);
        await push.Received(1).EnviarParaJogadorAsync(ProfessorId, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Quem_desligou_o_lembrete_nas_preferencias_nao_recebe()
    {
        var banco = Banco();
        SemearElenco(banco, alunoQuerLembrete: false);
        SemearAula(banco, QuandoEhAAula);

        var (avisos, push) = await VarrerAsync(banco, Vespera);

        Assert.Equal(1, avisos);
        await push.DidNotReceive().EnviarParaJogadorAsync(AlunoId, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task O_professor_que_desligou_o_lembrete_tambem_fica_de_fora()
    {
        // A preferência vale dos DOIS lados — e o lado do professor tem código próprio, que é
        // justamente onde uma checagem some sem ninguém ver.
        var banco = Banco();
        SemearElenco(banco, professorQuerLembrete: false);
        SemearAula(banco, QuandoEhAAula);

        var (avisos, push) = await VarrerAsync(banco, Vespera);

        Assert.Equal(1, avisos);
        await push.DidNotReceive().EnviarParaJogadorAsync(ProfessorId, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Conta_excluida_nao_recebe_lembrete()
    {
        var banco = Banco();
        SemearElenco(banco, alunoExcluidoEm: new DateTime(2026, 9, 1));
        SemearAula(banco, QuandoEhAAula);

        var (_, push) = await VarrerAsync(banco, Vespera);

        await push.DidNotReceive().EnviarParaJogadorAsync(AlunoId, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── O jogo-aula ──────────────────────────────────────────────────────────────────────────

    private static void SemearJogoAula(DbContextOptions<DbPadelContext> opcoes, DateTime quando,
        string status = "Ativo", int inscritos = 2, bool ultimoNaEspera = false)
    {
        using var ctx = new DbPadelContext(opcoes);
        var jogo = new JogoAula
        {
            Id = 1, ProfessorId = ProfessorId, LocalAulaId = LocalId, CategoriaPadraoId = CategoriaId,
            Modalidade = "Dupla", DataHora = quando, Status = status, LimiteVagas = 4,
        };
        ctx.JogosAula.Add(jogo);

        for (var i = 0; i < inscritos; i++)
        {
            ctx.Jogadores.Add(new Jogador { Id = 20 + i, Nome = $"Inscrito {i}", Cpf = $"5550000030{i}" });
            ctx.InscricoesJogoAula.Add(new InscricaoJogoAula
            {
                JogoAulaId = 1, JogadorId = 20 + i,
                EmListaDeEspera = ultimoNaEspera && i == inscritos - 1,
            });
        }
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Jogo_aula_avisa_os_inscritos_e_o_professor()
    {
        var banco = Banco();
        SemearElenco(banco);
        SemearJogoAula(banco, QuandoEhAAula);

        var (avisos, push) = await VarrerAsync(banco, Vespera);

        // Dois inscritos + o professor.
        Assert.Equal(3, avisos);
        await push.Received(1).EnviarParaJogadorAsync(20, Arg.Any<string>(), Arg.Any<string>(),
            "/JogoAula/Detalhes/1", Arg.Any<AlcanceDoAviso>());

        using var ctx = new DbPadelContext(banco);
        Assert.Equal(24, ctx.JogosAula.Single().UltimoLembreteEnviado);
    }

    [Fact]
    public async Task Jogo_aula_nao_avisa_quem_esta_na_lista_de_espera()
    {
        // Quem está na espera não tem vaga: avisar "seu jogo é amanhã" é prometer o que não é.
        var banco = Banco();
        SemearElenco(banco);
        SemearJogoAula(banco, QuandoEhAAula, inscritos: 2, ultimoNaEspera: true);

        var (avisos, push) = await VarrerAsync(banco, Vespera);

        Assert.Equal(2, avisos);
        await push.DidNotReceive().EnviarParaJogadorAsync(21, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Jogo_aula_cancelado_nao_avisa_ninguem()
    {
        var banco = Banco();
        SemearElenco(banco);
        SemearJogoAula(banco, QuandoEhAAula, status: "Cancelado");

        var (avisos, push) = await VarrerAsync(banco, Vespera);

        Assert.Equal(0, avisos);
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task Jogo_aula_SEM_inscrito_nao_incomoda_o_professor()
    {
        // Não há quem lembrar, e "seu jogo-aula é amanhã, ninguém se inscreveu" é aviso que só
        // serve pra desanimar. ⚠️ E o marco fica NULO de propósito: se alguém se inscrever
        // depois, o lembrete de 1h ainda sai.
        var banco = Banco();
        SemearElenco(banco);
        SemearJogoAula(banco, QuandoEhAAula, inscritos: 0);

        var (avisos, push) = await VarrerAsync(banco, Vespera);

        Assert.Equal(0, avisos);
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);

        using var ctx = new DbPadelContext(banco);
        Assert.Null(ctx.JogosAula.Single().UltimoLembreteEnviado);
    }

    // ── A aula que MUDOU de horário ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Mudar_o_horario_da_aula_ZERA_o_marco_ja_enviado()
    {
        // ⚠️ Sem isto, a aula que já levou o "é amanhã" e foi remarcada pra semana que vem
        // ficaria com o marco gravado e NUNCA receberia o lembrete novo — o defeito mais calado
        // possível: ninguém reclama de um aviso que não chegou.
        using var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Marcio", Login = "marcio", Cpf = "55500000401", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Wallau", PrecoPadrao = 100m };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        var aula = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id, NomeAlunoAvulso = "Medina",
            DataHora = DateTime.Today.AddDays(1).AddHours(19), Preco = 100m,
            Status = PoliticaAula.Confirmada, UltimoLembreteEnviado = 24,
        };
        ctx.Aulas.Add(aula);
        ctx.SaveChanges();

        var novoHorario = DateTime.Today.AddDays(8).AddHours(19);
        await TestInfra.NovoAulasController(ctx, professor.Id).Editar(aula.Id, local.Id,
            data: DataEHoraDoFormulario.ParaCampoDeData(novoHorario),
            hora: DataEHoraDoFormulario.ParaCampoDeHora(novoHorario), preco: 100m);

        Assert.Null((await ctx.Aulas.SingleAsync()).UltimoLembreteEnviado);
    }

    [Fact]
    public async Task Editar_SEM_mexer_no_horario_nao_zera_o_marco()
    {
        // O contrário do de cima: mudar só o preço não pode reabrir um aviso que já saiu.
        using var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Marcio", Login = "marcio2", Cpf = "55500000402", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Wallau", PrecoPadrao = 100m };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        var quando = DateTime.Today.AddDays(1).AddHours(19);
        var aula = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id, NomeAlunoAvulso = "Medina",
            DataHora = quando, Preco = 100m, Status = PoliticaAula.Confirmada, UltimoLembreteEnviado = 24,
        };
        ctx.Aulas.Add(aula);
        ctx.SaveChanges();

        await TestInfra.NovoAulasController(ctx, professor.Id).Editar(aula.Id, local.Id,
            data: DataEHoraDoFormulario.ParaCampoDeData(quando),
            hora: DataEHoraDoFormulario.ParaCampoDeHora(quando), preco: 130m);

        Assert.Equal(24, (await ctx.Aulas.SingleAsync()).UltimoLembreteEnviado);
    }
}
