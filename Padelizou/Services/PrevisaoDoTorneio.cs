namespace Padelizou.Services;

// Responde a pergunta que o organizador faz antes de anunciar o torneio: "cabe?"
//
// Ele escolhe o dia de começar, a hora de abrir, o teto do dia, o número de quadras e a
// duração do jogo. Nenhum desses números diz sozinho quando o torneio ACABA — isso depende
// de quantos jogos existem, e quantos jogos existem depende de quantas duplas entram.
// Sem essa conta o organizador só descobre no sábado à noite que o domingo não fecha.
public static class PrevisaoDoTorneio
{
    // Quantos grupos e quantos jogos de grupo saem de N duplas.
    //
    // Espelha a regra do GerarChaves: grupos de 3, e quando o total não é múltiplo de 3 os
    // melhores rankeados resolvem em grupo(s) de 2.
    //   resto 1 (ex. 13) → 2 grupos de 2 + o resto em grupos de 3
    //   resto 2 (ex. 14) → 1 grupo de 2  + o resto em grupos de 3
    public static (int Grupos, int Jogos) FaseDeGrupos(int duplas)
    {
        if (duplas < 2) return (0, 0);
        if (duplas < 3) return (1, 1);   // 2 duplas: chave direta

        int gruposDeDois = (duplas % 3) switch { 1 => 2, 2 => 1, _ => 0 };
        int emGruposDeTres = duplas - gruposDeDois * 2;
        int gruposDeTres = emGruposDeTres / 3;

        // Todos contra todos: grupo de 2 dá 1 jogo, grupo de 3 dá 3.
        return (gruposDeDois + gruposDeTres, gruposDeDois * 1 + gruposDeTres * 3);
    }

    // Quantos jogos o mata-mata inteiro tem, do primeiro cruzamento à final.
    //
    // Mesma conta do ChaveamentoMataMata: classificam 2 por grupo, e o quadro é a maior
    // potência de 2 que cabe nesses classificados. Um quadro de N duplas tem N-1 jogos —
    // cada jogo elimina exatamente uma, e sobra o campeão.
    public static int MataMata(int grupos)
    {
        if (grupos <= 0) return 0;
        if (grupos == 1) return 1;   // um grupo só fecha com a final direta 1º x 2º

        return MaiorPotenciaDe2Ate(grupos * 2) - 1;
    }

    private static int MaiorPotenciaDe2Ate(int teto)
    {
        int p = 1;
        while (p * 2 <= teto) p *= 2;
        return p;
    }

    // O torneio inteiro, de uma categoria: grupos + mata-mata.
    public static int TotalDeJogos(int duplas)
    {
        var (grupos, jogos) = FaseDeGrupos(duplas);
        return jogos + MataMata(grupos);
    }

    // Quando o ÚLTIMO jogo começa, dada a grade. Null se não há jogo nenhum.
    public static DateTime? UltimoJogo(
        DateTime inicio, TimeSpan ultimoInicioDoDia, TimeSpan aberturaDiasSeguintes,
        int quadras, int duracaoMinutos, int totalDeJogos)
    {
        if (totalDeJogos <= 0) return null;

        // A grade é preguiçosa: só materializa o último.
        return GradeDeJogos
            .Horarios(inicio, ultimoInicioDoDia, quadras, duracaoMinutos, totalDeJogos, aberturaDiasSeguintes)
            .Last();
    }

    // Quantos dias de quadra o torneio ocupa (o de abertura conta como 1).
    public static int DiasOcupados(DateTime inicio, DateTime ultimoJogo) =>
        (ultimoJogo.Date - inicio.Date).Days + 1;

    // ---- OS DOIS PAINÉIS DA TELA DE CRIAÇÃO ------------------------------------------
    //
    // ⚠️ Até 08/08/2026 a tela de criação carregava a PRÓPRIA CÓPIA destas contas, em
    // JavaScript: grupos de 3 e mata-mata (daqui), o que fecha no Americano individual
    // (DivisaoDoAmericano), o todos-contra-todos de duplas (RodadasAmericanoDeDuplas) e a
    // grade do dia (GradeDeJogos) — cinco blocos, ~120 linhas. O comentário de lá dizia,
    // com todas as letras: "se estas contas divergirem dos Services, a tela mente pro
    // organizador".
    //
    // E divergir era questão de tempo, porque a cópia estava FORA DO ALCANCE DOS TESTES —
    // a suíte exercita o C#, e nenhum teste lê JavaScript. O sintoma seria o pior tipo:
    // a criação prometendo um número de jogos e o sorteio entregando outro, com a quadra
    // já reservada e a gente já chamada. Agora a tela pergunta e só desenha a resposta.
    //
    // O que continua no navegador é FORMATAÇÃO (negrito, data por extenso, cor do aviso);
    // o que voltou pro servidor é toda a ARITMÉTICA. É essa a linha.

    // O teto do campo na tela (max="256"), repetido aqui porque requisição montada à mão não
    // passa pelo formulário. Sem ele, um número absurdo faz `DivisaoDoAmericano.Possiveis`
    // varrer milhões de divisões e a grade materializar bilhões de horários — a requisição
    // pendura, e quem chama já está autenticado (não seria um estranho).
    public const int MaximoDeInscritos = 256;

    // O ritmo de UM dia de quadra. Null quando o primeiro jogo já passa do teto do dia.
    public sealed record RitmoDoDia(int Rodadas, string UltimoComeca, string UltimoTermina, int Jogos);

    // O resumo vem em três pedaços porque a tela destaca o primeiro e o último em negrito.
    // Juntá-los aqui obrigaria um Service a devolver HTML, que é o tipo de coisa que começa
    // inocente e termina com marcação de tela espalhada pela regra de negócio.
    public sealed record ResumoDaConta(string Inscritos, string Meio, string Jogos);

    public sealed record Tela
    {
        // — painel do ritmo: só depende do relógio, não de quantos vêm —
        public bool TemRitmo { get; init; }
        public bool DiaSemHoraPraAcabar { get; init; }
        public RitmoDoDia? PrimeiroDia { get; init; }
        public RitmoDoDia? DiasSeguintes { get; init; }
        public bool DiasSeguintesIguais { get; init; }
        public int Quadras { get; init; }

        // — painel "vai caber?": depende do número esperado —
        public bool TemConta { get; init; }
        public bool Fecha { get; init; } = true;
        public string? PorQueNaoFecha { get; init; }
        public ResumoDaConta? Resumo { get; init; }
        public int TotalDeJogos { get; init; }
        // Quantas PESSOAS o número esperado representa (dupla = 2). É a base da estimativa
        // do pacote de registro de resultados, que cobra percentual sobre pessoas × preço.
        public int Pessoas { get; init; }
        public DateTime? Comeca { get; init; }
        public DateTime? Termina { get; init; }
        public int Dias { get; init; }
        public bool? CabeNoPrazo { get; init; }
    }

    public static Tela ParaATela(
        string? formato, int numero, int duracaoMinutos, int quadras,
        TimeSpan? abertura, TimeSpan? aberturaDiasSeguintes, TimeSpan? ultimoInicioDoDia,
        DateTime? dataInicio, DateTime? dataFim)
    {
        quadras = Math.Max(1, quadras);
        numero = Math.Clamp(numero, 0, MaximoDeInscritos);

        var ritmo = RitmoDosDias(duracaoMinutos, quadras, abertura, aberturaDiasSeguintes, ultimoInicioDoDia);

        // O piso muda com o formato: o Americano individual não tem dupla na inscrição — é
        // gente sorteada a cada rodada —, e 4 é o que fecha uma quadra.
        int minimo = formato == FormatoDoTorneio.Americano ? 4 : 2;

        if (numero < minimo || duracaoMinutos <= 0 || abertura == null
            || aberturaDiasSeguintes == null || ultimoInicioDoDia == null || dataInicio == null)
        {
            return ritmo with { TemConta = false };
        }

        int total;
        ResumoDaConta resumo;

        if (formato == FormatoDoTorneio.Americano)
        {
            // "Cada um com cada um" só fecha em certos números, e isso é aritmética — nenhum
            // sorteio contorna. Quando não fecha, a resposta é a saída, não só o problema.
            if (!DivisaoDoAmericano.Aceita(numero))
            {
                return ritmo with
                {
                    TemConta = true,
                    Fecha = false,
                    PorQueNaoFecha = DivisaoDoAmericano.PorQueNaoFecha(numero),
                };
            }

            // A primeira é a que MAIS MISTURA — mesma escolha que o sorteio faz.
            var divisao = DivisaoDoAmericano.Possiveis(numero)[0];
            total = divisao.PartidasTotais;
            resumo = new ResumoDaConta(
                $"{numero} pessoas",
                divisao.TemGrupoFinal
                    ? $" = {divisao.Grupos} grupos de {divisao.PorGrupo} + grupo final de "
                      + $"{divisao.TamanhoDoGrupoFinal}, {divisao.RodadasTotais} rodadas, "
                    : $" = 1 grupo de {divisao.PorGrupo}, {divisao.RodadasTotais} rodadas, ",
                $"{total} jogos");
        }
        else if (formato == FormatoDoTorneio.AmericanoDeDuplas)
        {
            total = RodadasAmericanoDeDuplas.Partidas(numero);
            resumo = new ResumoDaConta(
                $"{numero} duplas",
                $" = todos contra todos, {RodadasAmericanoDeDuplas.Rodadas(numero)} rodadas, ",
                $"{total} jogos");
        }
        else
        {
            var (grupos, jogosDeGrupo) = FaseDeGrupos(numero);
            int mataMata = MataMata(grupos);
            total = jogosDeGrupo + mataMata;
            resumo = new ResumoDaConta(
                $"{numero} duplas",
                $" = {grupos} grupos, {jogosDeGrupo} jogos de grupo + {mataMata} de mata-mata = ",
                $"{total} jogos");
        }

        var inicio = dataInicio.Value.Date.Add(abertura.Value);
        var ultimo = UltimoJogo(inicio, ultimoInicioDoDia.Value, aberturaDiasSeguintes.Value,
            quadras, duracaoMinutos, total);
        if (ultimo == null) return ritmo with { TemConta = false };

        // O prazo se mede pelo COMEÇO do último jogo, não pelo fim dele: um jogo que começa
        // 23h50 do último dia e termina depois da meia-noite é um torneio que coube — é a
        // mesma régua que a grade usa pra decidir se o dia virou.
        bool? cabe = dataFim == null
            ? null
            : ultimo.Value <= dataFim.Value.Date.AddDays(1).AddSeconds(-1);

        return ritmo with
        {
            TemConta = true,
            Resumo = resumo,
            TotalDeJogos = total,
            // Só o Americano individual inscreve pessoa a pessoa; nos demais o número é de
            // duplas e a inscrição cobra por pessoa (dupla paga o dobro).
            Pessoas = formato == FormatoDoTorneio.Americano ? numero : numero * 2,
            Comeca = inicio,
            Termina = ultimo.Value.AddMinutes(duracaoMinutos),
            Dias = DiasOcupados(inicio, ultimo.Value),
            CabeNoPrazo = cabe,
        };
    }

    private static Tela RitmoDosDias(int duracaoMinutos, int quadras,
        TimeSpan? abertura, TimeSpan? aberturaDiasSeguintes, TimeSpan? ultimoInicioDoDia)
    {
        if (duracaoMinutos <= 0 || abertura == null || aberturaDiasSeguintes == null || ultimoInicioDoDia == null)
            return new Tela { Quadras = quadras };

        // Quem decide "dia sem hora pra acabar" é a abertura dos DIAS SEGUINTES, e não a do
        // primeiro: é ela que se repete, e é com ela que a grade decide se o dia vira.
        if (ultimoInicioDoDia <= aberturaDiasSeguintes)
            return new Tela { Quadras = quadras, TemRitmo = true, DiaSemHoraPraAcabar = true };

        return new Tela
        {
            Quadras = quadras,
            TemRitmo = true,
            PrimeiroDia = Ritmo(abertura.Value, ultimoInicioDoDia.Value, duracaoMinutos, quadras),
            DiasSeguintes = Ritmo(aberturaDiasSeguintes.Value, ultimoInicioDoDia.Value, duracaoMinutos, quadras),
            DiasSeguintesIguais = abertura == aberturaDiasSeguintes,
        };
    }

    private static RitmoDoDia? Ritmo(TimeSpan abertura, TimeSpan ultimoInicioDoDia, int duracaoMinutos, int quadras)
    {
        var rodadas = GradeDeJogos.RodadasPorDia(abertura, ultimoInicioDoDia, duracaoMinutos);
        var ultimo = GradeDeJogos.UltimoInicioDoDia(abertura, ultimoInicioDoDia, duracaoMinutos);
        if (rodadas == null || ultimo == null) return null;

        return new RitmoDoDia(
            rodadas.Value,
            Relogio(ultimo.Value),
            Relogio(ultimo.Value + TimeSpan.FromMinutes(duracaoMinutos)),
            rodadas.Value * quadras);
    }

    // HH:mm dando a volta na meia-noite: o jogo que começa 23h20 e dura 50 min termina 00h10,
    // e "24:10" não é hora que alguém leia.
    private static string Relogio(TimeSpan t) => $"{(int)t.TotalHours % 24:00}:{t.Minutes:00}";
}
