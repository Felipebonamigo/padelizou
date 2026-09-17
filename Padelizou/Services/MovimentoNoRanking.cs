using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// QUANTAS POSIÇÕES O ÚLTIMO TORNEIO FEZ CADA UM GANHAR OU PERDER.
//
// A pergunta que o jogador faz depois de um torneio não é "como estou vs. o mês passado" — é
// **"e aí, eu subi?"**. Antes disso o hub comparava com ~1 mês atrás em duas abas (categoria e
// times) e com nada nas outras. Um mês é um recorte de calendário, e o ranking daqui não se move
// por calendário: ele se move em SALTOS, quando um torneio termina. Comparar com "há 30 dias"
// mistura dois torneios num número só, ou não mostra nada quando o torneio foi há 31.
//
// ⚠️ POR QUANTO TEMPO FICA NA TELA (decisão do Felipe delegada, 08/08/2026): **7 dias depois de
// o torneio acabar**. Torneio é evento de fim de semana; uma semana cobre a conversa que vem
// depois dele — o grupo do WhatsApp, o print, quem foi ver como ficou. Passou disso, "+2" não
// diz mais nada sobre hoje: diz sobre um sábado que ninguém lembra, e um selo que fica pra
// sempre deixa de significar "olha o que ACABOU de acontecer". Fora da janela a coluna não
// aparece vazia — ela **some inteira**, porque coluna sem conteúdo é pior que coluna nenhuma.
public static class MovimentoNoRanking
{
    public const int DiasNaTela = 7;

    // O torneio que causou o movimento e até quando o selo fica.
    //
    // `Corte` é o instante ANTES do torneio começar: é ele que se passa pro cálculo do "antes",
    // e é por isso que ele é DataInicio − 1 tick, e não DataInicio.
    public sealed record Janela(int TorneioId, string Nome, DateTime Corte, DateTime TerminouEm)
    {
        public DateTime SomeEm => TerminouEm.AddDays(DiasNaTela);
    }

    // O QUE O SELO DE UMA LINHA DIZ. São TRÊS estados, e dois deles vinham se confundindo.
    //
    // Isto era um `int?`, que só tem vaga pra dois: o número, e o `null` de "entrou agora". Aí
    // "não havia ranking antes" era gravado com o MESMO 0 de "jogou e ficou onde estava" — e a
    // tela desenhava um "–" dizendo *"Ficou na mesma posição"* pra uma tabela inteira em que
    // ninguém tinha posição nenhuma. Foi o que apareceu na aba Padelímetro depois do primeiro
    // torneio: 29 linhas garantindo a cada um que ele não tinha se mexido.
    //
    // É a MESMA mentira pequena que o `Novo` já existia pra não contar ("novo NÃO é +0", no
    // _SeloDeMovimento), e que o comentário do `Aplicar` já mandava não contar ("Sem base, sem
    // selo") — só faltava o estado pra dizer isso. Ver SeloSemBaseDeComparacaoTests.
    //
    // ⚠️ `SemBase` é o PRIMEIRO valor do enum de propósito: `default(Selo)` passa a ser o estado
    // que não afirma nada. Linha que nunca passou pelo `Aplicar` cala a boca, em vez de herdar
    // um "ficou parado" que ninguém mediu.
    public enum EstadoDoSelo
    {
        SemBase = 0,   // não havia ranking antes: o primeiro torneio deste recorte
        Novo,          // havia ranking antes, e esta linha não estava nele
        Moveu,         // havia ranking antes, e esta linha estava nele — inclusive parada, com 0
    }

    public readonly record struct Selo(EstadoDoSelo Estado, int Posicoes = 0)
    {
        public static readonly Selo SemBase = new(EstadoDoSelo.SemBase);
        public static readonly Selo Novo = new(EstadoDoSelo.Novo);

        // `Posicoes` só quer dizer alguma coisa aqui dentro: >0 subiu, <0 desceu, 0 ficou.
        public static Selo Moveu(int posicoes) => new(EstadoDoSelo.Moveu, posicoes);
    }

    // O último torneio do RANKING OFICIAL (categoria, Padelímetro, times). Mesma régua do
    // `EstatisticasService.ContaNoRanking`, escrita pra consulta: evento fechado e Americano
    // fora. Fechado são os DOIS: o de chave de acesso e o de um time só (16/09/2026).
    //
    // ⚠️ Carimbar o selo por um torneio que não move ponto é pior do que não carimbar nada:
    // ele anuncia "subiu 3 posições" numa lista que não mexeu, e a pessoa vai procurar a
    // mudança que não existe.
    public static Task<Janela?> DoOficialAsync(DbPadelContext ctx, DateTime agora) =>
        UltimoAsync(ctx, ctx.Torneios.Where(t => !t.Restrito
                                              && t.TimeExclusivoId == null
                                              && t.Formato != FormatoDoTorneio.Americano
                                              && t.Formato != FormatoDoTorneio.AmericanoDeDuplas), agora);

    // O último Americano que contou. É OUTRA janela de propósito: os dois rankings andam
    // separados, e o Americano de sábado não pode carimbar movimento no ranking oficial.
    //
    // ⚠️ Os formatos são comparados um a um, e não por `FormatoDoTorneio.EhAmericano`: isto vira
    // SQL, e o EF não traduz método — a consulta estourava em runtime. O teste com banco em
    // memória NÃO pega isso (lá tudo roda em C#), então quem paga é a tela.
    public static Task<Janela?> DoAmericanoAsync(DbPadelContext ctx, DateTime agora) =>
        UltimoAsync(ctx, ctx.Torneios.Where(t => t.PontuaNoRankingAmericano
                                              && t.RankingAmericanoPagoEm != null
                                              && (t.Formato == FormatoDoTorneio.Americano
                                               || t.Formato == FormatoDoTorneio.AmericanoDeDuplas)), agora);

    private static async Task<Janela?> UltimoAsync(DbPadelContext ctx, IQueryable<Torneio> candidatos, DateTime agora)
    {
        // "Finalizado" e não "tem jogo acabado": enquanto o torneio corre, a posição ainda
        // muda, e um selo que aparece e se corrige sozinho é pior do que um que demora.
        var torneio = await candidatos
            .Where(t => t.Status == "Finalizado" && t.DataInicio != null)
            .OrderByDescending(t => t.DataInicio)
            .Select(t => new { t.Id, t.Nome, t.DataInicio })
            .FirstOrDefaultAsync();

        if (torneio == null) return null;

        // Quando ACABOU de verdade: o último jogo que saiu. `DataInicio` é a largada, e num
        // torneio de sexta a domingo ela erraria a janela em dois dias.
        var fim = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id && p.Status == "Finalizada" && p.HorarioFimReal != null)
            .MaxAsync(p => (DateTime?)p.HorarioFimReal);

        var terminouEm = fim ?? torneio.DataInicio!.Value;
        if (agora > terminouEm.AddDays(DiasNaTela)) return null;   // passou da validade

        return new Janela(torneio.Id, torneio.Nome, torneio.DataInicio!.Value.AddTicks(-1), terminouEm);
    }

    // A CONTA, e ela mora num lugar só. Já existia copiada em `AplicarMovimentoCategorias` e
    // `AplicarMovimentoTimes`, com a mesma lógica escrita duas vezes — e agora seriam cinco.
    //
    // Os três estados estão no `Selo`, acima. `ordemAntes` são as CHAVES na ordem em que
    // estavam antes do torneio. Recebe chaves e não a lista inteira porque cada ranking guarda
    // um tipo de linha diferente (jogador, time, nível), e o que a conta precisa é só "quem
    // estava em que lugar".
    public static void Aplicar<T, TChave>(
        IReadOnlyList<T> agora, IReadOnlyList<TChave> ordemAntes,
        Func<T, TChave> chave, Action<T, Selo> gravar) where TChave : notnull
    {
        // ⚠️ Lista "antes" VAZIA não vira "todo mundo é novo" — é o primeiro torneio da história
        // daquele ranking (ou da categoria recém-criada), e anunciar 40 estreias não informa
        // nada. Também não vira "todo mundo ficou parado", que era o defeito: ninguém ficou
        // onde estava, porque ninguém estava em lugar nenhum.
        if (ordemAntes.Count == 0)
        {
            foreach (var item in agora) gravar(item, Selo.SemBase);
            return;
        }

        var posicaoAntes = new Dictionary<TChave, int>();
        for (int i = 0; i < ordemAntes.Count; i++) posicaoAntes[ordemAntes[i]] = i + 1;

        for (int i = 0; i < agora.Count; i++)
        {
            gravar(agora[i], posicaoAntes.TryGetValue(chave(agora[i]), out var pAntes)
                ? Selo.Moveu(pAntes - (i + 1))
                : Selo.Novo);
        }
    }
}
