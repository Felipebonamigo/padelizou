using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit.Abstractions;

namespace Padelizou.Tests;

// O "RECALCULAR HORÁRIOS" APERTADO DUAS VEZES, SEM NADA MUDADO, TEM QUE DAR A MESMA GRADE.
//
// ⚠️ CONFRONTOS FIXOS, E ISSO É O PONTO DO ARQUIVO (10/09/2026). O teste irmão que mede a mesma
// promessa pelo caminho completo — `GradeDoErMedidaTests.Refazer_grade_sem_nada_mudado_reproduz_a_
// grade_do_sorteio` — passa pelo `GerarChaves`, que embaralha com `Guid.NewGuid()`: ele só acusa o
// defeito no sorteio que der a azar certo, e por isso viveu três dias como "teste instável" em vez
// de "defeito anotado". Aqui os jogos são criados à mão, na mesma ordem, sempre: o teste acusa ou
// não acusa, e nunca "às vezes".
//
// 🕳️ O DEFEITO QUE ELE TRAVA: o recálculo zerava `HorarioPrevisto` e `NomeQuadra` e DEIXAVA o
// `ClubeId` — o carimbo do slot ANTIGO (`OrdemDeLiberacao.CarimbarOClube`). O reparo que fecha o
// recálculo (`ReparoDaGrade`) pergunta a `TrocaDeHorario.Lado.ClubeDaVaga` em que clube fica a
// vaga, e essa pergunta era respondida pelo carimbo velho — o clube de um horário que o jogo já
// não ocupava. No SORTEIO o mesmo reparo roda ANTES do carimbo, com `ClubeId` nulo, e a resposta
// vem da quadra, que é a certa. Duas respostas pra mesma pergunta, e o reparo recusava num caminho
// a troca que aceitava no outro: mesmas entradas, mesmo motor, grade diferente.
//
// Medido com confrontos fixos antes da correção: em 45 de 160 formas de torneio a grade saía
// diferente, e em nenhuma forma SEM categoria presa em casa — é a régua de sede (`presa`, em
// `TrocaDeHorario.NaoPodeIrPraVagaDe`) que lê o carimbo. O encaixe nunca diferia: a divergência
// era 100% do reparo.
public class RefazerAGradeEmDoisClubesTests
{
    private readonly ITestOutputHelper _saida;
    public RefazerAGradeEmDoisClubesTests(ITestOutputHelper saida) => _saida = saida;

    private static readonly DateTime Sexta = new(2026, 9, 11, 18, 0, 0);
    private static readonly DateTime Sabado = new(2026, 9, 12);

    // Seis categorias de 4 duplas, todos contra todos: 36 jogos. Cinco quadras em casa o fim de
    // semana inteiro e duas alugadas só na manhã de sábado — a forma do Er, que é onde o defeito
    // apareceu. A 1ª categoria é PRESA EM CASA (não joga no alugado), e é ela que faz a régua de
    // sede entrar na conta do reparo.
    private static (Torneio torneio, Jogador organizador) Montar(DbPadelContext ctx)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var casa = new Clube { Nome = "Er Padel" };
        var alugado = new Clube { Nome = "Radar" };
        ctx.Clubes.AddRange(casa, alugado);
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "Etapa de dois clubes", Codigo = "D2C",
            Status = "Chaves em Sorteio",
            ClubeId = casa.Id,
            DataInicio = Sexta, DataFim = Sabado.AddDays(1),
            HoraInicioDoDia = new TimeSpan(18, 0, 0),
            HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            QuantidadeQuadras = 7,
            TempoPrevistoPartidaMinutos = 50,
        };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();

        for (int i = 1; i <= 5; i++)
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = $"Arena {i}", ClubeId = casa.Id });
        for (int i = 1; i <= 2; i++)
            ctx.Quadras.Add(new Quadra
            {
                TorneioId = torneio.Id, Nome = $"Radar {i}", ClubeId = alugado.Id,
                DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12).AddMinutes(10),
            });

        var categorias = Enumerable.Range(1, 6).Select(i => new Categoria
        {
            Nome = $"Categoria {i}", Codigo = $"C{i}", Torneio = torneio,
            PodeJogarNaSedeExtra = i > 1,
        }).ToList();
        ctx.Categorias.AddRange(categorias);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        int proximo = 1;
        var duplas = new List<Dupla>();
        foreach (var categoria in categorias)
            for (int i = 0; i < 4; i++)
            {
                var j1 = TestInfra.NovoJogador(proximo++);
                var j2 = TestInfra.NovoJogador(proximo++);
                ctx.Jogadores.AddRange(j1, j2);
                var dupla = new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2, Grupo = "A" };
                duplas.Add(dupla);
            }

        // Impedimentos e concentrações espalhados de forma FIXA — de 7 em 7 e de 11 em 11, pra não
        // caírem todos na mesma categoria (mesmo critério de ReparoMedidoNoErTests).
        for (int i = 0; i < 12; i++) duplas[(i * 7) % duplas.Count].ImpedimentoSextaNoite = true;
        for (int i = 0; i < 8; i++)
        {
            var d = duplas[(i * 11 + 3) % duplas.Count];
            if (!d.ImpedimentoSextaNoite) d.ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        }

        ctx.Duplas.AddRange(duplas);
        ctx.SaveChanges();

        int proximoJogo = 1;
        foreach (var categoria in categorias)
        {
            var doGrupo = duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
            for (int a = 0; a < doGrupo.Count; a++)
                for (int b = a + 1; b < doGrupo.Count; b++)
                    ctx.Partidas.Add(new Partida
                    {
                        TorneioId = torneio.Id,
                        CategoriaId = categoria.Id,
                        Dupla1Id = doGrupo[a].Id,
                        Dupla2Id = doGrupo[b].Id,
                        Fase = "Grupo A",
                        Status = "Agendada",
                        Codigo = $"J{proximoJogo++:0000}",
                    });
        }
        ctx.SaveChanges();

        return (torneio, organizador);
    }

    private static async Task<Dictionary<int, (DateTime? Horario, string? Quadra, int? Clube)>> GradeAsync(
        DbPadelContext ctx, int torneioId) =>
        await ctx.Partidas.Where(p => p.TorneioId == torneioId).AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => (p.HorarioPrevisto, p.NomeQuadra, p.ClubeId));

    [Fact]
    public async Task Recalcular_os_horarios_duas_vezes_da_a_mesma_grade()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = Montar(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.RefazerGrade(torneio.Id);
        var primeira = await GradeAsync(ctx, torneio.Id);

        await controller.RefazerGrade(torneio.Id);
        var segunda = await GradeAsync(ctx, torneio.Id);

        var mudaram = primeira
            .Where(p => segunda[p.Key].Horario != p.Value.Horario)
            .Select(p => $"#{p.Key}: {p.Value.Horario:dd HH:mm} {p.Value.Quadra} → {segunda[p.Key].Horario:dd HH:mm} {segunda[p.Key].Quadra}")
            .ToList();

        foreach (var linha in mudaram) _saida.WriteLine(linha);

        Assert.True(mudaram.Count == 0,
            $"nada mudou entre os dois recálculos, e {mudaram.Count} de {primeira.Count} jogos trocaram de horário: "
            + string.Join(", ", mudaram.Take(6)));
    }
}
