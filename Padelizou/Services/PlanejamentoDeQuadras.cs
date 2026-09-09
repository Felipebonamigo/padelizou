using System.Globalization;

namespace Padelizou.Services;

// QUANTA QUADRA O TORNEIO PRECISA — a conta que o organizador faz ANTES de alugar (09/09/2026).
//
// 🗣️ Pedido do Felipe, pelo Er: "eles só têm 2 quadras (…) querem uma aba que possam ver
// quantos horários teriam que locar de quadra, extra, para fechar os jogos da chave (…) uma
// previsão de quantos jogos precisaria colocar lá na sexta, e no sábado".
//
// ── POR QUE ISTO NÃO É O `PrevisaoDoTorneio` ──────────────────────────────────────────────
// São duas perguntas OPOSTAS, e é por isso que são dois serviços:
//
//   • `PrevisaoDoTorneio` responde ONDE A GRADE TERMINA. O calendário é livre: se não couber
//     no domingo, o torneio vai pra segunda. A resposta é uma DATA.
//   • Aqui o calendário é a TRAVA — o clube está alugado até domingo às 14h e ponto —, e a
//     resposta é O QUE FALTA pra caber nele: quadras, ou horários pra alugar.
//
// Uma não substitui a outra, e as duas aparecem juntas na aba: "termina domingo 20h30" ao lado
// de "mas você queria ir embora 14h — faltam 15 horários".
//
// ── A REGRA QUE NÃO PODE SER COPIADA PRA CÁ ───────────────────────────────────────────────
// ⚠️ O FORMATO DE UM DIA (quantas rodadas cabem, a que horas a última começa) continua sendo
// do MOTOR do sorteio: `GradeDeJogos.RodadasPorDia` e `GradeDeJogos.UltimoInicioDoDia`. Nada
// aqui recalcula isso. Este projeto já pagou por uma segunda cópia dessas contas — a tela de
// criação as tinha em JavaScript, com um comentário admitindo que divergir faria a tela mentir
// pro organizador (ver o cabeçalho de `PrevisaoDoTorneio`). Uma aba de planejamento que
// divergisse do sorteio seria a mesma mentira, com a quadra já alugada.
//
// O que É novo aqui, e não existe no motor, é a CAPACIDADE: rodada × quadra = vaga.
//
// ── O LIMITE POR DIA MORA SÓ AQUI ─────────────────────────────────────────────────────────
// ⚠️ O torneio tem UMA `HoraFimDoDia` pra todos os dias, e continua tendo — coluna nova seria
// migration, e migration é outro nível de cerimônia. O limite por dia é entrada de
// PLANEJAMENTO: serve pra responder "e se eu quiser ir embora domingo às 14h?", não pra
// prometer que o sorteio vai obedecer. Quem faz o sorteio caber mais cedo é ter mais quadra —
// que é exatamente o que esta conta manda comprar. A tela diz isso com todas as letras.
public static class PlanejamentoDeQuadras
{
    // Teto do laço. O prazo de um torneio é um fim de semana; 30 dias existe só pra que uma
    // data absurda vinda da URL não vire uma lista gigante.
    public const int MaximoDeDias = 30;

    /// <summary>Um dia de quadra: o que ele comporta e o que de fato entra nele.</summary>
    /// <param name="Vagas">Rodadas × quadras — o teto do dia.</param>
    /// <param name="Jogos">Quantos dos jogos do torneio caem aqui.</param>
    /// <param name="UltimoComeca">Até que horas o dia ACEITARIA jogo (o formato do dia).</param>
    /// <param name="UltimoJogoComeca">Quando o último jogo DE VERDADE entra. Nulo se o dia fica vazio.</param>
    public sealed record Dia(
        DateTime Data,
        TimeSpan Abre,
        TimeSpan Limite,
        int Rodadas,
        int Vagas,
        int Jogos,
        TimeSpan? UltimoComeca,
        TimeSpan? UltimoJogoComeca);

    public sealed record Plano
    {
        public IReadOnlyList<Dia> Dias { get; init; } = Array.Empty<Dia>();

        public int TotalDeJogos { get; init; }
        public int Quadras { get; init; }
        public int DuracaoMinutos { get; init; }

        // Capacidade dentro do calendário planejado, e o que ela absorve.
        public int Vagas { get; init; }
        public int Alocados { get; init; }

        // Os jogos que NÃO cabem no prazo. É zero quando não há prazo: aí a grade só avança.
        public int Faltam { get; init; }
        public int Sobram { get; init; }

        // A folga que o encaixe pede pra desviar de impedimento sem dobrar ninguém — o número
        // é do motor (`GradeDeJogos.MargemDeHorarios`), não uma cópia.
        public int Margem { get; init; }
        public bool FolgaSuficiente { get; init; }

        // AS DUAS SAÍDAS DO MESMO BURACO: uma quadra a mais no fim de semana inteiro, ou
        // horários avulsos de quadra alugada. `HorariosExtras` é vaga (1 quadra × 1 rodada).
        public int QuadrasNecessarias { get; init; }
        public int HorariosExtras { get; init; }
        public int MinutosExtras { get; init; }

        public bool PrazoDefinido { get; init; }
        public DateTime? UltimoJogoComeca { get; init; }
        public DateTime? UltimoJogoTermina { get; init; }

        // Nenhum dia do calendário aceita jogo (o limite não passa da abertura). Sem isto a
        // tela mostraria uma grade vazia sem dizer por quê.
        public bool DiaSemHoraPraAcabar { get; init; }
    }

    /// <summary>
    /// O calendário do torneio dia a dia: quanto cabe, quanto entra e o que falta pra fechar
    /// dentro de <paramref name="ate"/>.
    /// </summary>
    /// <param name="ate">Último dia de quadra (o prazo). Nulo = sem prazo: a grade avança até caber.</param>
    /// <param name="limitesPorDia">Hora limite de um dia específico, vinda da tela de planejamento.</param>
    public static Plano Montar(
        DateTime inicio,
        TimeSpan aberturaDiasSeguintes,
        TimeSpan limitePadrao,
        int quadras,
        int duracaoMinutos,
        int totalDeJogos,
        DateTime? ate = null,
        IReadOnlyDictionary<DateTime, TimeSpan>? limitesPorDia = null)
    {
        // Mesma normalização do motor (0 vira 50): torneio com o tempo zerado existe, e sem
        // ela a divisão por duração estouraria — ver GradeDeJogos.Horarios.
        quadras = Math.Max(1, quadras);
        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;
        totalDeJogos = Math.Max(0, totalDeJogos);

        var dias = new List<Dia>();
        int restantes = totalDeJogos;
        int vagas = 0, rodadasTotais = 0;
        DateTime? ultimoJogo = null;

        for (int i = 0; i < MaximoDeDias; i++)
        {
            var data = inicio.Date.AddDays(i);

            // Sem prazo, o laço para quando os jogos acabam. Com prazo, ele varre o calendário
            // inteiro — os dias vazios do fim são capacidade que sobra, e é justamente ela que
            // o organizador olha pra decidir se dá pra ir embora mais cedo.
            if (ate == null)
            {
                if (restantes <= 0) break;
            }
            else if (data > ate.Value.Date)
            {
                break;
            }

            var abre = i == 0 ? inicio.TimeOfDay : aberturaDiasSeguintes;
            var limite = limitesPorDia != null && limitesPorDia.TryGetValue(data, out var proprio)
                ? proprio
                : limitePadrao;

            // Nulo é dia que não aceita jogo nenhum — o limite não passa da abertura. É a
            // guarda do próprio motor, e aqui ela ganha um segundo uso legítimo: é assim que o
            // organizador diz "domingo eu não jogo".
            //
            // ⚠️ O DIA VAZIO CONTINUA NA LISTA, e isso é requisito de tela, não capricho: é na
            // linha do dia que o organizador digita a hora de fechar. Sumir com a linha de
            // domingo no instante em que ele escreve "domingo eu não jogo" tiraria da tela o
            // único campo capaz de desfazer aquilo.
            var rodadas = GradeDeJogos.RodadasPorDia(abre, limite, duracaoMinutos) ?? 0;

            int vagasDoDia = rodadas * quadras;
            int jogosDoDia = Math.Min(vagasDoDia, Math.Max(restantes, 0));

            // O último jogo do dia entra na ceil(jogos ÷ quadras)-ésima rodada: as quadras
            // enchem antes de o relógio andar, que é a regra do GradeDeJogos.Horarios.
            TimeSpan? ultimoJogoDoDia = jogosDoDia > 0
                ? abre + TimeSpan.FromMinutes(((jogosDoDia + quadras - 1) / quadras - 1) * duracaoMinutos)
                : null;

            dias.Add(new Dia(
                data, abre, limite, rodadas, vagasDoDia, jogosDoDia,
                GradeDeJogos.UltimoInicioDoDia(abre, limite, duracaoMinutos),
                ultimoJogoDoDia));

            vagas += vagasDoDia;
            rodadasTotais += rodadas;
            restantes -= jogosDoDia;

            if (ultimoJogoDoDia != null) ultimoJogo = data.Add(ultimoJogoDoDia.Value);
        }

        int alocados = totalDeJogos - Math.Max(restantes, 0);
        int faltam = Math.Max(restantes, 0);
        int margem = GradeDeJogos.MargemDeHorarios(quadras);

        // ⚠️ QUANTAS QUADRAS FECHARIAM O PRAZO: rodada NÃO depende de quadra — quem manda no
        // número de rodadas é o relógio (abertura, limite, duração). Quadra só multiplica as
        // vagas de cada rodada. Por isso a resposta é uma divisão, e não uma busca.
        //
        // Sem prazo não há o que fechar: a grade avança sozinha, e responder um número aqui
        // seria inventar uma cobrança que ninguém fez.
        int quadrasNecessarias = ate != null && rodadasTotais > 0
            ? (totalDeJogos + rodadasTotais - 1) / rodadasTotais
            : quadras;

        return new Plano
        {
            Dias = dias,
            TotalDeJogos = totalDeJogos,
            Quadras = quadras,
            DuracaoMinutos = duracaoMinutos,
            Vagas = vagas,
            Alocados = alocados,
            Faltam = faltam,
            Sobram = Math.Max(vagas - totalDeJogos, 0),
            Margem = margem,
            FolgaSuficiente = vagas - totalDeJogos >= margem,
            QuadrasNecessarias = quadrasNecessarias,
            HorariosExtras = faltam,
            MinutosExtras = faltam * duracaoMinutos,
            PrazoDefinido = ate != null,
            UltimoJogoComeca = ultimoJogo,
            UltimoJogoTermina = ultimoJogo?.AddMinutes(duracaoMinutos),
            DiaSemHoraPraAcabar = dias.Count == 0 || dias.TrueForAll(d => d.Rodadas == 0),
        };
    }

    /// <summary>
    /// Lê os limites por dia da tela, no formato <c>yyyy-MM-dd=HH:mm</c> separados por <c>;</c>.
    /// </summary>
    // Uma string só, e não um par de vetores, porque ela atravessa uma URL de GET: vetor
    // paralelo que chega meio preenchido casaria o dia de um com a hora de outro, em silêncio.
    // Entrada ilegível é IGNORADA em vez de estourar — o valor vem do navegador, e derrubar a
    // tela de planejamento por causa de um caractere trocado seria trocar informação por erro.
    public static IReadOnlyDictionary<DateTime, TimeSpan> LerLimites(string? texto)
    {
        var limites = new Dictionary<DateTime, TimeSpan>();
        if (string.IsNullOrWhiteSpace(texto)) return limites;

        foreach (var pedaco in texto.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = pedaco.Split('=');
            if (partes.Length != 2) continue;

            if (!DateTime.TryParseExact(partes[0].Trim(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
                continue;

            if (!TimeSpan.TryParseExact(partes[1].Trim(), [@"hh\:mm", @"h\:mm", @"hh\:mm\:ss", @"h\:mm\:ss"],
                    CultureInfo.InvariantCulture, out var hora))
                continue;

            limites[dia.Date] = hora;
        }

        return limites;
    }
}
