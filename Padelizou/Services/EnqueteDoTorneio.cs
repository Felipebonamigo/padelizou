using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A ENQUETE DE DEPOIS DO TORNEIO: quem jogou dá nota pro clube e pra organização.
//
// Por que ela existe AGORA: o "Melhor Clube do ano" de 2027 precisa de um ano de avaliações, e
// esse relógio só começa quando a coleta começa. Se ela nascer em janeiro, o prêmio abre magro
// — a mesma razão que fez o MVP sair em 2026.
//
// Decisões de desenho:
// - A enquete mora na tela do MVP e usa a MESMA janela de 7 dias (o dono da janela é
//   MvpDoTorneio.DentroDaJanela) — mas NÃO obedece ao interruptor UsaVotacaoDeMvp nem ao
//   FORMATO: o interruptor e o formato são sobre a disputa entre jogadores; a enquete é coleta
//   NOSSA sobre o clube.
// - ⚠️ É por isso que o AMERICANO, que desde 16/08/2026 não elege MVP, continua avaliando o
//   clube: o rodízio de sábado é evento de clube como qualquer outro, e é provavelmente o
//   formato mais comum aqui. Amarrar a enquete ao MVP abriria um buraco no dado do "Melhor
//   Clube do ano" exatamente onde há mais eventos.
// - Quem responde é quem JOGOU (a régua do eleitorado do MVP, um dono só).
// - A média só aparece com 3+ respostas — "5,0 estrelas (1 avaliação)" é uma pessoa falando
//   com voz de consenso, o mesmo furo do "1º lugar com 0 pontos".
public static class EnqueteDoTorneio
{
    public const int NotaMinima = 1;
    public const int NotaMaxima = 5;

    // Mesmo espírito do MvpDoTorneio.VotosMinimos: abaixo disso não há "média", há uma pessoa.
    public const int RespostasParaMostrarMedia = 3;

    // A janela é A MESMA do MVP, e só ela: `DentroDaJanela` não pergunta interruptor nem
    // formato, então esta linha É a decisão do cabeçalho — sem `true` mágico pra alguém
    // interpretar errado depois.
    public static bool Aberta(string? statusDoTorneio, DateTime? ultimoJogo, DateTime agora) =>
        MvpDoTorneio.DentroDaJanela(statusDoTorneio, ultimoJogo, agora);

    public static bool MediaVisivel(int respostas) => respostas >= RespostasParaMostrarMedia;

    public static string? ProblemaComNotas(int notaClube, int notaOrganizacao)
    {
        if (notaClube < NotaMinima || notaClube > NotaMaxima
            || notaOrganizacao < NotaMinima || notaOrganizacao > NotaMaxima)
        {
            return $"A nota vai de {NotaMinima} a {NotaMaxima} estrelas.";
        }
        return null;
    }

    // Registra (ou troca) a resposta. Devolve o motivo da recusa, ou null quando deu certo.
    // ⚠️ TODA a validação acontece AQUI — a tela só esconde o que não cabe, e POST montado à
    // mão não passa por view nenhuma (a mesma régua do VotarAsync).
    public static async Task<string?> AvaliarAsync(
        DbPadelContext contexto, int torneioId, int jogadorId,
        int notaClube, int notaOrganizacao, DateTime agora)
    {
        if (ProblemaComNotas(notaClube, notaOrganizacao) is { } problemaNota) return problemaNota;

        var torneio = await contexto.Torneios
            .AsNoTracking()
            .Where(t => t.Id == torneioId)
            .Select(t => new { t.Status })
            .FirstOrDefaultAsync();
        if (torneio == null) return "Torneio não encontrado.";

        var fins = await contexto.Partidas
            .AsNoTracking()
            .Where(p => p.TorneioId == torneioId && p.VencedorId != null)
            .Select(p => p.HorarioFimReal ?? p.HorarioInicioReal ?? p.HorarioPrevisto)
            .ToListAsync();

        if (!Aberta(torneio.Status, MvpDoTorneio.UltimoJogo(fins), agora))
            return "A avaliação deste torneio não está aberta — ela vale na semana seguinte ao fim.";

        var eleitores = await MvpDoTorneio.EleitoresAsync(contexto, torneioId);
        if (!eleitores.Contains(jogadorId))
            return "Só quem jogou este torneio avalia o clube e a organização.";

        var existente = await contexto.AvaliacoesDeTorneio
            .FirstOrDefaultAsync(a => a.TorneioId == torneioId && a.JogadorId == jogadorId);

        if (existente != null)
        {
            // Trocar a resposta, nunca somar outra — o índice único do banco garante isso
            // mesmo em dois cliques simultâneos.
            existente.NotaClube = notaClube;
            existente.NotaOrganizacao = notaOrganizacao;
            existente.AtualizadoEm = agora;
        }
        else
        {
            contexto.AvaliacoesDeTorneio.Add(new AvaliacaoDoTorneio
            {
                TorneioId = torneioId,
                JogadorId = jogadorId,
                NotaClube = notaClube,
                NotaOrganizacao = notaOrganizacao,
                CriadoEm = agora,
            });
        }

        await contexto.SaveChangesAsync();
        return null;
    }

    // O resumo pro organizador (e pro futuro "Melhor Clube do ano"): médias e quantos
    // responderam. As médias vêm nulas enquanto não há resposta o bastante pra ser "média".
    public static async Task<ResumoDaEnquete> ResumoAsync(DbPadelContext contexto, int torneioId)
    {
        var notas = await contexto.AvaliacoesDeTorneio
            .AsNoTracking()
            .Where(a => a.TorneioId == torneioId)
            .Select(a => new { a.NotaClube, a.NotaOrganizacao })
            .ToListAsync();

        var resumo = new ResumoDaEnquete { Respostas = notas.Count };
        if (MediaVisivel(notas.Count))
        {
            resumo.MediaClube = Math.Round(notas.Average(n => n.NotaClube), 1);
            resumo.MediaOrganizacao = Math.Round(notas.Average(n => n.NotaOrganizacao), 1);
        }
        return resumo;
    }
}

public sealed class ResumoDaEnquete
{
    public int Respostas { get; set; }
    public double? MediaClube { get; set; }
    public double? MediaOrganizacao { get; set; }
    public bool TemMedia => MediaClube != null;
}
