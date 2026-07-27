namespace Padelizou.Services;

// Regras do feedback do site: o que é aceito e como as notas viram um número só.
//
// Nota de 0 a 10 é a escala do NPS, então a leitura segue a do NPS: 9-10 promotor, 7-8 neutro,
// 0-6 detrator. Uma média de notas esconderia justamente o que interessa — dez notas 8 e dez
// notas 4 dão a mesma média que vinte notas 6, e são situações bem diferentes.
public static class RegrasDeFeedback
{
    public const int TamanhoMaximo = 2000;

    // O texto é obrigatório; a nota, não — tem gente que só quer escrever.
    public static string? ProblemaCom(int? nota, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "Escreva o que você achou.";
        if (texto.Trim().Length < 3) return "Escreva um pouco mais pra gente entender.";
        if (texto.Length > TamanhoMaximo) return $"Deixe em até {TamanhoMaximo} caracteres.";
        if (nota is < 0 or > 10) return "A nota vai de 0 a 10.";
        return null;
    }

    public static string Classificacao(int? nota) => nota switch
    {
        null => "Sem nota",
        >= 9 => "Promotor",
        >= 7 => "Neutro",
        _ => "Detrator"
    };

    // NPS: % de promotores menos % de detratores, de -100 a 100. Quem não deu nota fica de
    // fora da conta — entrar como zero rebaixaria o resultado sem ninguém ter reclamado.
    public static int? Nps(IEnumerable<int?> notas)
    {
        var comNota = notas.Where(n => n is >= 0 and <= 10).Select(n => n!.Value).ToList();
        if (comNota.Count == 0) return null;

        var promotores = comNota.Count(n => n >= 9);
        var detratores = comNota.Count(n => n <= 6);

        return (int)Math.Round((promotores - detratores) * 100.0 / comNota.Count);
    }
}
