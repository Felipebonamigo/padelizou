using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

// A PÁGINA DO RANKING MONTADA UMA VEZ SÓ — as quatorze listas das oito abas, com o mesmo
// recorte regional, o mesmo período e a mesma canonização de cidade.
//
// ⚠️ NASCEU EXTRAÍDO DA AÇÃO `JogadorsController.Ranking` (14/09/2026), no dia em que o botão de
// compartilhar precisou das MESMAS listas pra desenhar a arte. É a única razão de ele existir, e
// é a razão de ele não ter sido "só a lista da aba pedida": duas montagens do mesmo ranking
// divergiriam na primeira mudança de régua — e a divergência sairia PUBLICADA, numa arte dizendo
// que o time A é o primeiro enquanto a tela ao lado diz que é o B.
//
// ⚠️ E É POR ISSO QUE ELE MONTA TUDO, inclusive as listas que a arte pedida não vai usar. Custa
// consultas; a alternativa custa confiança. O card é desenhado poucas vezes e sai com uma hora
// de cache (ver EntregaDeCard) — a conta fecha do lado da verdade.
public sealed class HubDoRanking
{
    private readonly DbPadelContext _context;
    private readonly IEstatisticasService _estatisticas;
    private readonly IPadelimetroService _padelimetro;
    private readonly IRankingAmericanoService _rankingAmericano;
    private readonly PortaDosDesafios _portaDosDesafios;
    private readonly TelaDoRankingDeDesafios _telaDeDesafios;

    public HubDoRanking(
        DbPadelContext context,
        IEstatisticasService estatisticas,
        IPadelimetroService padelimetro,
        IRankingAmericanoService rankingAmericano,
        PortaDosDesafios portaDosDesafios,
        TelaDoRankingDeDesafios telaDeDesafios)
    {
        _context = context;
        _estatisticas = estatisticas;
        _padelimetro = padelimetro;
        _rankingAmericano = rankingAmericano;
        _portaDosDesafios = portaDosDesafios;
        _telaDeDesafios = telaDeDesafios;
    }

    // `euId` nulo = visitante deslogado, e é o caso comum: esta página é PÚBLICA. Quem decide o
    // que ele enxerga continua sendo a `PortaDosDesafios`, aqui dentro, como já era.
    public async Task<RankingHubVM> MontarAsync(
        int? euId, int? torneioId, string[]? cidade, string? estado, string? periodo)
    {
        var hub = await _estatisticas.ObterRankingHubAsync(cidade, estado, periodo);

        // Opções dos selects de cidade/estado (cidades já filtradas pelo estado escolhido).
        // ⚠️ `somenteQuemJogouTorneio`: esta página é ranking, e ranking aqui só existe a
        // partir de resultado de torneio. Cidade sem ninguém que jogou é opção que só sabe
        // devolver tabela vazia — e era por essa porta que entravam na lista os apelidos e as
        // grafias soltas que cada um digita no cadastro.
        var (estados, cidades) = await _estatisticas.ObterLocaisDisponiveisAsync(estado, somenteQuemJogouTorneio: true);
        hub.EstadosDisponiveis = estados;
        hub.CidadesDisponiveis = cidades;

        // O que veio na URL passa a ser escrito como a lista escreve: link antigo com
        // `?cidade=GRAVATAI` mostraria o chip "GRAVATAI" e o select ofereceria "Gravataí" ao
        // lado — a mesma cidade duas vezes na mesma linha.
        hub.Cidades = CidadesSemRepetir.Canonizar(hub.Cidades, cidades);

        // Abas Padelímetro e Ranking Americano (RANKING.md): as duas respeitam o mesmo filtro
        // regional do hub. O Americano é ranking PRÓPRIO — não soma com o oficial, e por isso
        // vem de um serviço separado em vez de virar mais uma consulta do EstatisticasService.
        var doLocal = await _estatisticas.ObterJogadoresDoLocalAsync(cidade, estado);
        hub.Padelimetro = await _padelimetro.ListarRankingAsync(doLocal);
        var americano = await _rankingAmericano.ListarAsync(doLocal);
        hub.AmericanoIndividual = americano.Individual;
        hub.AmericanoDuplas = americano.Duplas;

        // "Quantas posições o último torneio me fez ganhar?" nas duas abas que faltavam.
        //
        // ⚠️ A janela do OFICIAL (que o EstatisticasService já abriu pro hub) serve pro
        // Padelímetro, porque é o mesmo torneio de chave que move os dois. O Americano tem
        // janela PRÓPRIA: são rankings separados, e o rodízio de sábado não move o oficial.
        if (hub.JanelaDoMovimento is { } janela)
            await _padelimetro.AplicarMovimentoAsync(hub.Padelimetro, janela.Corte);

        hub.JanelaDoAmericano = await MovimentoNoRanking.DoAmericanoAsync(_context, DateTime.Now);
        if (hub.JanelaDoAmericano is { } janelaAmericano)
        {
            var antes = await _rankingAmericano.ListarAsync(doLocal, ate: janelaAmericano.Corte);
            MovimentoNoRanking.Aplicar(hub.AmericanoIndividual,
                antes.Individual.Select(l => l.Jogador.Id).ToList(),
                l => l.Jogador.Id, (l, mov) => l.Movimento = mov);
            MovimentoNoRanking.Aplicar(hub.AmericanoDuplas,
                antes.Duplas.Select(l => l.Jogador.Id).ToList(),
                l => l.Jogador.Id, (l, mov) => l.Movimento = mov);
        }

        // Sub-aba "Americanos" dos Troféus. Ela obedece ao MESMO período que os troféus de chave
        // ao lado — meia tela em "este mês" e meia em "sempre" é como alguém compara os dois
        // números e tira a conclusão errada sem nada na tela ter mentido explicitamente.
        //
        // Em "sempre" (o padrão) a lista JÁ está pronta acima: uma segunda consulta pra chegar no
        // mesmo resultado seria trabalho puro de servidor em toda visita à página.
        if (hub.PeriodoDe is { } deDoPeriodo)
        {
            var noPeriodo = await _rankingAmericano.ListarAsync(doLocal, de: deDoPeriodo);
            hub.TrofeusAmericanoIndividual = noPeriodo.Individual;
            hub.TrofeusAmericanoDuplas = noPeriodo.Duplas;
        }
        else
        {
            hub.TrofeusAmericanoIndividual = hub.AmericanoIndividual;
            hub.TrofeusAmericanoDuplas = hub.AmericanoDuplas;
        }

        // Aba Desafios. A régua de quem enxerga é a MESMA do menu e do /Desafios — PortaDosDesafios,
        // um lugar só. Sem lista, a aba não é desenhada, e a promessa do topo da tela ("tudo aqui
        // sai de torneio") continua verdadeira pra quem não a tem.
        //
        // ⚠️ Anônimo cai fora antes de qualquer consulta: `FindFirstValue` devolve nulo, o
        // TryParse falha, e o `&&` curto-circuita. Esta página é PÚBLICA — sem isso, todo
        // visitante deslogado pagaria as consultas do módulo pra não ver aba nenhuma.
        // Quem está olhando, pra as tabelas destacarem a própria linha sem a VIEW ler claim
        // nenhuma — tela que interpreta credencial é tela que decide permissão.
        hub.EuId = euId;

        if (hub.EuId is int meuId && await _portaDosDesafios.PodeUsarAsync(meuId))
        {
            hub.Desafios = await _telaDeDesafios.MontarAsync(
                meuId, _portaDosDesafios.EmConstrucao, DateTime.Now);
        }

        // Aba PALPITEIROS: quem mais acerta no palpitômetro, com o MESMO filtro regional das
        // outras abas. ⚠️ Ela não mede resultado de chave — mede quem lê os jogos —, então
        // entra junto com os Desafios na lista de exceções da frase-promessa do topo da tela.
        //
        // ⚠️ E QUEM DESLIGOU O PALPITÔMETRO NO PERFIL NÃO A RECEBE (14/09/2026). Decisão do
        // Felipe: desligar some com TUDO, esta aba junto. A guarda vem ANTES da consulta, pelo
        // mesmo motivo da `PortaDosDesafios` logo acima — quem não vai ver a aba não paga a
        // conta dela. E some o botão de compartilhar junto de graça: o `TemArte` já lê a lista
        // vazia como "não há o que desenhar".
        if ((await PreferenciaDoPalpitometro.DeAsync(_context, hub.EuId)).VerPalpitometro)
            hub.Palpiteiros = await RankingDePalpiteiros.GeralAsync(_context, doLocal);

        // 3. RANKING DE UM TORNEIO: exibido embutido NESTA mesma página (não abre outra tela).
        //
        // ⚠️ O `torneioId` da URL passa pela MESMA régua do seletor (que fica no controller).
        // Filtrar só a lista tirava o torneio do <select> e entregava nome e ranking a quem
        // digitasse o número — oculto, cancelado ou esperando aprovação. Fora da vitrine conta
        // como id que não existe: a página abre sem torneio selecionado e não confirma nada.
        //
        // 🔑 A CHECAGEM VEIO DO PR #304 e ATRAVESSOU esta extração de propósito (14/09/2026).
        // As duas sessões correram em paralelo: lá a trava nasceu na ação, aqui a ação virou
        // este serviço. Perdê-la no merge deixaria o buraco reaberto sem ninguém ter escrito
        // uma linha pra isso — e é a arte do `/Cartoes/RankingImagem?aba=Torneio` que sairia
        // com o nome do torneio escondido dentro de um PNG com cache público.
        // Quem vigia são os testes do #304, em `SeletorDoRankingTests`: eles chamam a AÇÃO, e
        // por isso enxergam esta linha mesmo ela tendo mudado de arquivo.
        if (torneioId.HasValue)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId.Value);
            if (torneio != null && PermissaoDeOrganizador.ApareceNaDescoberta(torneio))
            {
                hub.TorneioSelecionadoId = torneio.Id;
                hub.TorneioSelecionadoNome = torneio.Nome;
                hub.RankingTorneio = await _estatisticas.ObterRankingDoTorneioAsync(torneio.Id);
            }
        }

        return hub;
    }
}
