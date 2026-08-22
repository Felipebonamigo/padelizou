using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// "Um card só, com os N nomes" — a decisão do Felipe pra como a turma aparece na agenda. As
// N linhas continuam existindo de verdade (é assim que cada aluno tem sua cobrança); isto
// aqui é só a leitura que colapsa o grupo numa representante pra tela.
public class AgendaDeTurmaTests
{
    private static Aula Linha(int id, Guid? turmaId, string nome, decimal preco) => new()
    {
        Id = id,
        ProfessorId = 1,
        LocalAulaId = 1,
        LocalAula = new LocalAula { Nome = "Batata Padel" },
        DataHora = new DateTime(2026, 9, 1, 9, 0, 0),
        DuracaoMinutos = 90,
        Preco = preco,
        Status = "Confirmada",
        QuantidadeAlunos = 3,
        TurmaId = turmaId,
        NomeAlunoAvulso = nome,
    };

    [Fact]
    public void Aula_sem_TurmaId_passa_direto_sem_colapsar()
    {
        var solo = Linha(1, null, "Medina", 110);

        var resultado = AgendaDeTurma.Colapsar(new[] { solo });

        Assert.Single(resultado);
        Assert.Same(solo, resultado[0]);
    }

    [Fact]
    public void Tres_linhas_do_mesmo_TurmaId_viram_uma_so()
    {
        var turma = Guid.NewGuid();
        var linhas = new[]
        {
            Linha(1, turma, "Medina", 60),
            Linha(2, turma, "Coello", 60),
            Linha(3, turma, "Lima", 60),
        };

        var resultado = AgendaDeTurma.Colapsar(linhas);

        Assert.Single(resultado);
        Assert.Equal("Medina, Coello e Lima", resultado[0].NomeCompletoAluno);
        Assert.Equal(180, resultado[0].Preco);
        Assert.Equal(3, resultado[0].QuantidadeAlunos);
    }

    [Fact]
    public void Duas_turmas_diferentes_nao_se_misturam()
    {
        var turmaA = Guid.NewGuid();
        var turmaB = Guid.NewGuid();
        var linhas = new[]
        {
            Linha(1, turmaA, "Medina", 60),
            Linha(2, turmaA, "Coello", 60),
            Linha(3, turmaB, "Outro", 100),
        };

        var resultado = AgendaDeTurma.Colapsar(linhas);

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, a => a.Preco == 120);
        Assert.Contains(resultado, a => a.Preco == 100);
    }

    [Fact]
    public void Aula_solo_e_turma_convivem_na_mesma_lista()
    {
        var turma = Guid.NewGuid();
        var linhas = new[]
        {
            Linha(1, turma, "Medina", 60),
            Linha(2, turma, "Coello", 60),
            Linha(3, null, "Leonardo", 110),
        };

        var resultado = AgendaDeTurma.Colapsar(linhas);

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, a => a.NomeAlunoAvulso == "Leonardo" && a.TurmaId == null);
        Assert.Contains(resultado, a => a.NomeCompletoAluno == "Medina e Coello");
    }
}
