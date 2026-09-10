using Padelizou.Models;

namespace Padelizou.Services;

// AS REGRAS DA RESERVA DE HORÁRIO (Models/ReservaDeHorario), num lugar só (10/09/2026).
//
// A reserva é a troca de horário feita numa eliminatória que ainda não nasceu. Três leitores
// obedecem a ela e precisam dizer a mesma coisa: a prévia (ProximasFasesDaChave.Agendar), o robô
// que cria a rodada (RoboDoChaveamento.AgendarNaGradeAsync) e o reencaixe que o robô faz quando
// outra categoria avança. O que os três têm em comum mora aqui — a numeração do jogo, a régua de
// validade e a aplicação ao jogo real.
public static class ReservasDeHorario
{
    // As reservas de um torneio. Pela categoria, e não por uma coluna TorneioId: a categoria é
    // obrigatória na reserva e já sabe o torneio dela. Traduzida pro SQL em ReservaDeHorarioTests.
    public static IQueryable<ReservaDeHorario> DoTorneio(DbPadelContext db, int torneioId) =>
        db.ReservasDeHorario.Where(r => r.Categoria.TorneioId == torneioId);

    // A reserva VALE se não vier antes de a fase anterior da própria categoria acabar — antes
    // disso os dois lados do jogo ainda podem estar em quadra. `abreARodada` é esse piso
    // (GradeDeJogos.AberturaDaProximaFase do último jogo da fase anterior); nulo é a categoria
    // sem fase anterior marcada, e aí não há o que esperar.
    //
    // ⚠️ É a ÚNICA régua que a reserva respeita. A barreira de posto entre categorias ela
    // atravessa de propósito: entre categorias são pessoas diferentes, e a ordem das fases é
    // preferência do torneio — o organizador que reservou é quem decide a preferência.
    public static bool Vale(DateTime reservado, DateTime? abreARodada) =>
        abreARodada is not DateTime abre || reservado >= abre;

    // O NÚMERO de cada jogo de mata-mata dentro da fase dele ("Quartas de Final 2"), por Id —
    // a ordem em que o robô grava a rodada, e a mesma com que a prévia e a tela citam o jogo
    // ("Vencedor Quartas de Final 2"). É a chave da reserva do lado do jogo real.
    public static Dictionary<int, int> NumeroNaFase(IEnumerable<Partida> partidas) =>
        partidas
            .Where(p => ChaveamentoMataMata.EhFaseDeMataMata(p.Fase))
            .GroupBy(p => new { p.CategoriaId, p.Fase })
            .SelectMany(g => g.OrderBy(p => p.Id).Select((p, i) => (p.Id, Numero: i + 1)))
            .ToDictionary(x => x.Id, x => x.Numero);

    // Dá a cada jogo real que tem reserva válida o horário e a quadra dela. Devolve os que
    // ganharam hora — são os "intocados" do encaixe que vem depois: quem foi reservado não
    // disputa vaga, e os outros desviam dele.
    public static List<Partida> Aplicar(IEnumerable<Partida> jogos, Func<Partida, int> numeroDe,
        IReadOnlyCollection<ReservaDeHorario> reservas, Func<Partida, DateTime?> abreARodadaDe)
    {
        if (reservas.Count == 0) return new List<Partida>();

        var porChave = reservas.ToDictionary(r => (r.CategoriaId, r.Fase, r.Numero));
        var reservados = new List<Partida>();

        foreach (var jogo in jogos)
        {
            if (!porChave.TryGetValue((jogo.CategoriaId, jogo.Fase, numeroDe(jogo)), out var reserva)) continue;
            if (!Vale(reserva.Horario, abreARodadaDe(jogo))) continue;

            jogo.HorarioPrevisto = reserva.Horario;
            jogo.NomeQuadra = reserva.NomeQuadra;
            reservados.Add(jogo);
        }

        return reservados;
    }

    // As reservas de jogos que AINDA VÃO NASCER, na forma de jogos marcados — pra que o encaixe
    // dos outros não ocupe o slot delas enquanto o jogo não existe.
    //
    // 🕳️ Sem isto: a Final da A está reservada pras 22h na Quadra Central; a Semifinal da B nasce
    // antes e o encaixe, que só enxerga o banco, a põe justamente às 22h na Quadra Central. Quando
    // a Final da A nasce, são dois jogos na mesma quadra no mesmo minuto. A prévia já reserva o
    // slot antes de qualquer cadeia passar por ele; o encaixe de verdade precisa da mesma coisa.
    //
    // `fasesQueExistem` é por (categoria, fase), e não por jogo: uma fase nasce inteira, então a
    // reserva de uma fase que já existe ou foi consumida ou é órfã (número que a rodada não tem) —
    // nos dois casos não há jogo por nascer, e ela não pode tomar quadra de ninguém.
    //
    // ⚠️ SÃO OBJETOS DE MEMÓRIA, NUNCA DO CONTEXTO: entram só como `jaMarcados` do encaixe e não
    // podem ser adicionados nem salvos. As duplas ficam em zero de propósito — `Ocupantes(0)` vira
    // a "pessoa 0", que não existe (pessoa de verdade tem Id positivo; dupla sem gente vira o
    // negativo do Id dela), então o fantasma bloqueia a QUADRA e a VAGA do horário, e ninguém.
    public static List<Partida> AindaPorNascer(IEnumerable<ReservaDeHorario> reservas,
        ISet<(int CategoriaId, string Fase)> fasesQueExistem,
        Func<int, string, DateTime?> abreARodadaDe) =>
        reservas
            .Where(r => !fasesQueExistem.Contains((r.CategoriaId, r.Fase))
                     && Vale(r.Horario, abreARodadaDe(r.CategoriaId, r.Fase)))
            .Select(r => new Partida
            {
                CategoriaId = r.CategoriaId,
                Fase = r.Fase,
                Status = "Agendada",
                Codigo = "reserva",
                HorarioPrevisto = r.Horario,
                NomeQuadra = r.NomeQuadra,
            })
            .ToList();
}
