using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O TORNEIO EM MAIS DE UM CLUBE (21/08/2026).
//
// Nasceu do Dez E Batata, que divide o torneio em dois clubes e põe cada categoria inteira num
// deles — é o jeito de ninguém passar o fim de semana indo e voltando. Até aqui o Padelizou só
// sabia contar UM lugar: `Torneio.ClubeId` é obrigatório e único, e `Quadra` pertencia ao
// torneio, não a um clube. A quadra não sabia onde ficava.
//
// ── A FONTE DA VERDADE É A QUADRA ────────────────────────────────────────────────────────
// Não existe tabela de "sedes do torneio". A lista de clubes é o DISTINCT de `Quadra.ClubeId`,
// e isso é escolha, não preguiça: uma sede cadastrada à parte poderia existir sem quadra
// nenhuma, e aí o torneio teria DUAS respostas pra "onde eu jogo?" — a lista de sedes e a lista
// de quadras — livres pra discordar. Este projeto já pagou caro por segunda fonte de verdade
// (ver o comentário de Models/TimeSede sobre o `Time.DonoId`).
//
// Quadra com `ClubeId` nulo é do clube do torneio. É o que TODA quadra que já existia é, sem
// precisar de conversão nenhuma no banco.
//
// ── DUAS COISAS DIFERENTES, E SÓ UMA É TRAVA ─────────────────────────────────────────────
// 1. ONDE A CATEGORIA JOGA (`Categoria.ClubeId`) é trava DURA: a categoria não recebe quadra de
//    outro clube nem com o horário lotado. Não podia pegar carona na quadra PREFERIDA
//    (Services/PreferenciaDeQuadra), que cede no degrau 3 — preferir a central e não conseguir
//    custa uma quadra pior; "preferir" o clube certo e não conseguir manda o jogador pro outro
//    lado da cidade no meio do torneio.
// 2. A FOLGA PRA TROCAR DE CLUBE (`Torneio.MinutosParaTrocarDeClube`) é MOLE, e isso é decisão
//    do Felipe: a prioridade declarada do torneio é "nenhuma quadra fica sem jogo até o final".
//    Quem aplica a folga é Services/GradeDeJogos.Encaixar, e ela cede quando respeitá-la
//    deixaria uma quadra vazia.
//
// ── O QUE ACONTECE NO TORNEIO DE UM CLUBE SÓ ─────────────────────────────────────────────
// `MaisDeUmClube` é false, `QuadrasDe` devolve null (sem restrição) e a folga é zero. Ou seja:
// exatamente o comportamento de antes desta classe existir, sem um `if` a mais no caminho
// quente. É o caso de todos os torneios do Padelizou até hoje.
public sealed class SedesDoTorneio
{
    // O torneio de um clube só, e o valor que os chamadores usam quando não têm o que carregar.
    public static readonly SedesDoTorneio Nenhuma = new(
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<int, string[]>(),
        new Dictionary<int, int>(),
        new Dictionary<int, string>(),
        new Dictionary<string, (DateTime?, DateTime?)>(StringComparer.OrdinalIgnoreCase),
        new HashSet<int>(),
        clubePrincipal: 0,
        TimeSpan.Zero,
        maisDeUmClube: false,
        evitarDoisJogosNaSedeExtra: false);

    private readonly Dictionary<string, int> _clubePorQuadra;
    private readonly Dictionary<int, string[]> _quadrasPorCategoria;
    private readonly Dictionary<int, int> _clubePorCategoria;
    private readonly Dictionary<int, string> _nomeDoClube;
    private readonly Dictionary<string, (DateTime? De, DateTime? Ate)> _janelaPorQuadra;
    private readonly HashSet<int> _categoriasPresasNaSedePrincipal;
    private readonly int _clubePrincipal;

    private SedesDoTorneio(Dictionary<string, int> clubePorQuadra,
        Dictionary<int, string[]> quadrasPorCategoria,
        Dictionary<int, int> clubePorCategoria,
        Dictionary<int, string> nomeDoClube,
        Dictionary<string, (DateTime? De, DateTime? Ate)> janelaPorQuadra,
        HashSet<int> categoriasPresasNaSedePrincipal,
        int clubePrincipal,
        TimeSpan folga, bool maisDeUmClube, bool evitarDoisJogosNaSedeExtra)
    {
        EvitarDoisJogosNaSedeExtra = evitarDoisJogosNaSedeExtra;
        _clubePorQuadra = clubePorQuadra;
        _quadrasPorCategoria = quadrasPorCategoria;
        _clubePorCategoria = clubePorCategoria;
        _nomeDoClube = nomeDoClube;
        _janelaPorQuadra = janelaPorQuadra;
        _categoriasPresasNaSedePrincipal = categoriasPresasNaSedePrincipal;
        _clubePrincipal = clubePrincipal;
        FolgaParaTrocarDeClube = folga;
        MaisDeUmClube = maisDeUmClube;
    }

    // ── O LOCAL EXTERNO ALUGADO POR ALGUMAS HORAS (08/09/2026) ───────────────────────────
    // 🗣️ Felipe: "ele terá q locar um local externo ao dele [...] e ai também vai ter q por
    // quantos jogos vão para la, ou quais horarios, quais categorias".
    //
    // "Quantos jogos" não virou campo: é `quadras × rodadas da janela`, ou seja uma CONTA que
    // a tela mostra. Dois campos pra mesma informação discordariam, e ninguém saberia qual
    // mandou. As outras duas viraram: a janela mora em `Quadra.DisponivelDe/Ate`, e "quais
    // categorias" em `Categoria.PodeJogarNaSedeExtra`.

    // Alguma quadra abre em ALGUM horário que a grade deste torneio ofereceria?
    //
    // ⚠️ A pergunta é feita nos horários que a grade DE FATO usaria (a cadência do torneio, dia a
    // dia, da abertura ao último início), e não "no dia inteiro": uma quadra aberta só às 3h da
    // manhã não serve pra nada e não pode inocentar a configuração.
    //
    // ⚠️ SEM `DataFim`, olha uma semana a partir do início. Torneio não dura mais que isso, e o
    // campo é OPCIONAL — pendurar o guarda nele deixaria justamente o torneio sem prazo à mercê
    // do defeito (foi o erro que eu já tinha cometido uma vez neste mesmo assunto).
    private static bool NenhumaQuadraAbreNoTorneio(
        IReadOnlyDictionary<string, (DateTime? De, DateTime? Ate)> janelas,
        IEnumerable<string> todasAsQuadras, Torneio torneio)
    {
        if (torneio.DataInicio is not DateTime inicio) return false;

        // Quadra sem janela já responde "aberta" em qualquer horário: se existe uma, a
        // configuração não é impossível e nem vale percorrer o relógio.
        if (todasAsQuadras.Any(q => !janelas.ContainsKey(q))) return false;

        int duracao = torneio.TempoPrevistoPartidaMinutos > 0 ? torneio.TempoPrevistoPartidaMinutos : 50;
        var ultimoDia = (torneio.DataFim?.Date ?? inicio.Date.AddDays(7));

        for (var dia = inicio.Date; dia <= ultimoDia; dia = dia.AddDays(1))
        {
            var abertura = dia == inicio.Date ? torneio.HoraInicioDoDia : torneio.HoraInicioDiasSeguintes;

            for (var hora = dia.Add(abertura); hora.TimeOfDay <= torneio.HoraFimDoDia && hora.Date == dia;
                 hora = hora.AddMinutes(duracao))
            {
                foreach (var janela in janelas.Values)
                {
                    if ((janela.De == null || hora >= janela.De) && (janela.Ate == null || hora <= janela.Ate))
                        return false;
                }
            }
        }

        return true;
    }

    // Esta quadra recebe jogo NESTE horário? Janela MEIO ABERTA ([De, Ate)), mesmo formato de
    // JanelasDeImpedimento — um jogo que COMEÇA às 14h já está fora de uma janela até 14h.
    //
    // ⚠️ Quadra desconhecida conta como ABERTA, mesma régua defensiva de `ClubeDaQuadra`:
    // `Partida.NomeQuadra` é texto solto, e uma quadra escrita direto no jogo não pode sumir
    // da grade por não estar no cadastro.
    public bool QuadraAberta(string? nomeQuadra, DateTime horario)
    {
        if (string.IsNullOrWhiteSpace(nomeQuadra)) return true;
        if (!_janelaPorQuadra.TryGetValue(nomeQuadra!.Trim(), out var janela)) return true;

        // ⚠️ O "ATÉ" É INCLUSIVO (09/09/2026), e isso é o oposto de JanelasDeImpedimento — de
        // propósito. A pergunta aqui é "dá pra COMEÇAR um jogo neste horário?", que é
        // exatamente a de `Torneio.HoraFimDoDia`, inclusiva desde sempre. O impedimento
        // responde outra coisa (um PERÍODO em que a pessoa não joga), e lá o fim de um turno é
        // o começo do outro — meio aberto é o certo pra ele e errado pra cá.
        //
        // 🗣️ Medido no combinado do Er: "no radar, 2 quadras — 08h, 08:50, 09:40, 10:30, 11:20,
        // 12:10. Vão ser 12 jogos". Com o fim exclusivo, o jogo das 12:10 caía fora da janela
        // que termina 12:10 e a tela prometia 10 — duas rodadas de quadra alugada sumiam da
        // conta, e o organizador alugava de menos.
        return (janela.De == null || horario >= janela.De)
            && (janela.Ate == null || horario <= janela.Ate);
    }

    // QUANTAS quadras cadastradas estão abertas neste horário. `null` quando NENHUMA quadra tem
    // janela — e aí quem pergunta não precisa fazer conta nenhuma, que é o caso de todo torneio
    // até 08/09/2026.
    //
    // Existe pro ORÇAMENTO DE VAGAS (Services/VagasDaGrade): cada rodada rende uma vaga por
    // quadra CADASTRADA, inclusive pelas fechadas, e sem descontar as mortas o orçamento acaba
    // antes dos jogos — o jogo entra numa vaga sem quadra aberta e nasce com hora e sem lugar.
    public int? QuadrasAbertasEm(DateTime horario)
    {
        if (_janelaPorQuadra.Count == 0) return null;

        int abertas = 0;
        foreach (var nome in _clubePorQuadra.Keys)
            if (QuadraAberta(nome, horario)) abertas++;

        return abertas;
    }

    // QUANTOS JOGOS CABEM na janela do local alugado — a resposta que o Felipe pediu como campo
    // ("vai ter q por quantos jogos vão para la") e que virou CONTA.
    //
    // 🗣️ A decisão: "quantos jogos" é `quadras × rodadas da janela`. Dois campos pra mesma
    // informação (uma janela E uma cota) discordariam no primeiro torneio, e o organizador não
    // teria como saber qual venceu. Aqui a tela CALCULA e mostra; quem manda é a janela.
    //
    // Null quando a janela não tem as duas pontas: sem começo ou sem fim não há quantas contar.
    public static int? JogosQueCabemNaJanela(int quadras, DateTime? de, DateTime? ate, int duracaoMinutos)
    {
        if (de is not DateTime inicio || ate is not DateTime fim) return null;

        var duracao = duracaoMinutos > 0 ? duracaoMinutos : 50;
        var minutos = (fim - inicio).TotalMinutes;
        if (minutos < 0) return 0;

        // Rodadas, e não "horas × quadras": o que a janela mede são HORAS DE COMEÇAR jogo.
        //
        // ⚠️ `+1` PORQUE O "ATÉ" ENTRA NA CONTA (09/09/2026, junto com `QuadraAberta`): das 8h
        // às 12h10 saem 6 rodadas — 8h, 8h50, 9h40, 10h30, 11h20 e a das 12h10 —, e não 5. Uma
        // janela de tamanho zero ("das 8h às 8h") é 1 rodada pelo mesmo motivo: dá pra começar
        // um jogo às 8h. Invertida é a única que cabe zero, e essa saiu logo acima.
        var rodadas = (int)(minutos / duracao) + 1;

        return Math.Max(quadras, 1) * rodadas;
    }

    // Esta quadra é do local EXTERNO (qualquer clube que não seja o principal do torneio)?
    public bool EhSedeExtra(string? nomeQuadra) =>
        ClubeDaQuadra(nomeQuadra) is { } clube && clube != _clubePrincipal;

    // Esta categoria pode transbordar pro local externo?
    //
    // ⚠️ FALSE é a EXCEÇÃO, não a regra — ver o comentário de Models/Categoria. Categoria
    // desconhecida responde `true` pela mesma razão que `QuadrasDe` nunca devolve lista vazia:
    // uma trava por engano tira a categoria inteira da grade, calada.
    public bool PodeIrPraSedeExtra(int categoriaId) =>
        !_categoriasPresasNaSedePrincipal.Contains(categoriaId);

    // O torneio acontece em mais de um clube? É o interruptor que as telas consultam antes de
    // escrever o clube ao lado da quadra: num torneio de uma sede só, "Nata · Quadra 2" seria
    // repetir o cabeçalho da página em cada linha da lista de jogos.
    public bool MaisDeUmClube { get; }

    // Zero quando não há mais de um clube, ou quando o organizador zerou o campo de propósito.
    public TimeSpan FolgaParaTrocarDeClube { get; }

    // "Que a dupla jogue apenas UM dos jogos no local externo" (Felipe, 08/09/2026). MOLE:
    // cede quando respeitá-la deixaria a quadra do lugar alugado parada — ver
    // Services/GradeDeJogos.Encaixar e Models/Torneio.EvitarDoisJogosNaSedeExtra.
    public bool EvitarDoisJogosNaSedeExtra { get; }

    // As quadras em que esta categoria PODE jogar. Null = pode em qualquer uma, que é o caminho
    // normal e o único que existia antes.
    //
    // ⚠️ Categoria apontando pra um clube SEM QUADRA no torneio também devolve null, e não uma
    // lista vazia. É de propósito: o organizador ainda apaga quadra depois do sorteio sem trava
    // nenhuma (TorneiosController.Criacao, ação Editar), e uma lista vazia faria a grade nunca
    // achar quadra pra essa categoria — ela ficaria sem horário nenhum, calada. O preço de uma
    // sede que evaporou não pode ser a categoria inteira sumir da grade.
    public IReadOnlyList<string>? QuadrasDe(int categoriaId) =>
        _quadrasPorCategoria.TryGetValue(categoriaId, out var quadras) ? quadras : null;

    // Em que clube esta categoria joga. Null = em qualquer um — inclusive no caso defensivo
    // acima, da categoria apontada pra um clube que não tem quadra no torneio. Aqui a resposta
    // TEM que casar com `QuadrasDe`: se a categoria pode jogar em qualquer lugar, ela não tem
    // clube pra folga de deslocamento comparar. Por isso os dois dicionários nascem no mesmo
    // laço, com a mesma condição.
    public int? ClubeDaCategoria(int categoriaId) =>
        _clubePorCategoria.TryGetValue(categoriaId, out var clube) ? clube : null;

    // Em que clube fica a quadra com este nome. Null = nome que não está no cadastro.
    //
    // Acontece de verdade: `Partida.NomeQuadra` é texto solto, e a lista de quadras "em uso"
    // (RoboDoChaveamento.QuadrasEmUsoAsync) inclui nomes escritos direto nos jogos, que podem
    // não existir na tabela `Quadra`. Nome desconhecido não recebe clube inventado — a grade o
    // trata como quadra de lugar nenhum, e as telas não escrevem clube ao lado dele.
    public int? ClubeDaQuadra(string? nomeQuadra) =>
        !string.IsNullOrWhiteSpace(nomeQuadra) && _clubePorQuadra.TryGetValue(nomeQuadra!.Trim(), out var clube)
            ? clube
            : null;

    // O nome do clube pra escrever ao lado da quadra. Null quando não dá pra saber — e aí a
    // tela mostra só a quadra, que é o que ela sempre mostrou. Escrever um clube errado seria
    // pior que não escrever nenhum: é ele que decide pra que prédio a pessoa dirige.
    public string? NomeDoClubeDaQuadra(string? nomeQuadra) =>
        ClubeDaQuadra(nomeQuadra) is { } clubeId && _nomeDoClube.TryGetValue(clubeId, out var nome)
            ? nome
            : null;

    // O CLUBE QUE A CATEGORIA JÁ DETERMINA, mesmo sem quadra no jogo (10/09/2026).
    //
    // 🗣️ Felipe, na grade do Er em produção, 97 jogos sem etiqueta: *"falta aparecer em qual
    // clube é os jogos"*. O Er é POR ORDEM (a quadra é apagada de propósito) e tem DOIS clubes —
    // a combinação exata em que `NomeDoClubeDaQuadra` não tem de onde tirar o clube.
    //
    // Duas coisas respondem sem chute: a categoria PRESA num clube (`Categoria.ClubeId`, o jeito
    // do Dez E Batata) e a categoria TIRADA DO EXTERNO (`PodeJogarNaSedeExtra = false`, o jeito
    // do Er — a 3ª e a 4ª só jogam em casa). A que pode transbordar joga em qualquer um dos
    // dois, e aí a resposta é null: escrever o principal mandaria metade do torneio pro
    // endereço errado.
    public string? NomeDoClubeDaCategoria(int categoriaId)
    {
        if (ClubeDaCategoria(categoriaId) is { } presa)
            return _nomeDoClube.TryGetValue(presa, out var nomePresa) ? nomePresa : null;

        return PodeIrPraSedeExtra(categoriaId) ? null : NomeDoClubePrincipal;
    }

    // O NOME DO CLUBE DO TORNEIO — o "Er Padel" que a lista de jogos escreve em toda linha.
    //
    // Existe porque a quadra não responde sozinha por ONDE é o jogo: num torneio por ordem de
    // chegada o jogo legitimamente não tem quadra (🗣️ Felipe, 09/09/2026: *"nao tem quadra
    // definida, apenas o clube, por que é por ordem de chegada (por ter checkin)"*), e ainda
    // assim a pessoa precisa saber pra que prédio ir. Quem usa é Services/LugarDoJogo, e SÓ no
    // torneio de um clube só — com dois, o clube certo é o da QUADRA, e chutar o principal
    // mandaria metade do torneio pro endereço errado.
    //
    // Null quando ninguém passou os nomes dos clubes (é o caso de quem monta o mapa pra GRADE,
    // que só compara "é o mesmo lugar?"). Aí a tela cai no comportamento antigo: só a quadra.
    public string? NomeDoClubePrincipal =>
        _nomeDoClube.TryGetValue(_clubePrincipal, out var nome) ? nome : null;

    // Monta o mapa a partir do que já está carregado.
    //
    // `nomesDosClubes` é opcional porque quem chama pela GRADE não precisa de nome nenhum — lá
    // o clube é só uma chave pra comparar "é o mesmo lugar?". Quem chama pra DESENHAR TELA
    // passa os nomes; sem eles, `NomeDoClubeDaQuadra` responde null e a tela cai no
    // comportamento antigo em vez de imprimir um lugar chutado.
    public static SedesDoTorneio Montar(int clubePrincipalId, int minutosParaTrocarDeClube,
        IEnumerable<Quadra> quadras, IEnumerable<Categoria> categorias,
        IReadOnlyDictionary<int, string>? nomesDosClubes = null,
        bool evitarDoisJogosNaSedeExtra = false,
        // O torneio, quando quem chama o tem na mão. Serve pra UMA coisa: reconhecer a janela
        // IMPOSSÍVEL — ver `JanelasImpossiveis`. Omitido, nada muda.
        Torneio? torneio = null)
    {
        // Quadra sem clube dito é do clube do torneio. É o que faz TODO torneio anterior a esta
        // opção continuar sendo de uma sede só, sem uma linha de conversão no banco.
        var clubePorQuadra = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var quadra in quadras)
        {
            var nome = (quadra.Nome ?? "").Trim();
            if (nome.Length == 0) continue;
            clubePorQuadra[nome] = quadra.ClubeId ?? clubePrincipalId;
        }

        // A janela de cada quadra. Só entra no mapa quem TEM janela — quadra sem limite (a
        // imensa maioria) não paga nem uma entrada de dicionário.
        //
        // ⚠️ MONTADA ANTES DO ATALHO DE UMA SEDE, e isso é o conserto de 09/09/2026. Ela nasceu
        // pro local alugado de OUTRA sede (08/09), e vivia depois do `return Nenhuma` logo
        // abaixo — então, com todas as quadras no mesmo clube, a janela era jogada fora em
        // silêncio: o organizador alugava duas quadras no próprio complexo das 8h às 14h,
        // digitava a janela, e o motor marcava jogo lá às 22h. Com a tela de planejamento
        // oferecendo o campo (pedido do Felipe), o atalho virava mentira.
        var janelaPorQuadra = new Dictionary<string, (DateTime? De, DateTime? Ate)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var quadra in quadras)
        {
            if (quadra.DisponivelDe == null && quadra.DisponivelAte == null) continue;

            var nome = (quadra.Nome ?? "").Trim();
            if (nome.Length == 0) continue;

            janelaPorQuadra[nome] = (quadra.DisponivelDe, quadra.DisponivelAte);
        }

        // ⚠️ JANELA QUE NÃO DEIXA NENHUMA QUADRA ABERTA EM NENHUM HORÁRIO DO TORNEIO É DADO
        // ERRADO, E O MOTOR PARA DE OBEDECÊ-LA (09/09/2026).
        //
        // 🗣️ Felipe, na TERCEIRA vez: *"refiz a grade, ainda ta pulando pro dia 15 [...] ta sem o
        // nome do clube que vai ser o jogo, e ainda pulando os dias"*.
        //
        // 🕳️ AS DUAS QUEIXAS ERAM O MESMO DEFEITO. `GradeDeJogos.Encaixar` só grava `NomeQuadra`
        // quando encontra quadra ABERTA naquele horário; sem nenhuma aberta ele marca a hora,
        // deixa o lugar em branco e escorrega pro horário seguinte — dia após dia, calado. Daí
        // sai tudo junto: o pulo de 12 pra 15, o jogo sem local, e dois jogos da MESMA dupla no
        // mesmo minuto (o último recurso, quando as vagas acabam).
        //
        // Obedecer uma janela assim destrói a grade inteira; ignorá-la devolve o comportamento de
        // quem nunca preencheu o campo — que é o que o organizador tinha antes de a tabela de
        // quadras existir. Entre um torneio sem grade e um torneio com a janela ignorada, o
        // segundo é o único que dá pra publicar.
        //
        // ⚠️ ESTREITO DE PROPÓSITO: basta UMA quadra abrir em UM horário do torneio pra ele não
        // disparar. A janela legítima do local alugado ("das 8h às 14h de sábado") deixa quadra
        // aberta, logo não é impossível, e continua valendo inteira.
        if (janelaPorQuadra.Count > 0 && torneio != null
            && NenhumaQuadraAbreNoTorneio(janelaPorQuadra, clubePorQuadra.Keys, torneio))
        {
            janelaPorQuadra.Clear();
        }

        // Uma sede só? Então nada de SEDE existe: sem folga pra atravessar a cidade, sem
        // categoria presa a um clube, sem transbordo. Sai por aqui pra que o torneio comum não
        // pague nem um dicionário a mais, e pra que `MaisDeUmClube` seja a única pergunta que
        // as telas precisem fazer.
        //
        // ⚠️ A JANELA SOBREVIVE À SAÍDA. Janela não é sede — é a quadra dizendo até que horas
        // existe —, e ela vale num lugar só do mesmo jeito que vale em dois. O objeto que sai
        // daqui carrega só o que a janela precisa (o mapa dela e a lista de quadras, pra
        // `QuadrasAbertasEm` ter o que contar) e `maisDeUmClube: false`, pra todo o resto
        // continuar respondendo como o torneio de uma sede sempre respondeu.
        //
        // ⚠️ O NOME DO CLUBE TAMBÉM SOBREVIVE, desde 09/09/2026. Ele era descartado aqui, e com
        // isso o torneio de uma sede não tinha como dizer ONDE é o jogo — a etiqueta das telas
        // saía só com a quadra, e a lista de jogos do Er não dizia "Er Padel" em lugar nenhum.
        // `Nenhuma` continua sendo a saída quando não há NADA a dizer: nem janela, nem nome.
        var nomes = new Dictionary<int, string>();
        if (nomesDosClubes != null)
            foreach (var (clubeId, nome) in nomesDosClubes)
                if (!string.IsNullOrWhiteSpace(nome)) nomes[clubeId] = nome.Trim();

        if (clubePorQuadra.Values.Distinct().Count() <= 1)
        {
            if (janelaPorQuadra.Count == 0 && !nomes.ContainsKey(clubePrincipalId)) return Nenhuma;

            return new SedesDoTorneio(
                clubePorQuadra,
                new Dictionary<int, string[]>(),
                new Dictionary<int, int>(),
                nomes,
                janelaPorQuadra,
                new HashSet<int>(),
                clubePrincipalId,
                TimeSpan.Zero,
                maisDeUmClube: false,
                evitarDoisJogosNaSedeExtra: false);
        }

        var quadrasPorClube = clubePorQuadra
            .GroupBy(par => par.Value)
            .ToDictionary(g => g.Key, g => g.Select(par => par.Key).ToArray());

        // Os dois nascem no MESMO laço, com a MESMA condição, de propósito: "a categoria está
        // presa a um clube" e "a categoria tem clube pra folga comparar" precisam ser a mesma
        // resposta. Duas condições parecidas em lugares diferentes é como se escreve o bug de
        // uma categoria que a grade prende num clube e a folga acha que joga em qualquer lugar.
        var quadrasPorCategoria = new Dictionary<int, string[]>();
        var clubePorCategoria = new Dictionary<int, int>();
        foreach (var categoria in categorias)
        {
            if (categoria.ClubeId is not { } clubeDaCategoria) continue;

            // Clube sem quadra neste torneio não vira lista vazia — ver o comentário de
            // `QuadrasDe`. A categoria simplesmente volta a jogar em qualquer lugar.
            if (!quadrasPorClube.TryGetValue(clubeDaCategoria, out var daSede) || daSede.Length == 0) continue;

            quadrasPorCategoria[categoria.Id] = daSede;
            clubePorCategoria[categoria.Id] = clubeDaCategoria;
        }

        // Quem NÃO pode transbordar. Guardado pelo lado negativo de propósito: o normal é poder
        // (ver Models/Categoria), então o conjunto fica vazio na imensa maioria dos torneios e
        // a pergunta `PodeIrPraSedeExtra` é um `Contains` num set vazio.
        var presas = new HashSet<int>();
        foreach (var categoria in categorias)
            if (!categoria.PodeJogarNaSedeExtra) presas.Add(categoria.Id);

        return new SedesDoTorneio(
            clubePorQuadra,
            quadrasPorCategoria,
            clubePorCategoria,
            nomes,
            janelaPorQuadra,
            presas,
            clubePrincipalId,
            TimeSpan.FromMinutes(Math.Max(0, minutosParaTrocarDeClube)),
            maisDeUmClube: true,
            evitarDoisJogosNaSedeExtra);
    }

    // Carrega do banco. Fica AQUI, e não em cada controller, porque são cinco telas que
    // precisam disto (a página do torneio, a lista de jogos, o calendário, o placar e a Mesa) e
    // cada uma montaria a consulta do seu jeito — que é como se escreve a tela que mostra o
    // clube certo ao lado da que mostra o errado. O precedente é `CatalogoLocais`, que também
    // recebe o contexto.
    //
    // Carrega o nome dos clubes junto mesmo quando quem pede é a GRADE, que não usa nome nenhum:
    // são poucas linhas, e ter duas formas de montar o mapa é o mesmo problema com outra roupa.
    public static async Task<SedesDoTorneio> CarregarAsync(DbPadelContext db, int torneioId)
    {
        // ⚠️ O TORNEIO INTEIRO, e não só três colunas: `Montar` precisa das datas e do expediente
        // pra reconhecer a janela IMPOSSÍVEL. Uma projeção enxuta economizaria bytes e devolveria
        // o guarda desligado em produção — que é o único lugar onde ele importa.
        var torneio = await db.Torneios.AsNoTracking().FirstOrDefaultAsync(t => t.Id == torneioId);

        if (torneio == null) return Nenhuma;
        var doTorneio = torneio;

        var quadras = await db.Quadras.Where(q => q.TorneioId == torneioId).AsNoTracking().ToListAsync();
        var categorias = await db.Categorias.Where(c => c.TorneioId == torneioId).AsNoTracking().ToListAsync();

        var clubes = quadras.Select(q => q.ClubeId)
            .Append(doTorneio.ClubeId)
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var nomes = await db.Clubes
            .Where(c => clubes.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome);

        return Montar(doTorneio.ClubeId, doTorneio.MinutosParaTrocarDeClube, quadras, categorias, nomes,
            doTorneio.EvitarDoisJogosNaSedeExtra, torneio);
    }

    // ── O que os formulários mandam ───────────────────────────────────────────────────────
    // Duas telas escrevem sede: a criação e a edição do torneio. A régua mora aqui, e não
    // dentro do controller, porque neste projeto a segunda cópia é sempre a que fica pra trás
    // — foi assim com o nome de quadra repetido, que passava calado numa das duas portas.

    // O clube de cada quadra, na POSIÇÃO em que o formulário a mostrou. Vazio, "0" ou clube que
    // não está entre os `permitidos` viram null = clube principal do torneio.
    //
    // ⚠️ Posição, e não Id: na criação a quadra nasce neste mesmo POST, e na edição ela pode
    // estar nascendo agora (o organizador que sobe de 3 pra 5 quadras escolhe o clube da quinta
    // antes de ela ter Id). É a mesma amarração de `PreferenciaDeQuadra.Ler`, e ela só se
    // sustenta porque o MESMO laço de JavaScript desenha o campo de nome e o de clube.
    public static int? ClubeDaQuadraNaPosicao(string[]? clubesPorPosicao, int posicao, ISet<int> permitidos)
    {
        if (clubesPorPosicao == null || posicao >= clubesPorPosicao.Length) return null;
        if (!int.TryParse(clubesPorPosicao[posicao], out var clube)) return null;
        return clube > 0 && permitidos.Contains(clube) ? clube : null;
    }

    // Pares "categoria:clube" das caixas de seleção. Mesma forma de `PreferenciaDeQuadra.Ler`
    // e pelo mesmo motivo: é o que o navegador manda de graça e o que o binder entrega sem
    // ajuda. Par repetido some — a categoria só joga num lugar.
    // A MESMA leitura serve pra QUADRA desde 08/09/2026, quando o editor de sedes saiu do
    // formulário de gestão e virou tela própria. No formulário antigo a quadra era endereçada
    // por POSIÇÃO (`ClubeDaQuadraNaPosicao`) porque nome e clube viajavam no MESMO POST;
    // separados, duas abas abertas fariam as posições discordarem e o clube da quadra 3 iria
    // parar na quadra 4, calado. Por Id isso não existe.
    public static Dictionary<int, int> LerClubePorQuadra(IEnumerable<string>? valores, ISet<int> permitidos) =>
        LerClubePorCategoria(valores, permitidos);

    public static Dictionary<int, int> LerClubePorCategoria(IEnumerable<string>? valores, ISet<int> permitidos)
    {
        var porCategoria = new Dictionary<int, int>();
        if (valores == null) return porCategoria;

        foreach (var valor in valores)
        {
            var partes = valor?.Split(':');
            if (partes is not { Length: 2 }) continue;
            if (!int.TryParse(partes[0], out var categoria) || !int.TryParse(partes[1], out var clube)) continue;
            if (categoria <= 0 || clube <= 0 || !permitidos.Contains(clube)) continue;

            porCategoria[categoria] = clube;
        }

        return porCategoria;
    }

    // O motivo pra não gravar, ou null quando está tudo certo.
    //
    // Uma só coisa é recusada: categoria num clube que não recebeu QUADRA NENHUMA. A grade
    // sobrevive a isso — ela trata como "sem sede" e marca em qualquer lugar (ver `QuadrasDe`)
    // —, e é justamente por isso que precisa ser recusado AQUI: o organizador escolheria o
    // clube, veria a tela salvar, e o torneio faria o contrário sem dizer nada. Falha muda é o
    // que este projeto mais pagou caro.
    public static string? MotivoParaNaoSalvar(IEnumerable<int?> clubeDeCadaQuadra,
        IEnumerable<int> clubeDeCadaCategoria)
    {
        var comQuadra = clubeDeCadaQuadra.Where(c => c != null).Select(c => c!.Value).ToHashSet();

        // Nenhuma quadra em clube nenhum quer dizer torneio de uma sede só — não há o que
        // conferir, e é o caminho de quase todo torneio.
        if (comQuadra.Count == 0) return null;

        foreach (var clube in clubeDeCadaCategoria.Distinct())
        {
            if (comQuadra.Contains(clube)) continue;

            return "Uma categoria ficou num clube que não tem nenhuma quadra neste torneio. "
                 + "Escolha ao menos uma quadra nesse clube, ou mude a categoria de lugar — "
                 + "senão ela jogaria em qualquer quadra e a escolha não valeria de nada.";
        }

        return null;
    }
}
