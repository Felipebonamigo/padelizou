using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Avaliação de professor (nota 0-10 + depoimento com interruptor) e o caderno de anotações
// da aula. As duas regras moram em serviços puros; aqui se confere cada borda.
public class AvaliacaoEAnotacoesDeAulaTests
{
    // ---- Nota em estrelas (1-5) ----

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(3, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public void Nota_vale_de_1_a_5_estrelas(int nota, bool esperado)
    {
        // Quase virou 0-10 em 29/07/2026; o Felipe preferiu manter as estrelas que o site
        // já falava. Zero não existe: estrela vazia é ausência de avaliação, não nota.
        Assert.Equal(esperado, AvaliacaoDoProfessor.NotaValida(nota));
    }

    [Fact]
    public void Com_interruptor_desligado_o_texto_nao_entra_nem_por_POST_direto()
    {
        // O interruptor é do professor. Se o formulário esconde o campo mas o POST aceita,
        // qualquer um manda o texto "por fora" — a regra tem que valer na gravação.
        Assert.Null(AvaliacaoDoProfessor.DepoimentoFinal(professorAceita: false, "Aula ótima!"));
    }

    [Fact]
    public void Com_interruptor_ligado_o_texto_entra_aparado_e_vazio_vira_nulo()
    {
        Assert.Equal("Aula ótima!", AvaliacaoDoProfessor.DepoimentoFinal(true, "  Aula ótima!  "));
        Assert.Null(AvaliacaoDoProfessor.DepoimentoFinal(true, "   "));
        Assert.Null(AvaliacaoDoProfessor.DepoimentoFinal(true, null));
    }

    [Fact]
    public void As_estrelas_desenham_a_media_arredondada()
    {
        Assert.Equal("★★★★☆", AvaliacaoDoProfessor.Estrelas(4.2));
        Assert.Equal("★★★★★", AvaliacaoDoProfessor.Estrelas(4.5));   // arredonda pra cima
        Assert.Equal("☆☆☆☆☆", AvaliacaoDoProfessor.Estrelas(null));  // sem média, sem estrela
    }

    // ---- Caderno da aula ----

    private static Aula AulaDe(int professorId, int? alunoId) => new()
    {
        Id = 1, ProfessorId = professorId, AlunoId = alunoId, Status = "Confirmada",
    };

    [Fact]
    public void So_professor_e_aluno_da_aula_participam_do_caderno()
    {
        var aula = AulaDe(professorId: 10, alunoId: 20);

        Assert.True(AnotacoesDeAula.PodeParticipar(aula, 10));
        Assert.True(AnotacoesDeAula.PodeParticipar(aula, 20));
        // Terceiro logado NÃO: a anotação é da aula, não do site.
        Assert.False(AnotacoesDeAula.PodeParticipar(aula, 30));
    }

    [Fact]
    public void Aula_de_aluno_avulso_so_tem_o_professor_no_caderno()
    {
        // Aluno sem conta (AlunoId nulo) não loga — e nulo não pode "casar" com ninguém.
        var aula = AulaDe(professorId: 10, alunoId: null);

        Assert.True(AnotacoesDeAula.PodeParticipar(aula, 10));
        Assert.False(AnotacoesDeAula.PodeParticipar(aula, 20));
    }

    [Fact]
    public void O_rotulo_diz_o_papel_de_quem_escreveu()
    {
        var aula = AulaDe(professorId: 10, alunoId: 20);

        Assert.Equal("Professor", AnotacoesDeAula.RotuloDoAutor(aula, 10));
        Assert.Equal("Aluno", AnotacoesDeAula.RotuloDoAutor(aula, 20));
    }

    [Fact]
    public void Anotacao_avisa_o_OUTRO_lado_da_aula()
    {
        var aula = AulaDe(professorId: 10, alunoId: 20);

        Assert.Equal(20, AnotacoesDeAula.QuemAvisar(aula, autorId: 10));   // professor escreveu → aluno sabe
        Assert.Equal(10, AnotacoesDeAula.QuemAvisar(aula, autorId: 20));   // aluno escreveu → professor sabe

        // Aula avulsa: professor escreve e não há outro lado logado pra avisar.
        Assert.Null(AnotacoesDeAula.QuemAvisar(AulaDe(10, null), autorId: 10));
    }

    [Fact]
    public void Texto_vazio_ou_gigante_nao_vira_anotacao()
    {
        Assert.False(AnotacoesDeAula.TextoValido(null));
        Assert.False(AnotacoesDeAula.TextoValido("   "));
        Assert.False(AnotacoesDeAula.TextoValido(new string('x', AnotacoesDeAula.TamanhoMaximo + 1)));
        Assert.True(AnotacoesDeAula.TextoValido("Trabalhamos bandeja e saída de parede."));
        Assert.True(AnotacoesDeAula.TextoValido(new string('x', AnotacoesDeAula.TamanhoMaximo)));
    }
}
