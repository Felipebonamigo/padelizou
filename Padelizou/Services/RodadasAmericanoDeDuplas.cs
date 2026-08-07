namespace Padelizou.Services;

// Sorteio das rodadas do AMERICANO DE DUPLAS: a dupla é fixa (a mesma da inscrição, como no
// formato padrão) e cada uma enfrenta cada uma das outras EXATAMENTE uma vez. No fim vence a
// dupla que somar mais games — a mesma régua do Americano individual, só que a unidade é a
// dupla, não a pessoa.
//
// A diferença aritmética pro Americano individual importa: lá "cada um faz dupla com cada um"
// só fecha em certos números (ver DivisaoDoAmericano); aqui é um todos-contra-todos comum, e
// QUALQUER quantidade de duplas fecha — com número ímpar, uma dupla descansa por rodada e o
// descanso reveza sozinho, porque é assim que o método do círculo gira.
public static class RodadasAmericanoDeDuplas
{
    // Uma partida: dupla A contra dupla B (Ids de Dupla, não de jogador).
    public record Confronto(int DuplaA, int DuplaB);

    // Duas duplas fecham um jogo; menos que isso não há torneio.
    public static bool Aceita(int duplas) => duplas >= 2;

    // Par: todas jogam toda rodada, n−1 rodadas. Ímpar: uma descansa por vez, n rodadas —
    // e cada uma joga n−1, porque descansa exatamente uma.
    public static int Rodadas(int duplas) =>
        !Aceita(duplas) ? 0 : (duplas % 2 == 0 ? duplas - 1 : duplas);

    public static int Partidas(int duplas) => !Aceita(duplas) ? 0 : duplas * (duplas - 1) / 2;

    public static int JogosPorDupla(int duplas) => !Aceita(duplas) ? 0 : duplas - 1;

    // As rodadas, na ordem, pelo método do círculo (um rótulo fixo, os outros giram). O
    // desenho é fixo; o SORTEIO é decidir qual dupla veste qual rótulo — e a ordem das
    // quadras dentro da rodada também sai sorteada.
    //
    // `sorteio` existe pra o resultado ser reproduzível nos testes. Em produção vem null.
    public static List<List<Confronto>> Montar(IReadOnlyList<int> duplas, Random? sorteio = null)
    {
        if (!Aceita(duplas.Count)) return new List<List<Confronto>>();

        var rng = sorteio ?? new Random();
        var mapa = duplas.OrderBy(_ => rng.Next()).ToList();

        // Ímpar ganha um rótulo fantasma: quem cruza com ele descansa a rodada. O fantasma
        // gira como os outros, então o descanso passa por cada dupla exatamente uma vez.
        int n = mapa.Count % 2 == 0 ? mapa.Count : mapa.Count + 1;
        int m = n - 1;   // quantos rótulos giram (o rótulo n-1 fica fixo)

        var rodadas = new List<List<Confronto>>();
        for (int r = 0; r < m; r++)
        {
            var confrontos = new List<Confronto>();
            for (int i = 0; i < n / 2; i++)
            {
                // Par i da rodada r: as duas pontas do círculo girado. O rótulo fixo (n-1)
                // cruza com o girante da vez.
                int a = i == 0 ? n - 1 : (r + i) % m;
                int b = (r + m - i) % m;

                if (a >= mapa.Count || b >= mapa.Count) continue;   // cruzou com o fantasma
                confrontos.Add(new Confronto(mapa[a], mapa[b]));
            }
            rodadas.Add(confrontos.OrderBy(_ => rng.Next()).ToList());
        }
        return rodadas;
    }

    // ---- As frases que a tela mostra ------------------------------------------------

    public static string ComoFica(int duplas)
    {
        if (!Aceita(duplas)) return "precisa de pelo menos 2 duplas completas pra ter jogo.";

        var descanso = duplas % 2 != 0 ? " Número ímpar: uma dupla descansa por rodada." : "";
        return $"{duplas} duplas — {Rodadas(duplas)} rodadas, {Partidas(duplas)} jogos, "
             + $"cada dupla joga {JogosPorDupla(duplas)} vezes.{descanso}";
    }
}
