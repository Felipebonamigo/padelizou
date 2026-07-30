namespace Padelizou.Services;

// De que MATERIAL é o troféu de cada categoria.
//
// A ideia: o troféu conta a força da categoria sem precisar de legenda — todo mundo sabe que
// ouro vale mais que madeira. Antes era o mesmo desenho pra todos, mudando só a cor do fundo
// da pílula; agora o troféu é desenhado NO material (ver Views/Shared/_TrofeusSprite).
//
// A escada metálica é diamante > ouro > prata > bronze > ferro > madeira > plástico. As
// categorias MISTAS ficam de fora dela de propósito, com **vidro**: misto não é "melhor" nem
// "pior" que uma numerada — é outro jogo. Encaixá-lo na escada obrigaria a responder uma
// pergunta que ninguém fez ("mista vale mais que a 3ª?").
public record MaterialDoTrofeu(
    string Chave,
    string Nome,
    int Forca,          // maior = categoria mais forte; 0 = fora da escada (vidro/geral)
    string CorFundo,    // fundo da pílula pequena (listas)
    string CorTexto);   // texto/ícone da pílula pequena

public record TrofeuContado(MaterialDoTrofeu Material, int Titulos);

public static class TrofeuDeMaterial
{
    public static readonly MaterialDoTrofeu Diamante = new("Diamante", "Diamante", 8, "#e0f7fa", "#00838f");
    public static readonly MaterialDoTrofeu Ouro     = new("Ouro", "Ouro", 6, "#fff6df", "#8a6d00");
    public static readonly MaterialDoTrofeu Prata    = new("Prata", "Prata", 5, "#f1f1f1", "#6c757d");
    public static readonly MaterialDoTrofeu Bronze   = new("Bronze", "Bronze", 4, "#fbe7d4", "#8a4b08");
    public static readonly MaterialDoTrofeu Ferro    = new("Ferro", "Ferro", 3, "#e8eaed", "#495057");
    public static readonly MaterialDoTrofeu Madeira  = new("Madeira", "Madeira", 2, "#f0e0c9", "#6b4423");
    public static readonly MaterialDoTrofeu Plastico = new("Plastico", "Plástico", 1, "#eef2f8", "#6c757d");
    public static readonly MaterialDoTrofeu Vidro    = new("Vidro", "Vidro", 0, "#e8f4f8", "#3d6b7a");
    public static readonly MaterialDoTrofeu Geral    = new("Geral", "Geral", 0, "#eef2f8", "#6c757d");

    // O nome da categoria é texto livre (o organizador escreve o que quiser), então a regra é
    // por trecho reconhecido. "1ª" entra junto com Open: pela régua de força do próprio sistema
    // (EstatisticasService.OrdemCategoria) ela é a segunda mais forte, e sem esta linha o
    // campeão dela sairia com o troféu genérico.
    public static MaterialDoTrofeu Do(string? nomeCategoria)
    {
        var n = nomeCategoria ?? "";
        if (n.Contains("Open") || n.Contains("1ª")) return Diamante;
        if (n.Contains("2ª")) return Ouro;
        if (n.Contains("3ª")) return Prata;
        if (n.Contains("4ª")) return Bronze;
        if (n.Contains("5ª")) return Ferro;
        if (n.Contains("6ª")) return Madeira;
        if (n.Contains("7ª") || n.Contains("Iniciantes")) return Plastico;
        if (n.Contains("Mista") || n.Contains("Misto")) return Vidro;
        return Geral;
    }

    // Quantos títulos o jogador tem de cada material. Só entra material com título de verdade:
    // uma prateleira com sete taças vazias diria "esse jogador não ganhou nada" de um jeito
    // mais triste do que não ter prateleira nenhuma.
    //
    // Ordena pelo mais forte; os de fora da escada (vidro/geral) vão pro fim, e entre iguais
    // manda quem tem mais título.
    public static List<TrofeuContado> Contar(IEnumerable<(string? Categoria, string? UltimaFase)> campanhas)
    {
        var porMaterial = new Dictionary<string, (MaterialDoTrofeu Material, int Titulos)>();

        foreach (var (categoria, fase) in campanhas)
        {
            if (fase != "Campeao") continue;

            var material = Do(categoria);
            var atual = porMaterial.TryGetValue(material.Chave, out var v) ? v.Titulos : 0;
            porMaterial[material.Chave] = (material, atual + 1);
        }

        return porMaterial.Values
            .OrderByDescending(t => t.Material.Forca)
            .ThenByDescending(t => t.Titulos)
            .ThenBy(t => t.Material.Nome)
            .Select(t => new TrofeuContado(t.Material, t.Titulos))
            .ToList();
    }
}
