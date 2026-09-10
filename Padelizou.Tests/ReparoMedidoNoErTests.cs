using Padelizou.Models;
using Padelizou.Services;
using Xunit;
using Xunit.Abstractions;

namespace Padelizou.Tests;

// O REPARO, MEDIDO — mesma entrada, com e sem.
//
// ⚠️ POR QUE ESTE ARQUIVO EXISTE, SEPARADO DO GradeDoErMedidaTests (10/09/2026): aquele passa pelo
// `GerarChaves`, que embaralha com `Guid.NewGuid()`. Duas execuções do MESMO código dão números
// diferentes — medi 20, 29 e 28 pontos em três rodadas seguidas sem mudar uma linha. Comparar
// "antes" e "depois" ali é comparar sorteios, não código, e foi o que quase me fez concluir que o
// reparo tinha piorado a grade.
//
// Aqui os confrontos são FIXOS: a mesma lista de jogos entra duas vezes no mesmo encaixe, e a
// única diferença entre as duas grades é o reparo. É a única forma honesta de dizer que ele ajuda.
public class ReparoMedidoNoErTests
{
    private readonly ITestOutputHelper _saida;
    public ReparoMedidoNoErTests(ITestOutputHelper saida) => _saida = saida;

    private static readonly DateTime Sexta = new(2026, 9, 11, 18, 0, 0);

    private static Torneio Torneio() => new()
    {
        Id = 1, Nome = "Er", Codigo = "EPT",
        DataInicio = Sexta, DataFim = Sexta.Date.AddDays(2),
        QuantidadeQuadras = 5,
        TempoPrevistoPartidaMinutos = 50,
        HoraInicioDoDia = new TimeSpan(18, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
    };

    // 12 categorias, grupos de 4 duplas, todos contra todos: 6 jogos por grupo. Confrontos FIXOS.
    private static (List<Partida> Jogos, List<Dupla> Duplas) MontarOEr(int comImpedimento, int comConcentracao)
    {
        var categorias = Enumerable.Range(1, 12)
            .Select(i => new Categoria { Id = i, Nome = $"Categoria {i}", Codigo = $"C{i}" })
            .ToList();

        var duplas = new List<Dupla>();
        var jogos = new List<Partida>();
        int proximaDupla = 1, proximoJogador = 1, proximoJogo = 1;

        foreach (var categoria in categorias)
        {
            for (int grupo = 0; grupo < 2; grupo++)           // 2 grupos de 4 = 8 duplas
            {
                var doGrupo = new List<Dupla>();
                for (int i = 0; i < 4; i++)
                {
                    var d = new Dupla
                    {
                        Id = proximaDupla++,
                        Jogador1Id = proximoJogador++,
                        Jogador2Id = proximoJogador++,
                        Categoria = categoria,
                    };
                    duplas.Add(d);
                    doGrupo.Add(d);
                }

                var letra = (char)('A' + grupo);
                for (int a = 0; a < doGrupo.Count; a++)
                    for (int b = a + 1; b < doGrupo.Count; b++)
                        jogos.Add(new Partida
                        {
                            Id = proximoJogo,
                            TorneioId = 1,
                            Codigo = $"J{proximoJogo++:0000}",
                            Status = "Agendada",
                            Fase = $"Grupo {letra}",
                            CategoriaId = categoria.Id,
                            Categoria = categoria,
                            Dupla1Id = doGrupo[a].Id,
                            Dupla2Id = doGrupo[b].Id,
                        });
            }
        }

        // As restrições, espalhadas de forma FIXA — de 7 em 7 o impedimento, de 11 em 11 a
        // concentração, pra não caírem todas na mesma categoria.
        for (int i = 0; i < comImpedimento; i++) duplas[(i * 7) % duplas.Count].ImpedimentoSextaNoite = true;
        for (int i = 0; i < comConcentracao; i++)
        {
            var d = duplas[(i * 11 + 3) % duplas.Count];
            if (!d.ImpedimentoSextaNoite) d.ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        }

        return (jogos, duplas);
    }

    private static void Encaixar(Torneio torneio, List<Partida> jogos, List<Dupla> duplas)
    {
        LevasDaGrade.Encaixar(torneio, jogos, torneio.AberturaDaGrade, Array.Empty<Partida>(),
            new LevasDaGrade.Restricoes(
                RoboDoChaveamento.OcupantesPorDupla(duplas),
                Enumerable.Range(1, torneio.QuantidadeQuadras).Select(i => $"Arena {i}").ToArray(),
                Janelas: JanelasDeImpedimento.PorDupla(torneio, duplas),
                Concentracao: ConcentracaoDeJogos.De(torneio, duplas)));
    }

    private sealed record Placar(int Impedimento, int Concentracao, int Seguidos, int ZeroDeFolga, int Total);

    private static Placar Medir(Torneio torneio, List<Partida> jogos, List<Dupla> duplas)
    {
        var achados = AuditoriaDaGrade.Conferir(torneio, jogos, duplas, SedesDoTorneio.Nenhuma);
        return new Placar(
            achados.Count(a => a.Regra == AuditoriaDaGrade.Impedimento),
            achados.Count(a => a.Regra == AuditoriaDaGrade.Concentracao),
            achados.Count(a => a.Regra == AuditoriaDaGrade.JogosSeguidos),
            achados.Count(a => a.Regra == AuditoriaDaGrade.JogosSeguidos && a.Descricao.Contains("— 0 horário")),
            achados.Count(a => a.Regra != AuditoriaDaGrade.RestricoesEmConflito));
    }

    [Theory]
    [InlineData(6, 4)]
    [InlineData(12, 8)]
    [InlineData(20, 0)]
    public void O_reparo_melhora_a_mesma_grade(int comImpedimento, int comConcentracao)
    {
        // Duas grades a partir da MESMA lista de confrontos.
        var (jogosSem, duplasSem) = MontarOEr(comImpedimento, comConcentracao);
        var (jogosCom, duplasCom) = MontarOEr(comImpedimento, comConcentracao);

        var torneio = Torneio();
        Encaixar(torneio, jogosSem, duplasSem);
        Encaixar(torneio, jogosCom, duplasCom);

        var antes = Medir(torneio, jogosSem, duplasSem);
        ReparoDaGrade.Reparar(torneio, jogosCom, duplasCom, SedesDoTorneio.Nenhuma);
        var depois = Medir(torneio, jogosCom, duplasCom);

        _saida.WriteLine($"{comImpedimento} impedidas / {comConcentracao} concentradas — {jogosSem.Count} jogos");
        _saida.WriteLine($"  sem reparo: {antes}");
        _saida.WriteLine($"  com reparo: {depois}");

        Assert.True(depois.Impedimento <= antes.Impedimento,
            $"o reparo CRIOU impedimento: {antes.Impedimento} → {depois.Impedimento}");
        Assert.True(depois.Total < antes.Total,
            $"o reparo não melhorou nada: {antes.Total} → {depois.Total}");
    }

    // O reparo é idempotente: rodar de novo não acha mais nada. Se achasse, é porque a primeira
    // passada parou cedo demais.
    [Fact]
    public void Rodar_o_reparo_duas_vezes_nao_muda_mais_nada()
    {
        var (jogos, duplas) = MontarOEr(12, 8);
        var torneio = Torneio();
        Encaixar(torneio, jogos, duplas);

        ReparoDaGrade.Reparar(torneio, jogos, duplas, SedesDoTorneio.Nenhuma);
        var segunda = ReparoDaGrade.Reparar(torneio, jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Equal(0, segunda.Trocas);
    }
}
