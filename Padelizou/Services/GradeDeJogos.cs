using Padelizou.Models;

namespace Padelizou.Services;

// Distribui os jogos de um torneio ao longo do relógio.
//
// O agendamento antigo somava a duração de um jogo por vez, a partir da data de início, sem
// nada mais: num torneio de 16 duplas em 4 grupos (24 jogos de 50 min) a grade marcava jogo
// às 3h40 da manhã. Dois erros somados —
//
//   1. ignorava as QUADRAS: com 3 quadras rodando em paralelo, 3 jogos começam no mesmo
//      horário, e o relógio só anda quando as quadras enchem;
//   2. ignorava o EXPEDIENTE: torneio não vira a noite. Ao passar do último horário do dia,
//      o que sobra vai pro dia seguinte.
//
// O padrão de um torneio de fim de semana (27/07/2026, descrito pelo Felipe):
//
//   sexta   — começa 18h (todo mundo trabalha de dia), últimos jogos 23h / 23h50
//   sábado  — começa 8h, vai até 23h / 23h50
//   domingo — começa 8h e vai até acabar, normalmente à tarde
//
// Daí duas coisas que a grade precisa saber e que uma hora só não expressa:
//
//   • o PRIMEIRO dia abre num horário e os DEMAIS em outro (18h × 8h);
//   • o corte do dia é a hora em que o último jogo COMEÇA, não em que termina — um jogo
//     das 23h50 varando a madrugada é normal, e ninguém quer calcular 23h50 + 50 min
//     pra preencher um campo.
public static class GradeDeJogos
{
    // Um horário por jogo, na ordem em que os jogos foram passados.
    //
    // inicio               — quando o torneio começa (data + hora da sexta, por exemplo).
    // ultimoInicioDoDia    — a hora limite pra COMEÇAR um jogo. Se for menor ou igual à
    //                        abertura dos dias seguintes, o dia é tratado como aberto (sem
    //                        virada), pra nunca entrar em laço infinito por configuração torta.
    // quadras              — quantos jogos rodam ao mesmo tempo.
    // aberturaDiasSeguintes— a que horas o dia seguinte recomeça. Omitida, repete a hora de
    //                        início — serve pro mata-mata, que entra emendado no meio do dia.
    public static IEnumerable<DateTime> Horarios(
        DateTime inicio, TimeSpan ultimoInicioDoDia, int quadras, int duracaoMinutos, int quantidade,
        TimeSpan? aberturaDiasSeguintes = null)
    {
        if (quantidade <= 0) yield break;

        quadras = Math.Max(quadras, 1);
        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;

        var abertura = aberturaDiasSeguintes ?? inicio.TimeOfDay;
        bool viraODia = ultimoInicioDoDia > abertura;

        var aberturaDoDia = inicio;   // quando o dia corrente abriu
        var horario = inicio;
        int naQuadra = 0;

        for (int i = 0; i < quantidade; i++)
        {
            // Encheu as quadras: todo mundo joga junto, então o relógio anda uma partida.
            if (naQuadra == quadras)
            {
                horario = horario.AddMinutes(duracaoMinutos);
                naQuadra = 0;

                // Comparação em data cheia, não em hora do dia: o jogo das 23h50 empurra o
                // próximo pra 0h40, que já é OUTRA data — comparar só TimeOfDay diria
                // "0h40 é cedo, cabe" e marcaria jogo na madrugada.
                if (viraODia && horario > aberturaDoDia.Date.Add(ultimoInicioDoDia))
                {
                    aberturaDoDia = aberturaDoDia.Date.AddDays(1).Add(abertura);
                    horario = aberturaDoDia;
                }
            }

            yield return horario;
            naQuadra++;
        }
    }

    // Encaixa cada jogo num horário SEM pôr o mesmo inscrito em duas quadras ao mesmo tempo.
    //
    // O encaixe antigo era posicional: jogo i ganhava o horário i. Só que os jogos de um
    // grupo saem em sequência — (A,B), (A,C), (B,C) — e com 2+ quadras os horários vêm em
    // pares iguais, então (A,B) e (A,C) caíam no MESMO horário e A jogava em duas quadras
    // simultaneamente. Descoberto ao testar a categoria de times (grupos de 3 são o padrão
    // dela), mas o defeito valia igualzinho pras duplas.
    //
    // Guloso de primeira vaga: pra cada horário, entra o primeiro jogo da fila cujos dois
    // lados estão livres naquele horário.
    //
    // ⚠️ Quando NADA cabe, a vaga fica VAZIA e o jogo tenta o horário seguinte — uma quadra
    // parada é muito mais barata que uma pessoa chamada em duas ao mesmo tempo. A versão
    // anterior enfiava o jogo assim mesmo, e isso furava justamente no fim da fila: sobram
    // poucos jogos, todos com gente já escalada naquele horário, e o conflito aparecia no
    // último lugar onde alguém olharia. Foi o CI que pegou — o sorteio da chave direta é
    // ALEATÓRIO, então cada execução monta uma grade diferente e a falha só aparecia em
    // alguns sorteios.
    //
    // Pular custa vaga, então quem chama precisa oferecer MAIS horários que jogos (ver
    // `MargemDeHorarios`). O último recurso continua existindo: se as vagas restantes forem
    // exatamente os jogos que faltam, entra com conflito mesmo — deixar jogo sem horário
    // nenhum seria pior.
    //
    // ⚠️ "O mesmo inscrito" é PESSOA, não dupla. Enquanto cada um jogava numa categoria só,
    // dupla e pessoa davam na mesma; com a categoria de CHAVE DIRETA a mesma pessoa disputa
    // duas coisas no mesmo torneio (a categoria dela e o mata-mata paralelo), em duplas de
    // Ids diferentes — comparar dupla marcaria as duas no mesmo horário, em quadras
    // diferentes, e ninguém descobriria antes de o nome ser chamado duas vezes.
    //
    // Daí `ocupantesPorDupla`: quem de fato ocupa a quadra quando aquela dupla joga. Dupla
    // ausente do mapa (e o mapa inteiro ausente) cai no Id NEGADO dela — injetivo, então é
    // exatamente o comportamento antigo, e negativo pra nunca colidir com um Id de jogador.
    // É o que vale pra categoria de TIMES: lá `Jogador1Id` é o ORGANIZADOR em todos os
    // times, e comparar por pessoa faria todo time conflitar com todo time.
    // `quadras` são os nomes cadastrados no torneio, em ordem. Cada horário comporta uma
    // partida por quadra, então quem entra em N-ésimo naquele horário joga na N-ésima
    // quadra — a grade já sabia ONDE, só não estava dizendo. Sem isso todo jogo nascia com
    // "Quadra a definir" mesmo num torneio com as cinco quadras cadastradas, e o jogador
    // tinha a hora sem ter o lugar (que é metade da informação de que ele precisa).
    public static void Encaixar(List<Partida> jogos, IReadOnlyList<DateTime> horarios,
        IReadOnlyDictionary<int, int[]>? ocupantesPorDupla = null,
        IReadOnlyList<string>? quadras = null)
    {
        int[] Ocupantes(int duplaId) =>
            ocupantesPorDupla != null && ocupantesPorDupla.TryGetValue(duplaId, out var pessoas) && pessoas.Length > 0
                ? pessoas
                : new[] { -duplaId };

        var fila = new List<Partida>(jogos);
        var ocupados = new Dictionary<DateTime, HashSet<int>>();
        var ocupadasNoHorario = new Dictionary<DateTime, int>();

        for (int i = 0; i < horarios.Count; i++)
        {
            if (fila.Count == 0) break;

            var horario = horarios[i];
            if (!ocupados.TryGetValue(horario, out var quem))
                ocupados[horario] = quem = new HashSet<int>();

            bool Livre(Partida p) =>
                !Ocupantes(p.Dupla1Id).Any(quem.Contains) && !Ocupantes(p.Dupla2Id).Any(quem.Contains);

            var jogo = fila.FirstOrDefault(Livre);

            if (jogo == null)
            {
                // Nada cabe sem repetir gente. Deixa a quadra vaga e tenta no próximo
                // horário — a menos que as vagas que sobram sejam contadas: aí não há mais
                // pra onde empurrar, e um conflito é melhor que um jogo sem horário.
                int vagasRestantes = horarios.Count - i - 1;
                if (vagasRestantes >= fila.Count) continue;
                jogo = fila[0];
            }

            jogo.HorarioPrevisto = horario;

            // A quadra sai da POSIÇÃO dentro do horário: o primeiro jogo das 20h vai pra
            // primeira quadra, o segundo pra segunda. Torneio sem quadra cadastrada segue
            // sem nome — inventar "Quadra 1" onde o clube chama de "Central" seria pior.
            if (quadras is { Count: > 0 })
            {
                int posicao = ocupadasNoHorario.GetValueOrDefault(horario);
                if (posicao < quadras.Count) jogo.NomeQuadra = quadras[posicao];
                ocupadasNoHorario[horario] = posicao + 1;
            }

            foreach (var pessoa in Ocupantes(jogo.Dupla1Id)) quem.Add(pessoa);
            foreach (var pessoa in Ocupantes(jogo.Dupla2Id)) quem.Add(pessoa);
            fila.Remove(jogo);
        }
    }

    // Quantos horários pedir além do número de jogos, pra que o `Encaixar` possa deixar uma
    // vaga vazia em vez de dobrar alguém. Sem folga ele não teria escolha: cada jogo teria
    // exatamente uma vaga, e "pular" viraria "deixar jogo sem horário".
    //
    // Três rodadas de folga é bastante — o conflito acontece no rabo da fila, com poucos
    // jogos restantes. Vaga que sobra não custa nada: ela simplesmente não é usada.
    public static int MargemDeHorarios(int quadras) => Math.Max(quadras, 1) * 3;

    // Quantas rodadas cabem num dia que abre às `abertura`.
    // null = dia sem hora pra acabar.
    public static int? RodadasPorDia(TimeSpan abertura, TimeSpan ultimoInicioDoDia, int duracaoMinutos)
    {
        if (ultimoInicioDoDia <= abertura) return null;

        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;
        return (int)((ultimoInicioDoDia - abertura).TotalMinutes / duracaoMinutos) + 1;
    }

    // A que horas começa de fato o ÚLTIMO jogo do dia. Não é o limite digitado: o limite é
    // um teto, e o jogo só começa nos horários que a cadência alcança — com jogos de 1h a
    // partir das 18h, o teto de 23h50 vira 23h, porque 23h50 não é múltiplo da cadência.
    // null quando o dia é aberto.
    public static TimeSpan? UltimoInicioDoDia(TimeSpan abertura, TimeSpan ultimoInicioDoDia, int duracaoMinutos)
    {
        var rodadas = RodadasPorDia(abertura, ultimoInicioDoDia, duracaoMinutos);
        if (rodadas == null) return null;

        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;
        return abertura + TimeSpan.FromMinutes((rodadas.Value - 1) * duracaoMinutos);
    }

    // Quando a próxima partida pode começar, dado o último jogo já marcado — usado pra
    // encaixar o mata-mata logo depois da fase de grupos, em vez de recomeçar do zero em
    // cima dela.
    public static DateTime DepoisDe(DateTime ultimoJogo, TimeSpan ultimoInicioDoDia,
        TimeSpan aberturaDiasSeguintes, int duracaoMinutos)
    {
        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;
        var proximo = ultimoJogo.AddMinutes(duracaoMinutos);

        if (ultimoInicioDoDia > aberturaDiasSeguintes && proximo > ultimoJogo.Date.Add(ultimoInicioDoDia))
        {
            proximo = ultimoJogo.Date.AddDays(1).Add(aberturaDiasSeguintes);
        }

        return proximo;
    }
}
