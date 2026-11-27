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
    // ⚠️ `NumeroNaFase` ANDA JUNTO desde 13/09/2026: sem ele a projeção voltaria a numerar a
    // fase pela ordem de Id, e um jogo nascido fora de ordem seria citado pelo número errado
    // ("Vencedor Semifinal 1" apontando pra Semifinal 2). Nulo = deduz, como sempre foi.
    // `Vencedor`: o nome de quem venceu, quando o jogo já acabou — é ele que ocupa a vaga
    // "Vencedor <fase> <n>" na prévia da rodada seguinte (VencedorJaApareceNaPreviaTests).
    public record PartidaDaChave(int Id, string Fase, string Dupla1, string Dupla2, DateTime? Horario,
                                 int? NumeroNaFase = null, string? Vencedor = null);

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
    // `OrdemNoHorario` é a posição dentro do horário que o organizador gravou pra esta prévia
    // (Models/ReservaDeHorario.OrdemNoHorario). Nula = automático. Quem preenche é quem lê as
    // reservas (TorneiosController.ProjetarProximasFasesAsync), e não o motor daqui: a projeção
    // decide QUANDO o jogo cai, não em que posição da linha o organizador quer vê-lo.
    //
    // `ClubeId` é ONDE o jogo previsto vai ser — a mesma pergunta que `Partida.ClubeId` responde
    // pro jogo real, e pela mesma régua (Services/OrdemDeLiberacao.CarimbarOClube): o clube é o da
    // quadra escolhida e, sem quadra escolhida, o do torneio.
    //
    // 🗣️ Felipe, num print do quadro do 2ª Etapa ER PADEL TOUR: *"quartas de final ta sem clube"*.
    // A hora daquelas quartas foi digitada na mão, e hora digitada não traz quadra
    // (TorneiosController.DefinirHorario) — sem este campo o jogo previsto ficava sem NADA que
    // dissesse o lugar, no torneio em que o clube é a única resposta que existe (o Er é "por
    // ordem": a quadra é apagada de propósito e quem chama é o balcão).
    public record JogoQueVem(string Categoria, string Fase, int Numero, DateTime? Horario,
                             Lado Lado1, Lado Lado2, string? Quadra = null, int? CategoriaId = null,
                             int? OrdemNoHorario = null, int? ClubeId = null)
    {
        // "Quartas de Final 2" — o rótulo que a tela mostra e que os lados citam.
        // A FINAL não numera: é um jogo só, e "Final 1" faria pensar que existe uma Final 2.
        public string FaseNumerada => Fase == "Final" ? Fase : $"{Fase} {Numero}";
    }

    // ================= ESTRUTURA: quem joga com quem, ainda sem hora =================

    // `PrimeiroNumero` é o número do primeiro confronto da lista dentro da fase — 1 quando a
    // rodada inteira ainda está por vir, e maior quando os primeiros jogos dela JÁ NASCERAM
    // (avanço parcial, 11/09/2026: a Semifinal 1 é real e só a 2 é projeção). Sem ele a
    // Semifinal 2 seria emitida como "Semifinal 1" e a reserva de horário do organizador, que
    // é guardada por (categoria, fase, número), cairia no jogo errado.
    // ⚠️ OS NÚMEROS, E NÃO "O PRIMEIRO + CONTÍGUOS" (13/09/2026). Desde que o jogo nasce assim
    // que as duas vagas dele têm dono, a parte REAL de uma fase deixou de ser um prefixo: pode
    // ser a Semifinal 2 sozinha, com a 1 ainda por vir. Um "primeiro número" não descreve mais
    // o que sobrou pra projetar, e a reserva de horário do organizador — guardada por
    // (categoria, fase, número) — cairia no jogo errado.
    //
    // Nulo = 1..N contíguo, que é o caso de toda fase inteiramente futura.
    // `UltimoIrmaoReal`: o horário do jogo mais tarde DESTA fase que já nasceu (a Semifinal 1 real
    // ao lado da Semifinal 2 prevista). Não é piso desta rodada — irmão não é fase anterior —, mas
    // é piso da SEGUINTE: a Final espera as duas semis. Ver `Montar` (14/11/2026).
    public record RodadaQueVem(string Fase, IReadOnlyList<(Lado Lado1, Lado Lado2)> Confrontos,
                               IReadOnlyList<int>? NumerosExplicitos = null, DateTime? UltimoIrmaoReal = null)
    {
        public IReadOnlyList<int> Numeros =>
            NumerosExplicitos ?? Enumerable.Range(1, Confrontos.Count).ToList();
    }

    // O caminho de UMA categoria até a final. `DepoisDe` é o último jogo que já tem hora e
    // que alimenta a primeira rodada projetada — dele sai a folga de abertura.
    // `FaseDoDepoisDe` é a fase do jogo que `DepoisDe` aponta — é ela que diz QUANTO ele dura
    // (Services/DuracaoDaPartida). Nula = fim dos grupos, que dura uma vaga.
    public record CadeiaDeFases(string Categoria, DateTime? DepoisDe, IReadOnlyList<RodadaQueVem> Rodadas,
                                int? CategoriaId = null, string? FaseDoDepoisDe = null)
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
        int? categoriaId = null,
        bool invertida = false)
    {
        if (partidasDeMataMata.Count == 0) return CadeiaDeFases.Vazia;

        // ⚠️ PARTE DA PRIMEIRA FASE, E NÃO DA MAIS ADIANTADA (11/09/2026). Com o avanço parcial
        // (Services/AvancoDaChave) a fase mais adiantada pode ter UM jogo só — a Semifinal 1
        // nascida assim que a Quartas 1 terminou, com a 2 ainda em quadra. Partindo dela, a
        // projeção teria um lado só, não encadearia nada, e a Final sumiria da tela justamente
        // quando metade do quadro já sabe o caminho. Partindo da primeira e PULANDO o que já é
        // real (ver Encadear), a conta é a mesma de sempre quando as fases estão cheias.
        var primeiraFase = FaseMenosAdiantada(partidasDeMataMata);
        if (primeiraFase == null) return CadeiaDeFases.Vazia;

        // A MESMA ordem do avanço de verdade — a do quadro, que desde 13/09/2026 sai do número
        // gravado quando existe, e não da ordem de criação.
        //
        // A categoria é uma só aqui (o chamador agrupa antes), então o 0 fixo serve de chave
        // de agrupamento: quem separa é a FASE.
        var numeroNaFase = ReservasDeHorario.NumeroNaFase(
            partidasDeMataMata.Select(p => (p.Id, 0, p.Fase, p.NumeroNaFase)));

        var daFase = partidasDeMataMata
            .Where(p => p.Fase == primeiraFase)
            .OrderBy(p => numeroNaFase.TryGetValue(p.Id, out var n) ? n : int.MaxValue)
            .ThenBy(p => p.Id)
            .ToList();

        // Cada jogo da primeira fase entrega um vencedor — citado pelo NÚMERO dele naquela
        // fase; cada bye entrega a própria dupla, que já tem nome.
        // QUEM JÁ VENCEU OCUPA A VAGA (11/11/2026) — 🗣️ Felipe: *"a dupla que já definiu seu
        // jogo já pode ir pro chaveamento prévio […] e assim com as demais fases do mata-mata"*.
        var vencedores = partidasDeMataMata
            .Where(p => p.Vencedor != null && numeroNaFase.ContainsKey(p.Id))
            .ToDictionary(p => (p.Fase, numeroNaFase[p.Id]), p => p.Vencedor!);

        // Na DTN já nascida quem segue é o perdedor (o nome do vencedor não serve de nada ali).
        var lados = daFase
            .Select((_, i) => invertida ? LadoDoPerdedor(primeiraFase, i + 1) : LadoDoVencedor(primeiraFase, i + 1, vencedores))
            .Concat(byes.Select(b => new Lado(b)))
            .ToList();

        // Quantos jogos cada fase JÁ TEM de verdade: a projeção não repete o que existe.
        var jaSaoReais = partidasDeMataMata
            .GroupBy(p => p.Fase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => numeroNaFase.TryGetValue(p.Id, out var n) ? n : 0)
                      .Where(n => n > 0)
                      .ToHashSet());

        // ⚠️ O PISO É A FASE ANTERIOR, NÃO O JOGO REAL MAIS TARDE (14/11/2026). 🗣️ Felipe, no THE
        // LAST DANCE: *"é as duas semi da mesma categoria"* — com a Semifinal 1 já real, a Semifinal
        // 2 prevista abria depois DELA, e a troca ⇄ entre as duas era recusada culpando a fase
        // anterior. O irmão real sai do piso desta rodada e entra no da seguinte
        // (ReservaNaFaseQueNasceuPelaMetadeTests).
        var rodadas = Encadear(lados, jaSaoReais, invertida ? null : vencedores, invertida);
        var faseQueSegue = rodadas.FirstOrDefault()?.Fase;
        var irmaos = partidasDeMataMata.Where(p => p.Fase == faseQueSegue).ToList();
        if (irmaos.Count > 0)
            rodadas[0] = rodadas[0] with { UltimoIrmaoReal = UltimoHorario(irmaos) };

        var anteriores = partidasDeMataMata.Where(p => p.Fase != faseQueSegue).ToList();
        var ultimoAnterior = UltimoHorario(anteriores);
        return new CadeiaDeFases(categoria, ultimoAnterior, rodadas, categoriaId,
            FaseDoDepoisDe: anteriores.FirstOrDefault(p => p.Horario == ultimoAnterior)?.Fase);
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
    // ---- Entrada 3: a CHAVE DTN que ainda vai nascer (27/11/2026, TRETA.md) ----
    //
    // 🗣️ Rafael (Treta): "só não tem os jogos da DTN previstos". A DTN nasce quando os grupos do
    // módulo fecham (RoboDoChaveamento.Dtn) — antes disso não se sabe QUEM vai, mas se sabe
    // QUANTOS e o formato: o último de cada grupo, chave direta do pior pro melhor, quem PERDE
    // avança. É isso que a prévia promete, com hora na mesma grade das outras categorias.
    //
    // Rótulos: com 2 vagas (2 grupos) cada uma é a colocação do grupo ("4º do Grupo A"); com
    // mais, a ordem entre os últimos só sai da campanha — "Melhor último", "Pior último".
    // A cadeia não tem CategoriaId: a categoria DTN ainda não existe, e reserva de horário presa
    // ao módulo cairia na Final do Principal (mesma fase, mesmo número).
    public static CadeiaDeFases DaDtn(IReadOnlyList<string> grupos, IReadOnlyList<int> duplasPorGrupo,
        int classificadosPorGrupo, int duplasNoDtnPorGrupo, DateTime? fimDosGrupos, string categoria)
    {
        // As vagas, grupo a grupo: os últimos `duplasNoDtnPorGrupo`, sem invadir o Principal.
        var vagasPorGrupo = new List<Lado>();
        for (int g = 0; g < grupos.Count; g++)
        {
            int tamanho = g < duplasPorGrupo.Count ? duplasPorGrupo[g] : 0;
            for (int pos = Math.Max(classificadosPorGrupo, tamanho - duplasNoDtnPorGrupo) + 1; pos <= tamanho; pos++)
                vagasPorGrupo.Add(new Lado($"{pos}º do {grupos[g]}", DeQualGrupo: grupos[g]));
        }
        int n = vagasPorGrupo.Count;
        if (n < 2) return CadeiaDeFases.Vazia;

        // Do pior pro melhor, como o robô ordena — com 2 vagas a ordem não muda o jogo.
        var doPiorProMelhor = n == 2
            ? vagasPorGrupo
            : Enumerable.Range(0, n).Select(k => new Lado(
                  k == 0 ? "Pior último" : k == n - 1 ? "Melhor último" : $"{n - k}º melhor último",
                  DeQualGrupo: grupos.FirstOrDefault())).ToList();

        var abertura = ChaveamentoMataMata.MontarChaveDireta(Enumerable.Range(0, n).ToList());
        var primeira = new RodadaQueVem(abertura.Fase,
            abertura.Confrontos.Select(c => (doPiorProMelhor[c.Dupla1Id], doPiorProMelhor[c.Dupla2Id])).ToList());

        var rodadas = new List<RodadaQueVem> { primeira };
        if (abertura.Fase != "Final")
        {
            var lados = abertura.Confrontos
                .Select((_, i) => LadoDoPerdedor(abertura.Fase, i + 1))
                .Concat(abertura.Byes.Select(id => doPiorProMelhor[id]))
                .ToList();
            rodadas.AddRange(Encadear(lados, invertida: true));
        }
        return new CadeiaDeFases(categoria, fimDosGrupos, rodadas);
    }

    // Na chave invertida (DTN), quem segue é quem PERDEU.
    private static Lado LadoDoPerdedor(string fase, int numero) => new($"Perdedor {fase} {numero}", fase, numero);

    public static CadeiaDeFases MontarDosGrupos(
        IReadOnlyList<string> grupos,
        int classificadosPorGrupo,
        DateTime? fimDosGrupos,
        string categoria = "",
        int? categoriaId = null,
        // Quantas duplas tem cada grupo, na MESMA ordem de `grupos`: é o que faz a prévia dar o
        // bye a quem o robô dá (ver ChaveProjetada). Nulo = campanha zerada, como era.
        IReadOnlyList<int>? duplasPorGrupo = null,
        // O cruzamento desenhado à mão (Models/Categoria.CruzamentoDoMataMata). Nulo = o motor
        // decide, como sempre. Passar a prometer o cruzamento DESENHADO é o que impede a prévia
        // de anunciar um confronto que o robô não vai criar na categoria que tem desenho.
        string? cruzamentoDesenhado = null,
        // QUAIS jogos da ABERTURA (pelo número na fase) já nasceram de verdade. Desde o avanço
        // parcial dos grupos (11/09/2026) ela nasce jogo a jogo: os que já existem estão na lista
        // de jogos, com nome e sobrenome, e a prévia continua prometendo só o que falta — sem
        // repetir nenhum. Eram QUANTOS até 14/11/2026, quando a abertura passou a nascer fora de
        // ordem (AberturaNasceForaDeOrdemTests): o jogo 2 real com o 1 esperando.
        IReadOnlyCollection<int>? numerosJaCriadosNaAbertura = null,
        // Os nomes que já não são promessa: o grupo fechou e a colocação virou dupla
        // (Services/ClassificadosJaConhecidos).
        IReadOnlyDictionary<(string Grupo, int Posicao), string>? jaConhecidos = null,
        // Quem já venceu um jogo de mata-mata que já existe (fase, número) → nome.
        IReadOnlyDictionary<(string Fase, int Numero), string>? vencedores = null)
    {
        var (fase, confrontos, byes) = ChaveProjetada.Montar(
            grupos, classificadosPorGrupo, duplasPorGrupo, cruzamentoDesenhado, jaConhecidos);
        if (confrontos.Count == 0) return CadeiaDeFases.Vazia;

        var jaReais = numerosJaCriadosNaAbertura ?? Array.Empty<int>();
        var faltam = confrontos
            .Select((c, i) => (Confronto: c, Numero: i + 1))
            .Where(x => !jaReais.Contains(x.Numero))
            .ToList();
        var primeira = new RodadaQueVem(fase,
            faltam.Select(x => (VagaDeGrupo(x.Confronto.Lado1), VagaDeGrupo(x.Confronto.Lado2))).ToList(),
            faltam.Select(x => x.Numero).ToList());

        var proximos = confrontos
            .Select((_, i) => LadoDoVencedor(fase, i + 1, vencedores))
            // Quem folga a primeira rodada entra DEPOIS dos vencedores, na mesma ordem do
            // avanço de verdade — é o que faz cada vencedor cruzar com uma vaga que passou
            // direto. Aqui o bye ainda não tem nome: é a colocação ("2º do Grupo C").
            .Concat(byes.Select(VagaDeGrupo))
            .ToList();

        var rodadas = new List<RodadaQueVem>();
        if (primeira.Confrontos.Count > 0) rodadas.Add(primeira);
        rodadas.AddRange(Encadear(proximos, vencedores: vencedores));

        return new CadeiaDeFases(categoria, fimDosGrupos, rodadas, categoriaId);
    }

    // A colocação de grupo vira lado guardando o GRUPO: é por ele que "só os meus jogos"
    // reconhece a vaga que pode ser do jogador ("2º do Grupo A" é dele; "1º do Grupo E" não).
    private static Lado VagaDeGrupo(ChaveProjetada.Vaga vaga) =>
        new(vaga.Rotulo, DeQualGrupo: vaga.Grupo);

    // O encadeamento, rodada a rodada, até a final.
    //
    // `jaSaoReais` diz quantos jogos de cada fase JÁ EXISTEM no banco: eles saem da projeção
    // (estão na lista de jogos de verdade), mas continuam na CORRENTE — é o vencedor deles que
    // ocupa a vaga da rodada seguinte. Sem essa separação, a fase criada pela metade pelo
    // avanço parcial apareceria duas vezes na tela, ou sumiria inteira.
    // A vaga "Vencedor <fase> <n>": o nome de quem venceu, se o jogo já acabou; senão a procedência.
    // A procedência (fase, número) fica nos dois casos — é ela que vira o link pro jogo de origem.
    private static Lado LadoDoVencedor(string fase, int numero,
        IReadOnlyDictionary<(string Fase, int Numero), string>? vencedores) =>
        new(vencedores != null && vencedores.TryGetValue((fase, numero), out var nome) ? nome : $"Vencedor {fase} {numero}",
            fase, numero);

    private static List<RodadaQueVem> Encadear(
        List<Lado> lados, IReadOnlyDictionary<string, HashSet<int>>? jaSaoReais = null,
        IReadOnlyDictionary<(string Fase, int Numero), string>? vencedores = null,
        bool invertida = false)
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

            // ⚠️ POR NÚMERO, E NÃO POR CONTAGEM. Com o avanço fora de ordem, os jogos já reais
            // de uma fase são um CONJUNTO qualquer de números — a Semifinal 2 pode existir com
            // a 1 ainda por vir. `Skip(reais)` descartaria a 1 e emitiria a 2, que já existe:
            // a tela mostraria o mesmo jogo duas vezes e esconderia o que falta.
            var reais = jaSaoReais?.GetValueOrDefault(fase) ?? new HashSet<int>();
            var faltam = confrontos
                .Select((c, i) => (Confronto: c, Numero: i + 1))
                .Where(x => !reais.Contains(x.Numero))
                .ToList();

            if (faltam.Count > 0)
                rodadas.Add(new RodadaQueVem(fase,
                    faltam.Select(x => x.Confronto).ToList(),
                    faltam.Select(x => x.Numero).ToList()));

            lados = confrontos.Select((_, i) => invertida ? LadoDoPerdedor(fase, i + 1) : LadoDoVencedor(fase, i + 1, vencedores)).ToList();

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
        SedesDoTorneio? Sedes = null,
        // O calendário do torneio, com a faixa própria de cada dia (Services/HorarioDoDia).
        // Nulo = só os dois campos acima, como sempre foi.
        HorarioDoDia.Calendario? Calendario = null,
        Func<string?, int>? MinutosDaFase = null);

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
    //
    // ⚠️ A ORDEM ENTRE CATEGORIAS CEDE QUANDO SEGUI-LA DEIXA JOGO SEM HORA (11/11/2026). Ela é
    // preferência — "entre categorias são pessoas diferentes" —, e o fim do torneio é regra
    // (🗣️ *"respeitar as datas e horários pré-estabelecidos"*). Um jogo de grupo de OUTRA categoria
    // empurrado pro último dia (dupla impedida) segurava o mata-mata de quem já tinha acabado, com
    // horário livre sobrando antes. Então: tenta com a ordem; se algum jogo ficou sem hora por
    // causa do teto do torneio, tenta sem a barreira entre categorias e fica com a que marcou mais.
    // O piso DA PRÓPRIA categoria (esperar o resultado) nunca cede. (GradeNuncaPassaDoFimDoTorneioTests)
    public static List<JogoQueVem> Agendar(
        IReadOnlyList<CadeiaDeFases> cadeias,
        ConfiguracaoDaGrade grade,
        IReadOnlyList<VagaOcupada>? jaMarcados = null,
        IReadOnlyList<HorarioReservado>? reservas = null)
    {
        var comOrdem = Agendar(cadeias, grade, jaMarcados, reservas, ordemEntreCategorias: true);
        if (grade.Calendario?.UltimoInicio == null || comOrdem.All(j => j.Horario != null)) return comOrdem;

        var semOrdem = Agendar(cadeias, grade, jaMarcados, reservas, ordemEntreCategorias: false);
        return semOrdem.Count(j => j.Horario != null) > comOrdem.Count(j => j.Horario != null) ? semOrdem : comOrdem;
    }

    private static List<JogoQueVem> Agendar(
        IReadOnlyList<CadeiaDeFases> cadeias,
        ConfiguracaoDaGrade grade,
        IReadOnlyList<VagaOcupada>? jaMarcados,
        IReadOnlyList<HorarioReservado>? reservas,
        bool ordemEntreCategorias)
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
            var cadeiaQueEmite = vivas.FirstOrDefault(c => c.CategoriaId == r.CategoriaId
                && c.Rodadas.Any(rod => rod.Fase == r.Fase && rod.Numeros.Contains(r.Numero)));
            if (cadeiaQueEmite == null) continue;

            // ⚠️ RESERVA QUE FICOU PRA TRÁS NÃO TOMA VAGA (revisão adversarial, 10/09/2026): a fase
            // anterior foi remarcada pra depois dela e ela não vai valer — mas o slot ficava
            // pré-reservado até a rodada dela ser emitida, e nesse meio tempo as outras cadeias
            // passavam pelo horário e eram empurradas por um jogo que nunca ia estar ali. Nenhuma
            // rodada da cadeia abre antes de `AbrirRodada(DepoisDe)`, então dá pra saber já aqui.
            // (O `Vale` na emissão continua: a rodada seguinte pode abrir ainda mais tarde.)
            if (!ReservasDeHorario.Vale(r.Horario, AbrirRodada(cadeiaQueEmite.DepoisDe, grade,
                    MinutosDaFaseNaGrade(grade, cadeiaQueEmite.FaseDoDepoisDe)))) continue;

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
        var abertura = vivas.Select(c => AbrirRodada(c.DepoisDe, grade, MinutosDaFaseNaGrade(grade, c.FaseDoDepoisDe))).ToArray();

        var jogos = new List<JogoQueVem>();

        // A BARREIRA DE POSTO: o posto que está sendo emitido agora e o fim do posto ANTERIOR.
        // Como a escolha abaixo pega sempre o menor posto disponível, quando um posto novo começa
        // todos os anteriores já saíram — os emitidos aqui ficam em `emitidos`, e os reais vêm de
        // `jaMarcados`; a barreira é o fim do BLOCO dos dois juntos (OrdemDasFases.FimDoBloco).
        int postoEmitido = int.MinValue;
        DateTime? fimDoPostoAnterior = null;
        var emitidos = new List<(int Posto, DateTime Horario)>();

        DateTime Seguinte(DateTime h) =>
            grade.Calendario?.DepoisDe(h, grade.DuracaoMinutos)
            ?? GradeDeJogos.DepoisDe(h, grade.UltimoInicioDoDia, grade.AberturaDiasSeguintes, grade.DuracaoMinutos);

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
        // ⚠️ A DURAÇÃO POR FASE (19/11/2026, Services/DuracaoDaPartida): o jogo mais longo que a
        // vaga segura as vagas seguintes da quadra dele, e a fase seguinte espera o FIM dele.
        // Sem `MinutosDaFase`, todo jogo dura uma vaga — a prévia de sempre.
        int MinutosDe(string? fase) => MinutosDaFaseNaGrade(grade, fase);
        int Vaga() => grade.DuracaoMinutos > 0 ? grade.DuracaoMinutos : 50;
        int VagasDe(string? fase) =>
            Math.Max(1, ((MinutosDe(fase) > 0 ? MinutosDe(fase) : Vaga()) + Vaga() - 1) / Vaga());

        void Tomar(DateTime h, string? quadra)
        {
            lotacao[h] = lotacao.GetValueOrDefault(h) + 1;
            if (string.IsNullOrEmpty(quadra)) return;
            if (!ocupadas.TryGetValue(h, out var nomes)) ocupadas[h] = nomes = new HashSet<string>();
            nomes.Add(quadra);
        }

        // As vagas que o jogo segura DEPOIS da primeira. A vaga é a da cadência da grade
        // (`Seguinte`), a mesma que a prévia já anda.
        void TomarAsSeguintes(DateTime h, string? quadra, string? fase)
        {
            var vaga = h;
            for (int m = 1; m < VagasDe(fase); m++)
            {
                vaga = Seguinte(vaga);
                Tomar(vaga, quadra);
            }
        }

        foreach (var vaga in jaMarcados ?? Array.Empty<VagaOcupada>())
            TomarAsSeguintes(vaga.Horario, vaga.Quadra, vaga.Fase);

        int CapacidadeEm(DateTime h) =>
            grade.Sedes?.QuadrasAbertasEm(h) is int abertas ? abertas : capacidade;

        // O CLUBE DO JOGO PREVISTO — a MESMA linha do carimbo dos jogos reais
        // (OrdemDeLiberacao.CarimbarOClube): o clube é o da quadra escolhida e, sem quadra que
        // responda, o do torneio. Duas contas diriam prédios diferentes pro mesmo jogo, e é o
        // clube que decide pra que lado da cidade a pessoa dirige.
        //
        // ⚠️ SEM SLOT NENHUM (nem hora nem quadra) NÃO HÁ CLUBE, e a régua é a mesma de lá: o
        // carimbo pula o jogo que ninguém colocou em lugar nenhum. Escrever o principal pra ele
        // seria chute — é o aviso de SedesDoTorneio.ClubeQueACategoriaDetermina.
        int? ClubeDoSlot(DateTime? quando, string? quadra) =>
            quando == null && string.IsNullOrWhiteSpace(quadra)
                ? null
                : grade.Sedes?.ClubeDaQuadra(quadra) ?? grade.Sedes?.ClubePrincipalId;

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
            if (ordemEntreCategorias
                && horario is DateTime abre && fimDoPostoAnterior is DateTime barreira && barreira > abre)
                horario = barreira;

            // O jogo mais tarde da rodada, reservado ou não: é dele que a rodada seguinte abre.
            // `horario` continua sendo o cursor dos jogos SEM reserva — reservar a Semifinal 1
            // pras 21h não arrasta a Semifinal 2 junto.
            DateTime? ultimoDaRodada = rodada.UltimoIrmaoReal;

            for (int i = 0; i < rodada.Confrontos.Count; i++)
            {
                string? quadra = null;
                DateTime? quando = null;

                // A reserva do organizador, se ele fez uma pra este jogo e ela ainda é possível.
                // A que deixou de ser (o torneio atrasou e a fase anterior passou dela) volta pra
                // grade como qualquer jogo — e a tela mostra a hora possível, não a prometida.
                if (cadeia.CategoriaId is int categoriaDaReserva
                    && reservadas.Remove((categoriaDaReserva, rodada.Fase, rodada.Numeros[i]), out var reserva))
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

                    // ⚠️ NUNCA DEPOIS DO ÚLTIMO DIA (11/11/2026). 🗣️ Felipe, com as quartas do Chakra
                    // previstas pra segunda: *"respeitar as datas e horários pré-estabelecidos"*. O
                    // jogo que não cabe fica SEM hora (a Mesa chama quando houver quadra), em vez de
                    // virar promessa num dia em que o torneio não existe.
                    // (GradeNuncaPassaDoFimDoTorneioTests)
                    if (grade.Calendario?.UltimoInicio is DateTime fimDoTorneio && h > fimDoTorneio)
                    {
                        horario = h;
                        jogos.Add(new JogoQueVem(cadeia.Categoria, rodada.Fase, rodada.Numeros[i], null,
                            rodada.Confrontos[i].Lado1, rodada.Confrontos[i].Lado2, null, cadeia.CategoriaId,
                            ClubeId: ClubeDoSlot(null, null)));
                        continue;
                    }

                    quadra = grade.Quadras.FirstOrDefault(q => Serve(q, h, permitidas));

                    Tomar(h, quadra);
                    TomarAsSeguintes(h, quadra, rodada.Fase);

                    horario = h;
                    quando = h;
                }

                jogos.Add(new JogoQueVem(cadeia.Categoria, rodada.Fase, rodada.Numeros[i], quando,
                    rodada.Confrontos[i].Lado1, rodada.Confrontos[i].Lado2, quadra, cadeia.CategoriaId,
                    ClubeId: ClubeDoSlot(quando, quadra)));

                if (quando is DateTime marcado)
                {
                    emitidos.Add((posto, marcado));
                    if (ultimoDaRodada == null || marcado > ultimoDaRodada) ultimoDaRodada = marcado;
                }
            }

            abertura[escolha] = AbrirRodada(ultimoDaRodada ?? horario, grade, MinutosDe(rodada.Fase));
            proxima[escolha]++;
        }

        return jogos;
    }

    // Quando a rodada abre: uma rodada de folga depois do fim da anterior. A regra mora em
    // GradeDeJogos.AberturaDaProximaFase, que é a mesma usada na grade de verdade — projeção
    // e grade dizendo horas diferentes seria pior que não projetar.
    private static int MinutosDaFaseNaGrade(ConfiguracaoDaGrade grade, string? fase) =>
        grade.MinutosDaFase?.Invoke(fase) is int m && m > 0 ? m : grade.DuracaoMinutos;   // cru: quem recebe já normaliza o 0

    // `minutos` é a duração do jogo da fase anterior (o DepoisDe da cadeia é o fim dos grupos,
    // que dura uma vaga). Ver Services/DuracaoDaPartida.
    private static DateTime? AbrirRodada(DateTime? ultimoDaFaseAnterior, ConfiguracaoDaGrade grade, int? minutos = null) =>
        ultimoDaFaseAnterior is DateTime ultimo
            ? grade.Calendario?.DepoisDe(ultimo, minutos ?? grade.DuracaoMinutos)
              ?? GradeDeJogos.AberturaDaProximaFase(ultimo, grade.UltimoInicioDoDia,
                                                    grade.AberturaDiasSeguintes, minutos ?? grade.DuracaoMinutos)
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

    // A PRIMEIRA fase de mata-mata da categoria — a que tem os byes e de onde a corrente parte.
    // Pela ordem do próprio motor, não por nome nem por Id: a fase que nenhuma outra presente
    // antecede é a de abertura (com bye o quadro começa em "Quartas de Final" sem nunca ter
    // tido Oitavas, então contar fases não serve).
    private static string? FaseMenosAdiantada(IReadOnlyList<PartidaDaChave> partidas)
    {
        var fases = partidas.Select(p => p.Fase).Distinct().ToHashSet();

        return fases.FirstOrDefault(f => !fases.Any(
            outra => ChaveamentoMataMata.ProximaFase(outra) == f));
    }

    private static DateTime? UltimoHorario(IReadOnlyList<PartidaDaChave> partidas) =>
        partidas.Where(p => p.Horario != null).Select(p => p.Horario!.Value)
            .DefaultIfEmpty()
            .Max() is { } m && m != default ? m : null;
}
