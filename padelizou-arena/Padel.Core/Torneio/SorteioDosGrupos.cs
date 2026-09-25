namespace Padel.Core.Torneio;

/// <summary>
/// Quem cai em qual grupo — a régua do sorteio de grupos do Padelizou
/// (<c>Padelizou/Controllers/TorneiosController.Chaves.cs</c>, o bloco "O normal dos torneios é
/// fechar em grupos de 3 duplas"), reimplementada sem banco.
/// </summary>
public static class SorteioDosGrupos
{
    /// <summary>
    /// Monta os grupos a partir das duplas JÁ na ordem da semeadura (a melhor primeiro):
    /// <list type="bullet">
    /// <item>o normal é grupo de 3;</item>
    /// <item>sobra 2 (ex.: 8 duplas): 1ª × 2ª viram um grupo de 2, o resto fecha de 3;</item>
    /// <item>sobra 1 (ex.: 7): as 4 melhores viram dois grupos de 2 (1ª × 4ª e 2ª × 3ª);</item>
    /// <item>menos de 3: um grupo só com todo mundo.</item>
    /// </list>
    /// Os grupos de 3 são preenchidos por FAIXAS: a primeira abre os grupos na ordem (A, B, C…) e
    /// toda faixa seguinte entra INVERTIDA — o grupo do cabeça mais forte recebe o pior de cada
    /// faixa. Com 9: A = 1ª, 6ª, 9ª; B = 2ª, 5ª, 8ª; C = 3ª, 4ª, 7ª (a régua do Felipe de
    /// 09/09/2026; não é a serpentina clássica, que punia o líder).
    /// Os grupos de 2 vêm antes, então ficam com as primeiras letras — é o "o A é o grupo dos
    /// cabeças de chave" que o bye lê depois.
    /// </summary>
    public static List<GrupoDoTorneio> Montar(IReadOnlyList<string> semeadura)
    {
        int n = semeadura.Count;
        var grupos = new List<List<string>>();
        if (n < 3)
        {
            grupos.Add(semeadura.ToList());
        }
        else
        {
            int resto = n % 3;
            IReadOnlyList<string> restantes;
            if (resto == 1)
            {
                grupos.Add([semeadura[0], semeadura[3]]);
                grupos.Add([semeadura[1], semeadura[2]]);
                restantes = semeadura.Skip(4).ToList();
            }
            else if (resto == 2)
            {
                grupos.Add([semeadura[0], semeadura[1]]);
                restantes = semeadura.Skip(2).ToList();
            }
            else
            {
                restantes = semeadura;
            }

            int gruposDeTres = restantes.Count / 3;
            if (gruposDeTres > 0)
            {
                var baldes = Enumerable.Range(0, gruposDeTres).Select(_ => new List<string>()).ToArray();
                for (int posicao = 0; posicao < restantes.Count; posicao++)
                {
                    int dentroDaFaixa = posicao % gruposDeTres;
                    int grupo = posicao < gruposDeTres ? dentroDaFaixa : gruposDeTres - 1 - dentroDaFaixa;
                    baldes[grupo].Add(restantes[posicao]);
                }
                grupos.AddRange(baldes);
            }
        }

        return grupos.Select((duplas, i) => new GrupoDoTorneio(((char)('A' + i)).ToString(), duplas)).ToList();
    }
}
