namespace Padelizou.Services;

// A régua técnica que o professor usa pra avaliar aluno: módulo → família → fundamento.
//
// Veio da planilha que o Felipe mandou em 05/08/2026 — o sistema de um professor de verdade,
// com 146 fundamentos. Ela é o PONTO DE PARTIDA de cada professor, não a lei: por decisão do
// Felipe, cada um recebe esta lista e depois edita, acrescenta e remove a dela (por isso o
// catálogo do professor vive no BANCO, e isto aqui só semeia).
//
// ⚠️ Consequência de ser editável, pra quem for mexer nisto depois: a nota de um aluno NÃO é
// comparável entre professores. "B em Bandeja (contenção centro)" com o Medina e com o Jonatas
// podem ser réguas diferentes — então nada de somar isso num ranking global.
//
// O que foi ajustado da planilha original, porque aqui vira conteúdo de produto e não anotação
// pessoal: os itens "testes", "Testes" e "testesw" (o mesmo item escrito de três jeitos)
// viraram um "Testes" por módulo, e a linha "utilização 5 taticas", que aparecia duplicada em
// seguida, entra uma vez só.
public static class CatalogoDeFundamentos
{
    public const string Iniciante = "Iniciante";
    public const string Intermediario = "Intermediário";
    public const string Avancado = "Avançado";

    // Família vazia = o módulo não agrupa (Iniciante e Avançado são listas corridas).
    public record FundamentoPadrao(string Modulo, string Familia, string Nome);

    public static readonly IReadOnlyList<string> Modulos = new[] { Iniciante, Intermediario, Avancado };

    public static IReadOnlyList<FundamentoPadrao> Todos { get; } = Montar();

    public static IEnumerable<FundamentoPadrao> DoModulo(string modulo) =>
        Todos.Where(f => f.Modulo == modulo);

    // As famílias de um módulo, na ordem em que aparecem.
    public static IReadOnlyList<string> FamiliasDe(string modulo) =>
        DoModulo(modulo).Select(f => f.Familia).Where(fa => fa != "").Distinct().ToList();

    private static List<FundamentoPadrao> Montar()
    {
        var lista = new List<FundamentoPadrao>();

        void Add(string modulo, string familia, params string[] nomes)
        {
            foreach (var nome in nomes) lista.Add(new FundamentoPadrao(modulo, familia, nome));
        }

        Add(Iniciante, "",
            "Drive e revés fundo de quadra sem vidro",
            "Drive e revés fundo com vidro",
            "Voleio drive e revés e bloqueio de smash",
            "Paredes laterais",
            "Smash e bandeja",
            "Lobby com e sem parede",
            "Voleio abaixo da rede",
            "Contra parede",
            "Giros de parede no fundo",
            "Saque",
            "Gancho e bandeja",
            "Víbora",
            "Parabrisa: saque e sobe (zonas da quadra)",
            "Parabrisa: fundo de quadra",
            "Parabrisa: voleios");

        Add(Intermediario, "Drive e revés de fundo",
            "Drive e revés sem vidro com direção (profundidade)",
            "Drive e revés com vidro com direção (profundidade)",
            "Drive e revés sem vidro com direção (corpo)",
            "Drive e revés com vidro com direção (ímpares)",
            "Drive e revés (soma 10)",
            "Tiquita centro e tiquita diagonal (drive)",
            "Tiquita de revés (paralela)",
            "Ênfase na defesa: semáforo");

        Add(Intermediario, "Lobby",
            "Lobby ao T (drive e revés)",
            "Lobby com e sem vidro, chutado (drive e revés)",
            "Lobby ao T por acertos (drive e revés)",
            "Lobby escondendo o golpe (drive e revés)");

        Add(Intermediario, "Vidro lateral",
            "Parede lateral com direção (corpo)",
            "Parede lateral com direção (lobby e paralela)",
            "Parede lateral com direção (lobby centro)",
            "Parede lateral com direção (baixada diagonal)",
            "Parede lateral com direção (baixada paralela)",
            "Parede lateral com direção (baixada tela diagonal)",
            "Giros de parede");

        Add(Intermediario, "Devolução de saque",
            "Devolução de saque (corpo paralela)",
            "Devolução de saque (buscando alto no revés)",
            "Devolução de saque (firme diagonal)",
            "Devolução de saque (baixa diagonal)",
            "Devolução de saque (lobby paralela)",
            "Devolução de saque (lobby centro)",
            "Devolução de saque (lobby diagonal)");

        Add(Intermediario, "Voleio",
            "Voleio drive e revés (direção vidros ímpares)",
            "Voleio revés (tela)",
            "Voleio drive (tela)",
            "Voleio drive (diagonal profundo)",
            "Voleio revés (diagonal profundo)",
            "Voleios drive e revés (profundidade)",
            "Voleios drive e revés (a morrer, cortado)",
            "Voleio drive (diagonal agressivo)",
            "Voleio drive (paralela agressivo)",
            "Voleio revés (defesa alto)",
            "Incremento de potência no voleio drive e revés",
            "Voleio nos pés",
            "Incremento de mira no voleio drive e revés (vidro 3)",
            "Incremento de mira no voleio drive e revés (acertos)");

        Add(Intermediario, "Bandeja",
            "Bandeja (potência)",
            "Bandeja (rebote + potência)",
            "Bandeja (contenção centro)",
            "Bandeja (contenção vidro lateral)",
            "Bandeja (jogo de decisões)",
            "Bandeja (taxa de acertos)",
            "Bandeja (profundidade + acertos)",
            "Rulo",
            "Rulo (chapado)",
            "Rulo (spin)");

        Add(Intermediario, "Víbora",
            "Víbora (potência)",
            "Víbora (rebote + potência)",
            "Víbora (contenção centro)",
            "Víbora (contenção vidro lateral)",
            "Víbora (jogo de decisões)",
            "Víbora (taxa de acertos)",
            "Víbora (profundidade + acertos)");

        Add(Intermediario, "Saída de parede",
            "Saída de parede: semáforo",
            "Saída de parede: semáforo corporal",
            "Saída de parede chapada (centro e cruzada)",
            "Saída de parede lenta (diagonal)",
            "Saída de parede lenta (paralela)",
            "Saída de parede + subida (decisão)",
            "Saída de parede: incremento de potência",
            "Contra parede (após a linha)");

        Add(Intermediario, "Smash",
            "Smash de definição",
            "Smash pelos 4",
            "Smash pelos 3",
            "Smash para voltar",
            "Ênfase na zona de smash",
            "Ênfase na zona de smash: semáforo",
            "Incremento de potência no smash para voltar");

        Add(Intermediario, "Táticas",
            "Tirar o adversário da rede: chiquita + passada no meio",
            "Tirar o adversário da rede: chiquita + lob",
            "Tirar o adversário da rede: lob + antecipação",
            "Tirar o adversário da rede: chiquita + antecipação",
            "Tirar o adversário da rede: lobby cruzado + antecipação na quina",
            "Tática de rede + profundidade",
            "Recuperação da tiquita",
            "Retomada de rede",
            "Cobrir a rede: parabrisa",
            "Voleio e bloqueio de smash com direção (vidros ímpares)",
            "Resistência de fundo",
            "Resistência de rede",
            "Resistência de smashes",
            "Retorno de bandeja com voleio agressivo",
            "Saque + tela",
            "Saque + smash inverno",
            "Defesa de rulo",
            "Smash inverno (surpresa)");

        Add(Avancado, "",
            "Controle de bola",
            "Regularidade na saída por baixo",
            "Treino de defesa em paredes laterais",
            "Treinamento de bloqueio e voleios",
            "Treinamento de voleio por zonas + voleio lob",
            "Golpes cortados de fundo de quadra (zona verde)",
            "Golpes cortados: chiquitas anguladas",
            "Bandejas defensivas",
            "Saque slice + poach",
            "Saídas de parede sem peso",
            "Saídas de parede planas + antecipação de voleio",
            "Giros de parede desde a rede",
            "Chiquita + lob + antecipação",
            "Treinos de percentual",
            "Contra-ataque: saída de parede rápida",
            "Voleio na tela e no meio",
            "Voleio a morrer, muito cortado",
            "Bandeja + gancho + recomposição",
            "Víbora + recomposição",
            "Bate-pronto na zona morta + bate-pronto na rede",
            "Smashes ofensivos: víbora + voltar + 3 + 4",
            "Ênfase em smash + víbora + voltar + 4 + 3",
            "Bola no corpo + 5 táticas",
            "Smashes + retomada de voleio",
            "Voleios planos + cortados",
            "Leitura de bola na rede",
            "Baixada de parede",
            "Fortalecimento de golpes aéreos",
            "Fortalecimento de defesa",
            "Fortalecimento dos voleios",
            "Buscando táticas de tirar da rede",
            "Velocidade de golpes (defesa)",
            "Fortalecimento de smashes",
            "Recuperação de rede",
            "Fortalecimento de voleios",
            "Utilização das 5 táticas",
            "Saída de parede rápida e lenta",
            "Testes");

        return lista;
    }
}
