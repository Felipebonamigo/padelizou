namespace Padelizou.Services;

// Regras da avaliação de professor (nota 0-10 + depoimento opcional).
//
// A NOTA nunca se desliga: é o dado que protege o próximo aluno na hora de escolher.
// O DEPOIMENTO é vitrine, e vitrine é do dono — o professor escolhe se a página dele
// exibe texto de aluno. Desligar não apaga nada: os textos ficam guardados e voltam
// se ele religar.
public static class AvaliacaoDoProfessor
{
    public const int NotaMinima = 0;
    public const int NotaMaxima = 10;

    public static bool NotaValida(int nota) => nota is >= NotaMinima and <= NotaMaxima;

    // O que efetivamente se grava de depoimento: nada quando o professor desligou (mandar
    // texto com o interruptor desligado não pode "passar por fora"), nada quando veio em
    // branco, e o texto aparado nos demais casos.
    public static string? DepoimentoFinal(bool professorAceita, string? texto)
    {
        if (!professorAceita) return null;
        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    // "9,2/10" — uma casa, no formato que o Brasil lê. A média nunca some por causa do
    // interruptor de depoimentos.
    public static string MediaFormatada(double media) =>
        $"{Math.Round(media, 1).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}/10";
}
