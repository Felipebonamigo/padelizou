namespace Padelizou.Services;

// OS GRUPOS DO SORTEIO, QUANDO O ORGANIZADOR DIZ QUANTOS (27/11/2026).
//
// 🗣️ Felipe, pelo Treta: "módulo branco 8 duplas chaves 4/4, preto 10 duplas 3/3/4, verde 7
// duplas 3/4". O sorteio de sempre fecha grupos de 3 (TorneiosController.GerarChaves) — com
// `Categoria.QuantidadeGrupos` numa categoria comum, reparte em N grupos aqui.
//
// A RÉGUA DE FAIXAS É A MESMA de 09/09/2026 (o comentário inteiro mora no GerarChaves): a lista
// chega do melhor pro pior; a 1ª faixa abre os grupos na ordem (A, B, C…) e TODA faixa seguinte
// entra invertida (…C, B, A). O tamanho de cada grupo sai sozinho dessa conta — a sobra cai nos
// ÚLTIMOS grupos (8 em 2 = 4/4; 10 em 3 = 3/3/4; 7 em 2 = 3/4), que é o que o organizador pediu.
public static class SorteioDosGrupos
{
    public static List<List<T>> EmGrupos<T>(IReadOnlyList<T> ordenados, int grupos)
    {
        grupos = Math.Max(1, grupos);
        var bucket = Enumerable.Range(0, grupos).Select(_ => new List<T>()).ToList();
        for (int posicao = 0; posicao < ordenados.Count; posicao++)
        {
            int dentroDaFaixa = posicao % grupos;
            int grupoIndex = posicao < grupos ? dentroDaFaixa : grupos - 1 - dentroDaFaixa;
            bucket[grupoIndex].Add(ordenados[posicao]);
        }
        return bucket.Where(b => b.Count > 0).ToList();
    }

    // Grupo de 1 não joga nada; acima de 8 grupos (A–H) não é o que esta opção resolve.
    public const int Maximo = 8;
    public static bool Valido(int grupos, int duplas) => grupos >= 1 && grupos <= Maximo && duplas >= grupos * 2;
}
