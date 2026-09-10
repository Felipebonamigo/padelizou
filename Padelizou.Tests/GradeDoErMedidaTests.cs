using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit.Abstractions;

namespace Padelizou.Tests;

// A GRADE DO ER, MEDIDA — não afirmada.
//
// 🗣️ Felipe, 09/09/2026, no Conferir grade do Er em produção, logo depois de um "Refazer grade"
// com o build-880: *17 pontos* — 14 "jogos seguidos" (6 com ZERO horário de folga), 1 impedimento
// ("Dupla 589 joga 11/09 às 19:40, dentro do impedimento Sexta à noite"), 1 concentração e o
// retardatário. *"no botão de recalcular horarios precisa ver isso, respeitar os impedimentos e
// os jogos seguidos"*.
//
// Este arquivo monta um torneio do TAMANHO e da FORMA do Er (64 duplas em 12 categorias, cinco
// quadras em casa o fim de semana inteiro e duas alugadas só na manhã de sábado, 3ª e 4ª presas
// em casa, gente com impedimento, gente concentrada, gente em duas categorias), sorteia pelo
// caminho de verdade (GerarChaves) e passa o Conferir grade em cima. Os números aqui são o que
// a régua promete de verdade; mexer no encaixe sem medir aqui é como as três tentativas
// piores de DescansoNaGradeTests nasceram.
public class GradeDoErMedidaTests
{
    private readonly ITestOutputHelper _saida;
    public GradeDoErMedidaTests(ITestOutputHelper saida) => _saida = saida;

    private static readonly DateTime Sexta = new(2026, 9, 11, 18, 0, 0);
    private static readonly DateTime Sabado = new(2026, 9, 12);

    private static (Torneio torneio, Jogador organizador) MontarOEr(DbPadelContext ctx)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var er = new Clube { Nome = "Er Padel" };
        var radar = new Clube { Nome = "Radar" };
        ctx.Clubes.AddRange(er, radar);
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "2ª Etapa ER PADEL TOUR", Codigo = "EPT2",
            Status = "Chaves em Sorteio",
            ClubeId = er.Id,
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
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = $"Arena {i}", ClubeId = er.Id });
        for (int i = 1; i <= 2; i++)
            ctx.Quadras.Add(new Quadra
            {
                TorneioId = torneio.Id, Nome = $"Radar {i}", ClubeId = radar.Id,
                DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12).AddMinutes(10),
            });

        // 64 duplas em 12 categorias — a forma do Er (3ª e 4ª são as fortes, e ficam em casa).
        var tamanhos = new (string Nome, int Duplas, bool PresaEmCasa)[]
        {
            ("3ª Masculina", 8, true), ("4ª Masculina", 8, true), ("5ª Masculina", 8, false),
            ("6ª Masculina", 8, false), ("7ª Masculina", 4, false),
            ("3ª Feminina", 4, true), ("4ª Feminina", 4, true), ("5ª Feminina", 4, false),
            ("6ª Feminina", 4, false), ("7ª Feminina", 4, false),
            ("Mista A", 4, false), ("Mista B", 4, false),
        };

        var categorias = tamanhos.Select((t, i) => new Categoria
        {
            Nome = t.Nome, Codigo = $"C{i}", Torneio = torneio, PodeJogarNaSedeExtra = !t.PresaEmCasa,
        }).ToList();
        ctx.Categorias.AddRange(categorias);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        int proximo = 1;
        var duplas = new List<Dupla>();
        var jogadores = new List<Jogador>();
        for (int c = 0; c < categorias.Count; c++)
        {
            for (int i = 0; i < tamanhos[c].Duplas; i++)
            {
                var j1 = TestInfra.NovoJogador(proximo++);
                var j2 = TestInfra.NovoJogador(proximo++);
                jogadores.AddRange(new[] { j1, j2 });
                ctx.Jogadores.AddRange(j1, j2);
                duplas.Add(new Dupla { Categoria = categorias[c], Jogador1 = j1, Jogador2 = j2 });
            }
        }

        // Gente em DUAS categorias (3ª Masculina e Mista): é por PESSOA que o descanso conta.
        for (int i = 0; i < 4; i++)
            duplas[64 - 8 + i].Jogador1 = duplas[i].Jogador1;

        // Impedimentos e concentrações, como no Er.
        duplas[10].ImpedimentoSextaNoite = true;
        duplas[21].ImpedimentoSextaNoite = true;
        duplas[33].ImpedimentoSextaNoite = true;
        duplas[45].ImpedimentoSabadoManha = true;
        duplas[50].ImpedimentoSabadoManha = true;
        duplas[55].ImpedimentoSabadoTarde = true;
        duplas[3].ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        duplas[17].ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        duplas[26].ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        duplas[40].ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        duplas[36].ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha;
        duplas[58].ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha;

        ctx.Duplas.AddRange(duplas);
        ctx.SaveChanges();

        return (torneio, organizador);
    }

    private static List<AuditoriaDaGrade.Achado> _ultimosAchados = new();

    private static async Task<Dictionary<string, int>> ConferirAsync(DbPadelContext ctx, int torneioId)
    {
        var torneio = await ctx.Torneios
            .Include(t => t.Categorias).ThenInclude(c => c.Duplas).ThenInclude(d => d.Jogador1)
            .Include(t => t.Categorias).ThenInclude(c => c.Duplas).ThenInclude(d => d.Jogador2)
            .FirstAsync(t => t.Id == torneioId);
        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneioId).ToListAsync();
        var duplas = torneio.Categorias.SelectMany(c => c.Duplas).ToList();
        var sedes = await SedesDoTorneio.CarregarAsync(ctx, torneioId);

        var achados = AuditoriaDaGrade.Conferir(torneio, jogos, duplas, sedes);
        _ultimosAchados = achados;
        return achados.GroupBy(a => a.Regra).ToDictionary(g => g.Key, g => g.Count());
    }

    private void Relatar(string rotulo, Dictionary<string, int> contagem, int jogos)
    {
        _saida.WriteLine($"{rotulo}: {jogos} jogos; " + string.Join(", ", contagem.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}")));
        foreach (var a in _ultimosAchados.Where(a => a.Regra != AuditoriaDaGrade.JogosSeguidos || true))
            _saida.WriteLine($"   {rotulo} · {a.Regra}: {a.Descricao}");
    }

    // Quatro sorteios, porque o sorteio é aleatório e um só mede sorte. Os números de baixo são
    // o TOTAL dos quatro — e o Refazer grade de cada um.
    [Fact]
    public async Task O_sorteio_e_o_refazer_do_Er_respeitam_impedimento_concentracao_e_descanso()
    {
        var total = new Dictionary<string, int>();
        int zeroDeFolga = 0, conflitos = 0, jogos = 0;

        void Somar(Dictionary<string, int> contagem)
        {
            foreach (var (regra, n) in contagem) total[regra] = total.GetValueOrDefault(regra) + n;
            zeroDeFolga += _ultimosAchados.Count(a => a.Regra == AuditoriaDaGrade.JogosSeguidos && a.Descricao.Contains("— 0 horário"));
        }

        for (int rodada = 1; rodada <= 4; rodada++)
        {
            using var ctx = TestInfra.NovoContexto();
            var (torneio, org) = MontarOEr(ctx);
            var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

            await controller.GerarChaves(torneio.Id);
            jogos = await ctx.Partidas.CountAsync(p => p.TorneioId == torneio.Id);
            var sorteio = await ConferirAsync(ctx, torneio.Id);
            Relatar($"Sorteio {rodada}", sorteio, jogos);
            Somar(sorteio);

            await controller.RefazerGrade(torneio.Id);
            var refeito = await ConferirAsync(ctx, torneio.Id);
            Relatar($"Refazer {rodada}", refeito, jogos);
            Somar(refeito);
        }

        _saida.WriteLine("TOTAL (8 grades): " + string.Join(", ", total.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"))
                         + $"; zero de folga={zeroDeFolga}");

        int impedimento = total.GetValueOrDefault(AuditoriaDaGrade.Impedimento);
        int concentracao = total.GetValueOrDefault(AuditoriaDaGrade.Concentracao);
        conflitos = total.GetValueOrDefault(AuditoriaDaGrade.RestricoesEmConflito);

        Assert.Equal(0, total.GetValueOrDefault(AuditoriaDaGrade.PessoaEmDoisJogos));
        Assert.Equal(0, total.GetValueOrDefault(AuditoriaDaGrade.SemHorario));
        Assert.Equal(0, total.GetValueOrDefault(AuditoriaDaGrade.QuadraFechada));
        // Toda violação de impedimento ou concentração tem que ser um jogo que NÃO TEM horário
        // possível — e aí a tela diz isso. Violação sem conflito é defeito do motor.
        Assert.True(impedimento + concentracao <= 2 * conflitos,
            $"violações sem conflito que as explique: impedimento={impedimento}, concentração={concentracao}, conflitos={conflitos}");
        // O DESCANSO, MEDIDO EM 10/09/2026 (4 sorteios + 4 refazeres, 44 jogos cada):
        //   • sem "quem descansou entra antes": sorteio 11-14 seguidos por grade, refazer 8-13;
        //     88 no total, 49 com ZERO horário de folga.
        //   • com a preferência (limiar cheio de 2 horários): sorteio 3-4, refazer 6-9; 46 no
        //     total, 24 com zero de folga.
        //   • com um degrau intermediário ("pelo menos 1 de folga"): 51 no total e a guarda do
        //     Americano (DescansoNaGradeTests) quebrou — descartado.
        // ⚠️ O Refazer sai pior que o sorteio nas quatro medições, e ainda não sei por quê — fica
        // registrado como pergunta aberta, não como regra. Estes tetos são "não piora o que foi
        // medido", com folga pra sorte do sorteio.
        Assert.True(total.GetValueOrDefault(AuditoriaDaGrade.JogosSeguidos) <= 64,
            $"jogos seguidos demais nas 8 grades: {total.GetValueOrDefault(AuditoriaDaGrade.JogosSeguidos)} (medido: 46 com a régua, 88 sem)");
        Assert.True(zeroDeFolga <= 36,
            $"jogos com ZERO horário de folga nas 8 grades: {zeroDeFolga} (medido: 24 com a régua, 49 sem)");
    }
}
