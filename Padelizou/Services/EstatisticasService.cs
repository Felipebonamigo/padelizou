using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

public class EstatisticasService : IEstatisticasService
{
    private readonly DbPadelContext _context;

    public EstatisticasService(DbPadelContext context)
    {
        _context = context;
    }

    // QUANTO VALE CADA CAMPANHA: `Services/PontosDoTorneio` (espec em RANKING.md).
    //
    // ⚠️ Aqui existia `PontosPorFase(fase)`, e ele foi REMOVIDO de propósito em 10/08/2026,
    // quando o ponto passou a depender do TAMANHO da categoria. São OITO lugares deste
    // arquivo somando ponto (ranking por categoria, times, times num torneio, ranking do
    // torneio, resumo do perfil, pontos por jogador e duas somas da evolução); um método que
    // ainda aceitasse só a fase deixaria qualquer um deles com a regra velha — compilando,
    // passando nos testes e mostrando um total diferente do outro. Apagar o método quebrou a
    // compilação nos oito, que é a única forma de nenhum ficar pra trás.
    //
    // Contagem de inscrições por categoria, pra alimentar o peso. Uma passada só.
    //
    // ⚠️ Conta a inscrição que VALE NO RANKING: dupla-TIME fica fora (o `Jogador1Id` dela é o
    // organizador que cadastrou, não quem jogou) e lista de espera também (quem não entrou
    // não fez parte do funil que o peso mede).
    private async Task<Dictionary<int, int>> ContarDuplasPorCategoriaAsync(IEnumerable<int>? categoriaIds = null)
    {
        // ⚠️ `EstaNaChave` e não só `!EmListaDeEspera`: quem se inscreveu SEM PARCEIRO também
        // fica fora do sorteio, e contá-lo aqui daria à categoria um tamanho que ela não teve.
        // É a MESMA régua que decide quem pontua — se as duas discordassem, a categoria teria
        // dois tamanhos ao mesmo tempo: um pra dividir o peso, outro pra somar o ponto.
        var q = _context.Duplas.Where(d => d.NomeTime == null).Where(ForaDoSorteio.EstaNaChave);

        if (categoriaIds != null)
        {
            var ids = categoriaIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, int>();
            q = q.Where(d => ids.Contains(d.CategoriaId));
        }

        // Agrupa em memória depois de trazer UMA coluna: `GroupBy` traduzido muda de
        // comportamento entre provedores, e este método é lido pelo InMemory dos testes e
        // pelo Postgres de produção.
        var categorias = await q.Select(d => d.CategoriaId).ToListAsync();
        return categorias.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
    }

    // A mesma contagem quando as duplas JÁ estão carregadas — evita uma segunda ida ao banco
    // nos métodos que leem a tabela inteira de qualquer jeito.
    private static Dictionary<int, int> ContarDuplasPorCategoria(IEnumerable<Dupla> duplas) =>
        duplas
            .Where(d => d.NomeTime == null && !ForaDoSorteio.FicaDeFora(d))
            .GroupBy(d => d.CategoriaId)
            .ToDictionary(g => g.Key, g => g.Count());

    // Torneio RESTRITO e AMERICANO não entram no ranking oficial.
    //
    // ── Americano (decisão do Felipe, 07/08/2026) ─────────────────────────────────────────
    // O Americano é o rodízio de sábado: gente conhecida, parceiro trocando a cada rodada,
    // criado na sexta à noite. Ele estava pontuando IGUAL a uma final de 3ª Categoria, e isso
    // era uma porta escancarada — três amigos criam um Americano, lançam os placares que
    // quiserem e fabricam ranking sem enfrentar ninguém.
    //
    // Pior que o campeão dos 100 pontos: no Americano cada RODADA cria uma dupla nova, então
    // um rodízio de 12 pessoas despejava ~30 linhas de "participou" no ranking de uma vez.
    //
    // O Americano passa a ter ranking PRÓPRIO (separado, e só pontua quando o organizador
    // contrata isso) — ver RANKING.md. Aqui ele sai do oficial, que é o que mede torneio.
    //
    // ── Restrito (decisão do Felipe, 31/07/2026) ──────────────────────────────────────────
    // Restrito é o torneio fechado: entra quem tem a chave de acesso — interno de clube,
    // grupo de amigos, confraternização de fim de ano. Pontuar evento fechado faria o
    // ranking medir ACESSO a torneio privado em vez de padel jogado: quem organiza um
    // interno por mês subiria sem nunca enfrentar ninguém de fora.
    //
    // O que NÃO muda: a participação, o título e os jogos continuam no perfil e no
    // histórico da pessoa — aconteceram. O que não existe é ponto de ranking.
    // Torneio NULO continua contando: é a dupla lida numa consulta que não fez Include, e
    // sumir do ranking por causa de um Include esquecido seria pior do que qualquer das duas
    // regras acima.
    public static bool ContaNoRanking(Torneio? torneio) =>
        torneio is null || ContaNoRanking(torneio.Restrito, torneio.Formato);

    // A mesma pergunta quando a consulta trouxe só os DOIS CAMPOS, e não o torneio inteiro —
    // é o caso de quem projeta (`.Select`) pra não carregar entidade à toa.
    //
    // ⚠️ Existe pra não haver uma terceira escrita da regra: as versões que liam só
    // `!Restrito` e esqueciam o Americano estavam justamente nesses `Select` enxutos, e cada
    // uma delas foi um bug diferente (pontos do perfil, pontos da busca, ranking de times).
    public static bool ContaNoRanking(bool restrito, string? formato) =>
        !restrito && !FormatoDoTorneio.EhAmericano(formato);

    // A MESMA régua acima, escrita pra rodar NO BANCO.
    //
    // Existe porque `ContaNoRanking` recebe uma entidade e o EF não sabe traduzir `is`/método
    // pra SQL — a consulta do ranking de times, que é a única que precisa filtrar antes de
    // trazer as linhas, tinha reescrito só metade da regra (`!Restrito`) e deixado o Americano
    // passar. Aqui as duas ficam lado a lado, e há teste comparando uma com a outra.
    public static readonly System.Linq.Expressions.Expression<Func<Dupla, bool>> DuplaContaNoRanking =
        d => !d.Categoria.Torneio.Restrito
             && d.Categoria.Torneio.Formato != FormatoDoTorneio.Americano
             && d.Categoria.Torneio.Formato != FormatoDoTorneio.AmericanoDeDuplas;

    // Ordem das fases para "melhor colocação" (maior = mais longe).
    private static int RankFase(string? fase) => fase switch
    {
        "Campeao" => 5,
        "Final" => 4,
        "Semifinal" => 3,
        "Quartas de Final" => 2,
        _ => 1
    };

    public static string RotuloFase(string? fase) => fase switch
    {
        "Campeao" => "Campeão",
        "Final" => "Vice",
        "Semifinal" => "Semifinal",
        "Quartas de Final" => "Quartas",
        _ => "Fase de Grupos"
    };

    // Decide o "material" do troféu só pelo texto do nome da categoria (mesma convenção do
    // catálogo padrão em Program.cs — "2ª Categoria Masculina/Feminina", "Categoria Open ...", etc).
    // O material de cada categoria mora em Services/TrofeuDeMaterial — um lugar só, porque a
    // mesma regra decide a pílula pequena das listas E o troféu desenhado da prateleira do
    // perfil. Duas cópias divergiriam no dia em que uma categoria nova entrasse.
    public static (string Chave, string Nome, string Icone, string CorFundo, string CorTexto) TierDaCategoria(string? nomeCategoria)
    {
        var m = TrofeuDeMaterial.Do(nomeCategoria);
        // Todos os tiers são taça, inclusive o diamante: o material aparece na cor da pílula,
        // não num ícone diferente. (O diamante já foi pedra lapidada e destoava da família.)
        return (m.Chave, m.Nome, "bi-trophy-fill", m.CorFundo, m.CorTexto);
    }

    // Ordem de FORÇA da categoria (maior número = categoria mais forte). Usado pela
    // regra anti-sandbagging: quem comprova nível numa categoria forte não pode
    // descer para categorias mais fracas (número de ordem menor). 0 = desconhecida.
    public static int OrdemCategoria(string? nomeCategoria)
    {
        string n = nomeCategoria ?? "";
        if (n.Contains("Open")) return 8;
        if (n.Contains("1ª")) return 7;
        if (n.Contains("2ª")) return 6;
        if (n.Contains("3ª")) return 5;
        if (n.Contains("4ª")) return 4;
        if (n.Contains("5ª")) return 3;
        if (n.Contains("6ª")) return 2;
        if (n.Contains("7ª") || n.Contains("Iniciantes")) return 1;
        return 0;
    }

    // Todas as grafias que uma cidade escolhida na tela tem no cadastro. Quem escolhe
    // "Gravataí" está escolhendo também quem digitou "GRAVATAI" e "gravatai" — o filtro
    // compara do jeito que uma PESSOA compara, senão a opção única do <select> traria só uma
    // fatia dos jogadores e ninguém teria como perceber.
    public async Task<List<string>> ObterGrafiasDasCidadesAsync(IEnumerable<string> escolhidas)
    {
        var conhecidas = await _context.Jogadores
            .Where(j => j.Cidade != null && j.Cidade != "")
            .Select(j => j.Cidade!)
            .Distinct().ToListAsync();

        return CidadesSemRepetir.GrafiasDe(escolhidas, conhecidas);
    }

    // Ids dos jogadores que batem com o filtro de estado + cidades. null = sem filtro (país todo).
    // Pode receber VÁRIAS cidades (jogador entra se estiver em qualquer uma delas).
    // Cidade/Estado são texto livre no cadastro, então compara sem diferenciar maiúsculas.
    //
    // ⚠️ NÃO filtre `PerfilPrivado` aqui: a ausência é DECISÃO, não esquecimento (Felipe,
    // 10/08/2026). O ranking não sai do cadastro, e sim de RESULTADO DE TORNEIO: evento
    // público, de chave pública, com o nome já divulgado na hora em que se jogou. Esconder a
    // pessoa daqui não a protegeria — tiraria ela do próprio ranking, que é o oposto. Mesmo
    // raciocínio que deixou cidade/UF fora da trava do `EnderecoPublico` (ver STATUS.md).
    //
    // 📌 Este comentário já disse que "a busca de jogadores faz o CONTRÁRIO", quando ela
    // escondia quem era privado. Não faz mais, e a régua vale IGUAL nas duas telas: "perfil
    // privado" é sobre CONTATO (Instagram e WhatsApp), não sobre resultado de padel — ver
    // JogadoresController.Buscar e PerfilPrivadoEContatoTests.
    // Preso por teste em `FiltroDeCidadeNoRankingTests`, pra ninguém "consertar" sem querer.
    public async Task<HashSet<int>?> ObterJogadoresDoLocalAsync(IEnumerable<string>? cidades, string? estado)
    {
        var escolhidas = (cidades ?? Enumerable.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct().ToList();
        bool temCidade = escolhidas.Count > 0;
        bool temEstado = !string.IsNullOrWhiteSpace(estado);
        if (!temCidade && !temEstado) return null;

        var q = _context.Jogadores.AsQueryable();
        if (temEstado)
        {
            var uf = estado!.Trim().ToUpper();
            q = q.Where(j => j.Estado != null && j.Estado.ToUpper() == uf);
        }
        if (temCidade)
        {
            var grafias = (await ObterGrafiasDasCidadesAsync(escolhidas))
                .Select(g => g.ToUpper()).ToList();
            q = q.Where(j => j.Cidade != null && grafias.Contains(j.Cidade.ToUpper()));
        }
        return (await q.Select(j => j.Id).ToListAsync()).ToHashSet();
    }

    // Estados e cidades para os selects do filtro, já SEM REPETIR: as grafias da mesma cidade
    // viram uma opção só (ver `CidadesSemRepetir`).
    //
    // `somenteQuemJogouTorneio` é o filtro do RANKING, e ele nasce do que a própria página diz
    // na primeira linha: tudo ali é calculado só a partir de resultados de torneio. Cidade sem
    // nenhum jogador com resultado é opção que só sabe devolver tabela vazia — e era por essa
    // porta que entrava na lista todo apelido de cidade digitado no cadastro.
    public async Task<(List<string> Estados, List<string> Cidades)> ObterLocaisDisponiveisAsync(
        string? estado, bool somenteQuemJogouTorneio = false)
    {
        var q = _context.Jogadores.AsQueryable();

        if (somenteQuemJogouTorneio)
        {
            // ⚠️ `DuplaContaNoRanking` e `NomeTime == null`, a mesma régua das outras consultas
            // daqui: torneio restrito e Americano não entram no ranking oficial, e linha de
            // TIME tem o organizador no `Jogador1Id` sem ele ter jogado nada. Sem os dois, a
            // lista de cidades prometeria ranking onde a tabela vem vazia.
            var inscricoes = await _context.Duplas
                .Where(DuplaContaNoRanking)
                .Where(d => d.NomeTime == null)
                .Select(d => new { d.Jogador1Id, d.Jogador2Id })
                .Distinct()
                .ToListAsync();

            var ids = inscricoes
                .SelectMany(d => new int?[] { d.Jogador1Id, d.Jogador2Id })
                .Where(id => id != null)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            q = q.Where(j => ids.Contains(j.Id));
        }

        var estados = await q
            .Where(j => j.Estado != null && j.Estado != "")
            .Select(j => j.Estado!.ToUpper())
            .Distinct().OrderBy(e => e).ToListAsync();

        var qc = q.Where(j => j.Cidade != null && j.Cidade != "");
        if (!string.IsNullOrWhiteSpace(estado))
        {
            var uf = estado.Trim().ToUpper();
            qc = qc.Where(j => j.Estado != null && j.Estado.ToUpper() == uf);
        }
        var grafias = await qc.Select(j => j.Cidade!).Distinct().ToListAsync();

        return (estados, CidadesSemRepetir.Nomes(grafias));
    }

    public async Task<List<RankingCategoriaVM>> ObterRankingPorCategoriaAsync(
        string? categoriaNome = null, DateTime? ate = null, HashSet<int>? jogadoresFiltro = null, DateTime? de = null)
    {
        var duplas = await _context.Duplas
            .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
            .Include(d => d.Jogador1)
            .Include(d => d.Jogador2)
            .ToListAsync();

        // ⚠️ Conta ANTES dos filtros de período/categoria: o peso é o tamanho real da
        // categoria em que se jogou, e não "quantas duplas sobraram no recorte da tela".
        var duplasPorCategoria = ContarDuplasPorCategoria(duplas);

        var porCategoria = duplas
            .Where(d => d.Categoria != null
                     && ContaNoRanking(d.Categoria.Torneio)   // torneio restrito fica fora
                     // Quem não entrou no sorteio (lista de espera, sem parceiro) se
                     // inscreveu mas não jogou — e estava levando ponto de participação.
                     && !ForaDoSorteio.FicaDeFora(d)
                     && (categoriaNome == null || d.Categoria.Nome == categoriaNome)
                     && (ate == null || d.Categoria.Torneio == null
                         || d.Categoria.Torneio.DataInicio == null
                         || d.Categoria.Torneio.DataInicio <= ate)
                     && (de == null || d.Categoria.Torneio == null
                         || d.Categoria.Torneio.DataInicio == null
                         || d.Categoria.Torneio.DataInicio >= de))
            .GroupBy(d => d.Categoria.Nome);

        var resultado = new List<RankingCategoriaVM>();

        foreach (var grupo in porCategoria)
        {
            var acc = new Dictionary<int, RankingLinhaVM>();

            foreach (var dupla in grupo)
            {
                foreach (var jogador in new[] { dupla.Jogador1, dupla.Jogador2 })
                {
                    if (jogador == null) continue;
                    if (jogadoresFiltro != null && !jogadoresFiltro.Contains(jogador.Id)) continue; // filtro cidade/estado

                    if (!acc.TryGetValue(jogador.Id, out var linha))
                    {
                        linha = new RankingLinhaVM { Jogador = jogador };
                        acc[jogador.Id] = linha;
                    }

                    linha.Pontos += PontosDoTorneio.Pontos(
                        dupla.UltimaFase, duplasPorCategoria.GetValueOrDefault(dupla.CategoriaId),
                        dupla.Categoria.Torneio?.Status);
                    linha.Torneios += 1;
                    if (dupla.UltimaFase == "Campeao") linha.Titulos += 1;
                    if (dupla.UltimaFase == "Final") linha.Finais += 1;
                }
            }

            var linhas = acc.Values
                // ⚠️ QUEM TEM ZERO NÃO É RANQUEADO (decisão do Felipe, 10/08/2026, olhando a
                // produção). Ficou visível quando o ponto passou a exigir torneio começado: a
                // tela mostrava "1º lugar — Fulano — 0 pontos", e primeiro colocado com zero
                // ponto é uma frase que não se sustenta. As outras seis abas, sem resultado,
                // dizem "Ainda não há resultados de torneio"; só esta insistia em ranquear
                // quem não pontuou. Inscrição se vê na página do torneio — ranking é de ponto.
                .Where(l => l.Pontos > 0)
                .OrderByDescending(l => l.Pontos)
                .ThenByDescending(l => l.Titulos)
                .ThenByDescending(l => l.Finais)
                .ThenByDescending(l => l.Torneios)
                .ThenBy(l => l.Jogador.Nome)
                .ToList();

            // Categoria sem ninguém (pelo filtro de cidade/estado, ou porque ninguém pontuou
            // ainda) não é exibida vazia.
            if (linhas.Count > 0)
            {
                resultado.Add(new RankingCategoriaVM { Categoria = grupo.Key, Linhas = linhas });
            }
        }

        return resultado.OrderBy(r => r.Categoria).ToList();
    }

    // Ranking de times: cada ponto de torneio que um jogador conquista (PontosPorFase da
    // fase alcançada por cada dupla dele) soma para o time ao qual ele pertence.
    public async Task<List<RankingTimeVM>> ObterRankingTimesAsync(DateTime? ate = null, HashSet<int>? jogadoresFiltro = null)
    {
        var jogadores = await _context.Jogadores
            .Where(j => j.TimeId != null && j.Time != null)
            .Select(j => new { j.Id, TimeId = j.TimeId!.Value, TimeNome = j.Time!.Nome, TimeLogo = j.Time!.Logo })
            .ToListAsync();

        if (jogadoresFiltro != null) jogadores = jogadores.Where(j => jogadoresFiltro.Contains(j.Id)).ToList();
        if (jogadores.Count == 0) return new List<RankingTimeVM>();

        var idsComTime = jogadores.Select(j => j.Id).ToHashSet();

        var duplas = await _context.Duplas
            // ⚠️ Torneio fechado E AMERICANO ficam fora — a mesma régua do ranking individual,
            // por `DuplaContaNoRanking`. Aqui estava escrito só `!Restrito`, e o buraco não era
            // teórico: no Americano cada RODADA cria uma dupla nova, então um rodízio de 10
            // pessoas despejava 4 linhas de "participou" (40 pontos) por jogador no ranking de
            // times, sem ninguém ter chegado a final nenhuma.
            .Where(EstatisticasService.DuplaContaNoRanking)
            .Where(ForaDoSorteio.EstaNaChave)   // lista de espera e sem parceiro não jogaram
            .Where(d => d.NomeTime == null   // dupla-TIME não pontua jogador nenhum
                     && (idsComTime.Contains(d.Jogador1Id)
                         || (d.Jogador2Id != null && idsComTime.Contains(d.Jogador2Id.Value)))
                     && (ate == null || d.Categoria.Torneio.DataInicio == null
                         || d.Categoria.Torneio.DataInicio <= ate))
            .Select(d => new
            {
                d.Jogador1Id,
                d.Jogador2Id,
                d.UltimaFase,
                d.CategoriaId,
                Status = d.Categoria.Torneio.Status,
            })
            .ToListAsync();

        // O peso vem do tamanho da categoria — e a contagem NÃO pode sair das duplas acima,
        // que já vêm filtradas por "tem time". Numa categoria de 16 duplas com 3 jogadores de
        // time, o peso viraria o de uma categoria de 3.
        var duplasPorCategoria = await ContarDuplasPorCategoriaAsync(duplas.Select(d => d.CategoriaId));

        // Pontos/títulos por jogador (mesma pontuação do ranking individual).
        var porJogador = new Dictionary<int, (int pontos, int titulos)>();
        void Somar(int jogadorId, string? fase, int categoriaId, string? status)
        {
            if (!idsComTime.Contains(jogadorId)) return;
            var atual = porJogador.GetValueOrDefault(jogadorId);
            atual.pontos += PontosDoTorneio.Pontos(fase, duplasPorCategoria.GetValueOrDefault(categoriaId), status);
            if (fase == "Campeao") atual.titulos += 1;
            porJogador[jogadorId] = atual;
        }
        foreach (var d in duplas)
        {
            Somar(d.Jogador1Id, d.UltimaFase, d.CategoriaId, d.Status);
            if (d.Jogador2Id != null) Somar(d.Jogador2Id.Value, d.UltimaFase, d.CategoriaId, d.Status);
        }

        return jogadores
            .GroupBy(j => new { j.TimeId, j.TimeNome, j.TimeLogo })
            .Select(g =>
            {
                int pontos = 0, titulos = 0;
                foreach (var j in g)
                {
                    if (porJogador.TryGetValue(j.Id, out var p)) { pontos += p.pontos; titulos += p.titulos; }
                }
                return new RankingTimeVM
                {
                    TimeId = g.Key.TimeId,
                    Time = g.Key.TimeNome,
                    Logo = g.Key.TimeLogo,
                    Jogadores = g.Count(),
                    Pontos = pontos,
                    Titulos = titulos
                };
            })
            // ⚠️ MESMA RÉGUA DA ABA DE PONTOS: time com zero não é ranqueado. Sem isto, metade
            // da tela ficava consertada — produção listava 25 times, todos com 0, numerados de
            // 1º a 25º. A aba já tem o estado vazio pronto pra quando não sobrar ninguém.
            //
            // ⚠️ `TimesController` (a LISTA de times, outra tela) lê isto num dicionário com
            // `GetValueOrDefault`, então time filtrado aqui continua valendo 0 lá — a listagem
            // de times não perde ninguém.
            .Where(t => t.Pontos > 0)
            .OrderByDescending(t => t.Pontos).ThenByDescending(t => t.Titulos).ThenBy(t => t.Time)
            .ToList();
    }

    public async Task<List<PontosTimeTorneioVM>> ObterPontosTimesNoTorneioAsync(int torneioId)
    {
        // AMERICANO NÃO PONTUA TIME NENHUM (decisão do Felipe, 08/08/2026) — e a tela nem
        // oferece a aba. É a mesma régua do ranking oficial, pelo mesmo motivo: no Americano
        // o parceiro troca a cada rodada, cada rodada cria uma dupla nova, e "participou" ×
        // nº de rodadas virava placar de time. O primeiro Americano real de produção fechou
        // 120 × 120 × 80 pontos sem que ninguém tivesse ganho coisa alguma.
        var torneio = await _context.Torneios.FindAsync(torneioId);
        if (!ContaNoRanking(torneio)) return new List<PontosTimeTorneioVM>();

        var duplas = await _context.Duplas
            // Dupla-TIME fora: o Jogador1 dela é o organizador, e a campanha do time
            // inflaria os pontos do TIME DO ORGANIZADOR neste placar.
            .Where(d => d.Categoria.TorneioId == torneioId && d.NomeTime == null)
            .Where(ForaDoSorteio.EstaNaChave)   // lista de espera e sem parceiro não jogaram
            .Include(d => d.Jogador1).ThenInclude(j => j.Time)
            // `Jogador2!` porque a dupla pode não ter parceiro (inscrição sozinho). O `!` é seguro
            // aqui: o EF lê a expressão pra montar o JOIN, não executa o acesso — quem não tem
            // parceiro simplesmente não traz linha.
            .Include(d => d.Jogador2!).ThenInclude(j => j.Time)
            .ToListAsync();

        // ⚠️ Não dá pra contar a partir de `duplas`: ela já vem sem as dupla-TIME, mas ainda
        // traz lista de espera — e o peso tem uma definição só, que mora no contador.
        var duplasPorCategoria = await ContarDuplasPorCategoriaAsync(duplas.Select(d => d.CategoriaId));

        var acc = new Dictionary<int, PontosTimeTorneioVM>();
        var membros = new Dictionary<int, HashSet<int>>();

        void Somar(Jogador? j, string? fase, int categoriaId)
        {
            var time = j?.Time;
            if (j == null || time == null) return;
            if (!acc.TryGetValue(time.Id, out var vm))
            {
                vm = new PontosTimeTorneioVM { TimeId = time.Id, Time = time.Nome, Logo = time.Logo };
                acc[time.Id] = vm;
                membros[time.Id] = new HashSet<int>();
            }
            vm.Pontos += PontosDoTorneio.Pontos(
                // `?.` e não `!.`: `ContaNoRanking(null)` devolve TRUE de propósito (ver lá),
                // então um torneioId inexistente chega vivo até aqui.
                fase, duplasPorCategoria.GetValueOrDefault(categoriaId), torneio?.Status);
            if (membros[time.Id].Add(j.Id)) vm.Jogadores++;
        }

        foreach (var d in duplas)
        {
            Somar(d.Jogador1, d.UltimaFase, d.CategoriaId);
            Somar(d.Jogador2, d.UltimaFase, d.CategoriaId);
        }

        return acc.Values.OrderByDescending(x => x.Pontos).ThenBy(x => x.Time).ToList();
    }

    // Presets de período para as seções de vitórias/invencibilidade/troféus.
    private static string NormalizarPeriodo(string? periodo) => (periodo ?? "").Trim().ToLower() switch
    {
        "mes" => "mes",
        "ano" => "ano",
        _ => "sempre"
    };

    // Intervalo [de, ate] correspondente ao período. "sempre" => sem corte. ate = null (até agora).
    private static (DateTime? de, DateTime? ate) IntervaloPeriodo(string periodo)
    {
        var agora = DateTime.Now;
        return periodo switch
        {
            "mes" => (new DateTime(agora.Year, agora.Month, 1), (DateTime?)null),
            "ano" => (new DateTime(agora.Year, 1, 1), (DateTime?)null),
            _ => ((DateTime?)null, (DateTime?)null)
        };
    }

    public async Task<RankingHubVM> ObterRankingHubAsync(IEnumerable<string>? cidades = null, string? estado = null, string? periodo = null)
    {
        var vm = new RankingHubVM();

        // Filtro regional (país todo = sem cidade e sem estado). Vale para todas as seções.
        var filtro = await ObterJogadoresDoLocalAsync(cidades, estado);
        vm.Cidades = (cidades ?? Enumerable.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim()).Distinct().ToList();
        vm.Estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpper();

        // Período (só afeta vitórias/invencibilidade/troféus; pontos e times são sempre o total).
        vm.Periodo = NormalizarPeriodo(periodo);
        var (dePeriodo, _) = IntervaloPeriodo(vm.Periodo);

        // Todas as categorias cadastradas no sistema (para o dropdown da aba "Por categoria").
        vm.TodasCategorias = await _context.Categorias
            .Select(c => c.Nome).Distinct().OrderBy(n => n).ToListAsync();

        // Corte para o indicador de movimento (subiu/desceu) vs ~1 mês atrás.
        var corteMes = DateTime.Now.AddMonths(-1);

        // 1: ranking por categoria (pontos) — sempre o total, com movimento vs ~1 mês.
        var porCategoria = await ObterRankingPorCategoriaAsync(jogadoresFiltro: filtro);
        var porCategoriaAntes = await ObterRankingPorCategoriaAsync(ate: corteMes, jogadoresFiltro: filtro);
        AplicarMovimentoCategorias(porCategoria, porCategoriaAntes);
        vm.PorCategoria = porCategoria;

        // 2: troféus por categoria — RESPEITA o período (só títulos/finais de torneios no período).
        var baseTrofeus = dePeriodo == null
            ? porCategoria
            : await ObterRankingPorCategoriaAsync(jogadoresFiltro: filtro, de: dePeriodo);
        vm.TrofeusPorCategoria = baseTrofeus
            .Select(c => new RankingCategoriaVM
            {
                Categoria = c.Categoria,
                Linhas = c.Linhas
                    .Where(l => l.Titulos > 0 || l.Finais > 0)
                    .OrderByDescending(l => l.Titulos)
                    .ThenByDescending(l => l.Finais)
                    .ThenByDescending(l => l.Pontos)
                    .ToList()
            })
            .Where(c => c.Linhas.Count > 0)
            .ToList();

        // 3 a 8: derivados das partidas de torneio finalizadas (com vencedor definido).
        //
        // ⚠️ A MESMA RÉGUA DO RANKING POR CATEGORIA VALE AQUI. Ela estava aplicada na seção 1
        // (`porCategoria`) e no ranking de Times, mas NÃO nas seções que saem desta lista —
        // então Vitórias (jogadores), Vitórias (duplas) e Invencibilidade continuavam contando
        // Americano e torneio Restrito. É a régua duplicada de sempre: corrigiram uma cópia e
        // a outra seguiu contando.
        //
        // O estrago era visível em produção: o "Americano das Gurias" (10 jogadoras, 13 jogos)
        // ocupava as 8 primeiras posições do ranking geral de vitórias sozinho — um rodízio de
        // uma noite valendo mais que qualquer campanha de torneio de chave.
        //
        // ⚠️ Isto NÃO tira o Americano do perfil de ninguém: o título, os jogos e a
        // participação continuam lá, e o ponto dele vive no Ranking Americano, que é próprio.
        // O que sai é só a contagem do ranking OFICIAL.
        var partidas = (await CarregarPartidasFinalizadasAsync(incluirTorneio: true))
            .Where(p => ContaNoRanking(p.Categoria?.Torneio))
            .ToList();

        DateTime Ordem(Partida p) =>
            p.HorarioFimReal ?? p.HorarioInicioReal ?? p.HorarioPrevisto
            ?? p.Categoria?.Torneio?.DataInicio ?? DateTime.MinValue;

        // Acumuladores das SEÇÕES DE EXIBIÇÃO (vitórias/invencibilidade) — respeitam o período.
        var jog = new Dictionary<int, JogadorContagemVM>();
        var jogCat = new Dictionary<(string cat, int jid), JogadorContagemVM>();
        var dup = new Dictionary<int, DuplaContagemVM>();
        var jogSeq = new Dictionary<int, List<(DateTime ord, bool venceu)>>();
        var dupSeq = new Dictionary<int, List<(DateTime ord, bool venceu)>>();

        // Totais SEM período: vitórias por jogador+categoria (coluna da aba de pontos) e por
        // jogador (somadas por time no ranking de Times). Aba de pontos e Times não filtram período.
        var vitCatTotal = new Dictionary<(string cat, int jid), int>();
        var vitJogTotal = new Dictionary<int, int>();

        foreach (var p in partidas.OrderBy(Ordem).ThenBy(p => p.Id))
        {
            var cat = p.Categoria?.Nome ?? "—";
            var torneio = p.Categoria?.Torneio?.Nome ?? "—";
            var ord = Ordem(p);
            bool noPeriodo = dePeriodo == null || ord >= dePeriodo.Value;

            foreach (var (dupla, dId) in new[] { (p.Dupla1, p.Dupla1Id), (p.Dupla2, p.Dupla2Id) })
            {
                if (dupla == null) continue;

                // Filtro regional: a dupla entra se ao menos um dos jogadores for do local.
                // Jogador1 sempre existe: a chave estrangeira é obrigatória e a consulta faz
                // Include dele. Só Jogador2 é de verdade opcional (inscrição sem parceiro). O
                // teste de nulo no primeiro era engano, e fazia o compilador achar que a linha
                // seguinte podia guardar um jogador inexistente na tela do ranking.
                bool duplaNoFiltro = filtro == null
                    || filtro.Contains(dupla.Jogador1.Id)
                    || (dupla.Jogador2 != null && filtro.Contains(dupla.Jogador2.Id));
                if (!duplaNoFiltro) continue;

                bool venceu = p.VencedorId == dId;

                // Dupla (inscrição por torneio) — só entra nos acumuladores de exibição se no período.
                if (noPeriodo)
                {
                    if (!dup.TryGetValue(dId, out var dc))
                    {
                        dc = new DuplaContagemVM
                        {
                            Jogador1 = dupla.Jogador1,
                            Jogador2 = dupla.Jogador2,
                            Categoria = cat,
                            Torneio = torneio
                        };
                        dup[dId] = dc;
                    }
                    dc.Jogos++;
                    if (venceu) dc.Vitorias++;

                    if (!dupSeq.TryGetValue(dId, out var dseq)) { dseq = new(); dupSeq[dId] = dseq; }
                    dseq.Add((ord, venceu));
                }

                // Jogadores da dupla
                foreach (var jgd in new[] { dupla.Jogador1, dupla.Jogador2 })
                {
                    if (jgd == null) continue;
                    if (filtro != null && !filtro.Contains(jgd.Id)) continue; // filtro cidade/estado

                    // Totais sem período (para a aba de pontos e o ranking de times).
                    if (venceu)
                    {
                        vitJogTotal[jgd.Id] = vitJogTotal.GetValueOrDefault(jgd.Id) + 1;
                        var chaveTot = (cat, jgd.Id);
                        vitCatTotal[chaveTot] = vitCatTotal.GetValueOrDefault(chaveTot) + 1;
                    }

                    if (!noPeriodo) continue; // seções de exibição só contam partidas do período

                    if (!jog.TryGetValue(jgd.Id, out var jc)) { jc = new JogadorContagemVM { Jogador = jgd }; jog[jgd.Id] = jc; }
                    jc.Jogos++;
                    if (venceu) jc.Vitorias++;

                    var chaveCat = (cat, jgd.Id);
                    if (!jogCat.TryGetValue(chaveCat, out var jcc)) { jcc = new JogadorContagemVM { Jogador = jgd }; jogCat[chaveCat] = jcc; }
                    jcc.Jogos++;
                    if (venceu) jcc.Vitorias++;

                    if (!jogSeq.TryGetValue(jgd.Id, out var jseq)) { jseq = new(); jogSeq[jgd.Id] = jseq; }
                    jseq.Add((ord, venceu));
                }
            }
        }

        // Maior sequência de vitórias consecutivas (sem derrota no meio). Como não há
        // empate em partida de torneio, "invicto" == vitórias seguidas.
        static (int seq, DateTime? de, DateTime? ate) MaiorSequencia(List<(DateTime ord, bool venceu)> hist)
        {
            int best = 0, cur = 0;
            DateTime? bestDe = null, bestAte = null, curDe = null;
            foreach (var h in hist.OrderBy(x => x.ord))
            {
                if (h.venceu)
                {
                    if (cur == 0) curDe = h.ord;
                    cur++;
                    if (cur > best) { best = cur; bestDe = curDe; bestAte = h.ord; }
                }
                else { cur = 0; curDe = null; }
            }
            return (best, bestDe, bestAte);
        }

        // 3. Jogador com mais vitórias (geral)
        vm.VitoriasJogadores = jog.Values
            .Where(x => x.Vitorias > 0)
            .OrderByDescending(x => x.Vitorias).ThenByDescending(x => x.Jogos).ThenBy(x => x.Jogador.Nome)
            .Take(50).ToList();

        // 4. Jogador com mais vitórias por categoria
        vm.VitoriasJogadoresPorCategoria = jogCat
            .GroupBy(kv => kv.Key.cat)
            .Select(g => new CategoriaJogadoresVM
            {
                Categoria = g.Key,
                Jogadores = g.Select(x => x.Value)
                    .Where(x => x.Vitorias > 0)
                    .OrderByDescending(x => x.Vitorias).ThenByDescending(x => x.Jogos).ThenBy(x => x.Jogador.Nome)
                    .ToList()
            })
            .Where(c => c.Jogadores.Count > 0)
            .OrderBy(c => c.Categoria).ToList();

        // 5. Jogador com mais jogos invicto
        foreach (var kv in jogSeq)
        {
            var (seq, de, ate) = MaiorSequencia(kv.Value);
            if (seq <= 0) continue;
            vm.InvictosJogadores.Add(new InvictoJogadorVM { Jogador = jog[kv.Key].Jogador, Sequencia = seq, De = de, Ate = ate });
        }
        vm.InvictosJogadores = vm.InvictosJogadores
            .OrderByDescending(x => x.Sequencia).ThenBy(x => x.Jogador.Nome)
            .Take(50).ToList();

        // 6. Dupla com mais vitórias (geral)
        vm.VitoriasDuplas = dup.Values
            .Where(x => x.Vitorias > 0)
            .OrderByDescending(x => x.Vitorias).ThenByDescending(x => x.Jogos)
            .Take(50).ToList();

        // 7. Dupla com mais vitórias por categoria
        vm.VitoriasDuplasPorCategoria = dup.Values
            .Where(x => x.Vitorias > 0)
            .GroupBy(d => d.Categoria)
            .Select(g => new CategoriaDuplasVM
            {
                Categoria = g.Key,
                Duplas = g.OrderByDescending(x => x.Vitorias).ThenByDescending(x => x.Jogos).ToList()
            })
            .OrderBy(c => c.Categoria).ToList();

        // 8. Dupla com mais tempo invicta
        foreach (var kv in dupSeq)
        {
            var (seq, de, ate) = MaiorSequencia(kv.Value);
            var dc = dup[kv.Key];
            dc.SequenciaInvicta = seq;
            dc.De = de;
            dc.Ate = ate;
        }
        vm.InvictasDuplas = dup.Values
            .Where(x => x.SequenciaInvicta > 0)
            .OrderByDescending(x => x.SequenciaInvicta).ThenByDescending(x => x.Vitorias)
            .Take(50).ToList();

        // R5: coluna de vitórias (total, sem período) na aba de pontos "Por categoria".
        foreach (var c in vm.PorCategoria)
            foreach (var l in c.Linhas)
                l.Vitorias = vitCatTotal.GetValueOrDefault((c.Categoria, l.Jogador.Id));

        // 9. Nível comprovado saiu da página de ranking — agora aparece no perfil de cada jogador.

        // 10. Ranking de times (sempre o total) + movimento vs ~1 mês + vitórias somadas (R6).
        vm.Times = await ObterRankingTimesAsync(jogadoresFiltro: filtro);
        var timesAntes = await ObterRankingTimesAsync(ate: corteMes, jogadoresFiltro: filtro);
        AplicarMovimentoTimes(vm.Times, timesAntes);
        if (vm.Times.Count > 0)
        {
            var jogadoresComTime = await _context.Jogadores
                .Where(j => j.TimeId != null)
                .Select(j => new { j.Id, TimeId = j.TimeId!.Value })
                .ToListAsync();
            var vitPorTime = new Dictionary<int, int>();
            foreach (var j in jogadoresComTime)
                if (vitJogTotal.TryGetValue(j.Id, out var v))
                    vitPorTime[j.TimeId] = vitPorTime.GetValueOrDefault(j.TimeId) + v;
            foreach (var t in vm.Times)
                t.Vitorias = vitPorTime.GetValueOrDefault(t.TimeId);
        }

        return vm;
    }

    // Ranking de UM torneio, agregado por jogador: pontos (por fase das duplas dele nesse
    // torneio), jogos e vitórias (partidas finalizadas) e títulos (categorias em que foi campeão).
    public async Task<List<RankingTorneioLinhaVM>> ObterRankingDoTorneioAsync(int torneioId)
    {
        var duplas = await _context.Duplas
            .Include(d => d.Jogador1)
            .Include(d => d.Jogador2)
            .Where(d => d.Categoria.TorneioId == torneioId)
            .Where(ForaDoSorteio.EstaNaChave)   // lista de espera e sem parceiro não jogaram
            .ToListAsync();

        // O status decide se este torneio já paga ponto. Vem numa consulta própria porque as
        // duplas acima não trazem o torneio, e um Include só pra ler uma string custaria mais.
        var statusDoTorneio = await _context.Torneios
            .Where(t => t.Id == torneioId)
            .Select(t => t.Status)
            .FirstOrDefaultAsync();

        var acc = new Dictionary<int, RankingTorneioLinhaVM>();

        RankingTorneioLinhaVM Linha(Jogador? j)
        {
            if (j == null) return null!;
            if (!acc.TryGetValue(j.Id, out var l)) { l = new RankingTorneioLinhaVM { Jogador = j }; acc[j.Id] = l; }
            return l;
        }

        var duplasPorCategoria = ContarDuplasPorCategoria(duplas);

        foreach (var d in duplas)
        {
            int pts = PontosDoTorneio.Pontos(
                d.UltimaFase, duplasPorCategoria.GetValueOrDefault(d.CategoriaId), statusDoTorneio);
            bool campeao = d.UltimaFase == "Campeao";
            foreach (var j in new[] { d.Jogador1, d.Jogador2 })
            {
                var l = Linha(j);
                if (l == null) continue;
                l.Pontos += pts;
                if (campeao) l.Titulos += 1;
            }
        }

        // Jogos e vitórias vêm das partidas finalizadas desse torneio.
        var partidas = await _context.Partidas
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .Where(p => p.Categoria.TorneioId == torneioId && p.VencedorId != null)
            .ToListAsync();

        foreach (var p in partidas)
        {
            foreach (var (dupla, dId) in new[] { (p.Dupla1, p.Dupla1Id), (p.Dupla2, p.Dupla2Id) })
            {
                if (dupla == null) continue;
                bool venceu = p.VencedorId == dId;
                foreach (var j in new[] { dupla.Jogador1, dupla.Jogador2 })
                {
                    var l = Linha(j);
                    if (l == null) continue;
                    l.Jogos += 1;
                    if (venceu) l.Vitorias += 1;
                }
            }
        }

        return acc.Values
            // ⚠️ Mesma régua das outras abas: quem tem zero não é ranqueado. Aqui a VIEW já
            // prometia isso antes do código cumprir — o estado vazio dela diz "Esse torneio
            // ainda não tem jogadores PONTUANDO", e mesmo assim a lista trazia todo mundo com
            // 0 quando o torneio não tinha começado. Quem se inscreveu aparece na página do
            // torneio, que é onde inscrição se vê.
            //
            // ⚠️ Filtra por PONTO, não por jogos: num torneio cancelado depois de jogado, a
            // pessoa tem jogos e vitórias, e mesmo assim não pontua — o evento não aconteceu.
            .Where(l => l.Pontos > 0)
            .OrderByDescending(l => l.Pontos)
            .ThenByDescending(l => l.Titulos)
            .ThenByDescending(l => l.Vitorias)
            .ThenBy(l => l.Jogador.Nome)
            .ToList();
    }

    // Preenche RankingLinhaVM.Movimento comparando a posição em cada categoria agora vs "antes".
    private static void AplicarMovimentoCategorias(List<RankingCategoriaVM> agora, List<RankingCategoriaVM> antes)
    {
        bool temHistorico = antes.Any(c => c.Linhas.Count > 0);
        var posAntes = new Dictionary<(string cat, int jid), int>();
        foreach (var c in antes)
            for (int i = 0; i < c.Linhas.Count; i++)
                posAntes[(c.Categoria, c.Linhas[i].Jogador.Id)] = i + 1;

        foreach (var c in agora)
            for (int i = 0; i < c.Linhas.Count; i++)
            {
                var linha = c.Linhas[i];
                if (!temHistorico) { linha.Movimento = 0; continue; } // sem base de comparação
                linha.Movimento = posAntes.TryGetValue((c.Categoria, linha.Jogador.Id), out var pAntes)
                    ? pAntes - (i + 1)   // >0 subiu, <0 desceu, 0 igual
                    : (int?)null;        // novo no ranking
            }
    }

    // Preenche RankingTimeVM.Movimento comparando a posição global agora vs "antes".
    private static void AplicarMovimentoTimes(List<RankingTimeVM> agora, List<RankingTimeVM> antes)
    {
        bool temHistorico = antes.Count > 0;
        var posAntes = new Dictionary<int, int>();
        for (int i = 0; i < antes.Count; i++) posAntes[antes[i].TimeId] = i + 1;

        for (int i = 0; i < agora.Count; i++)
        {
            if (!temHistorico) { agora[i].Movimento = 0; continue; }
            agora[i].Movimento = posAntes.TryGetValue(agora[i].TimeId, out var pAntes)
                ? pAntes - (i + 1)
                : (int?)null;
        }
    }

    // Fases (UltimaFase da dupla) que "comprovam" nível, conforme o gatilho escolhido
    // pelo organizador. "Livre" => nenhuma (não trava).
    public static string[] FasesQueComprovam(string? modo) => modo switch
    {
        "SaiuChave" => new[] { "Quartas de Final", "Semifinal", "Final", "Campeao" },
        "Semifinal" => new[] { "Semifinal", "Final", "Campeao" },
        "Final" => new[] { "Final", "Campeao" },
        _ => Array.Empty<string>() // Livre ou desconhecido
    };

    // Frase curta do que o jogador fez naquela categoria (para a mensagem de bloqueio).
    public static string RotuloComprovacao(string? fase) => fase switch
    {
        "Campeao" => "foi campeão",
        "Final" => "chegou à final",
        "Semifinal" => "chegou à semifinal",
        "Quartas de Final" => "passou da fase de grupos",
        _ => "jogou"
    };

    // Nível comprovado (categoria prevista) de UM jogador, para exibir no perfil.
    // Null se ainda não comprovou nível em nenhuma categoria com tier reconhecido.
    public async Task<NivelComprovadoVM?> ObterNivelComprovadoJogadorAsync(int jogadorId, string modo = "Final")
    {
        var todos = await ObterNiveisComprovadosAsync(modo);
        return todos.TryGetValue(jogadorId, out var nivel) ? nivel : null;
    }

    public async Task<Dictionary<int, NivelComprovadoVM>> ObterNiveisComprovadosAsync(string modo = "Final")
    {
        var fasesComprovam = FasesQueComprovam(modo);
        if (fasesComprovam.Length == 0) return new Dictionary<int, NivelComprovadoVM>(); // Livre

        // Só resultados de torneio: o nível é comprovado pela UltimaFase da dupla.
        // Dupla-TIME fora: a campanha de um time não comprova nível de jogador nenhum.
        var duplas = await _context.Duplas
            .Include(d => d.Categoria)
            .Where(d => d.NomeTime == null
                     && d.Categoria != null && fasesComprovam.Contains(d.UltimaFase))
            .Select(d => new { d.Jogador1Id, d.Jogador2Id, d.UltimaFase, CategoriaNome = d.Categoria.Nome })
            .ToListAsync();

        var mapa = new Dictionary<int, NivelComprovadoVM>();

        void Aplicar(int jogadorId, string? fase, string categoriaNome)
        {
            int ordem = OrdemCategoria(categoriaNome);
            if (ordem == 0) return; // categoria sem tier reconhecido não trava nível

            if (!mapa.TryGetValue(jogadorId, out var atual) || ordem > atual.Ordem)
            {
                mapa[jogadorId] = new NivelComprovadoVM
                {
                    Categoria = categoriaNome,
                    Ordem = ordem,
                    MelhorFase = fase
                };
            }
        }

        foreach (var d in duplas)
        {
            Aplicar(d.Jogador1Id, d.UltimaFase, d.CategoriaNome);
            if (d.Jogador2Id != null) Aplicar(d.Jogador2Id.Value, d.UltimaFase, d.CategoriaNome);
        }

        return mapa;
    }

    public async Task<Dictionary<int, Dictionary<string, HistoricoCategoriaVM>>> ObterMelhoresColocacoesAsync(
        IEnumerable<string> categoriaNomes, int? excluirTorneioId = null)
    {
        var nomes = categoriaNomes.ToHashSet();
        if (nomes.Count == 0) return new Dictionary<int, Dictionary<string, HistoricoCategoriaVM>>();

        var registros = await _context.Duplas
            .Include(d => d.Categoria)
            .Where(d => d.NomeTime == null   // campanha de time não é colocação de jogador
                     && nomes.Contains(d.Categoria.Nome)
                     && (excluirTorneioId == null || d.Categoria.TorneioId != excluirTorneioId))
            .Select(d => new { d.Jogador1Id, d.Jogador2Id, d.UltimaFase, CategoriaNome = d.Categoria.Nome })
            .ToListAsync();

        var mapa = new Dictionary<int, Dictionary<string, HistoricoCategoriaVM>>();

        void Aplicar(int jogadorId, string? fase, string categoriaNome)
        {
            var (tierChave, tierNome, icone, corFundo, corTexto) = TierDaCategoria(categoriaNome);

            if (!mapa.TryGetValue(jogadorId, out var porTier))
            {
                porTier = new Dictionary<string, HistoricoCategoriaVM>();
                mapa[jogadorId] = porTier;
            }
            if (!porTier.TryGetValue(tierChave, out var hist))
            {
                hist = new HistoricoCategoriaVM
                {
                    MelhorFase = "Grupos",
                    Titulos = 0,
                    Tier = tierChave,
                    TierNome = tierNome,
                    IconeTier = icone,
                    CorFundoTier = corFundo,
                    CorTextoTier = corTexto
                };
                porTier[tierChave] = hist;
            }
            if (RankFase(fase) > RankFase(hist.MelhorFase)) hist.MelhorFase = fase ?? "Grupos";
            if (fase == "Campeao") hist.Titulos += 1;
        }

        foreach (var r in registros)
        {
            Aplicar(r.Jogador1Id, r.UltimaFase, r.CategoriaNome);
            if (r.Jogador2Id != null) Aplicar(r.Jogador2Id.Value, r.UltimaFase, r.CategoriaNome);
        }

        return mapa;
    }

    public async Task<List<ConfrontoResumoVM>> ObterConfrontosAsync(int jogadorId)
    {
        var acc = new Dictionary<int, ConfrontoResumoVM>();

        void Somar(Jogador? oponente, bool? venci)
        {
            if (oponente == null || oponente.Id == jogadorId) return;
            if (!acc.TryGetValue(oponente.Id, out var resumo))
            {
                resumo = new ConfrontoResumoVM { Oponente = oponente };
                acc[oponente.Id] = resumo;
            }
            resumo.Jogos += 1;
            if (venci == true) resumo.Vitorias += 1;
            else if (venci == false) resumo.Derrotas += 1;
        }

        var partidas = await CarregarPartidasFinalizadasAsync();
        foreach (var p in partidas)
        {
            var (minhaDupla, oppDupla) = LocalizarDuplas(p, jogadorId);
            if (minhaDupla == null || oppDupla == null) continue;

            bool venci = p.VencedorId == minhaDupla.Id;
            Somar(oppDupla.Jogador1, venci);
            Somar(oppDupla.Jogador2, venci);
        }

        var jogosSemanais = await CarregarJogosSemanaisAsync(jogadorId);
        foreach (var j in jogosSemanais)
        {
            var (meuLado, oponentes) = LocalizarLadoJogoSemanal(j, jogadorId);
            if (meuLado == 0) continue;

            bool? venci = j.VencedorLado == 0 ? null : (j.VencedorLado == meuLado);
            Somar(oponentes.Item1, venci);
            Somar(oponentes.Item2, venci);
        }

        return acc.Values
            .OrderByDescending(c => c.Jogos)
            .ThenByDescending(c => c.Vitorias)
            .ToList();
    }

    public async Task<List<ParceiroResumoVM>> ObterParceirosAsync(int jogadorId)
    {
        var acc = new Dictionary<int, ParceiroResumoVM>();

        void Somar(Jogador? parceiro, bool? venci)
        {
            if (parceiro == null || parceiro.Id == jogadorId) return;
            if (!acc.TryGetValue(parceiro.Id, out var resumo))
            {
                resumo = new ParceiroResumoVM { Parceiro = parceiro };
                acc[parceiro.Id] = resumo;
            }
            resumo.Jogos += 1;
            if (venci == true) resumo.Vitorias += 1;
        }

        var partidas = await CarregarPartidasFinalizadasAsync();
        foreach (var p in partidas)
        {
            var (minhaDupla, _) = LocalizarDuplas(p, jogadorId);
            if (minhaDupla == null) continue;

            bool venci = p.VencedorId == minhaDupla.Id;
            var parceiro = minhaDupla.Jogador1Id == jogadorId ? minhaDupla.Jogador2 : minhaDupla.Jogador1;
            Somar(parceiro, venci);
        }

        var jogosSemanais = await CarregarJogosSemanaisAsync(jogadorId);
        foreach (var j in jogosSemanais)
        {
            var (meuLado, _) = LocalizarLadoJogoSemanal(j, jogadorId);
            if (meuLado == 0) continue;

            bool? venci = j.VencedorLado == 0 ? null : (j.VencedorLado == meuLado);
            var parceiro = meuLado == 1
                ? (j.Dupla1Jogador1Id == jogadorId ? j.Dupla1Jogador2 : j.Dupla1Jogador1)
                : (j.Dupla2Jogador1Id == jogadorId ? j.Dupla2Jogador2 : j.Dupla2Jogador1);
            Somar(parceiro, venci);
        }

        return acc.Values
            .OrderByDescending(p => p.Jogos)
            .ThenByDescending(p => p.Vitorias)
            .ToList();
    }

    public async Task<DestaquesJogadorVM> ObterDestaquesAsync(int jogadorId)
    {
        var parceiros = await ObterParceirosAsync(jogadorId);
        var confrontos = await ObterConfrontosAsync(jogadorId);
        return MontarDestaques(parceiros, confrontos);
    }

    // Monta os destaques a partir de listas já calculadas (evita recarregar partidas quando
    // a tela já tem parceiros/confrontos em mãos, como em Jogadores/Perfil).
    public static DestaquesJogadorVM MontarDestaques(
        List<ParceiroResumoVM> parceiros, List<ConfrontoResumoVM> confrontos)
    {
        return new DestaquesJogadorVM
        {
            MaisJogouJunto = parceiros
                .OrderByDescending(p => p.Jogos).ThenByDescending(p => p.Vitorias)
                .FirstOrDefault(),
            MaisEnfrentou = confrontos
                .OrderByDescending(c => c.Jogos).ThenByDescending(c => c.Vitorias)
                .FirstOrDefault(),
            MaisTeVenceu = confrontos
                .Where(c => c.Derrotas > 0)
                .OrderByDescending(c => c.Derrotas).ThenByDescending(c => c.Jogos)
                .FirstOrDefault(),
            VoceMaisVenceu = confrontos
                .Where(c => c.Vitorias > 0)
                .OrderByDescending(c => c.Vitorias).ThenByDescending(c => c.Jogos)
                .FirstOrDefault(),
        };
    }

    public async Task<List<ConquistaVM>> ObterConquistasAsync(int jogadorId)
    {
        var jogador = await _context.Jogadores.FindAsync(jogadorId);
        if (jogador == null) return new List<ConquistaVM>();

        bool temDupla = await _context.Duplas.AnyAsync(d =>
            d.NomeTime == null && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId));
        int totalJogosSemanais = await _context.JogosSemanais.CountAsync(j =>
            j.Dupla1Jogador1Id == jogadorId || j.Dupla1Jogador2Id == jogadorId ||
            j.Dupla2Jogador1Id == jogadorId || j.Dupla2Jogador2Id == jogadorId);
        bool ehOrganizador = await _context.TorneioOrganizadores.AnyAsync(o => o.JogadorId == jogadorId);
        int elogiosRecebidos = await _context.Elogios.CountAsync(e => e.ParaJogadorId == jogadorId);
        int aulasComoAluno = await _context.Aulas.CountAsync(a => a.AlunoId == jogadorId && a.Status == "Realizada");
        var resumo = await ObterResumoJogadorAsync(jogadorId);

        // Aqui só se COLETA; a regra de cada conquista mora no CatalogoConquistas, puro.
        return CatalogoConquistas.Montar(new DadosParaConquistas(
            JogouAlgumaVez: temDupla,
            JogosSemanais: totalJogosSemanais,
            EhOrganizador: ehOrganizador,
            TemTime: jogador.TimeId != null,
            EhProfessor: jogador.IsProfessor,
            Titulos: resumo.Titulos,
            Finais: resumo.Finais,
            TotalTorneios: resumo.TotalTorneios,
            Vitorias: resumo.Vitorias,
            ElogiosRecebidos: elogiosRecebidos,
            AulasComoAluno: aulasComoAluno));
    }

    public async Task<HeadToHeadVM> ObterHeadToHeadAsync(int jogadorId, int oponenteId)
    {
        var eu = await _context.Jogadores.FindAsync(jogadorId);
        var oponente = await _context.Jogadores.FindAsync(oponenteId);
        var vm = new HeadToHeadVM { Eu = eu!, Oponente = oponente! };

        var partidas = await CarregarPartidasFinalizadasAsync(incluirTorneio: true);

        foreach (var p in partidas)
        {
            var (minhaDupla, oppDupla) = LocalizarDuplas(p, jogadorId);
            if (minhaDupla == null || oppDupla == null) continue;

            // Só conta se o oponente específico estava na dupla adversária.
            bool oponenteNaOutraDupla = oppDupla.Jogador1Id == oponenteId || oppDupla.Jogador2Id == oponenteId;
            if (!oponenteNaOutraDupla) continue;

            bool venci = p.VencedorId == minhaDupla.Id;
            vm.Jogos += 1;
            if (venci) vm.Vitorias += 1; else vm.Derrotas += 1;

            int meusSets = minhaDupla.Id == p.Dupla1Id ? (p.SetsDupla1 ?? 0) : (p.SetsDupla2 ?? 0);
            int oppSets = minhaDupla.Id == p.Dupla1Id ? (p.SetsDupla2 ?? 0) : (p.SetsDupla1 ?? 0);
            int meusGames = minhaDupla.Id == p.Dupla1Id ? (p.GamesDupla1 ?? 0) : (p.GamesDupla2 ?? 0);
            int oppGames = minhaDupla.Id == p.Dupla1Id ? (p.GamesDupla2 ?? 0) : (p.GamesDupla1 ?? 0);

            vm.Partidas.Add(new ConfrontoPartidaVM
            {
                Data = p.HorarioFimReal ?? p.HorarioPrevisto ?? p.Categoria?.Torneio?.DataInicio,
                Torneio = p.Categoria?.Torneio?.Nome ?? "-",
                Categoria = p.Categoria?.Nome ?? "-",
                Fase = p.Fase,
                MinhaDupla = NomesDupla(minhaDupla),
                DuplaOponente = NomesDupla(oppDupla),
                Placar = (meusSets + oppSets) > 0 ? $"{meusSets} x {oppSets} ({meusGames}/{oppGames})" : $"{meusGames} x {oppGames}",
                EuVenci = venci
            });
        }

        vm.Partidas = vm.Partidas.OrderByDescending(x => x.Data).ToList();
        return vm;
    }

    public async Task<ResumoJogadorVM> ObterResumoJogadorAsync(int jogadorId)
    {
        // A fase de cada participação + se aquele torneio conta ponto. O torneio restrito e o
        // Americano continuam na CONTA de torneios/títulos (aconteceram, e o troféu do
        // Americano fica no perfil), mas não somam PONTO — senão o número do perfil
        // discordaria do número do ranking.
        //
        // ⚠️ Aqui estava escrito só `!Restrito`, e o Americano pontuava: a campeã do
        // "Americano das Gurias" aparecia com 170 pts no perfil por um rodízio de uma noite.
        var participacoes = await _context.Duplas
            // ⚠️ Quem ficou na LISTA DE ESPERA ou SEM PARCEIRO sai daqui inteiro — não só dos
            // pontos, também da conta de torneios. Os dois se inscreveram e nenhum dos dois
            // jogou; contá-los como "torneio disputado" no perfil seria a mesma mentira que
            // pagar ponto por eles. A régua tem UM dono (ForaDoSorteio), e reescrevê-la como
            // flag nesta projeção seria a terceira cópia da mesma frase.
            .Where(ForaDoSorteio.EstaNaChave)
            .Where(d => d.NomeTime == null   // time não é participação do organizador
                     && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId))
            .Select(d => new
            {
                d.UltimaFase,
                d.Categoria.TorneioId,
                d.CategoriaId,
                Restrito = d.Categoria.Torneio.Restrito,
                Formato = d.Categoria.Torneio.Formato,
                Status = d.Categoria.Torneio.Status,
            })
            .ToListAsync();

        var fases = participacoes.Select(p => p.UltimaFase).ToList();
        var duplasPorCategoria = await ContarDuplasPorCategoriaAsync(participacoes.Select(p => p.CategoriaId));

        int vitorias = 0, derrotas = 0;
        var partidas = (await CarregarPartidasFinalizadasAsync(incluirTorneio: true))
            // Mesma régua das vitórias do ranking: sem isto, o card de vitórias do perfil
            // contava rodízio e o do ranking não — dois números pra mesma pergunta.
            .Where(p => ContaNoRanking(p.Categoria?.Torneio));
        foreach (var p in partidas)
        {
            var (minhaDupla, oppDupla) = LocalizarDuplas(p, jogadorId);
            if (minhaDupla == null || oppDupla == null) continue;

            if (p.VencedorId == minhaDupla.Id) vitorias++; else derrotas++;
        }

        return new ResumoJogadorVM
        {
            Pontos = participacoes
                .Where(p => ContaNoRanking(p.Restrito, p.Formato))
                .Sum(p => PontosDoTorneio.Pontos(
                    p.UltimaFase, duplasPorCategoria.GetValueOrDefault(p.CategoriaId), p.Status)),

            // ⚠️ Torneios DISTINTOS, não linhas de dupla. No Americano cada RODADA cria uma
            // dupla nova, então um rodízio de uma noite se anunciava como "8 torneio(s)" no
            // perfil de quem jogou UM. No Padrão o efeito era menor e igualmente errado: quem
            // entra em duas categorias do mesmo torneio contava dois.
            TotalTorneios = participacoes.Select(p => p.TorneioId).Distinct().Count(),
            Titulos = fases.Count(f => f == "Campeao"),
            Finais = fases.Count(f => f == "Final"),
            Semis = fases.Count(f => f == "Semifinal"),
            Quartas = fases.Count(f => f == "Quartas de Final"),
            CaiuNaChave = fases.Count(f => f == "Quartas de Final" || f == "Semifinal" || f == "Final"),
            Vitorias = vitorias,
            Derrotas = derrotas
        };
    }

    // Pontos reais de vários jogadores numa consulta só — evita repetir esse mesmo loop
    // em cada tela que precisa de pontuação (busca, sorteio de chaves...).
    public async Task<Dictionary<int, int>> ObterPontosPorJogadorAsync(IEnumerable<int> jogadorIds)
    {
        var ids = jogadorIds.Distinct().ToHashSet();
        var pontos = ids.ToDictionary(id => id, _ => 0);
        if (ids.Count == 0) return pontos;

        var duplas = await _context.Duplas
            // ⚠️ `DuplaContaNoRanking` e não `!Restrito` na mão: escrita à mão, esta consulta
            // esquecia o Americano — e ela alimenta a BUSCA de jogadores e o sorteio de
            // chaves, então o rodízio inflava o número que o organizador usa pra montar as
            // chaves e pra julgar se alguém está se inscrevendo numa categoria fraca demais.
            .Where(EstatisticasService.DuplaContaNoRanking)
            .Where(ForaDoSorteio.EstaNaChave)   // lista de espera e sem parceiro não jogaram
            .Where(d => d.NomeTime == null
                     && (ids.Contains(d.Jogador1Id)
                         || (d.Jogador2Id != null && ids.Contains(d.Jogador2Id.Value))))
            .Select(d => new
            {
                d.Jogador1Id,
                d.Jogador2Id,
                d.UltimaFase,
                d.CategoriaId,
                Status = d.Categoria.Torneio.Status,
            })
            .ToListAsync();

        // ⚠️ De novo: a contagem NÃO sai de `duplas`, que aqui vem filtrada pelos jogadores
        // pedidos. Uma busca por um nome só faria toda categoria dele parecer ter 1 dupla.
        var duplasPorCategoria = await ContarDuplasPorCategoriaAsync(duplas.Select(d => d.CategoriaId));

        foreach (var d in duplas)
        {
            int p = PontosDoTorneio.Pontos(
                d.UltimaFase, duplasPorCategoria.GetValueOrDefault(d.CategoriaId), d.Status);
            if (pontos.ContainsKey(d.Jogador1Id)) pontos[d.Jogador1Id] += p;
            if (d.Jogador2Id != null && pontos.ContainsKey(d.Jogador2Id.Value)) pontos[d.Jogador2Id.Value] += p;
        }

        return pontos;
    }

    // Evolução mês a mês. Os pontos de um torneio contam no mês em que ele COMEÇOU
    // (Torneio.DataInicio) — é a única data que o torneio tem. Torneio sem data fica de fora
    // do gráfico, mas continua somando no total do perfil.
    public async Task<EvolucaoJogadorVM> ObterEvolucaoJogadorAsync(int jogadorId, int meses = 12)
    {
        var mesAtual = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var primeiroMes = mesAtual.AddMonths(-(meses - 1));

        // Torneio restrito fora: o gráfico desenha a linha do RANKING, e ela precisa
        // terminar no mesmo total que o perfil mostra.
        var participacoes = await _context.Duplas
            .Where(ForaDoSorteio.EstaNaChave)   // lista de espera e sem parceiro não jogaram
            .Where(d => d.NomeTime == null
                     && !d.Categoria.Torneio.Restrito
                     && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId))
            .Select(d => new
            {
                Data = d.Categoria.Torneio.DataInicio,
                d.UltimaFase,
                d.CategoriaId,
                Status = d.Categoria.Torneio.Status,
            })
            .ToListAsync();

        // A linha do gráfico tem que terminar no MESMO total que o perfil mostra, então ela
        // usa exatamente a mesma conta — inclusive o peso.
        var duplasPorCategoria = await ContarDuplasPorCategoriaAsync(participacoes.Select(p => p.CategoriaId));

        // ⚠️ A janela ESTICADA PRO FUTURO foi removida em 10/08/2026, e a remoção é a prova de
        // que o defeito era real. Ela existia porque o ranking pagava participação já na
        // INSCRIÇÃO: um torneio marcado pra dezembro entrava no total do perfil hoje, e o
        // gráfico precisava correr atrás até o torneio mais distante pra não terminar abaixo
        // do total exibido. Agora ponto só nasce com o torneio começado — nada no futuro
        // soma, e a linha termina no mês atual, que é onde um gráfico de evolução termina.
        int totalMeses = meses;

        // Tudo que aconteceu antes da janela já entra como saldo inicial do acumulado,
        // senão a linha começaria em zero e daria a impressão de que o jogador regrediu.
        int acumulado = participacoes
            .Where(p => p.Data != null && p.Data.Value < primeiroMes)
            .Sum(p => PontosDoTorneio.Pontos(
                p.UltimaFase, duplasPorCategoria.GetValueOrDefault(p.CategoriaId), p.Status));

        var naJanela = participacoes.Where(p => p.Data != null && p.Data.Value >= primeiroMes).ToList();

        var vm = new EvolucaoJogadorVM();
        for (int i = 0; i < totalMeses; i++)
        {
            var mes = primeiroMes.AddMonths(i);
            var fim = mes.AddMonths(1);
            var doMes = naJanela.Where(p => p.Data!.Value >= mes && p.Data.Value < fim).ToList();

            int ganhos = doMes.Sum(p => PontosDoTorneio.Pontos(
                p.UltimaFase, duplasPorCategoria.GetValueOrDefault(p.CategoriaId), p.Status));
            acumulado += ganhos;

            vm.Meses.Add(new MesEvolucaoVM
            {
                Mes = mes,
                Pontos = ganhos,
                Acumulado = acumulado,
                Torneios = doMes.Count,
                Titulos = doMes.Count(p => p.UltimaFase == "Campeao"),
            });
        }

        return vm;
    }

    // Primeiros passos do jogador novo. A ordem importa: cada passo só faz sentido depois do
    // anterior — sem perfil preenchido o app não sabe o que sugerir; sem seguir ninguém o
    // feed fica vazio; e a inscrição é o que finalmente coloca a pessoa dentro de um jogo.
    public async Task<OnboardingVM> ObterOnboardingAsync(int jogadorId)
    {
        var jogador = await _context.Jogadores.FindAsync(jogadorId);
        if (jogador == null) return new OnboardingVM();

        bool perfilCompleto =
            !string.IsNullOrWhiteSpace(jogador.FotoPerfil)
            && !string.IsNullOrWhiteSpace(jogador.Cidade)
            && !string.IsNullOrWhiteSpace(jogador.LadoQuadra);

        bool temCategoria = await _context.JogadorCategorias.AnyAsync(c => c.JogadorId == jogadorId);
        bool segueAlguem = await _context.SeguidoresJogador.AnyAsync(s => s.SeguidorId == jogadorId);
        bool temInscricao = await _context.Duplas
                .AnyAsync(d => d.NomeTime == null && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId))
            || await _context.InscricoesAmericanas.AnyAsync(i => i.JogadorId == jogadorId);
        // Duas provas de que o app está instalado, e qualquer uma serve:
        //   InstalouAppEm — o navegador abriu em modo app e avisou (AppInstaladoController).
        //   PushSubscription — liberou notificação, o que no iPhone só dá instalado.
        //
        // Era só a segunda até 03/08/2026, e isso media a coisa errada: quem instalava sem
        // liberar aviso continuava sendo cobrado pra instalar o que já tinha instalado.
        bool instalouApp = jogador.InstalouAppEm != null
            || await _context.PushSubscriptionsJogador.AnyAsync(s => s.JogadorId == jogadorId);

        return new OnboardingVM
        {
            Passos = new List<PassoOnboardingVM>
            {
                new()
                {
                    Titulo = "Complete seu perfil",
                    Explicacao = "Foto, cidade e de que lado você joga — é assim que te acham pra formar dupla.",
                    Icone = "bi-person-badge",
                    TextoBotao = "Completar perfil",
                    Controller = "Auth", Action = "EditarPerfil",
                    Concluido = perfilCompleto,
                },
                new()
                {
                    Titulo = "Diga sua categoria",
                    Explicacao = "Serve pra te convidarem pros jogos do seu nível.",
                    Icone = "bi-bar-chart-steps",
                    TextoBotao = "Escolher categoria",
                    Controller = "Auth", Action = "Preferencias",
                    Concluido = temCategoria,
                },
                new()
                {
                    Titulo = "Siga outros jogadores",
                    Explicacao = "Você fica sabendo quando eles se inscrevem num torneio ou ganham um jogo.",
                    Icone = "bi-person-plus",
                    TextoBotao = "Buscar jogadores",
                    Controller = "Jogadores", Action = "Buscar",
                    Concluido = segueAlguem,
                },
                new()
                {
                    Titulo = "Entre num torneio",
                    Explicacao = "É onde você começa a pontuar no ranking.",
                    Icone = "bi-trophy",
                    TextoBotao = "Ver torneios",
                    Controller = "Torneios", Action = "Index",
                    Concluido = temInscricao,
                },
                new()
                {
                    Titulo = "Instale o app no celular",
                    Explicacao = "No iPhone: botão de compartilhar → \"Adicionar à Tela de Início\". No Android o próprio navegador oferece.",
                    Icone = "bi-phone",
                    Concluido = instalouApp,
                },
            }
        };
    }

    // ---------- helpers ----------

    // "SEU ANO NO PADEL" — a COLETA. A regra mora em Services/RetrospectivaDoAno, puro.
    //
    // ⚠️ Conta jogo de torneio E jogo de grupo, e isso é decisão (ver o cabeçalho de lá): a
    // pergunta aqui é "quanto padel você jogou", não "quem é melhor". Só os PONTOS saem da
    // régua oficial — um número chamado "pontos" que discordasse do ranking seria a segunda
    // versão da mesma verdade.
    public async Task<RetrospectivaVM> ObterRetrospectivaAsync(int jogadorId, int ano)
    {
        var jogador = await _context.Jogadores.FindAsync(jogadorId);
        if (jogador == null)
        {
            return new RetrospectivaVM { Ano = ano, Jogador = new Jogador { Nome = "" } };
        }

        var inicio = new DateTime(ano, 1, 1);
        var fim = inicio.AddYears(1);

        var jogos = new List<JogoDoAno>();

        // ── Jogos de torneio ──────────────────────────────────────────────────────────────
        // Consulta própria (e não `CarregarPartidasFinalizadasAsync`) porque aqui se filtra por
        // jogador e por ano; o loader compartilhado traz o sistema inteiro.
        //
        // ⚠️ O CLUBE NÃO ENTRA POR `Include`, e isso não é estilo. `Torneio.ClubeId` é
        // obrigatório, e navegação obrigatória o EF traduz como INNER JOIN — um torneio sem
        // clube casando faria a PARTIDA sumir da retrospectiva, calada. Aconteceu de verdade no
        // card de campeão (o torneio inteiro desaparecia), e foi um teste que pegou. O nome do
        // clube vem depois, por um dicionário, onde a falta dele custa no máximo uma legenda.
        var partidas = await _context.Partidas
            .AsNoTracking()
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .Include(p => p.Categoria).ThenInclude(c => c.Torneio)
            .Where(p => p.VencedorId != null
                     && (p.Dupla1.Jogador1Id == jogadorId || p.Dupla1.Jogador2Id == jogadorId
                      || p.Dupla2.Jogador1Id == jogadorId || p.Dupla2.Jogador2Id == jogadorId))
            .ToListAsync();

        var clubeDoTorneio = await ClubesPorIdAsync(
            partidas.Select(p => p.Categoria?.Torneio?.ClubeId).Where(id => id != null).Select(id => id!.Value));

        foreach (var p in partidas)
        {
            var (minhaDupla, oponente) = LocalizarDuplas(p, jogadorId);
            if (minhaDupla == null || oponente == null) continue;

            // A MESMA escada de data do hub de ranking: o jogo aconteceu quando terminou; sem
            // isso, cai pro previsto e por fim pra abertura do torneio.
            var quando = p.HorarioFimReal ?? p.HorarioInicioReal ?? p.HorarioPrevisto
                      ?? p.Categoria?.Torneio?.DataInicio;
            if (quando == null || quando < inicio || quando >= fim) continue;

            var parceiro = minhaDupla.Jogador1Id == jogadorId ? minhaDupla.Jogador2 : minhaDupla.Jogador1;

            jogos.Add(new JogoDoAno(
                Quando: quando.Value,
                Venceu: p.VencedorId == minhaDupla.Id,
                ParceiroId: parceiro?.Id,
                ParceiroNome: parceiro?.ComoChamar,
                Clube: p.Categoria?.Torneio == null
                    ? null
                    : clubeDoTorneio.GetValueOrDefault(p.Categoria.Torneio.ClubeId),
                DeTorneio: true));
        }

        // ── Jogos de grupo (a panelinha de quinta) ────────────────────────────────────────
        var semanais = await _context.JogosSemanais
            .AsNoTracking()
            .Include(j => j.Dupla1Jogador1).Include(j => j.Dupla1Jogador2)
            .Include(j => j.Dupla2Jogador1).Include(j => j.Dupla2Jogador2)
            .Where(j => j.DataJogo >= inicio && j.DataJogo < fim
                     && (j.Dupla1Jogador1Id == jogadorId || j.Dupla1Jogador2Id == jogadorId
                      || j.Dupla2Jogador1Id == jogadorId || j.Dupla2Jogador2Id == jogadorId))
            .ToListAsync();

        // O clube do jogo de grupo é opcional (`ClubeId` é anulável), então aqui um `Include`
        // seria seguro — mas o nome vem do mesmo dicionário do torneio, pra haver UM jeito só
        // de resolver clube nesta função em vez de dois com garantias diferentes.
        var clubeDoJogo = await ClubesPorIdAsync(
            semanais.Select(j => j.ClubeId).Where(id => id != null).Select(id => id!.Value));

        foreach (var j in semanais)
        {
            var (meuLado, _) = LocalizarLadoJogoSemanal(j, jogadorId);
            if (meuLado == 0) continue;

            var parceiro = meuLado == 1
                ? (j.Dupla1Jogador1Id == jogadorId ? j.Dupla1Jogador2 : j.Dupla1Jogador1)
                : (j.Dupla2Jogador1Id == jogadorId ? j.Dupla2Jogador2 : j.Dupla2Jogador1);

            jogos.Add(new JogoDoAno(
                Quando: j.DataJogo,
                // ⚠️ Nulo no empate em games, que só existe aqui — o torneio sempre tem um
                // vencedor. Tratar empate como derrota mentiria no aproveitamento.
                Venceu: j.VencedorLado == 0 ? null : j.VencedorLado == meuLado,
                ParceiroId: parceiro?.Id,
                ParceiroNome: parceiro?.ComoChamar,
                Clube: j.ClubeId == null ? null : clubeDoJogo.GetValueOrDefault(j.ClubeId.Value),
                DeTorneio: false));
        }

        // ── Campanhas de torneio do ano (torneios, títulos, finais e PONTOS) ──────────────
        // ⚠️ `ForaDoSorteio.EstaNaChave` e `NomeTime == null`, a mesma régua do resumo do
        // perfil: quem ficou na lista de espera ou sem parceiro se inscreveu e não jogou, e a
        // linha de TIME tem o organizador no Jogador1Id.
        var participacoes = await _context.Duplas
            .AsNoTracking()
            .Where(ForaDoSorteio.EstaNaChave)
            .Where(d => d.NomeTime == null
                     && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId)
                     && d.Categoria.Torneio.DataInicio >= inicio
                     && d.Categoria.Torneio.DataInicio < fim)
            .Select(d => new
            {
                d.UltimaFase,
                d.CategoriaId,
                d.Categoria.TorneioId,
                Restrito = d.Categoria.Torneio.Restrito,
                Formato = d.Categoria.Torneio.Formato,
                Status = d.Categoria.Torneio.Status,
            })
            .ToListAsync();

        var duplasPorCategoria = await ContarDuplasPorCategoriaAsync(participacoes.Select(p => p.CategoriaId));

        // ── Melhor posição no ranking, DENTRO do ano ──────────────────────────────────────
        // Usa o próprio ranking por categoria com a janela do ano, pra a posição do card ser
        // exatamente a que a tela de ranking mostraria naquele recorte.
        int? melhorPosicao = null;
        string? categoriaDaPosicao = null;
        foreach (var categoria in await ObterRankingPorCategoriaAsync(de: inicio, ate: fim.AddDays(-1)))
        {
            var posicao = categoria.Linhas.FindIndex(l => l.Jogador.Id == jogadorId);
            if (posicao < 0) continue;
            if (melhorPosicao == null || posicao + 1 < melhorPosicao)
            {
                melhorPosicao = posicao + 1;
                categoriaDaPosicao = categoria.Categoria;
            }
        }

        return RetrospectivaDoAno.Montar(new DadosDaRetrospectiva(
            Ano: ano,
            Jogador: jogador,
            Jogos: jogos,
            Torneios: participacoes.Select(p => p.TorneioId).Distinct().Count(),
            Titulos: participacoes.Count(p => p.UltimaFase == "Campeao"),
            Finais: participacoes.Count(p => p.UltimaFase == "Final"),
            Pontos: participacoes
                .Where(p => ContaNoRanking(p.Restrito, p.Formato))
                .Sum(p => PontosDoTorneio.Pontos(
                    p.UltimaFase, duplasPorCategoria.GetValueOrDefault(p.CategoriaId), p.Status)),
            MelhorPosicao: melhorPosicao,
            CategoriaDaPosicao: categoriaDaPosicao));
    }

    // Nome de cada clube pedido, por id. Existe pra o clube ser sempre um ACRÉSCIMO à linha e
    // nunca uma condição pra ela existir — ver a nota do INNER JOIN em ObterRetrospectivaAsync.
    private async Task<Dictionary<int, string>> ClubesPorIdAsync(IEnumerable<int> ids)
    {
        var procurados = ids.Distinct().ToList();
        if (procurados.Count == 0) return new Dictionary<int, string>();

        return await _context.Clubes
            .AsNoTracking()
            .Where(c => procurados.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome);
    }

    private async Task<List<Partida>> CarregarPartidasFinalizadasAsync(bool incluirTorneio = false)
    {
        var query = _context.Partidas
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .Where(p => p.VencedorId != null);

        if (incluirTorneio)
        {
            query = query.Include(p => p.Categoria).ThenInclude(c => c.Torneio);
        }

        return await query.ToListAsync();
    }

    // Descobre em qual dupla o jogador está; retorna (minhaDupla, duplaAdversaria) ou (null,null).
    // Dupla-TIME nunca "contém" um jogador: o Jogador1Id dela é o organizador que cadastrou
    // o time, e sem esta guarda ele herdaria parceiro, confronto e vitória de jogo que não jogou.
    private static (Dupla? minha, Dupla? oponente) LocalizarDuplas(Partida p, int jogadorId)
    {
        bool naDupla1 = p.Dupla1 is { NomeTime: null } && (p.Dupla1.Jogador1Id == jogadorId || p.Dupla1.Jogador2Id == jogadorId);
        bool naDupla2 = p.Dupla2 is { NomeTime: null } && (p.Dupla2.Jogador1Id == jogadorId || p.Dupla2.Jogador2Id == jogadorId);

        if (naDupla1 && !naDupla2) return (p.Dupla1, p.Dupla2);
        if (naDupla2 && !naDupla1) return (p.Dupla2, p.Dupla1);
        return (null, null);
    }

    private async Task<List<JogoSemanal>> CarregarJogosSemanaisAsync(int jogadorId)
    {
        return await _context.JogosSemanais
            .Include(j => j.Dupla1Jogador1)
            .Include(j => j.Dupla1Jogador2)
            .Include(j => j.Dupla2Jogador1)
            .Include(j => j.Dupla2Jogador2)
            .Where(j => j.Dupla1Jogador1Id == jogadorId || j.Dupla1Jogador2Id == jogadorId ||
                        j.Dupla2Jogador1Id == jogadorId || j.Dupla2Jogador2Id == jogadorId)
            .ToListAsync();
    }

    // Descobre o lado (1 ou 2) do jogador num jogo semanal e os dois jogadores do lado adversário.
    private static (int meuLado, (Jogador?, Jogador?) oponentes) LocalizarLadoJogoSemanal(JogoSemanal j, int jogadorId)
    {
        bool naDupla1 = j.Dupla1Jogador1Id == jogadorId || j.Dupla1Jogador2Id == jogadorId;
        bool naDupla2 = j.Dupla2Jogador1Id == jogadorId || j.Dupla2Jogador2Id == jogadorId;

        if (naDupla1 && !naDupla2) return (1, (j.Dupla2Jogador1, j.Dupla2Jogador2));
        if (naDupla2 && !naDupla1) return (2, (j.Dupla1Jogador1, j.Dupla1Jogador2));
        return (0, (null, null));
    }

    private static string NomesDupla(Dupla d)
    {
        var n1 = d.Jogador1?.Nome ?? "?";
        var n2 = d.Jogador2?.Nome ?? "?";
        return $"{n1} / {n2}";
    }
}
