using Padelizou.Models;
using padelizou.Models;

namespace Padelizou.Services;

// Quem pode ler e escrever no caderno de uma aula.
//
// A anotação é da AULA, não do site: só as duas pessoas que estiveram nela participam.
// Cada linha guarda o autor — "quem escreveu o quê" é o pedido, e é o que separa um
// caderno compartilhado de um campo de texto anônimo.
public static class AnotacoesDeAula
{
    public const int TamanhoMaximo = 1000;

    // Professor da aula ou o aluno dela. Aluno avulso (sem conta, AlunoId nulo) fica de
    // fora por construção — não tem como logar.
    public static bool PodeParticipar(Aula aula, int jogadorId) =>
        aula.ProfessorId == jogadorId || aula.AlunoId == jogadorId;

    // Vazio não vira anotação; texto longo demais é cortado no controller com aviso, não
    // aqui — regra só valida.
    public static bool TextoValido(string? texto) =>
        !string.IsNullOrWhiteSpace(texto) && texto.Trim().Length <= TamanhoMaximo;

    // O selo ao lado do nome: numa conversa de duas pessoas, o PAPEL diz mais que o nome.
    public static string RotuloDoAutor(Aula aula, int autorId) =>
        autorId == aula.ProfessorId ? "Professor" : "Aluno";

    // Pra quem vai o push quando alguém anota: sempre pro OUTRO lado da aula. Anotação de
    // professor avisa o aluno; de aluno, o professor. Aula avulsa não tem outro lado logado.
    public static int? QuemAvisar(Aula aula, int autorId)
    {
        if (autorId == aula.ProfessorId) return aula.AlunoId;
        return aula.ProfessorId;
    }
}
