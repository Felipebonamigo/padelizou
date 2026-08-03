namespace Padelizou.Services;

public record TipoElogio(string Codigo, string Titulo, string Icone);

// Catálogo fixo, mesmo padrão das Conquistas (EstatisticasService.ObterConquistasAsync) —
// lista curta e estável, não precisa virar tabela no banco.
public static class CatalogoElogios
{
    public static readonly List<TipoElogio> Todos = new()
    {
        // Golpes
        new("SmashBom", "Smash Bom", "bi-lightning-charge-fill"),
        new("BomVoleio", "Bom Voleio", "bi-hand-index-thumb-fill"),
        new("BomLob", "Bom Lob", "bi-arrow-up-circle-fill"),
        new("BomSaque", "Bom Saque", "bi-bullseye"),
        new("BoaBandeja", "Boa Bandeja", "bi-stars"),
        new("BoaVibora", "Boa Víbora", "bi-tornado"),
        new("BoaChiquita", "Boa Chiquita", "bi-magic"),
        new("MaoMacia", "Mão Macia", "bi-feather"),

        // O que só existe no padel: a parede é metade do jogo
        new("SaidaDeParede", "Saída de Parede", "bi-bricks"),

        // Jeito de jogar
        new("BomDefensor", "Bom Defensor", "bi-shield-fill"),
        new("LeituraDeJogo", "Leitura de Jogo", "bi-eye-fill"),
        new("RapidoNaQuadra", "Rápido na Quadra", "bi-speedometer2"),
        new("Garra", "Garra", "bi-fire"),

        // Convivência — o que faz alguém ser chamado de novo pra jogar
        new("BomParceiro", "Bom Parceiro de Dupla", "bi-people-fill"),
        new("BoaVibra", "Boa Vibe", "bi-emoji-laughing-fill"),
        new("FairPlay", "Fair Play", "bi-hand-thumbs-up"),
        // Padel tem moda: entrar na quadra bem vestido é meio caminho da vitória moral.
        new("LookBonito", "Look Bonito", "bi-sunglasses"),
        // O terceiro tempo conta: tem gente que é lembrada pela mesa depois do jogo.
        new("BomDeCopo", "Bom de Copo", "bi-cup-straw"),
    };

    public static TipoElogio? Obter(string codigo) => Todos.FirstOrDefault(t => t.Codigo == codigo);
}
