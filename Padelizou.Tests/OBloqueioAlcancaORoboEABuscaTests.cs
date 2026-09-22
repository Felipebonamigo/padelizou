using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// AS DUAS PORTAS QUE O FILTRO DE POST NÃO ALCANÇA.
//
// ⚠️ EXISTEM QUATRO CRIADORES DE `Aula`, e só três passam por um POST do professor: ele mesmo
// (AdicionarManual/Encaixar), o ALUNO (Solicitar) e o ACEITE (ConfirmarSolicitacao). O quarto é
// um ROBÔ — `RenovacaoDaAulaFixa` repõe as aulas fixas sem prazo mantendo 12 semanas à frente,
// sem humano nenhum no meio. Um bloqueio só nos controllers deixaria o professor bloqueado
// GANHANDO aula nova na agenda, criada pelo próprio sistema — exatamente o contrário do pedido.
//
// E a outra ponta é a BUSCA: se ele não pode aceitar, não pode ser oferecido. O aluno que marca
// com um professor bloqueado fica pendurado esperando uma confirmação que não pode acontecer —
// é a mesma distinção do torneio fechado, "descoberta não é permissão", com o sinal trocado.
public class OBloqueioAlcancaORoboEABuscaTests
{
    private static PlanoProfessorSettings Ligado => new() { BloqueioAPartirDe = new DateTime(2026, 01, 01) };

    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar(DateTime? pagaAte)
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador
        {
            Nome = "Marcio", Login = "marcio", Cpf = "55500000001", IsProfessor = true,
            PlanoProfessor = PlanoDoProfessor.Assinante,
            AssinaturaProfessorPagaAte = pagaAte,
        };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Wallau", PrecoPadrao = 100, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Aula Fixa(Jogador professor, LocalAula local, DateTime quando, Guid serie) => new()
    {
        ProfessorId = professor.Id,
        LocalAulaId = local.Id,
        NomeAlunoAvulso = "Leonardo",
        DataHora = quando,
        DuracaoMinutos = 90,
        Preco = 100m,
        Status = PoliticaAula.Confirmada,
        RecorrenciaId = serie,
        RecorrenciaSemFim = true,
    };

    // ── O robô ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_robo_NAO_repoe_aula_de_professor_bloqueado()
    {
        // Mensalidade vencida em 2025: bloqueado com folga.
        var (ctx, professor, local) = Montar(new DateTime(2025, 01, 01));
        using var _ = ctx;

        var serie = Guid.NewGuid();
        var proxima = DateTime.Today.AddDays(3).AddHours(9);
        ctx.Aulas.AddRange(Fixa(professor, local, proxima, serie),
                           Fixa(professor, local, proxima.AddDays(7), serie));
        await ctx.SaveChangesAsync();

        var criadas = await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now, Ligado);

        // ⚠️ E AS DUAS QUE JÁ EXISTEM CONTINUAM LÁ. Bloquear é parar de repor, nunca apagar o
        // que já foi combinado — quem cancela aula marcada é o item 4, um mês depois, e com
        // aviso pro aluno.
        Assert.Equal(0, criadas);
        Assert.Equal(2, await ctx.Aulas.CountAsync());
    }

    [Fact]
    public async Task O_robo_continua_repondo_pra_quem_esta_em_dia()
    {
        var (ctx, professor, local) = Montar(DateTime.Now.AddMonths(2));
        using var _ = ctx;

        var serie = Guid.NewGuid();
        var proxima = DateTime.Today.AddDays(3).AddHours(9);
        ctx.Aulas.AddRange(Fixa(professor, local, proxima, serie),
                           Fixa(professor, local, proxima.AddDays(7), serie));
        await ctx.SaveChangesAsync();

        // O controle: sem ele, uma trava escrita larga demais passaria por "não repõe pro
        // bloqueado" tendo, na verdade, parado o renovador pra todo mundo.
        var criadas = await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now, Ligado);

        Assert.Equal(RenovacaoDaAulaFixa.HorizonteSemanas - 2, criadas);
    }

    [Fact]
    public async Task Com_o_bloqueio_dormente_o_robo_repoe_normalmente()
    {
        var (ctx, professor, local) = Montar(new DateTime(2025, 01, 01));
        using var _ = ctx;

        var serie = Guid.NewGuid();
        var proxima = DateTime.Today.AddDays(3).AddHours(9);
        ctx.Aulas.AddRange(Fixa(professor, local, proxima, serie),
                           Fixa(professor, local, proxima.AddDays(7), serie));
        await ctx.SaveChangesAsync();

        // ⚠️ O dia do deploy: código no ar, `BloqueioAPartirDe` nulo, e nenhuma agenda muda.
        var criadas = await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now, new PlanoProfessorSettings());

        Assert.Equal(RenovacaoDaAulaFixa.HorizonteSemanas - 2, criadas);
    }

    // ── A busca ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Professor_bloqueado_some_da_busca_do_aluno()
    {
        var (ctx, bloqueado, localBloqueado) = Montar(new DateTime(2025, 01, 01));
        using var _ = ctx;

        var emDia = new Jogador
        {
            Nome = "Ana", Login = "ana", Cpf = "55500000002", IsProfessor = true,
            PlanoProfessor = PlanoDoProfessor.Assinante,
            AssinaturaProfessorPagaAte = DateTime.Now.AddMonths(2),
        };
        ctx.Jogadores.Add(emDia);
        await ctx.SaveChangesAsync();

        var localDaAna = new LocalAula { ProfessorId = emDia.Id, Nome = "Alphaville", PrecoPadrao = 100, Ativo = true };
        ctx.LocaisAula.Add(localDaAna);
        await ctx.SaveChangesAsync();

        // Grade cheia nos dois: sem horário livre a busca já os esconderia por falta de oferta,
        // e o teste passaria sem provar nada sobre o bloqueio.
        foreach (var (prof, local) in new[] { (bloqueado, localBloqueado), (emDia, localDaAna) })
            for (var dia = 0; dia < 7; dia++)
                ctx.HorariosDisponiveis.Add(new HorarioDisponivel
                {
                    ProfessorId = prof.Id, LocalAulaId = local.Id, DiaSemana = dia,
                    HoraInicio = TimeSpan.FromHours(8), HoraFim = TimeSpan.FromHours(20),
                    DuracaoMinutos = 60, Ativo = true,
                });
        await ctx.SaveChangesAsync();

        var cidade = new Cidade { Nome = "Gravataí", Estado = "RS" };
        ctx.Cidades.Add(cidade);
        await ctx.SaveChangesAsync();

        ctx.ProfessorCidades.AddRange(
            new ProfessorCidade { ProfessorId = bloqueado.Id, CidadeId = cidade.Id },
            new ProfessorCidade { ProfessorId = emDia.Id, CidadeId = cidade.Id });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, emDia.Id, plano: Ligado);
        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.ObterOfertas(cidade.Id));

        // ⚠️ SE ELE NÃO PODE ACEITAR, NÃO PODE SER OFERECIDO. Deixá-lo na busca produz o pior
        // desfecho possível: o aluno marca, a aula nasce Pendente, e ninguém nunca confirma.
        var nomes = NomesDosProfessores(json.Value!);
        Assert.DoesNotContain(bloqueado.Nome, nomes);
        Assert.Contains(emDia.Nome, nomes);
    }

    private static List<string> NomesDosProfessores(object payload)
    {
        var professores = payload.GetType().GetProperty("professores")!.GetValue(payload)!;
        var lista = new List<string>();
        foreach (var p in (System.Collections.IEnumerable)professores)
            lista.Add((string)p.GetType().GetProperty("Nome")!.GetValue(p)!);
        return lista;
    }
}
