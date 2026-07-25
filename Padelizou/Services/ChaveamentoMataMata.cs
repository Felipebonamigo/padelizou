namespace Padelizou.Services;

// Motor ÚNICO de chaveamento do mata-mata pós-grupos, usado pelos robôs do
// TorneiosController (Mesa de Controle) e do PartidasController (Controle de Placar).
//
// Regras (generalizam o antigo comportamento, que só fechava chave com 1/2/4/8 grupos):
// - 1 grupo só: gera uma Final direta 1º x 2º do grupo (fecha categorias de 2-3 duplas,
//   que antes nunca tinham campeão).
// - N grupos: o quadro é a MAIOR potência de 2 que cabe em 2N. TODOS os 1ºs colocados
//   classificam; os MELHORES 2ºs (comparados entre grupos por vitórias e saldo de games)
//   completam o quadro. Ex.: 3 grupos → quadro de 4 (3 primeiros + melhor segundo);
//   5 grupos → quadro de 8 (5 primeiros + 3 melhores segundos); 8 grupos → quadro de 16.
//   Obs.: com grupos de tamanhos diferentes (regra do resto) a comparação entre 2ºs não é
//   perfeitamente justa — critério pragmático, mesmo usado em circuitos amadores.
// - Nome da fase pelo tamanho do quadro: 16=Oitavas, 8=Quartas, 4=Semifinal, 2=Final.
// - Semeadura 1º melhor x 2º pior (1 x último, 2 x penúltimo...), evitando reedição de
//   confronto do mesmo grupo na 1ª fase quando há troca possível.
public static class ChaveamentoMataMata
{
    // Um classificado de grupo: Posicao é 1 (campeão do grupo) ou 2 (vice do grupo).
    public record Classificado(int DuplaId, string Grupo, int Vitorias, int Saldo, int Posicao);

    public record Confronto(int Dupla1Id, int Dupla2Id);

    public static string NomeFase(int tamanhoQuadro) => tamanhoQuadro switch
    {
        16 => "Oitavas de Final",
        8 => "Quartas de Final",
        4 => "Semifinal",
        _ => "Final"
    };

    // Encadeamento das fases. Null = não há próxima (Final, fases de grupo, Americano...).
    public static string? ProximaFase(string? fase) => fase switch
    {
        "Oitavas de Final" => "Quartas de Final",
        "Quartas de Final" => "Semifinal",
        "Semifinal" => "Final",
        _ => null
    };

    // Quantos jogos uma fase completa tem (= vencedores esperados pra fase fechar).
    public static int JogosDaFase(string? fase) => fase switch
    {
        "Oitavas de Final" => 8,
        "Quartas de Final" => 4,
        "Semifinal" => 2,
        _ => 1
    };

    public static int MaiorPotenciaDe2Ate(int limite)
    {
        int p = 1;
        while (p * 2 <= limite) p *= 2;
        return p;
    }

    // Monta a primeira fase do mata-mata. Fase vazia = nada a gerar (sem classificados).
    public static (string Fase, List<Confronto> Confrontos) MontarPrimeiraFase(List<Classificado> classificados)
    {
        var primeiros = classificados.Where(c => c.Posicao == 1)
            .OrderByDescending(c => c.Vitorias).ThenByDescending(c => c.Saldo).ThenBy(c => c.Grupo)
            .ToList();
        var segundos = classificados.Where(c => c.Posicao == 2)
            .OrderByDescending(c => c.Vitorias).ThenByDescending(c => c.Saldo).ThenBy(c => c.Grupo)
            .ToList();

        if (primeiros.Count == 0) return ("", new List<Confronto>());

        // 1 grupo só: final direta entre 1º e 2º do grupo.
        if (primeiros.Count == 1)
        {
            if (segundos.Count == 0) return ("", new List<Confronto>());
            return ("Final", new List<Confronto> { new(primeiros[0].DuplaId, segundos[0].DuplaId) });
        }

        int quadro = MaiorPotenciaDe2Ate(primeiros.Count + segundos.Count);
        int vagasParaSegundos = quadro - primeiros.Count;

        var cabecas = new List<Classificado>(quadro);
        cabecas.AddRange(primeiros);
        cabecas.AddRange(segundos.Take(vagasParaSegundos));

        // Metade de cima (melhores) x metade de baixo invertida (1 x último, 2 x penúltimo...).
        var alta = cabecas.Take(quadro / 2).ToList();
        var baixa = cabecas.Skip(quadro / 2).Reverse().ToList();

        // Evita 1º x 2º do MESMO grupo na primeira fase, trocando com um vizinho quando possível.
        for (int i = 0; i < alta.Count; i++)
        {
            if (alta[i].Grupo != baixa[i].Grupo) continue;
            for (int j = 0; j < baixa.Count; j++)
            {
                if (j == i) continue;
                if (alta[j].Grupo != baixa[i].Grupo && alta[i].Grupo != baixa[j].Grupo)
                {
                    (baixa[i], baixa[j]) = (baixa[j], baixa[i]);
                    break;
                }
            }
        }

        var confrontos = new List<Confronto>(alta.Count);
        for (int i = 0; i < alta.Count; i++)
            confrontos.Add(new Confronto(alta[i].DuplaId, baixa[i].DuplaId));

        return (NomeFase(quadro), confrontos);
    }

    // Pareia os vencedores de uma fase concluída para a próxima (1º x último da lista).
    public static List<Confronto> ParearVencedores(IReadOnlyList<int> vencedores)
    {
        var confrontos = new List<Confronto>(vencedores.Count / 2);
        for (int i = 0; i < vencedores.Count / 2; i++)
            confrontos.Add(new Confronto(vencedores[i], vencedores[vencedores.Count - 1 - i]));
        return confrontos;
    }
}
