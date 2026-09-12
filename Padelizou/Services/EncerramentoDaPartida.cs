using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using System.Collections.Concurrent;

namespace Padelizou.Services;

// TUDO QUE ACONTECE QUANDO UM JOGO ACABA — num lugar só, para as duas telas.
//
// ⚠️ Este arquivo é a segunda metade da correção de 06/08/2026. A primeira unificou o robô
// que monta a chave (Services/RoboDoChaveamento); auditando o resto do encerramento, as duas
// telas ainda divergiam em TRÊS pontos, cada um invisível de um lado:
//
//   • a TELA CHEIA (Controle de Placar) não movia o PADELÍMETRO — o nível dos 4 jogadores
//     simplesmente não mudava, e o extrato do perfil ficava sem a linha daquele jogo;
//   • a MESA / botão do card não gerava a FINAL DO AMERICANO — o torneio terminava as
//     rodadas e ficava parado, esperando uma final que nenhum robô ia criar;
//   • a MESA / botão do card não disparava o "SEU JOGO É O PRÓXIMO" — justamente a tela que
//     se usa no dia do torneio era a que deixava o próximo par sem aviso.
//
// Ou seja: o que acontecia no fim de um jogo dependia de por onde o placar foi lançado. É o
// mesmo defeito de sempre neste projeto (ver Services/QuemVenceu, AvancoDaChave,
// RoboDoChaveamento): a MESMA regra escrita em dois lugares, e os dois discordando.
//
// A partir daqui só existe um caminho. Quem finaliza chama isto e pronto.
public class EncerramentoDaPartida
{
    private readonly DbPadelContext _context;
    private readonly RoboDoChaveamento _robo;
    private readonly IPadelimetroService _padelimetro;
    private readonly IPushNotificationService _push;
    private readonly ILogger<EncerramentoDaPartida> _logger;

    public EncerramentoDaPartida(DbPadelContext context, IPadelimetroService padelimetro,
        IPushNotificationService push, ILogger<EncerramentoDaPartida> logger,
        IEstatisticasService estatisticas)
    {
        _context = context;
        // O robô precisa do ranking pro desempate de grupo (ClassificacaoDeGrupos) — e é este
        // caminho que monta o mata-mata quando o último jogo do grupo termina.
        _robo = new RoboDoChaveamento(context, estatisticas);
        _padelimetro = padelimetro;
        _push = push;
        _logger = logger;
    }

    // Os endereços que o push leva. Vêm de fora porque quem sabe montar rota é o controller;
    // este serviço não carrega IUrlHelper só pra isso.
    public record LinksDoAviso(string? DoTorneio, string? DaListaDeJogos);

    // ── UM FINALIZAR DE CADA VEZ POR TORNEIO ──────────────────────────────────────────────
    //
    // ⚠️ Ensaio do Er (10/09/2026, anomalia C1): dois POSTs iguais de "finalizar" no último
    // jogo de grupo — clique duplo, duas abas, ou a fila offline da Mesa reentregando — criaram
    // a Semifinal DUAS VEZES (4 jogos em vez de 2), e a Final nunca ia nascer: o robô de avanço
    // espera as 4 semis, devolve 4 vencedores, "Semifinal" já existe, para. As guardas que
    // existiam ("já finalizada" no controller; `mataMataJaGerado` e `AnyAsync(proximaFase)` no
    // robô) são check-then-insert: cada requisição tem o próprio DbContext, as duas passam pela
    // checagem antes de qualquer uma gravar, e as duas gravam.
    //
    // A trava é EM PROCESSO (a app roda num processo só por ambiente — mesmo arranjo da
    // TravaDeEntrada) e POR TORNEIO. Por torneio, e não por partida, porque dois jogos
    // DIFERENTES da mesma categoria terminando juntos caem no mesmo buraco: cada um vê o outro
    // já finalizado e os dois montam a fase. Nada de lock no banco: a suíte roda em EF InMemory,
    // que não tem `FOR UPDATE`.
    //
    // ⚠️ ONDE ELA COMEÇA É O QUE IMPORTA: ANTES de o controller carregar a partida. A guarda "já
    // finalizada" só vale se a leitura é feita com a trava na mão — quem carrega antes de
    // esperar fica com o status de antes de o outro gravar, e passa. Por isso quem finaliza
    // (Mesa, Controle de Placar e W.O.) toma isto no TOPO da ação, com `using var`, e segura até
    // o último `return`: carregar → checar → gravar → robô, tudo dentro. A partida tem que ser o
    // PRIMEIRO toque daquele contexto nela — consulta sobre entidade já rastreada devolve a
    // instância velha, trava ou não; a chave (o TorneioId) sai de uma projeção justamente
    // porque projeção não rastreia nada.
    //
    // Sem torneio (jogo avulso) não há robô e não há trava: devolve um descartável vazio.
    //
    // atalho: o dicionário nunca esvazia — uma SemaphoreSlim por torneio que finalizou jogo
    // desde que o processo subiu. Teto real: centenas de torneios por ano; a saída, se um dia
    // pesar, é remover a entrada quando ninguém mais espera nela.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _travaPorTorneio = new();

    public static async Task<IDisposable> UmDeCadaVezPorTorneioAsync(int? torneioId)
    {
        if (torneioId is not int id) return TravaTomada.Nenhuma;

        var trava = _travaPorTorneio.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await trava.WaitAsync();
        return new TravaTomada(trava);
    }

    private sealed class TravaTomada : IDisposable
    {
        public static readonly TravaTomada Nenhuma = new(null);

        private SemaphoreSlim? _trava;

        public TravaTomada(SemaphoreSlim? trava) => _trava = trava;

        // Solta uma vez só, mesmo que alguém descarte duas vezes.
        public void Dispose() => Interlocked.Exchange(ref _trava, null)?.Release();
    }

    // `partida` já está com Status "Finalizada" e VencedorId gravados.
    // `acabouDeTerminar` separa "o jogo ACABOU agora" de "alguém corrigiu um placar antigo":
    // sem essa distinção, cada correção chamaria os jogadores do jogo seguinte de novo.
    public async Task AplicarAsync(Partida partida, bool acabouDeTerminar, LinksDoAviso links)
    {
        if (partida.VencedorId == null) return;

        int vencedorId = partida.VencedorId.Value;
        int perdedorId = vencedorId == partida.Dupla1Id ? partida.Dupla2Id : partida.Dupla1Id;

        // 0. A HORA EM QUE ACABOU (Felipe, 08/08/2026: "a ordem das finalizadas tem que ser por
        //    qual terminou por último vem primeiro").
        //
        // ⚠️ Só a tela cheia carimbava isso. Quem finalizava pelo botão do card AO VIVO ou pela
        // Mesa — que são as duas telas do dia do torneio — deixava `HorarioFimReal` NULO, e a
        // lista de Finalizadas, que ordena por ele, caía no desempate: num torneio "por ordem
        // de liberação" (sem horário previsto) sobrava o Id, ou seja, a ordem em que os jogos
        // foram SORTEADOS. O jogo que acabou agora aparecia no meio da lista.
        //
        // Mora aqui pelo motivo de sempre neste arquivo: encerramento é UM só, e regra de
        // encerramento escrita no chamador é regra que uma das telas não tem.
        //
        // `??=`: a tela cheia grava o carimbo antes de chamar (e é o mesmo instante). Correção
        // de placar antigo (`acabouDeTerminar` falso) não passa por aqui — lá o jogo terminou
        // quando terminou, e reescrever a hora jogaria um jogo de ontem pro topo da lista.
        //
        // Gravado na hora, e não no fim: daqui pra baixo tudo é robô, push e try/catch, e o
        // carimbo não pode depender de nenhum deles chegar ao fim.
        if (acabouDeTerminar && partida.HorarioFimReal == null)
        {
            partida.HorarioFimReal = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        // 1. PADELÍMETRO — o placar acabou de virar oficial, move o nível dos 4 jogadores
        //    (regras em RANKING.md; restrito/time/W.O. o serviço filtra). Em try/catch porque
        //    falha aqui não pode travar a Mesa no meio do torneio: o replay do admin
        //    reconstrói qualquer jogo perdido.
        try
        {
            await _padelimetro.AplicarAsync(partida.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Padelímetro falhou na partida {PartidaId}", partida.Id);
        }

        // 2. OS ROBÔS. A fase de grupos existe em duas grafias ("Fase de Grupos" nos seeds
        //    antigos, "Grupo A/B/..." no GerarChaves) — o gatilho aceita as duas, senão o
        //    mata-mata nunca é gerado.
        if (partida.TorneioId is int torneioId)
        {
            if (FasesTorneio.EhFaseDeGrupos(partida.Fase))
            {
                await _robo.MontarMataMataDosGruposAsync(partida.CategoriaId, torneioId);
            }
            else if (ChaveamentoMataMata.ProximaFase(partida.Fase) != null)
            {
                await _robo.AvancarFaseAsync(partida.CategoriaId, torneioId, partida.Fase);
            }
            else if (partida.Fase.StartsWith("Americano"))
            {
                await _robo.MontarFinalDoAmericanoAsync(partida.CategoriaId, torneioId);
            }
            else if (partida.Fase == "Final")
            {
                await CoroarCampeaoAsync(partida, vencedorId, torneioId);

                // A campanha da categoria fecha junto com a final: bônus de campeão, pena
                // de quem ficou na chave (RANKING.md, "A campanha também move o número").
                // Em try/catch pelo mesmo motivo do gancho de partida: falha aqui não pode
                // travar a Mesa, e o replay do admin reconstrói.
                try
                {
                    await _padelimetro.AplicarCampanhaAsync(partida.CategoriaId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Ajuste de campanha do Padelímetro falhou na categoria {CategoriaId}",
                        partida.CategoriaId);
                }
            }
            else if (partida.Fase == TabelaDoAmericano.FaseDesempate)
            {
                await CoroarDesempateDoAmericanoAsync(vencedorId, torneioId);
            }
        }

        // 3. AVISOS. Nesta ordem: primeiro o resultado (pra quem jogou), depois a chamada do
        //    próximo — que só faz sentido quando o jogo REALMENTE acabou agora.
        await NotificarResultadoAsync(partida, vencedorId, perdedorId, links.DoTorneio);

        if (acabouDeTerminar) await AvisarProximoDaQuadraAsync(partida, links.DaListaDeJogos);
    }

    private async Task CoroarCampeaoAsync(Partida partida, int vencedorId, int torneioId)
    {
        var campeao = await _context.Duplas.FindAsync(vencedorId);
        if (campeao != null) campeao.UltimaFase = "Campeao";
        await _context.SaveChangesAsync();

        await FinalizarOTorneioSeAcabouEmTodasAsCategoriasAsync(torneioId);
    }

    // O TORNEIO SÓ ACABA QUANDO ACABA EM TODAS AS CATEGORIAS.
    //
    // ⚠️ Ensaio do Er (10/09/2026, anomalia C2): até aqui a PRIMEIRA final que terminava
    // carimbava `Status = "Finalizado"` no torneio inteiro. Final da 4ª Feminina às 12:10 de
    // sábado, 1 de 12 — e /Torneios listou o Er em "Finalizados", disse "nenhum torneio em
    // andamento", a votação de MVP abriu e a Home do jogador perdeu o card "Seus torneios", com
    // ele ainda tendo duas finais por jogar.
    //
    // O carimbo da CATEGORIA (campeã com UltimaFase = "Campeao", cards, campanha do Padelímetro)
    // continua na hora, por categoria — só o do TORNEIO espera.
    //
    // A régua é "não sobra jogo por jogar em categoria nenhuma", e não "toda categoria tem
    // campeão": categoria sem jogo (uma inscrição só, nunca sorteada) não teria campeão nunca e
    // seguraria o torneio pra sempre. `Partida.Status` é Agendada / AoVivo / Finalizada — não há
    // "cancelada" pra descontar. E a partida que acabou de terminar já está gravada como
    // Finalizada quando se chega aqui: as três telas salvam antes de chamar AplicarAsync.
    //
    // Quem lê "Finalizado" (MvpDoTorneio, MovimentoNoRanking, AvisoDoMvpBackgroundService,
    // JanelaDoParceiro, CancelamentoDoTorneio, a Home) não muda: cada um deles supõe que o
    // carimbo é o fim de verdade, e agora é. O Reabrir de uma final devolve "Fase de Grupos"
    // (PartidasController.ReabrirPartida) e a final refeita passa por aqui de novo — fecha o
    // ciclo. Os quatro carimbos do Americano em RoboDoChaveamento continuam incondicionais.
    private async Task FinalizarOTorneioSeAcabouEmTodasAsCategoriasAsync(int torneioId)
    {
        bool aindaTemJogo = await _context.Partidas
            .AnyAsync(p => p.TorneioId == torneioId && p.Status != "Finalizada");
        if (aindaTemJogo) return;

        var torneio = await _context.Torneios.FindAsync(torneioId);
        if (torneio == null) return;

        torneio.Status = "Finalizado";
        await _context.SaveChangesAsync();
    }

    // Desempate do Americano: os dois empatados escolheram um parceiro cada e jogaram pelo
    // título. Quem disputa o título é o EMPATADO — o parceiro entrou pra fechar a quadra.
    //
    // ⚠️ Por isso não dá pra carimbar a dupla vencedora inteira, como se faz no mata-mata:
    // isso daria o título também a quem foi convidado. `TorneiosController.CriarDesempateAmericano`
    // sempre põe o empatado em `Jogador1Id` e o parceiro em `Jogador2Id` — é dessa posição que
    // sai o campeão. O carimbo vai numa linha SEM parceiro, igual ao campeão sem desempate
    // (ver RoboDoChaveamento.CoroarNoAmericanoAsync).
    private async Task CoroarDesempateDoAmericanoAsync(int vencedorId, int torneioId)
    {
        var torneio = await _context.Torneios.FindAsync(torneioId);
        var duplaVencedora = await _context.Duplas.FindAsync(vencedorId);
        if (duplaVencedora != null)
        {
            // No AMERICANO DE DUPLAS quem disputa o desempate é a própria dupla inscrita —
            // o título é dos dois, e o carimbo vai nela mesma.
            if (torneio?.Formato == "AmericanoDuplas")
            {
                duplaVencedora.UltimaFase = "Campeao";
            }
            else
            {
                _context.Duplas.Add(new Dupla
                {
                    CategoriaId = duplaVencedora.CategoriaId,
                    Jogador1Id = duplaVencedora.Jogador1Id,
                    Jogador2Id = null,
                    UltimaFase = "Campeao",
                });
            }
        }

        await _context.SaveChangesAsync();

        // Mesma régua da final do mata-mata: o desempate fecha a CATEGORIA; o torneio só
        // fecha quando não sobra rodada em nenhuma outra.
        await FinalizarOTorneioSeAcabouEmTodasAsCategoriasAsync(torneioId);
    }

    // Fim de jogo: avisa quem jogou e quem acompanha esses jogadores. É o momento em que o
    // app tem algo a dizer — antes disso a pessoa precisava abrir a tela pra descobrir.
    //
    // ⚠️ NADA DAQUI VAI POR E-MAIL desde 09/08/2026 (decisão do Felipe, cortando volume depois
    // de a cota do Gmail estourar e derrubar 130 e-mails num dia, duas recuperações de senha
    // entre eles). O motivo não é só cota: quem jogou estava na quadra e já sabe o placar — o
    // e-mail chega pra contar o que a pessoa acabou de viver. E o resultado de quem se segue é
    // bilhete social, mesmo caso do elogio.
    //
    // ⚠️ E o volume aqui é de RAJADA: um jogo avisa 4 jogadores mais os seguidores de cada um,
    // e uma rodada inteira termina junto. Era o segundo maior gasto de cota do sistema, atrás
    // só do "Novo torneio aberto".
    //
    // O push e a caixa de entrada continuam iguais — ali o aviso não custa cota nem incomoda.
    private async Task NotificarResultadoAsync(Partida partida, int vencedorId, int perdedorId, string? url)
    {
        try
        {
            var duplas = await _context.Duplas
                .Include(d => d.Jogador1).Include(d => d.Jogador2)
                .Where(d => d.Id == vencedorId || d.Id == perdedorId)
                .ToListAsync();

            var vencedora = duplas.FirstOrDefault(d => d.Id == vencedorId);
            var perdedora = duplas.FirstOrDefault(d => d.Id == perdedorId);
            if (vencedora == null || perdedora == null) return;

            var torneio = partida.TorneioId == null ? null : await _context.Torneios.FindAsync(partida.TorneioId.Value);

            // Push é lido de relance: apelido identifica mais rápido que nome completo.
            //
            // ⚠️ SÓ UM NOME QUANDO A VAGA ESTÁ ABERTA (09/09/2026): a dupla sem parceiro passou
            // a entrar na chave, e este texto era `"{j1} e {j2}"` — saía "Vocês venceram Paulo
            // Prass e  (6x0)", com o `e` pendurado no vazio. Escrever "e parceiro" aqui seria
            // pior: o jogo foi contra uma pessoa só.
            string Nomes(Dupla d) => d.Jogador2?.ComoChamar is { } parceiro
                ? $"{d.Jogador1?.ComoChamar} e {parceiro}"
                : d.Jogador1?.ComoChamar ?? "";
            var placar = $"{partida.GamesDupla1}x{partida.GamesDupla2}";
            var ondeFoi = torneio != null ? $" · {torneio.Nome}" : "";
            bool ehFinal = partida.Fase == "Final";

            // Dupla incompleta não chega a jogar, mas o filtro protege o push de nulo.
            var idsVencedores = new[] { vencedora.Jogador1Id, vencedora.Jogador2Id }
                .Where(id => id != null).Select(id => id!.Value).ToArray();
            var idsPerdedores = new[] { perdedora.Jogador1Id, perdedora.Jogador2Id }
                .Where(id => id != null).Select(id => id!.Value).ToArray();

            foreach (var id in idsVencedores)
            {
                await _push.EnviarParaJogadorAsync(id,
                    ehFinal ? "🏆 Campeões!" : "Vitória!",
                    ehFinal
                        ? $"Vocês venceram a final{ondeFoi}!"
                        : $"Vocês venceram {Nomes(perdedora)} ({placar}){ondeFoi}.",
                    url, AlcanceDoAviso.AppSemEmail);
            }

            foreach (var id in idsPerdedores)
            {
                await _push.EnviarParaJogadorAsync(id,
                    "Resultado do seu jogo",
                    $"{Nomes(vencedora)} venceu ({placar}){ondeFoi}.",
                    url, AlcanceDoAviso.AppSemEmail);
            }

            // Quem SEGUE os jogadores fica sabendo — mas só do mata-mata. Num dia de torneio a
            // fase de grupos tem dezenas de jogos; avisar seguidor a cada um viraria spam e a
            // pessoa desligaria a notificação de vez.
            if (FasesTorneio.EhFaseDeGrupos(partida.Fase)) return;

            var idsEmQuadra = idsVencedores.Concat(idsPerdedores).ToHashSet();
            var seguidores = await _context.SeguidoresJogador
                .Include(s => s.Seguidor)
                .Where(s => idsEmQuadra.Contains(s.SeguidoId)
                         && !idsEmQuadra.Contains(s.SeguidorId)
                         && s.Seguidor.NotificarSeguidosTorneio)
                .Select(s => s.SeguidorId)
                .Distinct()
                .ToListAsync();

            foreach (var seguidorId in seguidores)
            {
                await _push.EnviarParaJogadorAsync(seguidorId,
                    ehFinal ? "Saiu o campeão!" : "Resultado de quem você segue",
                    $"{Nomes(vencedora)} venceu {Nomes(perdedora)} ({placar}){ondeFoi}.",
                    url, AlcanceDoAviso.AppSemEmail);
            }
        }
        catch (Exception ex)
        {
            // Push é acessório — o resultado já está gravado, não pode falhar por isso.
            _logger.LogWarning(ex, "Falha ao notificar resultado da partida {PartidaId}.", partida.Id);
        }
    }

    // Push de "seu jogo é o próximo", disparado pelo FIM do jogo anterior — não por relógio.
    // Torneio atrasa, e um aviso preso ao horário previsto chegaria com o jogador ainda
    // almoçando, ou depois de ele já ter jogado. Quem sabe de verdade que a quadra vagou é a
    // partida que acabou de terminar nela.
    private async Task AvisarProximoDaQuadraAsync(Partida terminada, string? url)
    {
        if (terminada.TorneioId == null) return;

        try
        {
            var agendadas = await _context.Partidas
                .Include(p => p.Dupla1)
                .Include(p => p.Dupla2)
                .Where(p => p.TorneioId == terminada.TorneioId && p.Status == "Agendada")
                .ToListAsync();

            var proxima = AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, agendadas, DateTime.Now);
            if (proxima == null) return;

            // O clube da quadra, pro aviso não mandar quem está no clube A correr pra uma quadra
            // do clube B. Torneio de uma sede só devolve o mapa vazio e o texto não muda.
            var sedes = await SedesDoTorneio.CarregarAsync(_context, terminada.TorneioId.Value);

            foreach (var jogadorId in AvisosDoDiaDeJogo.JogadoresDa(proxima))
            {
                await _push.EnviarParaJogadorAsync(jogadorId,
                    "Seu jogo é o próximo!",
                    AvisosDoDiaDeJogo.CorpoDoProximo(proxima, sedes),
                    // ⚠️ SAIU DO WHATSAPP EM 21/08/2026, por decisão do Felipe, junto com o
                    // resto da família de torneio (chaves, cancelamento, vaga na lista de
                    // espera). Era o maior volume do canal de longe — 4 mensagens por partida,
                    // ~350 num torneio de 100 —, e volume é o que resta de risco depois que
                    // ritmo e consentimento foram resolvidos.
                    //
                    // O aviso continua sendo o mais urgente do sistema, e é por isso que ele
                    // NÃO virou `AppSemEmail`: quem está no clube pode não ter o app, e aí o
                    // e-mail é o único caminho que sobra.
                    url, AlcanceDoAviso.SoApp);
            }

            proxima.AvisoProximoEnviadoEm = DateTime.Now;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // O placar já está salvo e o mata-mata já avançou. Push é acessório.
            _logger.LogWarning(ex, "Falha ao avisar o próximo jogo da quadra depois da partida {PartidaId}.", terminada.Id);
        }
    }
}
