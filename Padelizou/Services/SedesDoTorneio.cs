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
        TimeSpan.Zero,
        maisDeUmClube: false);

    private readonly Dictionary<string, int> _clubePorQuadra;
    private readonly Dictionary<int, string[]> _quadrasPorCategoria;
    private readonly Dictionary<int, int> _clubePorCategoria;
    private readonly Dictionary<int, string> _nomeDoClube;

    private SedesDoTorneio(Dictionary<string, int> clubePorQuadra,
        Dictionary<int, string[]> quadrasPorCategoria,
        Dictionary<int, int> clubePorCategoria,
        Dictionary<int, string> nomeDoClube,
        TimeSpan folga, bool maisDeUmClube)
    {
        _clubePorQuadra = clubePorQuadra;
        _quadrasPorCategoria = quadrasPorCategoria;
        _clubePorCategoria = clubePorCategoria;
        _nomeDoClube = nomeDoClube;
        FolgaParaTrocarDeClube = folga;
        MaisDeUmClube = maisDeUmClube;
    }

    // O torneio acontece em mais de um clube? É o interruptor que as telas consultam antes de
    // escrever o clube ao lado da quadra: num torneio de uma sede só, "Nata · Quadra 2" seria
    // repetir o cabeçalho da página em cada linha da lista de jogos.
    public bool MaisDeUmClube { get; }

    // Zero quando não há mais de um clube, ou quando o organizador zerou o campo de propósito.
    public TimeSpan FolgaParaTrocarDeClube { get; }

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

    // Monta o mapa a partir do que já está carregado.
    //
    // `nomesDosClubes` é opcional porque quem chama pela GRADE não precisa de nome nenhum — lá
    // o clube é só uma chave pra comparar "é o mesmo lugar?". Quem chama pra DESENHAR TELA
    // passa os nomes; sem eles, `NomeDoClubeDaQuadra` responde null e a tela cai no
    // comportamento antigo em vez de imprimir um lugar chutado.
    public static SedesDoTorneio Montar(int clubePrincipalId, int minutosParaTrocarDeClube,
        IEnumerable<Quadra> quadras, IEnumerable<Categoria> categorias,
        IReadOnlyDictionary<int, string>? nomesDosClubes = null)
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

        // Uma sede só? Então nada disto existe. Sai por aqui pra que o torneio comum não pague
        // nem um dicionário a mais, e pra que `MaisDeUmClube` seja a única pergunta que as
        // telas precisem fazer.
        if (clubePorQuadra.Values.Distinct().Count() <= 1) return Nenhuma;

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

        var nomes = new Dictionary<int, string>();
        if (nomesDosClubes != null)
            foreach (var (clubeId, nome) in nomesDosClubes)
                if (!string.IsNullOrWhiteSpace(nome)) nomes[clubeId] = nome.Trim();

        return new SedesDoTorneio(
            clubePorQuadra,
            quadrasPorCategoria,
            clubePorCategoria,
            nomes,
            TimeSpan.FromMinutes(Math.Max(0, minutosParaTrocarDeClube)),
            maisDeUmClube: true);
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
        var doTorneio = await db.Torneios
            .Where(t => t.Id == torneioId)
            .Select(t => new { t.ClubeId, t.MinutosParaTrocarDeClube })
            .FirstOrDefaultAsync();

        if (doTorneio == null) return Nenhuma;

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

        return Montar(doTorneio.ClubeId, doTorneio.MinutosParaTrocarDeClube, quadras, categorias, nomes);
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
