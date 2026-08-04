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
//   opostas e só podem se reencontrar NA FINAL.
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
        var passamDireto = candidatos.Take(quadro - candidatos.Count).Select(c => c.DuplaId).ToList();
        var cabecas = candidatos.Skip(passamDireto.Count).ToList();

        // Os melhores abrem os jogos, na ordem; o resto é distribuído contra eles.
        var mandantes = cabecas.Take(cabecas.Count / 2).ToList();
        var adversarios = cabecas.Skip(cabecas.Count / 2).ToList();   // do melhor pro pior

        // ⚠️ QUEM CAI DE QUAL LADO DA CHAVE.
        //
        // Não basta cruzar "melhor x pior": os dois classificados de um MESMO GRUPO precisam
        // cair em METADES OPOSTAS do quadro. Senão eles se reencontram já na semifinal —
        // dois que acabaram de se enfrentar na fase de grupos decidindo vaga na final, e um
        // deles eliminado por quem já tinha enfrentado. Só podem se cruzar de novo NA FINAL.
        //
        // O desenho antigo (melhor x pior direto, invertendo a lista) produzia exatamente
        // isso: com 4 grupos, o 1ºA saía no jogo 1 e o 2ºA no jogo 4 — e os jogos 1 e 4
        // desembocam na MESMA semifinal.
        var ladoDoJogo = LadosDoQuadro(mandantes.Count);
        var gruposNoLado = new[] { new HashSet<string>(), new HashSet<string>() };

        for (int i = 0; i < mandantes.Count; i++)
            gruposNoLado[ladoDoJogo[i]].Add(mandantes[i].Grupo);

        var confrontos = new List<Confronto>(mandantes.Count);

        for (int i = 0; i < mandantes.Count; i++)
        {
            var escolhido = EscolherAdversario(adversarios, mandantes[i], gruposNoLado[ladoDoJogo[i]]);
            adversarios.Remove(escolhido);
            gruposNoLado[ladoDoJogo[i]].Add(escolhido.Grupo);
            confrontos.Add(new Confronto(mandantes[i].DuplaId, escolhido.DuplaId));
        }

        return (NomeFase(quadro), confrontos, passamDireto);
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

    // A qual metade do quadro cada jogo pertence (0 ou 1), na ordem de criação.
    //
    // Sai do desenho da chave: dois jogos estão na mesma metade quando os vencedores deles
    // podem se encontrar antes da final. Services/OrdemDoQuadro sabe essa geometria — a
    // primeira metade da ordem de desenho é um lado, a segunda é o outro.
    private static int[] LadosDoQuadro(int jogos)
    {
        var ordem = OrdemDoQuadro.De(jogos);
        var lado = new int[jogos];

        for (int posicao = 0; posicao < ordem.Count; posicao++)
            lado[ordem[posicao] - 1] = posicao < (ordem.Count + 1) / 2 ? 0 : 1;

        return lado;
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
    public static List<Confronto> ParearVencedores(IReadOnlyList<int> vencedores)
    {
        var confrontos = new List<Confronto>(vencedores.Count / 2);
        for (int i = 0; i < vencedores.Count / 2; i++)
            confrontos.Add(new Confronto(vencedores[i], vencedores[vencedores.Count - 1 - i]));
        return confrontos;
    }
}
