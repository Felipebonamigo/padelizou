using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Como um jogador aparece na arte: o @ quando pode ser marcado, senão o nome.
//
// Guarda o NOME junto do texto de propósito — a tela da arte lista quem saiu sem @ e por quê,
// e pra isso precisa dos dois. `MotivoSemArroba` nulo = saiu marcado.
public record MarcacaoNaArte(string Texto, bool EhArroba, string Nome, string? MotivoSemArroba);

// Um lado da rede. Duas marcações no padel de dupla, UMA no americano (e na dupla que ainda
// não fechou parceiro) — a arte não inventa a segunda pessoa.
public record LadoDaArte(IReadOnlyList<MarcacaoNaArte> Marcacoes);

public record JogoParaArte(
    int PartidaId,
    string Fase,
    string Categoria,
    string Torneio,
    LadoDaArte Dupla1,
    LadoDaArte Dupla2,
    string? ArrobaDoOrganizador);

// O QUE A ARTE DO JOGO PRECISA SABER, lido do banco.
//
// Separado do desenho (`ArteDoJogoParaStory`) pelo mesmo motivo que `ChaveParaCard` é separado
// do `CartaoDaChave`: a régua de quem pode ser marcado é a parte que precisa de teste, e teste
// de régua não deveria precisar de canvas nem de fonte instalada.
public static class JogosParaArte
{
    // A RÉGUA. Quem responde "pode marcar?" é o ContatoDoJogador — a mesma régua que decide se
    // o perfil mostra o Instagram —, nunca um `if` escrito aqui.
    public static MarcacaoNaArte Marcacao(Jogador? jogador)
    {
        if (jogador == null) return new MarcacaoNaArte("", false, "", "sem jogador");

        var nome = NomeBonito.Curto(jogador.Nome);
        var arroba = ArrobaDoInstagram.ParaMostrar(jogador.Instagram);

        // A checagem do nulo é DIRETA no ponto de uso: um bool guardado antes não ensina nada
        // ao compilador (ver a nota do CS8602 no CLAUDE.md).
        if (arroba != null && ContatoDoJogador.PodeMarcarNaArte(jogador))
        {
            return new MarcacaoNaArte(arroba, true, nome, null);
        }

        return new MarcacaoNaArte(nome, false, nome, ContatoDoJogador.MotivoParaNaoMarcar(jogador));
    }

    public static LadoDaArte Lado(Jogador? um, Jogador? outro)
    {
        var marcacoes = new List<MarcacaoNaArte>();
        if (um != null) marcacoes.Add(Marcacao(um));
        if (outro != null) marcacoes.Add(Marcacao(outro));
        return new LadoDaArte(marcacoes);
    }

    // ⚠️ As consultas daqui são conferidas contra o Npgsql em
    // TraducaoDasConsultasDaArteTests: elas navegam Partida → Dupla → Jogador em dois níveis
    // nas duas duplas, e o EF InMemory do resto da suíte não traduz SQL nenhum (19/08/2026).
    public static async Task<JogoParaArte?> DoJogoAsync(DbPadelContext ctx, int partidaId)
    {
        var partida = await ComOsQuatroJogadores(ctx)
            .FirstOrDefaultAsync(p => p.Id == partidaId);

        if (partida?.TorneioId == null) return null;

        var torneio = await ctx.Torneios.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == partida.TorneioId);
        if (torneio == null) return null;

        return new JogoParaArte(
            partida.Id,
            FaseNaTela.Rotulo(partida.Fase, partida.NumeroNaFase),
            CategoriaNaTela.Curto(partida.Categoria?.Nome),
            torneio.Nome,
            Lado(partida.Dupla1?.Jogador1, partida.Dupla1?.Jogador2),
            Lado(partida.Dupla2?.Jogador1, partida.Dupla2?.Jogador2),
            ArrobaDoInstagram.ParaMostrar(torneio.InstagramDoOrganizador));
    }

    // Os jogos do torneio pra escolher de qual gerar a arte.
    //
    // O MAIS RECENTE EM CIMA, e a ordem é o recurso: quem abre esta tela acabou de ver um jogo
    // ser jogado e vai fotografar a dupla na rede. Ordenar por horário crescente (como a grade)
    // poria a semifinal de agora no fim de uma lista de 47 jogos.
    public static async Task<List<Partida>> DoTorneioAsync(DbPadelContext ctx, int torneioId) =>
        await ComOsQuatroJogadores(ctx)
            .Where(p => p.TorneioId == torneioId)
            .OrderByDescending(p => p.HorarioInicioReal ?? p.HorarioPrevisto)
            .ThenByDescending(p => p.Id)
            .ToListAsync();

    private static IQueryable<Partida> ComOsQuatroJogadores(DbPadelContext ctx) =>
        ctx.Partidas
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2);
}
