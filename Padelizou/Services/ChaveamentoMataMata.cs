namespace Padelizou.Services;

// Motor ÚNICO de chaveamento do mata-mata pós-grupos, usado pelos robôs do
// TorneiosController (Mesa de Controle) e do PartidasController (Controle de Placar).
//
// Regras (decisão de produto de 05/08/2026, pedida pelo Felipe no Interno):
// - TODO classificado avança — 1º e 2º de cada grupo (ou quantos a categoria definir).
//   A régua antiga cortava pro MAIOR quadro que coubesse: com 3 grupos, dois segundos
//   colocados eram eliminados sem jogar mata-mata nenhum. Agora o quadro é a MENOR
//   potência de 2 que CABE todo mundo, e as vagas que sobram viram BYE.
// - O BYE é dos melhores: quem fez a melhor campanha pula a primeira rodada e entra
//   direto na seguinte, enquanto os piores ranqueados jogam uma etapa a mais.
//   Ex.: 3 grupos → 6 classificados → quadro de 8: os 2 melhores 1ºs descansam,
//   os outros 4 disputam 2 jogos.
// - 1 grupo só: Final direta 1º x 2º (fecha categorias de 2-3 duplas).
// - Nome da fase pelo tamanho do QUADRO: 32=Primeira Rodada, 16=Oitavas, 8=Quartas,
//   4=Semifinal, 2=Final. Com bye a fase tem menos jogos que o nome promete — quem
//   confere se ela fechou conta as partidas que existem (Services/AvancoDaChave).
// - Semeadura por LADO da chave: dois classificados do mesmo grupo caem em metades
//   opostas e só podem se reencontrar NA FINAL — contando quem DESCANSA: o bye também
//   tem lado, o da vaga em que ele entra na rodada seguinte (ver Semear).
public static class ChaveamentoMataMata
{
    // Um classificado de grupo: Posicao é 1 (campeão do grupo) ou 2 (vice do grupo).
    public record Classificado(int DuplaId, string Grupo, int Vitorias, int Saldo, int Posicao);

    public record Confronto(int Dupla1Id, int Dupla2Id);

    public const string PrimeiraRodada = "Primeira Rodada";

    public static string NomeFase(int tamanhoQuadro) => tamanhoQuadro switch
    {
        32 => PrimeiraRodada,
        16 => "Oitavas de Final",
        8 => "Quartas de Final",
        4 => "Semifinal",
        _ => "Final"
    };

    // Encadeamento das fases. Null = não há próxima (Final, fases de grupo, Americano...).
    public static string? ProximaFase(string? fase) => fase switch
    {
        PrimeiraRodada => "Oitavas de Final",
        "Oitavas de Final" => "Quartas de Final",
        "Quartas de Final" => "Semifinal",
        "Semifinal" => "Final",
        _ => null
    };

    // Essa fase é de mata-mata? "Final" precisa de menção própria porque ProximaFase("Final")
    // é null — o mesmo null de "Grupo A" e do Americano, que NÃO são mata-mata.
    public static bool EhFaseDeMataMata(string? fase) =>
        fase == "Final" || ProximaFase(fase) != null;

    // Quantos jogos uma fase COMPLETA tem (= vencedores esperados pra fase fechar).
    //
    // ⚠️ Só vale pra chave cheia. Numa chave direta com bye a primeira rodada tem MENOS
    // jogos que o quadro pede (24 duplas num quadro de 32 = 8 jogos, não 16), então quem
    // decide se a fase fechou conta as partidas que existem de verdade — ver o robô de
    // progressão em TorneiosController.Chaves.
    public static int JogosDaFase(string? fase) => fase switch
    {
        PrimeiraRodada => 16,
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
    //
    // Byes: as vagas do quadro que sobram vão pros MELHORES (posição no grupo primeiro,
    // campanha depois) — eles entram direto na fase seguinte, e o robô de avanço os soma
    // aos vencedores (Services/AvancoDaChave). classificadosPorGrupo é 2 no padrão; a
    // categoria de TIMES passa o número que o organizador decidiu.
    public static (string Fase, List<Confronto> Confrontos, List<int> Byes) MontarPrimeiraFase(
        List<Classificado> classificados, int classificadosPorGrupo = 2)
    {
        // Posição no grupo manda primeiro (todo 1º entra antes de qualquer 2º); dentro da
        // mesma posição, a campanha compara entre grupos — a régua de sempre.
        var candidatos = classificados
            .Where(c => c.Posicao >= 1 && c.Posicao <= classificadosPorGrupo)
            .OrderBy(c => c.Posicao)
            .ThenByDescending(c => c.Vitorias).ThenByDescending(c => c.Saldo).ThenBy(c => c.Grupo)
            .ToList();

        if (candidatos.Count < 2) return ("", new List<Confronto>(), new List<int>());

        // O quadro CABE todo mundo: ninguém que classificou é cortado. A régua antiga usava
        // a maior potência que coubesse DENTRO e eliminava os piores 2ºs sem mata-mata
        // nenhum — com 3 grupos, duas duplas classificadas iam embora sem jogar.
        int quadro = MenorPotenciaDe2APartirDe(candidatos.Count);
        var passamDireto = candidatos.Take(quadro - candidatos.Count).ToList();
        var cabecas = candidatos.Skip(passamDireto.Count).ToList();

        var confrontos = Semear(cabecas, passamDireto)
            .Select(jogo => new Confronto(jogo.Mandante.DuplaId, jogo.Adversario.DuplaId))
            .ToList();

        return (NomeFase(quadro), confrontos, passamDireto.Select(c => c.DuplaId).ToList());
    }

    // ---- A SEMEADURA: quem joga contra quem na primeira rodada, e em que jogo ----

    private record struct Jogo(Classificado Mandante, Classificado Adversario);

    // ⚠️ QUEM CAI DE QUAL LADO DA CHAVE — inclusive quem DESCANSA.
    //
    // Não basta cruzar "melhor x pior": os dois classificados de um MESMO GRUPO precisam
    // cair em METADES OPOSTAS do quadro. Senão eles se reencontram já na semifinal —
    // dois que acabaram de se enfrentar na fase de grupos decidindo vaga na final, e um
    // deles eliminado por quem já tinha enfrentado. Só podem se cruzar de novo NA FINAL.
    //
    // O desenho antigo (melhor x pior direto, invertendo a lista) produzia exatamente
    // isso: com 4 grupos, o 1ºA saía no jogo 1 e o 2ºA no jogo 4 — e os jogos 1 e 4
    // desembocam na MESMA semifinal.
    //
    // 🕳️ E O BYE TAMBÉM TEM LADO (ensaio do Er, 10/09/2026). A rodada seguinte é montada
    // sobre a lista [vencedores na ordem dos jogos, byes do melhor pro pior]
    // (AvancoDaChave.QuemAvancaAsync), cruzada primeiro × último (ParearVencedores) — e
    // esse cruzamento, repetido até a final, é a GEOMETRIA da chave: cada vaga dessa lista
    // cai numa metade fixa. Num quadro de 8 com 2 jogos e 2 byes, a semifinal 1 é
    // "vencedor do jogo 1 × pior bye" e a 2 é "vencedor do jogo 2 × melhor bye". Até aqui a
    // semeadura só punha os JOGOS num lado e o 1º C descansado esperava o 2º C do outro
    // lado do mesmo jogo: 3ª Masc #83 = 5 (2º C) × 8 (1º C), 6ª Masc #84 = 29 (2º C) ×
    // 27 (1º C), havendo alternativa nas duas.
    private static List<Jogo> Semear(List<Classificado> cabecas, List<Classificado> byes)
    {
        int jogos = cabecas.Count / 2;
        int vagas = jogos + byes.Count;   // a lista da rodada seguinte, na ordem de AvancoDaChave

        var ladoDoJogo = new int[jogos];
        for (int k = 0; k < jogos; k++) ladoDoJogo[k] = LadoDaVaga(vagas, k);

        var gruposDosByes = new[] { new HashSet<string>(), new HashSet<string>() };
        for (int b = 0; b < byes.Count; b++)
            gruposDosByes[LadoDaVaga(vagas, jogos + b)].Add(byes[b].Grupo);

        // A semeadura de sempre: os melhores abrem os jogos, na ordem, cada um contra o pior
        // que cabe no lado dele. Quando ela cumpre a promessa, é ela que vale — a chave cheia
        // (2, 4, 8 grupos) sai exatamente como sempre saiu.
        var deSempre = SemearNaOrdem(cabecas, ladoDoJogo, gruposDosByes);
        if (SemReencontroAntesDaFinal(deSempre, ladoDoJogo, gruposDosByes)) return deSempre;

        // Ela não cumpre quando o pior que cabe no lado do mandante é do grupo de um bye
        // daquele lado — o caso do Er. Aí o mandante pode trocar de jogo: a busca abaixo
        // acha um arranjo sem reencontro, e só devolve a semeadura imperfeita se não
        // existir nenhum (número torto de grupos, 3+ classificados por grupo).
        return ProcurarArranjoPerfeito(cabecas, ladoDoJogo, gruposDosByes) ?? deSempre;
    }

    private static List<Jogo> SemearNaOrdem(
        List<Classificado> cabecas, int[] ladoDoJogo, HashSet<string>[] gruposDosByes)
    {
        int jogos = cabecas.Count / 2;

        // Os melhores abrem os jogos, na ordem; o resto é distribuído contra eles.
        var mandantes = cabecas.Take(jogos).ToList();
        var adversarios = cabecas.Skip(jogos).ToList();   // do melhor pro pior

        var gruposNoLado = new[] { new HashSet<string>(gruposDosByes[0]), new HashSet<string>(gruposDosByes[1]) };
        for (int i = 0; i < jogos; i++)
            gruposNoLado[ladoDoJogo[i]].Add(mandantes[i].Grupo);

        var confrontos = new List<Jogo>(jogos);
        for (int i = 0; i < jogos; i++)
        {
            var escolhido = EscolherAdversario(adversarios, mandantes[i], gruposNoLado[ladoDoJogo[i]]);
            adversarios.Remove(escolhido);
            gruposNoLado[ladoDoJogo[i]].Add(escolhido.Grupo);
            confrontos.Add(new Jogo(mandantes[i], escolhido));
        }

        return confrontos;
    }

    // O adversário do mandante, escolhido do PIOR pro melhor (é o que mantém "1º melhor x
    // último pior"), respeitando duas regras em ordem de importância:
    //
    //   1. não pode já ter alguém do grupo dele nesta metade da chave — é a regra que
    //      empurra o reencontro pra final;
    //   2. não pode ser do mesmo grupo do mandante — reeditar na estreia o jogo que os dois
    //      acabaram de fazer no grupo.
    //
    // As duas são afrouxadas na marra se não sobrar ninguém: com número torto de grupos (5
    // grupos num quadro de 8, por exemplo) nem sempre existe arranjo perfeito, e um
    // confronto imperfeito é melhor que um jogo sem adversário.
    private static Classificado EscolherAdversario(
        List<Classificado> disponiveis, Classificado mandante, HashSet<string> gruposDesteLado)
    {
        for (int i = disponiveis.Count - 1; i >= 0; i--)
            if (!gruposDesteLado.Contains(disponiveis[i].Grupo) && disponiveis[i].Grupo != mandante.Grupo)
                return disponiveis[i];

        for (int i = disponiveis.Count - 1; i >= 0; i--)
            if (disponiveis[i].Grupo != mandante.Grupo)
                return disponiveis[i];

        return disponiveis[^1];
    }

    // A promessa, conferida: nenhum jogo entre duplas do mesmo grupo, e nenhum grupo com
    // duas duplas na mesma metade — contando os byes. (Dois byes do mesmo grupo na mesma
    // metade são coisa da campanha, que a semeadura não escolhe; não entram na conta.)
    private static bool SemReencontroAntesDaFinal(
        List<Jogo> arranjo, int[] ladoDoJogo, HashSet<string>[] gruposDosByes)
    {
        var gruposNoLado = new[] { new HashSet<string>(gruposDosByes[0]), new HashSet<string>(gruposDosByes[1]) };

        for (int k = 0; k < arranjo.Count; k++)
        {
            var lado = gruposNoLado[ladoDoJogo[k]];
            var (mandante, adversario) = arranjo[k];
            if (mandante.Grupo == adversario.Grupo || !lado.Add(mandante.Grupo) || !lado.Add(adversario.Grupo))
                return false;
        }

        return true;
    }

    // Teto de passos da busca. Uma chave real tem no máximo 16 jogos e o arranjo aparece
    // nos primeiros passos; o teto existe pra dado torto não virar laço longo dentro do
    // finalizar de um jogo — estourou, vale a semeadura de sempre.
    private const int TetoDaBusca = 5_000;

    // Um arranjo SEM reencontro antes da final, mantendo a preferência de sempre: o melhor
    // ainda sem jogo abre o próximo, contra o pior que ainda cabe — no primeiro jogo em que
    // os dois cabem. Null = não existe (ou a busca estourou o teto).
    //
    // A escolha do "melhor sem jogo" não é ramificação: ele PRECISA estar em algum jogo, então
    // percorrer todos os adversários e todos os jogos dele cobre todo arranjo possível. A
    // busca volta atrás quando um caminho não fecha — é o que resolve o Er (o 1º A cabe no
    // jogo 2 com o 2º B, e o jogo 1 fica pros 2ºs de C e A) e também o quadro de 5 grupos,
    // onde as vagas obrigam dois "de cima" a se enfrentar.
    private static List<Jogo>? ProcurarArranjoPerfeito(
        List<Classificado> cabecas, int[] ladoDoJogo, HashSet<string>[] gruposDosByes)
    {
        // Três ou mais do mesmo grupo não cabem em duas metades sem repetir: com 3+
        // classificados por grupo a promessa não existe, e não há o que procurar.
        var quantosPorGrupo = cabecas.Select(c => c.Grupo)
            .Concat(gruposDosByes.SelectMany(g => g))
            .GroupBy(g => g).Select(g => g.Count());
        if (quantosPorGrupo.Any(n => n > 2)) return null;

        int jogos = cabecas.Count / 2;
        var gruposNoLado = new[] { new HashSet<string>(gruposDosByes[0]), new HashSet<string>(gruposDosByes[1]) };
        var jaTemJogo = new bool[cabecas.Count];
        var arranjo = new Jogo?[jogos];
        int passos = 0;

        bool Preencher()
        {
            int x = Array.IndexOf(jaTemJogo, false);
            if (x < 0) return true;
            if (++passos > TetoDaBusca) return false;

            var grupoDeX = cabecas[x].Grupo;
            jaTemJogo[x] = true;

            for (int y = cabecas.Count - 1; y > x; y--)
            {
                var grupoDeY = cabecas[y].Grupo;
                if (jaTemJogo[y] || grupoDeY == grupoDeX) continue;

                for (int jogo = 0; jogo < jogos; jogo++)
                {
                    var lado = gruposNoLado[ladoDoJogo[jogo]];
                    if (arranjo[jogo] != null || lado.Contains(grupoDeX) || lado.Contains(grupoDeY)) continue;

                    jaTemJogo[y] = true;
                    lado.Add(grupoDeX);
                    lado.Add(grupoDeY);
                    arranjo[jogo] = new Jogo(cabecas[x], cabecas[y]);

                    if (Preencher()) return true;

                    arranjo[jogo] = null;
                    lado.Remove(grupoDeY);
                    lado.Remove(grupoDeX);
                    jaTemJogo[y] = false;
                }
            }

            jaTemJogo[x] = false;
            return false;
        }

        if (!Preencher()) return null;

        var completo = new List<Jogo>(jogos);
        foreach (var jogo in arranjo)
        {
            if (jogo is not Jogo preenchido) return null;   // não acontece: Preencher só devolve true com tudo preenchido
            completo.Add(preenchido);
        }
        return completo;
    }

    // A metade da chave (0 ou 1) em que cai a vaga `vaga` de uma rodada com `vagas`
    // participantes, seguindo a régua de ParearVencedores — primeiro × último, rodada após
    // rodada, até sobrarem os dois lados da final. A vaga i cruza com a vaga n-1-i e o
    // vencedor ocupa a vaga min(i, n-1-i) da rodada seguinte.
    //
    // ⚠️ Não sai de Services/OrdemDoQuadro de propósito: aquela ordem é de DESENHO e supõe
    // que a rodada seguinte tem metade dos jogos desta — o que só vale sem bye. Com bye a
    // rodada seguinte tem (jogos + byes) / 2, e a conta de lá diverge da chave de verdade
    // (7 grupos: 6 jogos + 2 byes). Quem manda é a régua que o robô aplica.
    private static int LadoDaVaga(int vagas, int vaga)
    {
        while (vagas > 2)
        {
            vaga = Math.Min(vaga, vagas - 1 - vaga);
            vagas /= 2;
        }
        return vaga;
    }

    // ---- CHAVE DIRETA: mata-mata sem fase de grupos ----

    // Teto de uma chave direta. 32 duplas = 5 rodadas e 31 jogos; acima disso o quadro
    // precisaria de um nome de fase que não existe (e é evento de outro tamanho).
    public const int MaximoDeDuplasNaChaveDireta = 32;

    public record PrimeiraRodadaDaChave(string Fase, List<Confronto> Confrontos, List<int> Byes);

    // Recusa ANTES do sorteio, quando ajustar ainda é de graça. Null = pode sortear.
    public static string? ProblemaNaChaveDireta(int duplas) => duplas switch
    {
        < 2 => "Uma chave direta precisa de pelo menos 2 duplas.",
        > MaximoDeDuplasNaChaveDireta => $"O máximo de uma chave direta é {MaximoDeDuplasNaChaveDireta} duplas.",
        _ => null
    };

    public static int MenorPotenciaDe2APartirDe(int minimo)
    {
        int p = 1;
        while (p < minimo) p *= 2;
        return p;
    }

    // Monta a PRIMEIRA rodada de um mata-mata puro a partir das duplas já na ordem do
    // sorteio (embaralhadas por quem chamou — aqui não existe cabeça de chave: numa chave
    // direta de duplas remontadas ninguém tem campanha, e ranking fingido seria pior que
    // sorteio limpo).
    //
    // O total quase nunca é potência de 2, e é aí que mora a regra: o quadro é a menor
    // potência de 2 que CABE todo mundo, e as vagas que sobram viram BYE — a dupla não joga
    // a primeira rodada e entra direto na segunda. Com 24 duplas: quadro de 32, 8 byes e
    // 8 jogos (as outras 16). Os byes saem do começo da lista sorteada.
    //
    // Quem recebe os byes é devolvido à parte porque o robô de progressão precisa somá-los
    // aos vencedores pra fechar a rodada seguinte — sozinhos, os 8 vencedores fariam 4 jogos
    // e os 8 que pegaram bye sumiriam do torneio sem perder.
    public static PrimeiraRodadaDaChave MontarChaveDireta(IReadOnlyList<int> duplasSorteadas)
    {
        if (duplasSorteadas.Count < 2 || duplasSorteadas.Count > MaximoDeDuplasNaChaveDireta)
            return new PrimeiraRodadaDaChave("", new List<Confronto>(), new List<int>());

        int quadro = MenorPotenciaDe2APartirDe(duplasSorteadas.Count);
        int byes = quadro - duplasSorteadas.Count;

        var passamDireto = duplasSorteadas.Take(byes).ToList();
        var jogam = duplasSorteadas.Skip(byes).ToList();

        // Mesma semeadura do mata-mata pós-grupos: primeiro da metade de cima x último da
        // metade de baixo, pra que os dois lados da chave se encontrem só no fim.
        var alta = jogam.Take(jogam.Count / 2).ToList();
        var baixa = jogam.Skip(jogam.Count / 2).Reverse().ToList();

        var confrontos = new List<Confronto>(alta.Count);
        for (int i = 0; i < alta.Count; i++)
            confrontos.Add(new Confronto(alta[i], baixa[i]));

        return new PrimeiraRodadaDaChave(NomeFase(quadro), confrontos, passamDireto);
    }

    // Pareia os vencedores de uma fase concluída para a próxima (1º x último da lista).
    //
    // ⚠️ É esta regra, repetida rodada após rodada, que define a metade da chave em que cada
    // vaga cai — a semeadura da primeira fase (Semear/LadoDaVaga) conta com ela pra pôr os
    // dois classificados de um grupo em lados opostos. Mudar o cruzamento aqui muda os
    // lados lá.
    public static List<Confronto> ParearVencedores(IReadOnlyList<int> vencedores)
    {
        var confrontos = new List<Confronto>(vencedores.Count / 2);
        for (int i = 0; i < vencedores.Count / 2; i++)
            confrontos.Add(new Confronto(vencedores[i], vencedores[vencedores.Count - 1 - i]));
        return confrontos;
    }
}
