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
    // `jaMarcados` são os jogos do torneio que JÁ têm hora — as fases anteriores, as outras
    // categorias. Sem eles o encaixe começa do zero e acha que o torneio inteiro está vago:
    // marca a semifinal na Quadra A das 22h onde já existe um jogo, e chama pra duas quadras
    // quem estiver nos dois. Passando-os, uma rodada nova entra EMENDADA nas vagas livres em
    // vez de esperar tudo acabar — que era o preço de não saber o que já estava marcado.
    // `quadrasPorCategoria` é a quadra que cada categoria PREFERE, escolhida pelo organizador
    // (Models/QuadraDaCategoria). Sem ela — que é o caso da maioria dos torneios — nada muda:
    // a quadra continua sendo a primeira livre do horário. Com ela, a regra dos três degraus
    // de Services/PreferenciaDeQuadra decide, e é ela também que autoriza a ÚNICA quebra da
    // ordem da fila que existe aqui: quando o primeiro jogo livre só caberia tomando a quadra
    // reservada de outra categoria, quem entra na vaga é um jogo da categoria dona dela, se
    // houver algum esperando.
    // ⚠️ `duracaoMinutos` NÃO TEM VALOR PADRÃO, de propósito (21/08/2026). Ele é o que
    // transforma "mesmo minuto" em "se cruzam no relógio" — ver o comentário de `ocupados`
    // logo abaixo. Um default faria os três chamadores compilarem calados com uma duração
    // inventada, e o erro seria uma janela de conflito do tamanho errado: invisível.
    //
    // `janelasProibidasPorDupla` é o impedimento de horário PAGO na inscrição (ver
    // Services/JanelasDeImpedimento) — até 21/08/2026 cobrado e nunca lido por aqui. Dupla
    // ausente do mapa (ou o mapa inteiro ausente) não tem restrição nenhuma, como sempre foi.
    public static void Encaixar(List<Partida> jogos, IReadOnlyList<DateTime> horarios,
        int duracaoMinutos,
        IReadOnlyDictionary<int, int[]>? ocupantesPorDupla = null,
        IReadOnlyList<string>? quadras = null,
        IReadOnlyList<Partida>? jaMarcados = null,
        IReadOnlyDictionary<int, string[]>? quadrasPorCategoria = null,
        IReadOnlyDictionary<int, (DateTime Inicio, DateTime Fim)[]>? janelasProibidasPorDupla = null)
    {
        int[] Ocupantes(int duplaId) =>
            ocupantesPorDupla != null && ocupantesPorDupla.TryGetValue(duplaId, out var pessoas) && pessoas.Length > 0
                ? pessoas
                : new[] { -duplaId };

        // ⚠️ MESMA NORMALIZAÇÃO DO `Horarios` (0 vira 50), e ela é OBRIGATÓRIA aqui: o
        // TorneiosController.Americano monta a grade com `tempoPartida` (que já cai pra 50) mas
        // chegou a passar o `TempoPrevistoPartidaMinutos` cru pra cá. Num torneio com o tempo
        // zerado, as vagas nasciam de 50 em 50 e a janela de conflito era de 1 minuto — o
        // detector voltava a ser "instante exato", em silêncio, exatamente o bug que este
        // método passou a resolver.
        var duracao = TimeSpan.FromMinutes(duracaoMinutos > 0 ? duracaoMinutos : 50);

        // ⚠️ A AGENDA É POR OCUPANTE, E A COLISÃO É POR INTERVALO — as duas eram por INSTANTE
        // EXATO (`Dictionary<DateTime, HashSet<int>>`) até 21/08/2026, e isso era um bug vivo
        // em torneio de um clube só:
        //
        // O "refazer grade" do meio do torneio parte de `DateTime.Now` (ver
        // TorneiosController.Chaves.AberturaDoRecalculo). Aperte às 20h13 e os jogos novos
        // nascem em 20:13, 21:03…, enquanto os que já estavam marcados seguem em 20:00, 20:50.
        // Duas grades desalinhadas — e o dicionário por instante NUNCA cruzava as duas. Dava
        // pra chamar a mesma pessoa pras 20:13 com o jogo dela das 20:00 ainda em quadra, e pra
        // pôr dois jogos na mesma quadra a 20 minutos de distância. Nada reclamava.
        //
        // Consertar só a pessoa e deixar a quadra no instante seria conserto pela metade: o
        // agendamento âncora por CATEGORIA (RoboDoChaveamento) já desalinha categorias entre si
        // dentro do mesmo torneio.
        var ocupados = new Dictionary<int, List<DateTime>>();      // pessoa  -> quando joga
        var ocupadas = new Dictionary<string, List<DateTime>>();   // quadra  -> quando está tomada

        bool Cruza(List<DateTime> quando, DateTime horario) =>
            quando.Any(h => (h - horario).Duration() < duracao);

        void Anotar<T>(Dictionary<T, List<DateTime>> agenda, T chave, DateTime quando) where T : notnull
        {
            if (!agenda.TryGetValue(chave, out var lista)) agenda[chave] = lista = new List<DateTime>();
            lista.Add(quando);
        }

        var fila = new List<Partida>(jogos);

        // A preferência de quadra, pronta pra ser consultada jogo a jogo. `comDono` é o que
        // distingue quadra neutra de quadra que alguém pediu — ver PreferenciaDeQuadra.
        var comDono = PreferenciaDeQuadra.ComDono(quadrasPorCategoria);
        IReadOnlyList<string> Preferidas(Partida p) =>
            PreferenciaDeQuadra.Da(quadrasPorCategoria, p.CategoriaId);

        foreach (var marcado in jaMarcados ?? Array.Empty<Partida>())
        {
            if (marcado.HorarioPrevisto is not DateTime quando) continue;

            foreach (var pessoa in Ocupantes(marcado.Dupla1Id)) Anotar(ocupados, pessoa, quando);
            foreach (var pessoa in Ocupantes(marcado.Dupla2Id)) Anotar(ocupados, pessoa, quando);

            if (string.IsNullOrEmpty(marcado.NomeQuadra)) continue;
            Anotar(ocupadas, marcado.NomeQuadra!, quando);
        }

        for (int i = 0; i < horarios.Count; i++)
        {
            if (fila.Count == 0) break;

            var horario = horarios[i];

            // A ÚNICA coisa impossível na vida real é a mesma PESSOA em duas quadras ao mesmo
            // tempo. Fases diferentes dividindo o horário é normal e desejável: a final de uma
            // categoria pode acontecer junto com a semifinal de outra, porque são pessoas
            // diferentes — e proibir isso só espalharia a grade e atrasaria o encerramento.
            // Dentro de UMA categoria a impossibilidade já é estrutural: a folga entre fases
            // (AberturaDaProximaFase) nunca deixa a final encostar na semifinal que a decide.
            bool OcupadaAgora(int pessoa) =>
                ocupados.TryGetValue(pessoa, out var quando) && Cruza(quando, horario);

            // A janela é MEIO ABERTA ([Inicio, Fim)): o corte de sábado passa de "ImpedimentoManha"
            // pra "ImpedimentoTarde" exatamente no meio-dia, e um jogo marcado ÀS 12h00 precisa
            // cair num dos dois lados, nunca nos dois.
            bool DentroDeJanelaProibida(int duplaId) =>
                janelasProibidasPorDupla != null
                && janelasProibidasPorDupla.TryGetValue(duplaId, out var janelas)
                && janelas.Any(j => horario >= j.Inicio && horario < j.Fim);

            bool Livre(Partida p) =>
                !Ocupantes(p.Dupla1Id).Any(OcupadaAgora) && !Ocupantes(p.Dupla2Id).Any(OcupadaAgora)
                && !DentroDeJanelaProibida(p.Dupla1Id) && !DentroDeJanelaProibida(p.Dupla2Id);

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

            // As quadras ainda livres NESTE horário. Só interessam quando o torneio cadastrou
            // quadra; sem cadastro a grade marca hora e não nomeia lugar, como sempre fez.
            List<string> livresAgora = new();
            bool temQuadraCadastrada = quadras is { Count: > 0 };

            if (temQuadraCadastrada)
            {
                livresAgora = quadras!
                    .Where(q => !(ocupadas.TryGetValue(q, out var quando) && Cruza(quando, horario)))
                    .ToList();

                // ⚠️ MUDANÇA DE COMPORTAMENTO (21/08/2026): com quadra cadastrada e NENHUMA
                // livre no intervalo, a vaga é PULADA em vez de o jogo nascer com hora e sem
                // quadra. É o incidente do Interno de 05/08/2026 fechado na origem — uma
                // semifinal entrou ao vivo sem quadra enquanto os outros quatro jogos tinham a
                // delas, e o organizador não conseguia nem arrumar na mão.
                //
                // Mesma válvula da fila logo acima: se as vagas que sobram não dão conta dos
                // jogos que faltam, marcar sem quadra volta a ser melhor que não marcar.
                if (livresAgora.Count == 0)
                {
                    int vagasRestantes = horarios.Count - i - 1;
                    if (vagasRestantes >= fila.Count) continue;
                }
            }

            // A ÚNICA quebra da ordem da fila que existe aqui, e só quando há preferência
            // cadastrada: se o primeiro da fila só caberia tomando a quadra preferida de outra
            // categoria, quem entra na vaga é um jogo da categoria DONA dela — se estiver
            // esperando na fila. É o que faz a preferência valer sempre, e não por sorte.
            //
            // Nada disso empurra jogo pra fora: o preterido continua no topo da fila e pega a
            // vaga seguinte, que é o mesmo horário na outra quadra ou o horário seguinte.
            if (comDono.Count > 0 && livresAgora.Count > 0
                && PreferenciaDeQuadra.TomariaQuadraDeOutro(
                       PreferenciaDeQuadra.Escolher(livresAgora, Preferidas(jogo), comDono),
                       Preferidas(jogo), comDono)
                && fila.FirstOrDefault(p => Livre(p) && Preferidas(p).Any(livresAgora.Contains)) is { } dono)
            {
                jogo = dono;
            }

            jogo.HorarioPrevisto = horario;

            // A quadra sai de PreferenciaDeQuadra: a preferida da categoria, senão uma neutra,
            // senão a primeira livre — que é exatamente o comportamento antigo quando ninguém
            // pediu quadra nenhuma. Livre, e não a posição na fila, porque o horário pode já
            // ter jogo marcado de outra fase: aí a primeira quadra está ocupada e contar
            // posição daria o mesmo nome duas vezes. Torneio sem quadra cadastrada segue sem
            // nome: inventar "Quadra 1" onde o clube chama de "Central" seria pior.
            if (temQuadraCadastrada
                && PreferenciaDeQuadra.Escolher(livresAgora, Preferidas(jogo), comDono) is { } livre)
            {
                jogo.NomeQuadra = livre;
                Anotar(ocupadas, livre, horario);
            }

            foreach (var pessoa in Ocupantes(jogo.Dupla1Id)) Anotar(ocupados, pessoa, horario);
            foreach (var pessoa in Ocupantes(jogo.Dupla2Id)) Anotar(ocupados, pessoa, horario);
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

    // Tira da lista de horários as vagas que JÁ TÊM DONO. Cada horário aparece uma vez por
    // quadra; se duas delas já estão ocupadas às 22h, sobram as outras — e o encaixe para de
    // oferecer quadra que não existe. Sem este desconto, `Horarios` diria "cinco vagas às
    // 22h" num horário em que três já estão jogando.
    public static List<DateTime> Descontando(IEnumerable<DateTime> horarios, IEnumerable<DateTime> jaTomados)
    {
        var tomados = new Dictionary<DateTime, int>();
        foreach (var h in jaTomados) tomados[h] = tomados.GetValueOrDefault(h) + 1;

        var sobram = new List<DateTime>();
        foreach (var h in horarios)
        {
            if (tomados.TryGetValue(h, out var quantas) && quantas > 0)
            {
                tomados[h] = quantas - 1;
                continue;
            }

            sobram.Add(h);
        }

        return sobram;
    }

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

    // Quando a PRÓXIMA FASE pode abrir, dado o último jogo da fase que a alimenta.
    //
    // A fase seguinte abre assim que a anterior TERMINA — nunca no mesmo horário dela, porque
    // os dois finalistas saem da semifinal e não podem estar em duas quadras ao mesmo tempo.
    // Mas também não mais tarde que isso.
    //
    // ⚠️ Já foi uma RODADA INTEIRA de folga, pra ninguém jogar dois jogos seguidos. O
    // organizador corrigiu (05/08/2026), e a correção é sobre o mundo real: **evitar jogo
    // seguido só faz sentido se houver outro jogo pra pôr no meio**. Com quadra sobrando, a
    // folga não vira descanso — vira quadra parada e torneio mais longo. Num interno rápido
    // (jogos de 11 min, 5 quadras) jogar seguido é o normal; é quando as quadras são poucas
    // que a grade intercala sozinha — e ela já faz isso, porque o horário cheio empurra a
    // fase seguinte pra frente (ver Encaixar e ProximasFasesDaChave.Agendar). Ou seja: a
    // folga aparece quando há com que preenchê-la, e some quando não há. É o que se pediu.
    //
    // Continua existindo separado de `DepoisDe` porque a pergunta é outra — "quando a FASE
    // pode abrir" e não "quando o próximo jogo cabe" —, e é nessa distinção que a regra mora.
    public static DateTime AberturaDaProximaFase(DateTime ultimoJogoDaFase, TimeSpan ultimoInicioDoDia,
        TimeSpan aberturaDiasSeguintes, int duracaoMinutos) =>
        DepoisDe(ultimoJogoDaFase, ultimoInicioDoDia, aberturaDiasSeguintes, duracaoMinutos);
}
