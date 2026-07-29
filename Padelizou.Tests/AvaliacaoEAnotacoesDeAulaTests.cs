using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Avaliação de professor (nota 0-10 + depoimento com interruptor) e o caderno de anotações
// da aula. As duas regras moram em serviços puros; aqui se confere cada borda.
public class AvaliacaoEAnotacoesDeAulaTests
{
    // ---- Nota 0-10 ----

    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(7, true)]
    [InlineData(-1, false)]
    [InlineData(11, false)]
    public void Nota_vale_de_0_a_10_inclusive(int nota, bool esperado)
    {
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
    public void A_media_sai_no_formato_brasileiro_com_uma_casa()
    {
        Assert.Equal("9,2/10", AvaliacaoDoProfessor.MediaFormatada(9.24));
        Assert.Equal("10,0/10", AvaliacaoDoProfessor.MediaFormatada(10));
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
