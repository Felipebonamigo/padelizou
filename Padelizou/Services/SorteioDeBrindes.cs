using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O SORTEIO DE BRINDES DO TORNEIO (21/11/2026). 🗣️ Felipe: *"alguns torneios sorteiam brindes e
// outras coisas para quem joga"*.
//
// 🎟️ QUEM CONCORRE: quem está NA CHAVE — *"se ficou na lista de espera (não vai participar do
// torneio), não participa"*. É a régua de InscricaoQueConta sem a linha de TIME (o Jogador1Id
// dela é o organizador que cadastrou, não quem joga). UMA chance por pessoa, mesmo inscrita em
// duas categorias.
//
// 🔁 REPETIR, a critério do organizador:
//   - ADICIONAR: outro sorteio por cima; quem já ganhou num sorteio que vale fica de fora, a não
//     ser que ele marque "quem já ganhou concorre de novo".
//   - SUBSTITUIR: refaz um sorteio. O antigo NÃO some — fica com SubstituidoPorId e aparece
//     riscado. Os ganhadores dele voltam a concorrer (ele deixou de valer).
//
// 🎲 Quem decide é o servidor, com RandomNumberGenerator, ANTES da animação: a roleta na tela é
// só teatro, e o resultado já está gravado quando ela começa a girar.
//
// 📣 Ganhador avisado na hora (app + e-mail, sem WhatsApp, como os outros avisos de torneio). No
// refeito, quem SAIU também fica sabendo (*"sim, avisa"*) — ele já tinha recebido "você ganhou".
public class SorteioDeBrindes
{
    // Quanto tempo o sorteio fica em destaque no telão (que se atualiza a cada 20 s).
    public static readonly TimeSpan NoTelao = TimeSpan.FromMinutes(3);

    public sealed record Resultado(SorteioDoTorneio? Sorteio, string? Erro);

    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public SorteioDeBrindes(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    // Os jogadores que concorrem, sem repetir.
    public static IQueryable<int> Participantes(DbPadelContext context, int torneioId)
    {
        var naChave = context.Duplas
            .Where(d => d.Categoria.TorneioId == torneioId && d.NomeTime == null)
            .Where(InscricaoQueConta.Expressao);
        return naChave.Select(d => d.Jogador1Id)
            .Union(naChave.Where(d => d.Jogador2Id != null).Select(d => d.Jogador2Id ?? 0));
    }

    // O sorteio que o telão mostra: o mais novo que vale, feito nos últimos 3 minutos.
    public static IQueryable<SorteioDoTorneio> Recente(DbPadelContext context, int torneioId, DateTime agora)
    {
        var desde = agora - NoTelao;
        return context.SorteiosDoTorneio
            .Where(s => s.TorneioId == torneioId && s.SubstituidoPorId == null && s.FeitoEm >= desde && s.FeitoEm <= agora)
            .OrderByDescending(s => s.FeitoEm).ThenByDescending(s => s.Id)
            .Take(1);
    }

    public async Task<Resultado> SortearAsync(int torneioId, int quantidade, string? premio, bool quemJaGanhouConcorre,
        int? substituirSorteioId, int feitoPorJogadorId, DateTime agora)
    {
        var torneio = await _context.Torneios.FirstOrDefaultAsync(t => t.Id == torneioId);
        if (torneio == null) return new(null, "Torneio não encontrado.");

        SorteioDoTorneio? antigo = null;
        if (substituirSorteioId is int idAntigo)
        {
            antigo = await _context.SorteiosDoTorneio.Include(s => s.Ganhadores)
                .FirstOrDefaultAsync(s => s.Id == idAntigo && s.TorneioId == torneioId);
            if (antigo == null) return new(null, "Não encontrei esse sorteio neste torneio.");
            if (antigo.SubstituidoPorId != null) return new(null, "Esse sorteio já foi refeito — refaça o que vale agora.");
        }

        var pool = await Participantes(_context, torneioId).ToListAsync();
        if (!quemJaGanhouConcorre)
        {
            var jaGanharam = await _context.GanhadoresDoSorteio
                .Where(g => g.Sorteio.TorneioId == torneioId && g.Sorteio.SubstituidoPorId == null
                            && g.SorteioId != (substituirSorteioId ?? 0))
                .Select(g => g.JogadorId)
                .ToListAsync();
            pool = pool.Except(jaGanharam).ToList();
        }

        if (quantidade < 1) return new(null, "Diga quantos nomes sortear (pelo menos 1).");
        if (quantidade > pool.Count)
            return new(null, pool.Count == 0
                ? "Não sobrou ninguém pra concorrer: todo mundo da chave já ganhou. Marque \"quem já ganhou concorre de novo\"."
                : $"Só {pool.Count} pessoa(s) concorrem agora — não dá pra sortear {quantidade}.");

        // Fisher–Yates com o gerador criptográfico: cada ordem tem a mesma chance.
        var embaralhado = pool.ToArray();
        for (int i = embaralhado.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (embaralhado[i], embaralhado[j]) = (embaralhado[j], embaralhado[i]);
        }
        var ganhadores = embaralhado.Take(quantidade).ToList();

        var sorteio = new SorteioDoTorneio
        {
            TorneioId = torneioId,
            Premio = string.IsNullOrWhiteSpace(premio) ? null : premio.Trim(),
            Quantidade = quantidade,
            FeitoEm = agora,
            FeitoPorJogadorId = feitoPorJogadorId,
        };
        for (int i = 0; i < ganhadores.Count; i++)
            sorteio.Ganhadores.Add(new GanhadorDoSorteio { JogadorId = ganhadores[i], Ordem = i + 1 });
        _context.SorteiosDoTorneio.Add(sorteio);
        await _context.SaveChangesAsync();

        if (antigo != null)
        {
            antigo.SubstituidoPorId = sorteio.Id;
            antigo.SubstituidoEm = agora;
            await _context.SaveChangesAsync();
        }

        await AvisarAsync(torneio, sorteio, antigo, ganhadores);
        return new(sorteio, null);
    }

    private async Task AvisarAsync(Torneio torneio, SorteioDoTorneio sorteio, SorteioDoTorneio? antigo, List<int> ganhadores)
    {
        var url = $"/Torneios/Details/{torneio.Id}";
        var premio = sorteio.Premio != null ? $" — prêmio: {sorteio.Premio}" : "";
        foreach (var jogadorId in ganhadores)
            await _push.EnviarParaJogadorAsync(jogadorId, "Você ganhou no sorteio! 🎁",
                $"{torneio.Nome}: você foi sorteado(a){premio}.", url, AlcanceDoAviso.SoApp);

        if (antigo == null) return;
        var doQue = antigo.Premio != null ? $" de {antigo.Premio}" : "";
        foreach (var saiu in antigo.Ganhadores.Select(g => g.JogadorId).Except(ganhadores))
            await _push.EnviarParaJogadorAsync(saiu, "Sorteio refeito",
                $"{torneio.Nome}: o organizador refez o sorteio{doQue}, e desta vez saiu outro nome.", url, AlcanceDoAviso.SoApp);
    }
}
