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

// Um troféu da prateleira: o material desenha, mas quem dá nome é a CATEGORIA. Ninguém ganha
// "um diamante" — ganha a 1ª Categoria Masculina, e é isso que a pessoa quer ler no perfil.
// O material continua existindo pra desenhar a taça e pra ordenar a fila.
public record TrofeuContado(MaterialDoTrofeu Material, string Categoria, int Titulos);

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
        // Casal e mista dividem o vidro pelo mesmo motivo: são OUTRO jogo, não um degrau
        // da escada — o troféu não tem por que dizer "mais forte" nem "mais fraco".
        if (n.Contains("Mista") || n.Contains("Misto") || n.Contains("Casal") || n.Contains("Casais")) return Vidro;
        return Geral;
    }

    // Quantos títulos o jogador tem em cada CATEGORIA. Só entra categoria com título de verdade:
    // uma prateleira com sete taças vazias diria "esse jogador não ganhou nada" de um jeito
    // mais triste do que não ter prateleira nenhuma.
    //
    // Agrupa por categoria, não por material: bicampeão da 2ª aparece como "2× 2ª Categoria",
    // e quem ganhou a 2ª masculina e a 2ª feminina vê as duas — mesmo troféu de ouro desenhado
    // duas vezes, com o nome de cada uma embaixo. Juntar as duas num "2× Ouro" apagava
    // justamente o que a pessoa quer mostrar.
    //
    // A chave ignora caixa e espaço sobrando ("2ª CATEGORIA " e "2ª Categoria" são a mesma),
    // mas o nome exibido é a primeira grafia vista — o organizador escreveu assim.
    //
    // Ordena pelo material mais forte; os de fora da escada (vidro/geral) vão pro fim, e entre
    // iguais manda quem tem mais título.
    public static List<TrofeuContado> Contar(IEnumerable<(string? Categoria, string? UltimaFase)> campanhas)
    {
        var porCategoria = new Dictionary<string, (MaterialDoTrofeu Material, string Nome, int Titulos)>();

        foreach (var (categoria, fase) in campanhas)
        {
            if (fase != "Campeao") continue;

            var material = Do(categoria);

            // Categoria sem nome (dado antigo ou torneio importado) cai no nome do material:
            // é feio deixar a taça sem legenda nenhuma.
            var nome = string.IsNullOrWhiteSpace(categoria) ? material.Nome : categoria.Trim();
            var chave = nome.ToLowerInvariant();

            // Na segunda passagem só o contador sobe: reescrever o nome faria a grafia da
            // ÚLTIMA campanha ganhar, e um " 2ª CATEGORIA " digitado torto apagaria a boa.
            porCategoria[chave] = porCategoria.TryGetValue(chave, out var v)
                ? (v.Material, v.Nome, v.Titulos + 1)
                : (material, nome, 1);
        }

        return porCategoria.Values
            .OrderByDescending(t => t.Material.Forca)
            .ThenByDescending(t => t.Titulos)
            .ThenBy(t => t.Nome)
            .Select(t => new TrofeuContado(t.Material, t.Nome, t.Titulos))
            .ToList();
    }
}
