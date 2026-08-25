namespace Padelizou.Services;

// Até 25/08/2026 toda aula era implicitamente de padel. Alguns professores (o próprio João,
// por exemplo) também dão tênis e beach tênis — pedido do Felipe pra deixar isso visível na
// agenda em vez de forçado dentro do texto livre de observações.
//
// String + classe estática com const, e não um enum de C#: é o mesmo padrão que Aula.Status
// já usa (ver PoliticaAula) — nenhum enum do projeto é mapeado como coluna de banco hoje.
public static class EsporteDaAula
{
    public const string Padel = "Padel";
    public const string Tenis = "Tênis";
    public const string BeachTenis = "Beach Tênis";

    // Nasce em Padel: é o esporte implícito de toda aula lançada antes deste campo existir,
    // e o professor que só dá padel não deveria precisar escolher nada a mais.
    public const string Padrao = Padel;

    public static readonly IReadOnlyList<string> Todos = new[] { Padel, Tenis, BeachTenis };
}
