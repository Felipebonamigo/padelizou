using System.Text;

namespace Padelizou.Services;

// O CHAVEAMENTO DESENHADO À MÃO — quem cruza com quem na primeira eliminatória.
//
// 🗣️ Felipe, 10/09/2026: *"permita alterar na mao o chaveamento, como funciona a chave de cada um,
// se o primeiro passar quem enfrenta, etc (obviamente que apenas organizadores e adm do sistema
// podem fazer isso)"* — e, no mesmo fôlego: *"cuidado para nao mexer nada no que ja tem do ER hoje,
// isso é para os próximos torneios"*.
//
// ⚠️ É A SEGUNDA FRASE QUE DESENHA ESTE ARQUIVO. O cruzamento mora num campo NOVO e ANULÁVEL
// (`Categoria.CruzamentoDoMataMata`), e **null quer dizer "o motor decide" — o comportamento de
// hoje, letra por letra**. Toda categoria que já existe no banco nasce e continua null, então o Er
// não muda porque nenhum caminho de código novo passa por ele. A régua nova inteira vive atrás de
// um `if` que só abre quando alguém desenhou.
//
// ── POR QUE SÓ A PRIMEIRA RODADA (decisão do Felipe) ─────────────────────────────────────────
// Da segunda em diante o quadro é GEOMETRIA, não escolha: a lista [vencedores na ordem dos jogos,
// byes do melhor pro pior] cruzada primeiro × último, repetida até a final (AvancoDaChave). Deixar
// editar isso permitiria desenhar uma chave que não fecha. Editando a primeira, o caminho inteiro
// já muda junto — que é o "se o primeiro passar quem enfrenta" do pedido.
//
// ── O FORMATO ────────────────────────────────────────────────────────────────────────────────
// `1A×2C|1B×2D;bye:1E` — legível de propósito: quem for ler o banco às 3h da manhã de um torneio
// precisa entender sem abrir o código. A ordem dos jogos é a ordem do quadro, e é ela que decide
// as metades.
public static class CruzamentoDoMataMata
{
    // Uma vaga do quadro, por COLOCAÇÃO: "1º do Grupo A" antes de se saber quem é.
    public record Vaga(int Posicao, string Grupo)
    {
        public string Rotulo => $"{Posicao}º do Grupo {Grupo}";
        public override string ToString() => $"{Posicao}{Grupo}";
    }

    public record Confronto(Vaga Lado1, Vaga Lado2);

    public record Mapa(IReadOnlyList<Confronto> Confrontos, IReadOnlyList<Vaga> Byes)
    {
        public string Escrever()
        {
            var texto = new StringBuilder(string.Join("|", Confrontos.Select(c => $"{c.Lado1}×{c.Lado2}")));
            if (Byes.Count > 0) texto.Append(";bye:").Append(string.Join(",", Byes));
            return texto.ToString();
        }

        // Todas as vagas citadas, na ordem do quadro (jogos primeiro, byes depois) — é a ordem que
        // AvancoDaChave usa pra montar a rodada seguinte, e por isso a que decide as metades.
        public IEnumerable<Vaga> Todas => Confrontos.SelectMany(c => new[] { c.Lado1, c.Lado2 }).Concat(Byes);
    }

    /// <summary>
    /// Lê o desenho guardado. <c>null</c> = não há desenho (ou ele está corrompido), e aí quem
    /// decide é o motor de sempre — nunca uma categoria sem mata-mata.
    /// </summary>
    public static Mapa? Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        var partes = texto.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var confrontos = new List<Confronto>();
        var byes = new List<Vaga>();

        foreach (var jogo in partes[0].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var lados = jogo.Split('×', StringSplitOptions.TrimEntries);
            if (lados.Length != 2) return null;
            if (LerVaga(lados[0]) is not { } lado1 || LerVaga(lados[1]) is not { } lado2) return null;
            confrontos.Add(new Confronto(lado1, lado2));
        }

        foreach (var extra in partes.Skip(1))
        {
            if (!extra.StartsWith("bye:", StringComparison.OrdinalIgnoreCase)) return null;
            foreach (var vaga in extra[4..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (LerVaga(vaga) is not { } lida) return null;
                byes.Add(lida);
            }
        }

        if (confrontos.Count == 0) return null;
        return new Mapa(confrontos, byes);
    }

    // "1A" → (1, "A"). Grupo é o que vem depois do número, e vale qualquer nome (o cadastro deixa
    // renomear grupo); só não pode ser vazio.
    private static Vaga? LerVaga(string? texto)
    {
        texto = texto?.Trim();
        if (string.IsNullOrEmpty(texto) || !char.IsDigit(texto[0])) return null;

        int corte = 0;
        while (corte < texto.Length && char.IsDigit(texto[corte])) corte++;
        if (corte == texto.Length) return null;                       // só número, sem grupo

        return new Vaga(int.Parse(texto[..corte]), texto[corte..]);
    }

    /// <summary>
    /// O desenho que o MOTOR faria — o que o organizador vê ao abrir a tela.
    /// </summary>
    /// <remarks>
    /// ⚠️ Abrir o formulário e salvar sem mexer em nada tem que produzir a MESMA chave de antes.
    /// Por isso o padrão sai de <see cref="ChaveProjetada"/>, e não de uma conta paralela: a
    /// primeira visita à tela não pode mudar o torneio.
    /// </remarks>
    public static Mapa? Padrao(IReadOnlyList<string> grupos, int classificadosPorGrupo = 2,
        IReadOnlyList<int>? duplasPorGrupo = null)
    {
        var (_, confrontos, byes) = ChaveProjetada.Montar(grupos, classificadosPorGrupo, duplasPorGrupo);
        if (confrontos.Count == 0) return null;

        static Vaga Converter(ChaveProjetada.Vaga v) => new(v.Posicao, SemPrefixo(v.Grupo));

        return new Mapa(
            confrontos.Select(c => new Confronto(Converter(c.Lado1), Converter(c.Lado2))).ToList(),
            byes.Select(Converter).ToList());
    }

    /// <summary>
    /// Aplica o desenho: devolve a primeira fase como o motor devolveria, mas com os cruzamentos
    /// que o organizador escolheu.
    /// </summary>
    public static (string Fase, List<ChaveamentoMataMata.Confronto> Confrontos, List<int> Byes) Aplicar(
        Mapa mapa, IReadOnlyCollection<ChaveamentoMataMata.Classificado> classificados)
    {
        int IdDa(Vaga vaga) => classificados
            .First(c => c.Posicao == vaga.Posicao && SemPrefixo(c.Grupo) == vaga.Grupo).DuplaId;

        int quadro = ChaveamentoMataMata.MenorPotenciaDe2APartirDe(
            mapa.Confrontos.Count * 2 + mapa.Byes.Count);

        return (
            ChaveamentoMataMata.NomeFase(quadro),
            mapa.Confrontos.Select(c => new ChaveamentoMataMata.Confronto(IdDa(c.Lado1), IdDa(c.Lado2))).ToList(),
            mapa.Byes.Select(IdDa).ToList());
    }

    /// <summary>
    /// O motivo pra NÃO usar o desenho, ou <c>null</c> se ele serve. Desenho que não serve é
    /// descartado e o motor decide — nunca uma categoria sem mata-mata.
    /// </summary>
    public static string? Conferir(Mapa mapa, IReadOnlyCollection<ChaveamentoMataMata.Classificado> classificados)
    {
        var todas = mapa.Todas.ToList();

        var repetida = todas.GroupBy(v => v.ToString()).FirstOrDefault(g => g.Count() > 1);
        if (repetida != null)
            return $"A mesma vaga ({repetida.First().Rotulo}) aparece mais de uma vez.";

        var doTorneio = classificados
            .Select(c => new Vaga(c.Posicao, SemPrefixo(c.Grupo)).ToString())
            .ToHashSet();

        var inventada = todas.FirstOrDefault(v => !doTorneio.Contains(v.ToString()));
        if (inventada != null)
            return $"{inventada.Rotulo} não existe nesta categoria.";

        var deFora = doTorneio.Except(todas.Select(v => v.ToString())).ToList();
        if (deFora.Count > 0)
            return $"{deFora.Count} vaga(s) que classificam ficaram de fora do desenho.";

        return null;
    }

    /// <summary>
    /// O que está torto no desenho mas o organizador pode querer assim.
    /// </summary>
    /// <remarks>
    /// 🗣️ Decisão do Felipe (10/09/2026): *avisa e deixa passar*. A régua de metades — dois do
    /// mesmo grupo só se reencontram na FINAL — é convenção forte, e o motor a persegue sozinho
    /// (ChaveamentoMataMata.Semear); mas em categoria pequena às vezes não há como evitar, e às
    /// vezes ele QUER o cruzamento. Mesma postura do Conferir grade: conta, não proíbe.
    /// </remarks>
    public static List<string> Avisos(Mapa mapa, IReadOnlyCollection<ChaveamentoMataMata.Classificado> classificados)
    {
        var avisos = new List<string>();
        var todas = mapa.Todas.ToList();
        int vagas = mapa.Confrontos.Count + mapa.Byes.Count;

        // A metade de cada vaga: as duas de um jogo dividem o lado do jogo; o bye tem o lado dele.
        var metadePorVaga = new Dictionary<string, int>();
        for (int i = 0; i < mapa.Confrontos.Count; i++)
        {
            int lado = ChaveamentoMataMata.LadoDaVaga(vagas, i);
            metadePorVaga[mapa.Confrontos[i].Lado1.ToString()] = lado;
            metadePorVaga[mapa.Confrontos[i].Lado2.ToString()] = lado;
        }
        for (int b = 0; b < mapa.Byes.Count; b++)
            metadePorVaga[mapa.Byes[b].ToString()] = ChaveamentoMataMata.LadoDaVaga(vagas, mapa.Confrontos.Count + b);

        foreach (var doGrupo in todas.GroupBy(v => v.Grupo).Where(g => g.Count() > 1))
        {
            var metades = doGrupo.Select(v => metadePorVaga.GetValueOrDefault(v.ToString(), -1)).Distinct().ToList();
            if (metades.Count == 1)
            {
                avisos.Add($"Grupo {doGrupo.Key}: {string.Join(" e ", doGrupo.Select(v => v.Rotulo))} "
                         + "caem na mesma metade da chave — eles se reencontram antes da final.");
            }
        }

        return avisos;
    }

    // Os grupos chegam ora como "Grupo A", ora como "A" (o desenho guarda a letra). Uma régua só,
    // aqui, pra que os dois lados sempre se reconheçam.
    private static string SemPrefixo(string grupo) =>
        grupo.StartsWith("Grupo ", StringComparison.OrdinalIgnoreCase) ? grupo["Grupo ".Length..] : grupo;
}
