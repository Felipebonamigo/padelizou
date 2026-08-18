namespace Padelizou.Services;

// O SORTEADOR DE DUPLAS DA PANELINHA — o motor, sem banco e sem tela.
//
// O problema real (Felipe, 18/08/2026): "para que as duplas não sejam sempre as mesmas". Em
// panelinha que joga toda semana, quem chega junto joga junto, e em pouco tempo são sempre os
// mesmos quatro confrontos. O sorteio quebra isso — mas não pode quebrar o PADEL: dupla de
// padel tem um jogador na direita e um na esquerda, e quem joga na esquerda a vida inteira não
// vira destro porque o sorteio mandou.
//
// Então são duas regras que puxam em direções opostas, e a ordem entre elas é o que importa:
//   1º o LADO manda (é físico, não é preferência);
//   2º dentro do que o lado permite, sorteia-se preferindo quem menos jogou junto.
//
// ⚠️ QUEM JOGA DOS DOIS LADOS É O CORINGA, e é isso que faz a coisa fechar. Quem é "Direita"
// fica na direita, quem é "Esquerda" fica na esquerda, e os "Ambos" preenchem o que sobrou —
// nessa ordem, que é literalmente o pedido ("sempre priorizando o jogador que joga na direita,
// seguir sendo na direita, e o esquerda seguir sendo na esquerda").
//
// ⚠️ E QUANDO NÃO FECHA, NÃO SE RECUSA A SORTEAR. Seis destros e nenhum canhoto é o normal de
// uma panelinha, não um erro do usuário — recusar deixaria a tela inútil justamente no caso
// mais comum. Sorteia-se assim mesmo, tirando do lado certo só quem for inevitável, e a tela
// diz quantos e quem ficou trocado.
//
// O vocabulário de lado é o de Services/LadoNaQuadra, o mesmo do perfil, da inscrição de
// torneio e do cartão — quinto uso, régua compartilhada, nenhuma cópia nova.
public static class SorteioDeDuplas
{
    // Um confirmado da semana, com o lado JÁ resolvido (ver LadoNaQuadra.Efetivo: o lado da
    // confirmação quando existe, senão o do perfil).
    public record Candidato(int JogadorId, string Nome, string Lado);

    // `ForaDoLadoDele` marca quem foi deslocado — a tela precisa dizer isso pessoa a pessoa,
    // senão o jogador chega na quadra e descobre na hora.
    public record NaDupla(int JogadorId, string Nome, bool ForaDoLadoDele);

    public record DuplaSorteada(NaDupla Direita, NaDupla Esquerda, int VezesQueJaJogaramJuntos);

    public record Resultado(
        IReadOnlyList<DuplaSorteada> Duplas,
        IReadOnlyList<string> Avisos,
        IReadOnlyList<string> Sobraram);

    // Chave de uma dupla independente da ordem — {A,B} e {B,A} são a mesma parceria.
    public static (int, int) Chave(int a, int b) => a < b ? (a, b) : (b, a);

    public static Resultado Sortear(
        IReadOnlyList<Candidato> confirmados,
        IReadOnlyDictionary<(int, int), int> vezesJuntos,
        Random sorte,
        bool respeitarLado = true)
    {
        var avisos = new List<string>();

        if (confirmados.Count < 2)
            return new Resultado([], ["Precisa de pelo menos 2 confirmados pra sortear uma dupla."], []);

        // Embaralhar ANTES de qualquer coisa: é o que faz duas rodadas do botão com o mesmo
        // grupo darem resultados diferentes. Sem isto o desempate cairia sempre na ordem da
        // consulta, e "sortear" viraria "ordenar por Id".
        var baralho = confirmados.OrderBy(_ => sorte.Next()).ToList();

        // Ímpar: alguém fica de fora, e quem fica é sorteado também — escolher pelo primeiro
        // da lista puniria sempre a mesma pessoa.
        var sobraram = new List<string>();
        if (baralho.Count % 2 == 1)
        {
            var deFora = baralho[^1];
            baralho.RemoveAt(baralho.Count - 1);
            sobraram.Add(deFora.Nome);
            avisos.Add($"{deFora.Nome} ficou de fora do sorteio — o número de confirmados é ímpar.");
        }

        int duplas = baralho.Count / 2;

        var (direitas, esquerdas) = respeitarLado
            ? DistribuirPorLado(baralho, duplas, avisos)
            : DistribuirIgnorandoLado(baralho, duplas);

        if (!respeitarLado)
            avisos.Add("Sorteio feito SEM olhar o lado de cada um, como você pediu.");

        return new Resultado(Parear(direitas, esquerdas, vezesJuntos, sorte), avisos, sobraram);
    }

    // A distribuição que o Felipe descreveu, na ordem em que ele descreveu.
    private static (List<NaDupla> Direitas, List<NaDupla> Esquerdas) DistribuirPorLado(
        List<Candidato> baralho, int duplas, List<string> avisos)
    {
        var fixosDireita = baralho.Where(c => c.Lado == LadoNaQuadra.Direita).ToList();
        var fixosEsquerda = baralho.Where(c => c.Lado == LadoNaQuadra.Esquerda).ToList();
        var coringas = baralho.Where(c => c.Lado == LadoNaQuadra.Ambos).ToList();

        // 1º: quem tem lado ocupa o lado dele. `Take` porque pode haver mais destros do que
        // vagas na direita — o excedente volta pro bolo mais abaixo.
        var direitas = fixosDireita.Take(duplas).Select(c => new NaDupla(c.JogadorId, c.Nome, false)).ToList();
        var esquerdas = fixosEsquerda.Take(duplas).Select(c => new NaDupla(c.JogadorId, c.Nome, false)).ToList();

        // 2º: os coringas tapam os buracos. Eles não entram como "fora do lado" — jogar dos
        // dois lados é o lado deles.
        var fila = new Queue<Candidato>(coringas);
        while (direitas.Count < duplas && fila.Count > 0)
        {
            var c = fila.Dequeue();
            direitas.Add(new NaDupla(c.JogadorId, c.Nome, false));
        }
        while (esquerdas.Count < duplas && fila.Count > 0)
        {
            var c = fila.Dequeue();
            esquerdas.Add(new NaDupla(c.JogadorId, c.Nome, false));
        }

        // 3º: acabaram os coringas e ainda falta gente num lado — aí sim alguém joga trocado.
        // Quem sobrou do lado oposto é exatamente quem não coube no próprio lado.
        var sobraDireita = fixosDireita.Skip(duplas).ToList();
        var sobraEsquerda = fixosEsquerda.Skip(duplas).ToList();

        int trocadosNaEsquerda = 0, trocadosNaDireita = 0;

        foreach (var c in sobraDireita)
        {
            if (esquerdas.Count >= duplas) break;
            esquerdas.Add(new NaDupla(c.JogadorId, c.Nome, true));
            trocadosNaEsquerda++;
        }
        foreach (var c in sobraEsquerda)
        {
            if (direitas.Count >= duplas) break;
            direitas.Add(new NaDupla(c.JogadorId, c.Nome, true));
            trocadosNaDireita++;
        }

        if (trocadosNaEsquerda > 0) avisos.Add(FaltaGente(trocadosNaEsquerda, "ESQUERDA", "direita", "esquerda"));
        if (trocadosNaDireita > 0) avisos.Add(FaltaGente(trocadosNaDireita, "DIREITA", "esquerda", "direita"));

        return (direitas, esquerdas);
    }

    private static string FaltaGente(int quantos, string ladoQueFalta, string ladoDeles, string ladoQueVaoJogar) =>
        quantos == 1
            ? $"Não tem gente suficiente pra {ladoQueFalta}: 1 jogador de {ladoDeles} vai ter que jogar na {ladoQueVaoJogar}."
            : $"Não tem gente suficiente pra {ladoQueFalta}: {quantos} jogadores de {ladoDeles} vão ter que jogar na {ladoQueVaoJogar}.";

    private static (List<NaDupla>, List<NaDupla>) DistribuirIgnorandoLado(List<Candidato> baralho, int duplas) =>
        (baralho.Take(duplas).Select(c => new NaDupla(c.JogadorId, c.Nome, false)).ToList(),
         baralho.Skip(duplas).Select(c => new NaDupla(c.JogadorId, c.Nome, false)).ToList());

    // Quantos sorteios inteiros são testados antes de escolher o melhor. 200 embaralhadas de
    // no máximo 8 nomes é trabalho nenhum pro servidor, e é o que troca "quase sempre bom" por
    // "praticamente sempre o melhor possível".
    private const int TentativasDeSorteio = 200;

    // O PAREAMENTO: sorteia o conjunto inteiro várias vezes e fica com o que repete menos
    // parceria.
    //
    // ⚠️ A primeira versão disto era gulosa — cada jogador da direita pegava o parceiro com
    // quem menos tinha jogado. Parece certo e não é: a escolha boa de um pode ENCURRALAR o
    // último par. Com D1 que já jogou 5× com E1, o guloso pode casar D2+E2 primeiro (custo
    // zero, escolha "ótima" naquele passo) e aí sobra exatamente D1+E1, a única dupla que a
    // gente queria evitar. O erro é clássico: ótimo local em cada passo, péssimo no total.
    //
    // Sortear o conjunto inteiro e comparar o TOTAL não tem esse buraco, porque a conta é
    // sempre do sorteio completo. Não é garantidamente ótimo como um matching húngaro seria,
    // mas com 200 tentativas sobre 4 a 8 duplas ele acha o melhor com folga — e custa 10
    // linhas em vez de 100.
    //
    // ⚠️ E o empate é sorteado DE VERDADE (reservoir sampling): entre todos os sorteios que
    // empatam no melhor custo, cada um tem a mesma chance. Ficar com o primeiro faria o botão
    // devolver sempre o mesmo resultado num grupo sem histórico — que é justamente o grupo
    // novo, e justamente o que o Felipe pediu pra evitar.
    private static List<DuplaSorteada> Parear(
        List<NaDupla> direitas, List<NaDupla> esquerdas,
        IReadOnlyDictionary<(int, int), int> vezesJuntos, Random sorte)
    {
        int quantas = Math.Min(direitas.Count, esquerdas.Count);
        if (quantas == 0) return [];

        List<NaDupla>? melhor = null;
        int melhorCusto = int.MaxValue;
        int empatados = 0;

        for (int tentativa = 0; tentativa < TentativasDeSorteio; tentativa++)
        {
            var ordem = esquerdas.OrderBy(_ => sorte.Next()).ToList();

            int custo = 0;
            for (int i = 0; i < quantas; i++)
                custo += vezesJuntos.GetValueOrDefault(Chave(direitas[i].JogadorId, ordem[i].JogadorId));

            if (custo < melhorCusto)
            {
                melhorCusto = custo;
                melhor = ordem;
                empatados = 1;
            }
            else if (custo == melhorCusto && sorte.Next(++empatados) == 0)
            {
                melhor = ordem;
            }

            if (melhorCusto == 0) break;   // não existe sorteio melhor que "ninguém repetiu"
        }

        return Enumerable.Range(0, quantas)
            .Select(i => new DuplaSorteada(direitas[i], melhor![i],
                vezesJuntos.GetValueOrDefault(Chave(direitas[i].JogadorId, melhor[i].JogadorId))))
            .ToList();
    }
}
