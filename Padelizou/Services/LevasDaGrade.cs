using Padelizou.Models;

namespace Padelizou.Services;

// A GRADE MONTADA POSTO POR POSTO — a régua única da ORDEM DAS FASES do torneio.
//
// 🗣️ Felipe, 09/09/2026: *"o torneio tem q seguir uma ordem, primeiro todas as chaves, depois
// todas as primeiras eliminatorias (decimas > oitavas > quartas > semi > final) a ideia e fazer
// as finais de cada categorias ser os ultimos jogos do torneio"*.
//
// ⚠️ ISTO EXISTE PORQUE A RÉGUA PRECISA SER UMA SÓ, e por um motivo medido, não por elegância.
// A ordem das fases era decidida em TRÊS lugares que enxergam coisas diferentes:
//
//   • TorneiosController.Chaves.EncaixarNasLevas — o sorteio e o "Refazer grade", que veem o
//     torneio inteiro de uma vez;
//   • RoboDoChaveamento.AgendarNaGradeAsync — a rodada que nasce quando a anterior fecha, que
//     vê SÓ O PASSADO;
//   • ProximasFasesDaChave.Agendar — a prévia de tela, que projeta o futuro.
//
// 🕳️ E é a do meio que produz o defeito, porque **o robô não enxerga fase que ainda não
// nasceu**. Medido no torneio de 16/8/4 duplas: a categoria de 4 fecha os grupos às 11h e o robô
// cria a Semifinal dela na hora; as Quartas da categoria de 8 só nascem às 11h30, quando os
// grupos DELA fecham. Qualquer barreira que o robô calcule sobre "o que já está marcado" chega
// tarde demais — a semifinal já foi marcada para 12h30 e as quartas caem em cima dela.
//
// ✅ A saída é não adivinhar: quando uma rodada nova entra na grade, o que ficou FORA DE ORDEM é
// REENCAIXADO. Nenhuma estimativa, nenhum palpite sobre quanto tempo a outra categoria vai levar
// — e nenhum risco de travar o torneio esperando uma categoria que desistiu.
public static class LevasDaGrade
{
    // Tudo que o encaixe precisa saber e que não muda de leva pra leva. Vem num pacote porque
    // são NOVE coisas, e nove parâmetros repetidos em dois chamadores é como duas cópias da
    // receita começam a divergir (foi o que aconteceu com VagasDaGrade até 21/08/2026).
    public sealed record Restricoes(
        IReadOnlyDictionary<int, int[]> Ocupantes,
        IReadOnlyList<string> Quadras,
        IReadOnlyDictionary<int, string[]>? QuadrasPorCategoria = null,
        IReadOnlyDictionary<int, (DateTime, DateTime)[]>? Janelas = null,
        ConcentracaoDeJogos.Concentracoes? Concentracao = null,
        IReadOnlyDictionary<int, (DateTime, DateTime)[]>? NoiteDeSabado = null,
        SedesDoTorneio? Sedes = null);

    /// <summary>
    /// Distribui <paramref name="jogos"/> na grade, um POSTO de fase por vez, sem que nenhum
    /// posto comece antes de o anterior acabar. <paramref name="jaMarcados"/> são os jogos que
    /// já têm hora e que não podem ser atropelados.
    /// </summary>
    public static void Encaixar(Torneio torneio, List<Partida> jogos, DateTime abre,
        IReadOnlyList<Partida> jaMarcados, Restricoes restricoes)
    {
        // Tudo que já tem hora e quadra e que as levas seguintes precisam enxergar pra não marcar
        // em cima. Começa com o que veio de fora e VAI CRESCENDO a cada leva.
        //
        // ⚠️ ATÉ 21/08/2026 A SEGUNDA LEVA SÓ RECEBIA OS DE FORA — os jogos que a primeira leva
        // acabara de marcar ficavam invisíveis pra ela. Não dava problema por acidente: a segunda
        // leva começava depois do último jogo de grupo do torneio INTEIRO, então nunca havia o que
        // atropelar. Com âncora por categoria isso deixou de valer, e sem esta lista duas levas
        // marcariam duas partidas na mesma quadra no mesmo horário.
        var jaEmQuadra = new List<Partida>(jaMarcados);

        void Agendar(List<Partida> daLeva, DateTime inicio)
        {
            if (daLeva.Count == 0) return;

            // ⚠️ NENHUMA LEVA COMEÇA ANTES DA ABERTURA DA GRADE. Num "refazer grade" a abertura é
            // AGORA, e a âncora de uma leva vem do fim da fase anterior — que pode ser uma hora do
            // passado quando aquela fase já foi jogada. Sem este piso, a grade nasceria em cima de
            // horários que já passaram: jogo marcado pra ontem, que não aparece pra ninguém.
            if (inicio < abre) inicio = abre;

            // ⚠️ `peloMenosAte` É O QUE FAZ A CONCENTRAÇÃO ACONTECER. A lista normal é
            // `jogos + margem`, que num fim de semana mal passa da manhã de sábado — sem este
            // alcance, "os 2 jogos no sábado à tarde" nunca encontra vaga e o encaixe cede em
            // silêncio. O alcance olha as TRÊS restrições, e não só a concentração: ver
            // VagasDaGrade.AlcanceNecessario.
            var vagas = VagasDaGrade.Montar(torneio, inicio, daLeva.Count, jaEmQuadra,
                peloMenosAte: VagasDaGrade.MaisTarde(
                    restricoes.Concentracao?.AteQuando,
                    VagasDaGrade.AlcanceNecessario(restricoes.Janelas, restricoes.NoiteDeSabado)),
                sedes: restricoes.Sedes,
                jogosComJanela: VagasDaGrade.JogosComJanela(daLeva, restricoes.Janelas, restricoes.NoiteDeSabado));

            GradeDeJogos.Encaixar(daLeva, vagas, VagasDaGrade.Duracao(torneio),
                restricoes.Ocupantes, restricoes.Quadras, jaEmQuadra, restricoes.QuadrasPorCategoria,
                restricoes.Janelas, restricoes.Sedes, restricoes.Concentracao?.Janelas,
                restricoes.NoiteDeSabado);

            jaEmQuadra.AddRange(daLeva.Where(j => j.HorarioPrevisto != null));
        }

        // ── A BARREIRA DE POSTO ──────────────────────────────────────────────────────────
        // O horário do ÚLTIMO jogo já marcado de um posto ANTERIOR a este. É o que segura a final
        // de uma categoria atrás da fase de grupos de outra.
        //
        // ⚠️ SEM `+ duração`, de propósito: é o horário daquele jogo, e não a rodada seguinte a
        // ele. No minuto em que o último jogo de grupo roda ainda sobra quadra, e quem a ocupa é o
        // primeiro jogo do posto seguinte — *"a menos que fique horario vazio"*, nas palavras do
        // pedido. Empurrar pra rodada seguinte deixaria essas quadras vazias por regra.
        //
        // ⚠️ Conta os de fora junto (`jaMarcados`): num recálculo no meio do torneio a maior parte
        // dos grupos já rolou, e olhar só pros remarcados diria que a fase de grupos acabou cedo —
        // as eliminatórias subiriam pra cima dela.
        DateTime? FimDosPostosAnteriores(int posto)
        {
            var fim = jaEmQuadra
                .Where(j => j.HorarioPrevisto != null && OrdemDasFases.Posto(j.Fase) < posto)
                .Select(j => j.HorarioPrevisto!.Value)
                .DefaultIfEmpty()
                .Max();

            return fim == default ? null : fim;
        }

        // ── O SEGUNDO PISO: A DEPENDÊNCIA DE RESULTADO ───────────────────────────────────
        // A barreira responde "o torneio já chegou neste degrau?"; este piso responde "esta
        // categoria já sabe QUEM joga?". Os dois valem, e vence o mais tarde.
        //
        // Aqui SIM entra a folga de uma rodada (AberturaDaProximaFase): quem disputa o último jogo
        // da fase anterior é candidato a passar, e emendar as duas no mesmo horário o poria na
        // quadra no minuto em que saiu dela.
        DateTime? PisoDaCategoria(int posto, int categoriaId)
        {
            var fim = jaEmQuadra
                .Where(j => j.CategoriaId == categoriaId && j.HorarioPrevisto != null
                         && OrdemDasFases.Posto(j.Fase) < posto)
                .Select(j => j.HorarioPrevisto!.Value)
                .DefaultIfEmpty()
                .Max();

            return fim == default
                ? null
                : GradeDeJogos.AberturaDaProximaFase(fim, torneio.HoraFimDoDia,
                    torneio.HoraInicioDiasSeguintes, VagasDaGrade.Duracao(torneio));
        }

        static DateTime MaisTarde(DateTime um, DateTime? outro) =>
            outro is DateTime o && o > um ? o : um;

        // Posto por posto, do menor pro maior. Cada um só é montado depois que o anterior inteiro
        // já tem horário — é isso que faz `FimDosPostosAnteriores` ter o que ler.
        foreach (var doPosto in jogos
                     .GroupBy(j => OrdemDasFases.Posto(j.Fase))
                     .OrderBy(g => g.Key)
                     .ToList())
        {
            var barreira = MaisTarde(abre, FimDosPostosAnteriores(doPosto.Key));

            // ⚠️ A FASE DE GRUPOS É UMA LEVA SÓ, e não uma por categoria (alerta do Felipe:
            // *"cuidado por que os grupos podem ter rodada 2 tambem"*). O intercalamento que dá
            // descanso à dupla é ENTRE grupos de todas as categorias — ver
            // OrdemDasRodadas.IntercalarFaseDeGrupos —, e fatiar por categoria aqui o desmontaria:
            // cada categoria jogaria os três jogos de cada grupo em sequência, que é exatamente o
            // defeito de 07/09. E não há o que esperar dentro dela: a rodada 2 de um grupo não
            // depende da rodada 1 de outro.
            if (doPosto.Key == OrdemDasFases.PostoDaFaseDeGrupos)
            {
                Agendar(OrdemDaFila(doPosto, torneio.QuantidadeQuadras), barreira);
                continue;
            }

            // Da categoria que libera primeiro pra que libera por último. O encaixe é guloso e
            // pega a primeira vaga livre: marcar fora dessa ordem daria as vagas mais cedo pra
            // quem ainda nem terminou a fase anterior.
            foreach (var daCategoria in doPosto
                         .GroupBy(j => j.CategoriaId)
                         .Select(g => new
                         {
                             Abre = MaisTarde(barreira, PisoDaCategoria(doPosto.Key, g.Key)),
                             Jogos = g.ToList(),
                         })
                         .OrderBy(x => x.Abre)
                         .ToList())
            {
                Agendar(OrdemDaFila(daCategoria.Jogos, torneio.QuantidadeQuadras), daCategoria.Abre);
            }
        }
    }

    // A ORDEM DA FILA QUE CHEGA AO ENCAIXE.
    //
    // O posto decide QUANDO cada família entra; a intercalação decide a ordem DENTRO da fase de
    // grupos, que é o que dá descanso à dupla (ver Services/OrdemDasRodadas). As duas são
    // independentes: a segunda não tira nenhum jogo do posto em que a primeira o pôs.
    //
    // ⚠️ A CHAVE DIRETA DEIXOU DE ABRIR O TORNEIO em 09/09/2026, e isso inverte a decisão de
    // 05/08/2026. Ela abria porque não espera resultado de ninguém e é a que tem mais rodadas pela
    // frente (24 duplas são cinco), e empurrá-la pro fim dos grupos empurrava as cinco junto — no
    // Interno a final da chave geral foi parar às 23h18. Perguntado sobre exatamente esse custo, o
    // Felipe escolheu o outro lado: *"a menos que fique horario vazio, mas a ordem é colocar todos
    // jogos de chave antes"*. A primeira rodada dela é eliminatória como qualquer outra, e o posto
    // trata as duas igual.
    public static List<Partida> OrdemDaFila(IEnumerable<Partida> jogos, int quadras) =>
        OrdemDasRodadas.IntercalarFaseDeGrupos(
            jogos.OrderBy(j => OrdemDasFases.Posto(j.Fase)).ToList(),
            quadras);

    /// <summary>
    /// Os jogos AINDA NÃO JOGADOS que uma rodada nova de posto <paramref name="posto"/> pode ter
    /// deixado fora de ordem: os de posto MAIOR que já tinham horário.
    /// </summary>
    /// <remarks>
    /// ⚠️ POR POSTO, E NÃO POR HORÁRIO. A rodada que está nascendo ainda não tem hora nenhuma — é
    /// justamente isso que este método existe pra resolver —, então comparar relógio devolveria
    /// lista vazia sempre e o conserto não aconteceria.
    ///
    /// ⚠️ SÓ "Agendada", e só posto MAIOR. Jogo FINALIZADO ou EM QUADRA não se remarca: é a mesma
    /// linha que o "Refazer grade" não cruza. E um jogo de posto menor ou igual nunca fica fora de
    /// ordem por causa de uma rodada que acabou de entrar — quem entra é que se acomoda a ele.
    ///
    /// ⚠️ O CUSTO, dito com todas as letras: um jogo já anunciado pode MUDAR DE HORA quando outra
    /// categoria avança. Ele só anda pra FRENTE (a ordem é a mesma, o degrau é que chegou), e a
    /// alternativa é a final de uma categoria antes da fase de grupos de outra — que foi o que o
    /// organizador viu na tela e mandou consertar.
    /// </remarks>
    public static List<Partida> ForaDeOrdem(IEnumerable<Partida> candidatos, int posto) =>
        candidatos
            .Where(p => p.Status == "Agendada"
                     && p.HorarioPrevisto != null
                     && OrdemDasFases.Posto(p.Fase) > posto)
            .ToList();
}
