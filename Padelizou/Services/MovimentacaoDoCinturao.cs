using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Quem MEXE no cinturão no banco. As regras estão em Services/Cinturao (puras, sem banco);
// aqui só se aplica o que elas decidiram.
//
// ⚠️ TODA troca de mão passa por aqui, e este serviço é chamado de dentro do
// FechamentoDoDesafio — o caminho único por onde um desafio vira resultado, tanto quando a outra
// dupla confirma no botão quanto quando o relógio confirma sozinho. Chamar isto do controller
// deixaria o cinturão parado nos desafios fechados pelo prazo, e o defeito seria mudo: o placar
// entra no ranking e o cinturão simplesmente não troca.
public class MovimentacaoDoCinturao
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public MovimentacaoDoCinturao(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    public Task<ReinadoNoCinturao?> DonoAtualAsync(int categoriaPadraoId,
        CancellationToken cancelationToken = default) =>
        _context.ReinadosNoCinturao
            .FirstOrDefaultAsync(r => r.CategoriaPadraoId == categoriaPadraoId && r.TerminouEm == null,
                cancelationToken);

    // Aplica o resultado de UM desafio já confirmado. Devolve o que aconteceu, pra quem chamou
    // poder contar na tela.
    public async Task<EfeitoNoCinturao> AplicarAsync(Desafio desafio, DateTime agora,
        CancellationToken cancelationToken = default)
    {
        var dono = await DonoAtualAsync(desafio.CategoriaPadraoId, cancelationToken);
        var efeito = Cinturao.Efeito(dono, desafio);
        if (efeito == EfeitoNoCinturao.Nada) return efeito;

        var lado = EstadoDoDesafio.LadoVencedor(desafio)!.Value;
        var (vencedor1, vencedor2) = Cinturao.DuplaVencedora(desafio, lado);

        if (efeito == EfeitoNoCinturao.Defendeu)
        {
            dono!.Defesas++;
            await _context.SaveChangesAsync(cancelationToken);
            return efeito;
        }

        if (efeito == EfeitoNoCinturao.Tomou)
        {
            dono!.TerminouEm = agora;
            dono.ComoTerminou = ReinadoNoCinturao.PerdeuNaQuadra;
            dono.DesafioDaPerdaId = desafio.Id;
        }

        _context.ReinadosNoCinturao.Add(new ReinadoNoCinturao
        {
            CategoriaPadraoId = desafio.CategoriaPadraoId,
            Jogador1Id = vencedor1,
            Jogador2Id = vencedor2,
            ComecouEm = agora,
            DesafioDeConquistaId = desafio.Id,
        });

        await _context.SaveChangesAsync(cancelationToken);
        await AvisarAsync(desafio.CategoriaPadraoId, new[] { vencedor1, vencedor2 },
            dono, efeito == EfeitoNoCinturao.Tomou, cancelationToken);

        return efeito;
    }

    // A defesa obrigatória: o dono que recusou ou ignorou 3 desafios em 14 dias perde o cinturão
    // pro primeiro que ficou sem resposta. Devolve o reinado novo, ou null se ninguém herdou.
    //
    // Chamado pelo VigiaDoCinturaoBackgroundService. ⚠️ A tela NÃO depende dele: ela mostra
    // quantas faltam lendo o relógio (Cinturao.QuantasFaltamParaPerder), então um vigia parado
    // atrasa a troca em vez de esconder o estado.
    public async Task<ReinadoNoCinturao?> TransferirPorOmissaoAsync(ReinadoNoCinturao dono,
        DateTime agora, CancellationToken cancelationToken = default)
    {
        var daCategoria = await _context.Desafios
            .Where(d => d.CategoriaPadraoId == dono.CategoriaPadraoId
                && d.PropostoEm >= dono.ComecouEm - Cinturao.JanelaDaDefesa)
            .ToListAsync(cancelationToken);

        var herdeiro = Cinturao.QuemHerdaPorOmissao(dono, daCategoria, agora);
        if (herdeiro == null) return null;

        // Quem herda é quem DESAFIOU e não foi atendido — o lado desafiante daquele desafio.
        var (novo1, novo2) = Cinturao.EmOrdem(
            herdeiro.DesafianteJogador1Id, herdeiro.DesafianteJogador2Id);

        dono.TerminouEm = agora;
        dono.ComoTerminou = ReinadoNoCinturao.PerdeuPorNaoDefender;

        var reinado = new ReinadoNoCinturao
        {
            CategoriaPadraoId = dono.CategoriaPadraoId,
            Jogador1Id = novo1,
            Jogador2Id = novo2,
            ComecouEm = agora,
            // Nulo de propósito: este cinturão não foi ganho num jogo, e gravar o desafio
            // recusado aqui diria que ele foi.
            DesafioDeConquistaId = null,
        };
        _context.ReinadosNoCinturao.Add(reinado);

        await _context.SaveChangesAsync(cancelationToken);
        await AvisarPorOmissaoAsync(dono, reinado, cancelationToken);

        return reinado;
    }

    // ── Os avisos ─────────────────────────────────────────────────────────────────────

    private async Task AvisarAsync(int categoriaPadraoId, int[] novosDonos,
        ReinadoNoCinturao? antigo, bool tomou, CancellationToken cancelationToken)
    {
        var categoria = await _context.CategoriasPadrao
            .Where(c => c.Id == categoriaPadraoId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync(cancelationToken) ?? "categoria";

        var ganhou = tomou
            ? AvisoDoDesafio.CinturaoTomado(categoria)
            : AvisoDoDesafio.CinturaoVago(categoria);

        foreach (var id in novosDonos)
            await _push.EnviarParaJogadorAsync(id, ganhou.Titulo, ganhou.Corpo,
                "/Desafios/Ranking", AlcanceDoAviso.AppSemEmail);

        if (antigo != null && tomou)
        {
            var perdeu = AvisoDoDesafio.CinturaoPerdido(categoria);
            foreach (var id in antigo.Donos)
                await _push.EnviarParaJogadorAsync(id, perdeu.Titulo, perdeu.Corpo,
                    "/Desafios/Ranking", AlcanceDoAviso.AppSemEmail);
        }

        await AvisarACategoriaAsync(categoriaPadraoId, categoria, novosDonos, antigo, cancelationToken);
    }

    // O ANÚNCIO PRA CATEGORIA (20/08/2026): cinturão que troca de mão em silêncio é cinturão
    // que ninguém corre atrás. Quem já jogou (ou propôs) desafio naquela categoria fica
    // sabendo que o alvo mudou — os donos novos e antigos ficam de fora, que os avisos deles
    // acabaram de sair aí em cima.
    //
    // ⚠️ A audiência são os PARTICIPANTES dos desafios da categoria, e não a base inteira:
    // broadcast pra quem nunca jogou um desafio é exatamente o spam que o comentário lá de
    // cima promete não ligar.
    private async Task AvisarACategoriaAsync(int categoriaPadraoId, string categoria,
        int[] novosDonos, ReinadoNoCinturao? antigo, CancellationToken cancelationToken)
    {
        var deFora = novosDonos.Concat(antigo?.Donos ?? Array.Empty<int>()).ToHashSet();

        var participantes = (await _context.Desafios
                .Where(d => d.CategoriaPadraoId == categoriaPadraoId)
                .Select(d => new { d.DesafianteJogador1Id, d.DesafianteJogador2Id,
                                   d.DesafiadoJogador1Id, d.DesafiadoJogador2Id })
                .ToListAsync(cancelationToken))
            .SelectMany(d => new[] { d.DesafianteJogador1Id, d.DesafianteJogador2Id,
                                     d.DesafiadoJogador1Id, d.DesafiadoJogador2Id })
            .Distinct()
            .Where(id => !deFora.Contains(id))
            .ToList();

        if (participantes.Count == 0) return;

        var nomes = await _context.Jogadores
            .Where(j => novosDonos.Contains(j.Id))
            .Select(j => new { j.Nome, j.Apelido })
            .ToListAsync(cancelationToken);
        var dupla = string.Join(" e ", nomes.Select(n => NomeBonito.ComApelido(n.Nome, n.Apelido)));

        var texto = AvisoDoDesafio.CinturaoTemNovosDonos(categoria, dupla);
        foreach (var id in participantes)
            await _push.EnviarParaJogadorAsync(id, texto.Titulo, texto.Corpo,
                "/Desafios/Ranking", AlcanceDoAviso.AppSemEmail);
    }

    private async Task AvisarPorOmissaoAsync(ReinadoNoCinturao antigo, ReinadoNoCinturao novo,
        CancellationToken cancelationToken)
    {
        var categoria = await _context.CategoriasPadrao
            .Where(c => c.Id == antigo.CategoriaPadraoId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync(cancelationToken) ?? "categoria";

        var ganhou = AvisoDoDesafio.CinturaoPorOmissao(categoria);
        foreach (var id in novo.Donos)
            await _push.EnviarParaJogadorAsync(id, ganhou.Titulo, ganhou.Corpo,
                "/Desafios/Ranking", AlcanceDoAviso.AppSemEmail);

        // ⚠️ Este é o único aviso do cinturão que NÃO é bilhete social: quem perde por não
        // defender precisa entender POR QUE perdeu, senão vira "o site tirou meu cinturão".
        var perdeu = AvisoDoDesafio.CinturaoPerdidoPorOmissao(categoria);
        foreach (var id in antigo.Donos)
            await _push.EnviarParaJogadorAsync(id, perdeu.Titulo, perdeu.Corpo,
                "/Desafios/Ranking", AlcanceDoAviso.SoApp);

        // A categoria fica sabendo também — a troca por omissão é a mais invisível de todas
        // (ninguém entrou em quadra), e é justamente a que mais precisa de anúncio.
        await AvisarACategoriaAsync(antigo.CategoriaPadraoId, categoria,
            novo.Donos.ToArray(), antigo, cancelationToken);
    }
}
