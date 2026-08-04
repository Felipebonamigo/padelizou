using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O professor marca a aula PELO aluno — ele é quem senta e preenche. Até 04/08/2026 ele
// redigitava nome e telefone a cada vez, inclusive pro aluno de toda terça, e o telefone era
// obrigatório mesmo com ele na beira da quadra sem o número na mão.
public class AlunosDoProfessorTests
{
    private static Aula AulaDe(string? nomeAvulso, int? alunoId = null, string status = "Confirmada",
        int diasAtras = 0, string? telefone = null, Jogador? aluno = null) => new()
    {
        ProfessorId = 1,
        AlunoId = alunoId,
        NomeAlunoAvulso = nomeAvulso,
        TelefoneAlunoAvulso = telefone,
        Aluno = aluno,
        DataHora = DateTime.Today.AddDays(-diasAtras),
        Status = status,
        LocalAulaId = 1,
        Preco = 100m,
    };

    [Fact]
    public void Cada_aluno_aparece_uma_vez_com_o_total_de_aulas()
    {
        var lista = AlunosDoProfessor.Montar(new[]
        {
            AulaDe("Pedro", diasAtras: 21),
            AulaDe("Pedro", diasAtras: 14),
            AulaDe("Pedro", diasAtras: 7),
            AulaDe("Marina", diasAtras: 3),
        });

        Assert.Equal(2, lista.Count);
        Assert.Equal(3, lista.Single(a => a.Nome == "Pedro").TotalDeAulas);
        Assert.Equal(1, lista.Single(a => a.Nome == "Marina").TotalDeAulas);
    }

    [Fact]
    public void Quem_voltou_e_recorrente_quem_veio_uma_vez_nao()
    {
        // Uma aula só é um teste; duas já é rotina — e é isso que o professor quer ver do
        // lado do nome antes de escolher.
        var lista = AlunosDoProfessor.Montar(new[]
        {
            AulaDe("Pedro", diasAtras: 14),
            AulaDe("Pedro", diasAtras: 7),
            AulaDe("Marina", diasAtras: 3),
        });

        Assert.True(lista.Single(a => a.Nome == "Pedro").Recorrente);
        Assert.False(lista.Single(a => a.Nome == "Marina").Recorrente);
    }

    [Fact]
    public void Aula_cancelada_nao_faz_de_alguem_aluno_do_professor()
    {
        var lista = AlunosDoProfessor.Montar(new[]
        {
            AulaDe("Pedro", diasAtras: 7),
            AulaDe("Fantasma", status: PoliticaAula.Cancelada, diasAtras: 5),
            AulaDe("Recusado", status: "Recusada", diasAtras: 4),
        });

        Assert.Single(lista);
        Assert.Equal("Pedro", lista[0].Nome);
    }

    [Fact]
    public void Quem_veio_por_ultimo_aparece_primeiro()
    {
        // A aula que ele vai marcar agora é quase sempre pra alguém que ele acabou de ver.
        var lista = AlunosDoProfessor.Montar(new[]
        {
            AulaDe("Antigo", diasAtras: 60),
            AulaDe("Recente", diasAtras: 1),
            AulaDe("Meio", diasAtras: 15),
        });

        Assert.Equal(new[] { "Recente", "Meio", "Antigo" }, lista.Select(a => a.Nome));
    }

    [Fact]
    public void Aluno_com_conta_e_aluno_sem_conta_nao_se_misturam()
    {
        var comConta = new Jogador { Nome = "Pedro Silva", Cpf = "11144477735", Celular = "51999990000" };
        var lista = AlunosDoProfessor.Montar(new[]
        {
            AulaDe("Pedro", alunoId: 7, aluno: comConta, diasAtras: 2),
            AulaDe("Pedro", diasAtras: 5),
        });

        // Mesmo nome escrito, pessoas diferentes pro sistema: uma tem conta, a outra não.
        Assert.Equal(2, lista.Count);
        Assert.Single(lista, a => a.TemConta);
        Assert.Single(lista, a => !a.TemConta);
    }

    [Fact]
    public void O_nome_que_o_professor_escreveu_ganha_do_nome_do_cadastro()
    {
        // Ele chama a pessoa como a conhece na quadra, não como ela assinou o cadastro.
        var cadastro = new Jogador { Nome = "Pedro Henrique da Silva Santos", Cpf = "11144477735" };
        var lista = AlunosDoProfessor.Montar(new[] { AulaDe("Pedrinho", alunoId: 7, aluno: cadastro) });

        Assert.Equal("Pedrinho", lista[0].Nome);
    }

    [Fact]
    public void O_celular_vem_do_que_foi_anotado_ou_do_cadastro()
    {
        var cadastro = new Jogador { Nome = "Pedro", Cpf = "11144477735", Celular = "51988887777" };

        var anotado = AlunosDoProfessor.Montar(new[] { AulaDe("Pedro", telefone: "51999990000") });
        Assert.Equal("51999990000", anotado[0].Celular);

        var doCadastro = AlunosDoProfessor.Montar(new[] { AulaDe("Pedro", alunoId: 7, aluno: cadastro) });
        Assert.Equal("51988887777", doCadastro[0].Celular);
    }

    [Fact]
    public void Aluno_sem_telefone_nenhum_continua_na_lista()
    {
        // O telefone deixou de ser obrigatório: quem foi marcado sem número não pode sumir
        // do atalho por causa disso.
        var lista = AlunosDoProfessor.Montar(new[] { AulaDe("Pedro") });

        Assert.Single(lista);
        Assert.True(string.IsNullOrEmpty(lista[0].Celular));
    }

    [Theory]
    [InlineData("Jônatas", "jonatas", true)]   // digitou sem acento
    [InlineData("Jonatas", "jônatas", true)]   // digitou com acento
    [InlineData("Marina", "MAR", true)]
    [InlineData("Marina", "rina", true)]       // pedaço do meio
    [InlineData("Marina", "pedro", false)]
    [InlineData("Marina", "", true)]           // sem termo, mostra todo mundo
    public void Busca_ignora_acento_e_caixa(string nome, string termo, bool esperado)
    {
        Assert.Equal(esperado, AlunosDoProfessor.Casa(nome, termo));
    }
}
