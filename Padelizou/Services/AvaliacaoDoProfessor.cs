namespace Padelizou.Services;

// Regras da avaliação de professor (1 a 5 estrelas + depoimento com interruptor).
//
// A escala é a de ESTRELAS que o sistema sempre teve (o Felipe cogitou 0-10 em 29/07 e
// voltou atrás no mesmo dia — a tela de estrelas já existia e já era a linguagem do site).
//
// A NOTA nunca se desliga: é o dado que protege o próximo aluno na hora de escolher.
// O DEPOIMENTO é vitrine, e vitrine é do dono — o professor escolhe se a página dele
// exibe texto de aluno. Desligar não apaga nada: os textos ficam guardados e voltam
// se ele religar.
public static class AvaliacaoDoProfessor
{
    public const int NotaMinima = 1;
    public const int NotaMaxima = 5;

    public static bool NotaValida(int nota) => nota is >= NotaMinima and <= NotaMaxima;

    // O que efetivamente se grava de depoimento: nada quando o professor desligou (mandar
    // texto com o interruptor desligado não pode "passar por fora"), nada quando veio em
    // branco, e o texto aparado nos demais casos.
    public static string? DepoimentoFinal(bool professorAceita, string? texto)
    {
        if (!professorAceita) return null;
        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    // "★★★★☆" a partir da média (arredonda pro inteiro mais próximo). Mora aqui, e não
    // solto em cada view, porque duas telas desenham estrelas e divergir seria pior.
    public static string Estrelas(double? media)
    {
        int cheias = media == null ? 0 : Math.Clamp((int)Math.Round(media.Value), 0, NotaMaxima);
        return string.Concat(Enumerable.Range(1, NotaMaxima).Select(i => i <= cheias ? "★" : "☆"));
    }
}
