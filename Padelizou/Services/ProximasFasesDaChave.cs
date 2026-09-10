namespace Padelizou.Services;

// As fases que AINDA VÃO ACONTECER, com hora, QUADRA e com quem pode jogá-las.
//
// O motor do mata-mata cria cada rodada só quando a anterior fecha (Dupla1Id/Dupla2Id são
// obrigatórios — não existe partida "a definir" no banco). O efeito na tela era que a
// Semifinal e a Final simplesmente NÃO EXISTIAM pra quem olhava: o jogador via a primeira
// rodada e mais nada, sem saber a que horas voltar nem contra quem pode jogar.
//
// ⚠️ Os jogos do mata-mata são NUMERADOS por fase ("Quartas de Final 1", "Quartas de Final
// 2"...) e um lado se descreve pelo jogo de onde vem: "Vencedor Quartas de Final 1". A
// primeira versão listava os candidatos por nome — "Um destes 4: Bernardo Mendonça &
// Alexandre Medina, Geison Moyses & …" — e a linha estourava a tela, sendo cortada
// justamente no fim, onde estavam os últimos nomes. O número diz a mesma coisa em três
// palavras e é rastreável: o jogo "Quartas de Final 1" está na mesma lista, logo acima.
//
// O pareamento usa a MESMA regra do avanço de verdade (ChaveamentoMataMata.ParearVencedores):
// primeiro cruza com último. Uma conta paralela diria um cruzamento que o sorteio não faz.
//
// ---- Por que ESTRUTURA e GRADE são duas coisas separadas aqui ----
//
// Cada categoria sabe sozinha QUEM joga contra quem (a cadeia de fases). Mas ONDE e QUANDO
// é uma pergunta do TORNEIO INTEIRO: as quadras são as mesmas pra todo mundo. Enquanto cada
// categoria calculava o próprio horário por conta própria, a tela do Interno mostrou OITO
// jogos às 22:23 num torneio de CINCO quadras — e nenhum deles com quadra, porque não havia
// como dizer qual. Por isso a cadeia sai sem hora (`Montar`, `MontarDosGrupos`) e uma
// segunda passada (`Agendar`) põe todas as cadeias na mesma grade, junto com o que já está
// marcado de verdade.
public static class ProximasFasesDaChave
{
    // O que a projeção precisa saber de uma partida já existente.
    public record PartidaDaChave(int Id, string Fase, string Dupla1, string Dupla2, DateTime? Horario);

    // Um lado do confronto que ainda não tem dono: ou a dupla que passou direto (bye, e aí
    // tem nome), ou a procedência ("Vencedor Quartas de Final 1", "1º do Grupo A").
    //
    // `DeQualFase`/`DeQualNumero` guardam a procedência DESMONTADA quando ela é um jogo desta
    // mesma chave — é o que deixa a tela transformar o rótulo em link pro jogo citado. Nulos
    // no bye (que já tem nome) e na colocação de grupo ("2º do Grupo C"), que não apontam pra
    // jogo nenhum.
    //
    // `DeQualGrupo` é o outro lado da mesma ideia: na colocação de grupo, o GRUPO desmontado
    // ("Grupo C"). Sem ele, "só os meus jogos" não tinha como saber que o jogador do Grupo A
    // nunca vai jogar a oitava do "1º do Grupo E" — e mostrava a chave inteira da categoria.
    // Fica em campo próprio, e não lido do rótulo, pelo mesmo motivo da procedência: o rótulo
    // é de tela e muda.
    public record Lado(string Rotulo, string? DeQualFase = null, int? DeQualNumero = null,
                       string? DeQualGrupo = null);

    // A categoria vem junto porque a lista de jogos mistura todas: sem ela, duas semifinais
    // de categorias diferentes viram duas linhas idênticas.
    public record JogoQueVem(string Categoria, string Fase, int Numero, DateTime? Horario,
                             Lado Lado1, Lado Lado2, string? Quadra = null, int? CategoriaId = null)
    {
        // "Quartas de Final 2" — o rótulo que a tela mostra e que os lados citam.
        // A FINAL não numera: é um jogo só, e "Final 1" faria pensar que existe uma Final 2.
        public string FaseNumerada => Fase == "Final" ? Fase : $"{Fase} {Numero}";
    }

    // ================= ESTRUTURA: quem joga com quem, ainda sem hora =================

    public record RodadaQueVem(string Fase, IReadOnlyList<(Lado Lado1, Lado Lado2)> Confrontos);

    // O caminho de UMA categoria até a final. `DepoisDe` é o último jogo que já tem hora e
    // que alimenta a primeira rodada projetada — dele sai a folga de abertura.
    public record CadeiaDeFases(string Categoria, DateTime? DepoisDe, IReadOnlyList<RodadaQueVem> Rodadas,
                                int? CategoriaId = null)
    {
        public static readonly CadeiaDeFases Vazia = new("", null, Array.Empty<RodadaQueVem>());
    }

    // ---- Entrada 1: a chave JÁ COMEÇOU (existe pelo menos uma fase de mata-mata) ----
    //
    // `byes` são as duplas que pularam a primeira rodada: elas entram na conta do pareamento
    // DEPOIS dos vencedores, igual ao avanço de verdade. Sem elas, o quadro projetado teria
    // menos gente que o real e os cruzamentos sairiam todos errados.
    public static CadeiaDeFases Montar(
        IReadOnlyList<PartidaDaChave> partidasDeMataMata,
        IReadOnlyList<string> byes,
        string categoria = "",
        int? categoriaId = null)
    {
        if (partidasDeMataMata.Count == 0) return CadeiaDeFases.Vazia;

        var faseAtual = FaseMaisAdiantada(partidasDeMataMata);
        if (faseAtual == null) return CadeiaDeFases.Vazia;

        var daFase = partidasDeMataMata
            .Where(p => p.Fase == faseAtual)
            .OrderBy(p => p.Id)          // a MESMA ordem do avanço de verdade
            .ToList();

        // Cada jogo da fase atual entrega um vencedor — citado pelo NÚMERO dele naquela
        // fase; cada bye entrega a própria dupla, que já tem nome.
        var lados = daFase
            .Select((_, i) => new Lado($"Vencedor {faseAtual} {i + 1}", faseAtual, i + 1))
            .Concat(byes.Select(b => new Lado(b)))
            .ToList();

        return new CadeiaDeFases(categoria, UltimoHorario(partidasDeMataMata), Encadear(lados), categoriaId);
    }

    // ---- Entrada 2: a chave AINDA NEM COMEÇOU (categoria na fase de grupos) ----
    //
    // Aqui não existe partida de mata-mata nenhuma, então a projeção parte das COLOCAÇÕES:
    // "1º do Grupo A × 2º do Grupo C". Sem isto, a categoria que sai de grupos não mostrava
    // mata-mata nenhum na lista de jogos — só a chave direta aparecia, porque ela já nasce
    // com a primeira rodada criada e a projeção tinha de onde partir.
    //
    // Os confrontos da primeira rodada saem do mesmo motor da aba de chaves
    // (Services/ChaveProjetada), que por sua vez usa o do sorteio de verdade.
    public static CadeiaDeFases MontarDosGrupos(
        IReadOnlyList<string> grupos,
        int classificadosPorGrupo,
        DateTime? fimDosGrupos,
        string categoria = "",
        int? categoriaId = null)
    {
        var (fase, confrontos, byes) = ChaveProjetada.Montar(grupos, classificadosPorGrupo);
        if (confrontos.Count == 0) return CadeiaDeFases.Vazia;

        var primeira = new RodadaQueVem(fase, confrontos
            .Select(c => (VagaDeGrupo(c.Lado1), VagaDeGrupo(c.Lado2)))
            .ToList());

        var proximos = confrontos
            .Select((_, i) => new Lado($"Vencedor {fase} {i + 1}", fase, i + 1))
            // Quem folga a primeira rodada entra DEPOIS dos vencedores, na mesma ordem do
            // avanço de verdade — é o que faz cada vencedor cruzar com uma vaga que passou
            // direto. Aqui o bye ainda não tem nome: é a colocação ("2º do Grupo C").
            .Concat(byes.Select(VagaDeGrupo))
            .ToList();

        var rodadas = new List<RodadaQueVem> { primeira };
        rodadas.AddRange(Encadear(proximos));

        return new CadeiaDeFases(categoria, fimDosGrupos, rodadas, categoriaId);
    }

    // A colocação de grupo vira lado guardando o GRUPO: é por ele que "só os meus jogos"
    // reconhece a vaga que pode ser do jogador ("2º do Grupo A" é dele; "1º do Grupo E" não).
    private static Lado VagaDeGrupo(ChaveProjetada.Vaga vaga) =>
        new(vaga.Rotulo, DeQualGrupo: vaga.Grupo);

    // O encadeamento, rodada a rodada, até a final.
    private static List<RodadaQueVem> Encadear(List<Lado> lados)
    {
        var rodadas = new List<RodadaQueVem>();

        // Trava de segurança: uma chave real nunca passa de meia dúzia de rodadas, e um dado
        // torto não pode virar laço infinito numa página que o jogador abre.
        for (int rodada = 0; lados.Count >= 2 && rodada < 10; rodada++)
        {
            // O NOME sai de quantos ainda estão vivos, não de encadear ProximaFase — é o que
            // os dois robôs de verdade fazem (NomeFase(avancam.Count)). Numa chave com BYE os
            // dois divergem: 2 jogos de primeira rodada + 2 duplas descansadas são 4 duplas,
            // ou seja SEMIFINAL, mas encadear diria "Oitavas de Final" — e a tela anunciaria
            // uma fase que o torneio nunca vai criar.
            var fase = ChaveamentoMataMata.NomeFase(lados.Count);
            var confrontos = Parear(lados);

            rodadas.Add(new RodadaQueVem(fase, confrontos));
            lados = confrontos.Select((_, i) => new Lado($"Vencedor {fase} {i + 1}", fase, i + 1)).ToList();

            if (fase == "Final") break;
        }

        return rodadas;
    }

    // ================= GRADE: quando e em que quadra =================

    // Uma vaga que já tem dono — os jogos REAIS que estão marcados. A projeção precisa deles
    // pra não prometer uma quadra que já está ocupada. `Quadra` nula é o torneio que não
    // cadastrou quadra: ela ocupa a vaga sem tomar um nome.
    //
    // ⚠️ `Fase` entrou em 09/09/2026 e NÃO é enfeite: é ela que diz o POSTO daquele jogo real, e
    // sem posto a projeção não tem como esperar a fase de grupos de OUTRA categoria. Opcional
    // porque nem todo chamador sabe dizer — e vaga sem fase declarada NÃO vira barreira, de
    // propósito: chutar "deve ser grupo" seguraria a chave inteira atrás de um jogo que talvez
    // seja a final.
    public record VagaOcupada(DateTime Horario, string? Quadra, string? Fase = null);

    // O horário que o organizador RESERVOU pra um jogo previsto — a troca de horário feita na
    // prévia (Models/ReservaDeHorario). A chave é a mesma numeração desta projeção.
    public record HorarioReservado(int CategoriaId, string Fase, int Numero, DateTime Horario, string? Quadra = null);

    // `Quadras` são os nomes NA ORDEM; `Capacidade` é quantos jogos rodam ao mesmo tempo.
    // Os dois existem porque podem discordar: um torneio de 5 quadras pode ter cadastrado só
    // 3 nomes, e aí duas vagas de cada horário ficam sem nome — o que é a verdade, e melhor
    // que inventar "Quadra 4".
    public record ConfiguracaoDaGrade(
        int DuracaoMinutos, int Capacidade, IReadOnlyList<string> Quadras,
        TimeSpan UltimoInicioDoDia, TimeSpan AberturaDiasSeguintes,
        SedesDoTorneio? Sedes = null);

    // Põe TODAS as cadeias na mesma grade, disputando as mesmas quadras.
    //
    // A regra que manda é a folga entre fases (GradeDeJogos.AberturaDaProximaFase): uma
    // rodada abre uma rodada inteira depois do fim da que a alimenta. Dentro disso, os jogos
    // ocupam as quadras livres do horário; quando o horário lota, o resto cai no seguinte.
    //
    // ⚠️ E A ORDEM ENTRE CADEIAS É POR POSTO DE FASE (09/09/2026, Services/OrdemDasFases). Até
    // aqui cada cadeia andava sozinha, a partir do fim dos grupos DELA — e foi exatamente isso que
    // o Felipe viu na tela do Er: a *Final* da 3ª Feminina com o selo "prévia" às 22:10 de 12/09,
    // e jogos de GRUPO da 6ª Masculina em 15/09. A categoria de 4 duplas fecha os grupos cedo,
    // projeta semi e final na sequência, e acaba antes de a de 32 ter jogado a primeira
    // eliminatória.
    //
    // 🗣️ *"o torneio tem q seguir uma ordem, primeiro todas as chaves, depois todas as primeiras
    // eliminatorias (decimas > oitavas > quartas > semi > final) a ideia e fazer as finais de cada
    // categorias ser os ultimos jogos do torneio"*.
    //
    // ⚠️ ESTA É A MESMA RÉGUA DA GRADE DE VERDADE (Services/LevasDaGrade), e tem que continuar
    // sendo: prévia e grade dizendo ordens diferentes seria pior que não projetar — o jogador leria
    // uma coisa na aba de jogos e jogaria outra.
    //
    // ⚠️ Nada aqui sabe QUEM vai jogar (os lados são "Vencedor Quartas de Final 1"), então a
    // grade não tem como evitar que a mesma pessoa apareça em duas categorias no mesmo
    // horário. Quem garante isso é o encaixe de verdade (GradeDeJogos.Encaixar), na hora em
    // que a rodada nasce com nome e sobrenome.
    public static List<JogoQueVem> Agendar(
        IReadOnlyList<CadeiaDeFases> cadeias,
        ConfiguracaoDaGrade grade,
        IReadOnlyList<VagaOcupada>? jaMarcados = null,
        IReadOnlyList<HorarioReservado>? reservas = null)
    {
        var vivas = cadeias.Where(c => c.Rodadas.Count > 0).ToList();
        if (vivas.Count == 0) return new List<JogoQueVem>();

        int capacidade = Math.Max(1, grade.Capacidade);
        var ocupadas = new Dictionary<DateTime, HashSet<string>>();
        var lotacao = new Dictionary<DateTime, int>();

        foreach (var vaga in jaMarcados ?? Array.Empty<VagaOcupada>())
        {
            lotacao[vaga.Horario] = lotacao.GetValueOrDefault(vaga.Horario) + 1;

            if (!string.IsNullOrEmpty(vaga.Quadra))
            {
                if (!ocupadas.TryGetValue(vaga.Horario, out var nomes))
                    ocupadas[vaga.Horario] = nomes = new HashSet<string>();
                nomes.Add(vaga.Quadra!);
            }
        }

        // ── AS RESERVAS DO ORGANIZADOR (10/09/2026, Models/ReservaDeHorario) ─────────────
        // 🗣️ *"permita também trocar de horário as eliminatórias, não apenas as de chave"*. A
        // troca feita numa prévia mora numa reserva, e a prévia é a primeira a obedecê-la.
        //
        // ⚠️ O SLOT É RESERVADO AQUI, ANTES DE QUALQUER CADEIA PASSAR POR ELE. As cadeias saem em
        // ordem de posto e de abertura; a Final da Y é emitida depois da Final da X, e se o slot
        // só fosse tomado na hora de emitir a Y, a X já teria passado por ele e a troca do
        // organizador sumiria na primeira categoria da fila. Reserva que no fim não vale (ver
        // `Vale`, abaixo) devolve o slot — `Liberar`.
        //
        // Só reserva o que alguma cadeia viva vai emitir: reserva órfã (a fase virou real, a
        // categoria saiu) não pode tomar quadra de ninguém.
        var reservadas = new Dictionary<(int Categoria, string Fase, int Numero), HorarioReservado>();
        var quadrasReservadas = new HashSet<(DateTime, string)>();

        void Liberar(HorarioReservado r)
        {
            lotacao[r.Horario] = Math.Max(0, lotacao.GetValueOrDefault(r.Horario) - 1);
            if (r.Quadra != null && quadrasReservadas.Remove((r.Horario, r.Quadra))
                && ocupadas.TryGetValue(r.Horario, out var nomes))
                nomes.Remove(r.Quadra);
        }

        foreach (var r in reservas ?? Array.Empty<HorarioReservado>())
        {
            bool algumaCadeiaEmite = vivas.Any(c => c.CategoriaId == r.CategoriaId
                && c.Rodadas.Any(rod => rod.Fase == r.Fase && r.Numero >= 1 && r.Numero <= rod.Confrontos.Count));
            if (!algumaCadeiaEmite) continue;

            reservadas[(r.CategoriaId, r.Fase, r.Numero)] = r;
            lotacao[r.Horario] = lotacao.GetValueOrDefault(r.Horario) + 1;

            if (r.Quadra == null) continue;
            if (!ocupadas.TryGetValue(r.Horario, out var nomesDaHora))
                ocupadas[r.Horario] = nomesDaHora = new HashSet<string>();
            // Só o que ESTA reserva pôs é o que `Liberar` tira: a quadra que um jogo real já
            // ocupa no mesmo minuto continua ocupada.
            if (nomesDaHora.Add(r.Quadra)) quadrasReservadas.Add((r.Horario, r.Quadra));
        }

        // Estado de cada cadeia: qual a próxima rodada e a partir de quando ela pode abrir.
        var proxima = new int[vivas.Count];
        var abertura = vivas.Select(c => AbrirRodada(c.DepoisDe, grade)).ToArray();

        var jogos = new List<JogoQueVem>();

        // A BARREIRA DE POSTO: o posto que está sendo emitido agora e o fim do posto ANTERIOR.
        // Como a escolha abaixo pega sempre o menor posto disponível, quando um posto novo começa
        // todos os anteriores já saíram — os emitidos aqui ficam em `emitidos`, e os reais vêm de
        // `jaMarcados`; a barreira é o fim do BLOCO dos dois juntos (OrdemDasFases.FimDoBloco).
        int postoEmitido = int.MinValue;
        DateTime? fimDoPostoAnterior = null;
        var emitidos = new List<(int Posto, DateTime Horario)>();

        DateTime Seguinte(DateTime h) =>
            GradeDeJogos.DepoisDe(h, grade.UltimoInicioDoDia, grade.AberturaDiasSeguintes, grade.DuracaoMinutos);

        // ── AS QUADRAS SÃO AS DA GRADE DE VERDADE (09/09/2026) ───────────────────────────
        // 🗣️ *"no domingo (dia 13/09) nao tem radar, o radar vai ser só no sabado"* — e a tela
        // mostrava "Oitavas de Final 3 · Radar · Radar 1" às 08h de domingo, com o selo "prévia".
        // Até aqui a projeção pegava a primeira quadra livre pelo NOME, e contava a capacidade
        // como se todas abrissem o dia inteiro. Com `Sedes` na configuração ela passa a fazer as
        // duas perguntas que GradeDeJogos.Encaixar já fazia: a quadra está ABERTA neste horário
        // (janela do local alugado) e esta CATEGORIA pode jogar nela (trava de clube — a 4ª fica
        // em casa)? Sem `Sedes` (chamador antigo, testes), nada muda.
        //
        // QUANTOS jogos cabem no horário: as quadras abertas nele, quando alguma tem janela —
        // senão a capacidade do torneio, como sempre. É o que faz o domingo do Er ter 5 vagas por
        // horário, e não 7.
        int CapacidadeEm(DateTime h) =>
            grade.Sedes?.QuadrasAbertasEm(h) is int abertas ? abertas : capacidade;

        bool Serve(string q, DateTime h, IReadOnlyList<string>? permitidas) =>
            !(ocupadas.TryGetValue(h, out var usadas) && usadas.Contains(q))
            && (permitidas == null || permitidas.Contains(q))
            && (grade.Sedes == null || grade.Sedes.QuadraAberta(q, h));

        // ⚠️ A trava de clube é a única que faz o jogo escorregar SEM o horário estar lotado: a
        // categoria presa em casa não entra no Radar mesmo com quadra do Radar sobrando. A janela
        // não precisa disso — `CapacidadeEm` já não conta quadra fechada.
        bool SemLugar(DateTime h, IReadOnlyList<string>? permitidas) =>
            lotacao.GetValueOrDefault(h) >= CapacidadeEm(h)
            || (permitidas != null && !grade.Quadras.Any(q => Serve(q, h, permitidas)));

        // Teto de 14 dias de grade: existe só pra que uma janela absurda não vire laço sem fim.
        int teto = 14 * 24 * 60 / Math.Max(1, grade.DuracaoMinutos);

        // ⚠️ E O QUE JÁ ESTÁ MARCADO DE VERDADE CONTA NA BARREIRA (09/09/2026, segunda rodada do
        // pedido). Ordenar as fases PROJETADAS entre si não basta: o piso da primeira delas vinha
        // do fim dos grupos DA PRÓPRIA CATEGORIA (`CadeiaDeFases.DepoisDe`), e jogo de grupo de
        // OUTRA categoria não é cadeia nenhuma — é jogo real, que chega aqui como `jaMarcados`.
        //
        // 🕳️ Foi assim que a tela do Er mostrou a Quartas da 6ª Feminina em 12/09 18:50 com jogos
        // de GRUPO da 6ª Masculina marcados pra 15/09. Reproduzido em
        // ProximasFasesDaChaveTests.A_previa_espera_o_jogo_de_grupo_ja_marcado_de_outra_categoria,
        // que falhava com esse mesmo 12/09 18:50.
        var reaisPorPosto = (jaMarcados ?? Array.Empty<VagaOcupada>())
            .Where(v => v.Fase != null)
            .Select(v => (Posto: OrdemDasFases.Posto(v.Fase), v.Horario))
            .ToList();

        // ⚠️ É O FIM DO BLOCO, NÃO O `Max` (OrdemDasFases.FimDoBloco, 09/09/2026): um jogo de grupo
        // retardatário no domingo de manhã não segura as eliminatórias de sábado à noite — nem o
        // real (`jaMarcados`) nem o que esta projeção emitiu com piso tardio.
        DateTime? FimAntesDoPosto(int posto) =>
            OrdemDasFases.FimDoBloco(
                reaisPorPosto.Where(v => v.Posto < posto).Select(v => v.Horario)
                    .Concat(emitidos.Where(e => e.Posto < posto).Select(e => e.Horario)),
                Seguinte, capacidade);

        int PostoDaProxima(int i) => OrdemDasFases.Posto(vivas[i].Rodadas[proxima[i]].Fase);

        while (true)
        {
            // Primeiro o menor POSTO — a fase mais longe da final entra na frente, venha da
            // categoria que vier. Empatado o posto, a cadeia que abre mais cedo: é o que faz as
            // categorias dividirem as quadras do mesmo horário em vez de uma esperar a outra.
            int escolha = -1;
            for (int i = 0; i < vivas.Count; i++)
            {
                if (proxima[i] >= vivas[i].Rodadas.Count) continue;
                if (escolha < 0) { escolha = i; continue; }

                int daVez = PostoDaProxima(i);
                int daEscolha = PostoDaProxima(escolha);
                if (daVez > daEscolha) continue;

                // Sem horário (torneio por ordem de liberação) todas empatam: vale a ordem
                // de entrada, e a lista sai agrupada por categoria.
                if (daVez < daEscolha
                    || (abertura[i] ?? DateTime.MaxValue) < (abertura[escolha] ?? DateTime.MaxValue))
                    escolha = i;
            }

            if (escolha < 0) break;

            var cadeia = vivas[escolha];
            var rodada = cadeia.Rodadas[proxima[escolha]];

            // Virou o posto: o que já saiu é tudo de posto menor, e o fim disso é a barreira —
            // contando tanto o que ESTA projeção emitiu quanto os jogos REAIS de posto menor.
            int posto = OrdemDasFases.Posto(rodada.Fase);
            if (posto > postoEmitido)
            {
                fimDoPostoAnterior = FimAntesDoPosto(posto);
                postoEmitido = posto;
            }

            // As quadras em que ESTA categoria pode jogar — as mesmas duas réguas de
            // GradeDeJogos.Encaixar: presa a um clube (`QuadrasDe`) ou tirada do local externo
            // (`PodeIrPraSedeExtra`). Null = qualquer uma.
            IReadOnlyList<string>? permitidas = null;
            if (cadeia.CategoriaId is int categoriaId && grade.Sedes is { } sedes)
            {
                permitidas = sedes.QuadrasDe(categoriaId)
                    ?? (sedes.PodeIrPraSedeExtra(categoriaId)
                        ? null
                        : grade.Quadras.Where(q => !sedes.EhSedeExtra(q)).ToList());
            }

            var horario = abertura[escolha];

            // A dependência de resultado DESTA categoria: antes disto os dois lados do jogo ainda
            // podem estar em quadra. É o piso que nem a reserva do organizador atravessa — a
            // barreira de posto (abaixo) ela atravessa, porque entre categorias são pessoas
            // diferentes e a ordem das fases é preferência do torneio, não impossibilidade.
            var abreARodada = abertura[escolha];

            // ⚠️ SEM `+ duração`: é o horário do último jogo do posto anterior, e não a rodada
            // seguinte a ele — no minuto em que aquele jogo roda ainda sobra quadra. Mesma escolha
            // de LevasDaGrade, e pelo mesmo pedido: *"a menos que fique horario vazio"*.
            if (horario is DateTime abre && fimDoPostoAnterior is DateTime barreira && barreira > abre)
                horario = barreira;

            // O jogo mais tarde da rodada, reservado ou não: é dele que a rodada seguinte abre.
            // `horario` continua sendo o cursor dos jogos SEM reserva — reservar a Semifinal 1
            // pras 21h não arrasta a Semifinal 2 junto.
            DateTime? ultimoDaRodada = null;

            for (int i = 0; i < rodada.Confrontos.Count; i++)
            {
                string? quadra = null;
                DateTime? quando = null;

                // A reserva do organizador, se ele fez uma pra este jogo e ela ainda é possível.
                // A que deixou de ser (o torneio atrasou e a fase anterior passou dela) volta pra
                // grade como qualquer jogo — e a tela mostra a hora possível, não a prometida.
                if (cadeia.CategoriaId is int categoriaDaReserva
                    && reservadas.Remove((categoriaDaReserva, rodada.Fase, i + 1), out var reserva))
                {
                    if (ReservasDeHorario.Vale(reserva.Horario, abreARodada))
                    {
                        quando = reserva.Horario;
                        quadra = reserva.Quadra;
                    }
                    else
                    {
                        Liberar(reserva);
                    }
                }

                if (quando == null && horario is DateTime h)
                {
                    // Horário lotado (ou sem quadra que sirva a esta categoria): o jogo escorrega
                    // pro seguinte. Sem esta parada, a projeção anunciava 8 jogos no mesmo minuto
                    // num torneio de 5 quadras.
                    for (int passo = 0; SemLugar(h, permitidas) && passo < teto; passo++)
                        h = Seguinte(h);

                    quadra = grade.Quadras.FirstOrDefault(q => Serve(q, h, permitidas));

                    lotacao[h] = lotacao.GetValueOrDefault(h) + 1;
                    if (quadra != null)
                    {
                        if (!ocupadas.TryGetValue(h, out var nomes)) ocupadas[h] = nomes = new HashSet<string>();
                        nomes.Add(quadra);
                    }

                    horario = h;
                    quando = h;
                }

                jogos.Add(new JogoQueVem(cadeia.Categoria, rodada.Fase, i + 1, quando,
                    rodada.Confrontos[i].Lado1, rodada.Confrontos[i].Lado2, quadra, cadeia.CategoriaId));

                if (quando is DateTime marcado)
                {
                    emitidos.Add((posto, marcado));
                    if (ultimoDaRodada == null || marcado > ultimoDaRodada) ultimoDaRodada = marcado;
                }
            }

            abertura[escolha] = AbrirRodada(ultimoDaRodada ?? horario, grade);
            proxima[escolha]++;
        }

        return jogos;
    }

    // Quando a rodada abre: uma rodada de folga depois do fim da anterior. A regra mora em
    // GradeDeJogos.AberturaDaProximaFase, que é a mesma usada na grade de verdade — projeção
    // e grade dizendo horas diferentes seria pior que não projetar.
    private static DateTime? AbrirRodada(DateTime? ultimoDaFaseAnterior, ConfiguracaoDaGrade grade) =>
        ultimoDaFaseAnterior is DateTime ultimo
            ? GradeDeJogos.AberturaDaProximaFase(ultimo, grade.UltimoInicioDoDia,
                                                 grade.AberturaDiasSeguintes, grade.DuracaoMinutos)
            : null;

    // Primeiro cruza com último — a regra de ChaveamentoMataMata.ParearVencedores, aplicada
    // aqui sobre LADOS em vez de ids, porque aqui ainda não há vencedor nenhum.
    private static List<(Lado Lado1, Lado Lado2)> Parear(IReadOnlyList<Lado> lados)
    {
        var pares = new List<(Lado, Lado)>(lados.Count / 2);
        for (int i = 0; i < lados.Count / 2; i++)
            pares.Add((lados[i], lados[lados.Count - 1 - i]));
        return pares;
    }

    private static string? FaseMaisAdiantada(IReadOnlyList<PartidaDaChave> partidas)
    {
        // "Mais adiantada" pela ordem do próprio motor, não por nome nem por Id: seguir a
        // corrente de ProximaFase a partir de cada fase presente diz qual é a última.
        var fases = partidas.Select(p => p.Fase).Distinct().ToHashSet();

        return fases.FirstOrDefault(f =>
        {
            var proxima = ChaveamentoMataMata.ProximaFase(f);
            return proxima == null || !fases.Contains(proxima);
        });
    }

    private static DateTime? UltimoHorario(IReadOnlyList<PartidaDaChave> partidas) =>
        partidas.Where(p => p.Horario != null).Select(p => p.Horario!.Value)
            .DefaultIfEmpty()
            .Max() is { } m && m != default ? m : null;
}
