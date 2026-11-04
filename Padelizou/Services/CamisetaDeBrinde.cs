using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// CAMISETA DE BRINDE DO TORNEIO (04/11/2026).
//
// 🗣️ Mila (ER Padel): *"esqueci que esse vai ter camiseta — como altero ali?"*. Felipe: o
// organizador liga o brinde, marca os tamanhos que tem (tradicional e babylook, do PP ao XGG)
// e, se quiser, uma data limite; cada inscrito escolhe o seu, e escolher é OPCIONAL.
//
// As regras moram aqui; o TorneiosController.Camisetas só faz o HTTP.
public static class CamisetaDeBrinde
{
    // A tabela inteira, na ordem em que aparece na tela e no resumo. O código é o que se grava
    // (curto, estável); o nome é o que se lê. XG e XGG são tamanhos diferentes no mercado — a
    // lista tem os dois e cada organizador marca só os que tem.
    public static readonly IReadOnlyList<(string Codigo, string Nome)> Tamanhos = new[]
    {
        ("PP", "PP"), ("P", "P"), ("M", "M"), ("G", "G"), ("GG", "GG"), ("XG", "XG"), ("XGG", "XGG"),
        ("BLPP", "Babylook PP"), ("BLP", "Babylook P"), ("BLM", "Babylook M"),
        ("BLG", "Babylook G"), ("BLGG", "Babylook GG"), ("BLXG", "Babylook XG"), ("BLXGG", "Babylook XGG"),
    };

    public static bool EhBabylook(string codigo) => codigo.StartsWith("BL", StringComparison.Ordinal);

    public static string NomeDo(string codigo) =>
        Tamanhos.FirstOrDefault(t => t.Codigo == codigo).Nome ?? codigo;

    // Os códigos marcados, na ordem da tabela, sem repetir e sem o que não existe. Nulo quando
    // não sobra nenhum — é o "sem tamanho" que o Configurar recusa.
    public static string? Normalizar(IEnumerable<string>? codigos)
    {
        var marcados = (codigos ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        var validos = Tamanhos.Where(t => marcados.Contains(t.Codigo)).Select(t => t.Codigo).ToList();
        return validos.Count == 0 ? null : string.Join(",", validos);
    }

    public static IReadOnlyList<string> Oferecidos(Torneio torneio) =>
        torneio.OfereceCamiseta && !string.IsNullOrEmpty(torneio.TamanhosDeCamiseta)
            ? torneio.TamanhosDeCamiseta.Split(',', StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();

    // Dá pra escolher (ou trocar) agora? A data limite vale o DIA INTEIRO: quem põe "até 10/11"
    // pensa no dia 10 todo, não na meia-noite em que ele começa.
    public static bool AceitaEscolha(Torneio torneio, DateTime agora) =>
        torneio.OfereceCamiseta
        && (torneio.CamisetaEscolhaAte is not DateTime ate || agora.Date <= ate.Date);

    // O TAMANHO ESCOLHIDO NO FORMULÁRIO DE INSCRIÇÃO (🗣️ Felipe, com o print: *"não achei aonde
    // escolho minha camiseta"*). Anota (ou troca) sem salvar — quem chama salva junto com o
    // resto da inscrição. Aceita jogador ainda sem Id (CPF novo): a linha vai pela navegação.
    //
    // ⚠️ NUNCA derruba a inscrição: vazio, tamanho não oferecido, prazo vencido ou brinde
    // desligado só não anotam nada. Vazio também não APAGA escolha anterior — no formulário,
    // "não escolhi" quer dizer "não mexi", e o "não quero" mora no card da página do torneio.
    public static async Task AnotarNaInscricaoAsync(DbPadelContext context, Torneio torneio, Jogador jogador,
        string? tamanho, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(tamanho)
            || !AceitaEscolha(torneio, agora)
            || !Oferecidos(torneio).Contains(tamanho))
            return;

        var atual = jogador.Id == 0 ? null : await context.CamisetasDosAtletas.FindAsync(torneio.Id, jogador.Id);
        if (atual == null)
            context.CamisetasDosAtletas.Add(new CamisetaDoAtleta
            {
                TorneioId = torneio.Id, Jogador = jogador, Tamanho = tamanho, EscolhidaEm = agora,
            });
        else
        {
            atual.Tamanho = tamanho;
            atual.EscolhidaEm = agora;
        }
    }

    // Quem está inscrito, pelas DUAS portas (dupla e individual/americano), uma vez por pessoa:
    // quem joga duas categorias leva uma camiseta só.
    public static async Task<List<int>> InscritosAsync(DbPadelContext context, int torneioId)
    {
        var daDupla = await context.Duplas
            .Where(d => d.Categoria.TorneioId == torneioId)
            .Select(d => new { d.Jogador1Id, d.Jogador2Id })
            .ToListAsync();
        var doAmericano = await context.InscricoesAmericanas
            .Where(i => i.Categoria.TorneioId == torneioId)
            .Select(i => i.JogadorId)
            .ToListAsync();

        return daDupla.Select(d => d.Jogador1Id)
            .Concat(daDupla.Where(d => d.Jogador2Id != null).Select(d => d.Jogador2Id!.Value))
            .Concat(doAmericano)
            .Distinct()
            .ToList();
    }

    public static async Task<bool> EstaInscritoAsync(DbPadelContext context, int torneioId, int jogadorId) =>
        await context.Duplas.AnyAsync(d => d.Categoria.TorneioId == torneioId
                                        && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId))
        || await context.InscricoesAmericanas.AnyAsync(i => i.Categoria.TorneioId == torneioId && i.JogadorId == jogadorId);

    // QUEM VÊ O TAMANHO AO LADO DO NOME, na lista de inscritos (🗣️ Felipe: *"para saber que já
    // selecionou"*). Tamanho de roupa é dado pessoal: o organizador (que encomenda) e quem é da
    // MESMA inscrição. ⚠️ `quemOlha` nulo nunca casa com `Jogador2Id` nulo da dupla sem parceiro.
    public static bool PodeVerNaLista(bool podeGerenciar, int? quemOlha, IEnumerable<int?> daInscricao) =>
        podeGerenciar || (quemOlha is int eu && daInscricao.Contains(eu));

    public sealed record Pedido(string Nome, string Tamanho);
    public sealed record PorTamanho(string Tamanho, int Quantas);
    public sealed record Resumo(List<PorTamanho> PorTamanho, List<Pedido> Pedidos, int SemEscolha);

    // O que o organizador leva pro fornecedor: quantas de cada (na ordem da tabela), quem pediu
    // qual, e quantos inscritos ainda não escolheram. Escolha de quem saiu do torneio não conta.
    public static async Task<Resumo> ResumoAsync(DbPadelContext context, int torneioId)
    {
        var inscritos = await InscritosAsync(context, torneioId);

        var escolhas = (await context.CamisetasDosAtletas
                .Where(c => c.TorneioId == torneioId)
                .Select(c => new { c.JogadorId, c.Tamanho, c.Jogador.Nome })
                .ToListAsync())
            .Where(c => inscritos.Contains(c.JogadorId))
            .ToList();

        var ordem = Tamanhos.Select(t => t.Codigo).ToList();
        int Posicao(string codigo) => ordem.IndexOf(codigo) is var i && i >= 0 ? i : int.MaxValue;

        var porTamanho = escolhas
            .GroupBy(e => e.Tamanho)
            .OrderBy(g => Posicao(g.Key))
            .Select(g => new PorTamanho(NomeDo(g.Key), g.Count()))
            .ToList();

        var pedidos = escolhas
            .OrderBy(e => e.Nome)
            .Select(e => new Pedido(e.Nome, NomeDo(e.Tamanho)))
            .ToList();

        return new Resumo(porTamanho, pedidos, inscritos.Count - escolhas.Count);
    }
}
