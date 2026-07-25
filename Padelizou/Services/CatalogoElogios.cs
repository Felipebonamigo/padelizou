namespace Padelizou.Services;

public record TipoElogio(string Codigo, string Titulo, string Icone);

// Catálogo fixo, mesmo padrão das Conquistas (EstatisticasService.ObterConquistasAsync) —
// lista curta e estável, não precisa virar tabela no banco.
public static class CatalogoElogios
{
    public static readonly List<TipoElogio> Todos = new()
    {
        new("SmashBom", "Smash Bom", "bi-lightning-charge-fill"),
        new("BomVoleio", "Bom Voleio", "bi-hand-index-thumb-fill"),
        new("BomLob", "Bom Lob", "bi-arrow-up-circle-fill"),
        new("BomSaque", "Bom Saque", "bi-bullseye"),
        new("BomDefensor", "Bom Defensor", "bi-shield-fill"),
        new("BoaBandeja", "Boa Bandeja", "bi-stars"),
        new("BomParceiro", "Bom Parceiro de Dupla", "bi-people-fill"),
        new("BoaVibra", "Boa Vibe", "bi-emoji-laughing-fill"),
    };

    public static TipoElogio? Obter(string codigo) => Todos.FirstOrDefault(t => t.Codigo == codigo);
}
