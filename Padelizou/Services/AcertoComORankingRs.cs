using Padelizou.Models;

namespace Padelizou.Services;

// Quanto o Padelizou deve ao Ranking RS: R$ 1 por INSCRITO, e inscrito é PESSOA, não inscrição.
//
// ⚠️ É por isso que esta regra não pôde reaproveitar TaxaDoTorneioExterno.PessoasInscritas,
// que parece a mesma pergunta e não é: lá o que se cobra é o dinheiro que entrou, então quem
// se inscreveu em duas categorias PAGOU duas vezes e conta duas. Aqui a régua é a pessoa —
// quem joga a 4ª e a 6ª do mesmo torneio custa **um real**, não dois. Somar inscrições em vez
// de gente pagaria a mais, calado, em todo torneio com gente em duas categorias.
//
// De brinde, agrupar por pessoa dissolve o problema da CHAVE DIRETA: as duplas dela remontam
// os MESMOS jogadores em outros pares, e o filtro que a taxa dos 5% precisa manter na query
// aqui é desnecessário — quem aparecer duas vezes vira uma linha só de qualquer jeito.
public static class AcertoComORankingRs
{
    // Uma inscrição achatada: uma pessoa numa categoria. As duas formas de se inscrever
    // (dupla e americano) viram isto, e o resto da regra não precisa saber de qual veio.
    public record Inscrito(int JogadorId, string Nome, string Categoria);

    // O que a tela de detalhe mostra: a pessoa e TODAS as categorias dela naquele torneio —
    // é a linha que explica por que ela custou um real só.
    public record PessoaCobrada(int JogadorId, string Nome, IReadOnlyList<string> Categorias);

    // Uma linha da tela de listagem. Fica aqui, e não num ViewModel solto, porque o `Acerto`
    // nulo é o que separa "em aberto" de "já pago" — e essa é regra, não formatação.
    public record LinhaDoTorneio(
        Torneio Torneio,
        int Pessoas,
        decimal Valor,
        AcertoRankingRs? Acerto,
        bool InscricoesAbertas)
    {
        public bool EmAberto => Acerto == null && Pessoas > 0;
        public bool SemNinguem => Acerto == null && Pessoas == 0;
    }

    // Lista de espera fica de fora: quem não entrou não joga, e não vai pro ranking deles.
    // TIME também: `Dupla.Jogador1Id` de um time é o ORGANIZADOR que o cadastrou, não um
    // atleta inscrito — contá-lo cobraria por quem não está jogando.
    public static List<Inscrito> Achatar(
        IEnumerable<Dupla> duplas,
        IEnumerable<InscricaoAmericana> americanas,
        IReadOnlyDictionary<int, string> nomeDaCategoria,
        IReadOnlyDictionary<int, string> nomeDoJogador)
    {
        var lista = new List<Inscrito>();

        foreach (var dupla in duplas)
        {
            if (dupla.EmListaDeEspera || dupla.EhTime) continue;

            foreach (var jogadorId in new[] { dupla.Jogador1Id, dupla.Jogador2Id })
            {
                // Dupla ainda sem parceiro: só quem já está definido conta. Cobrar pela vaga
                // vazia seria cobrar por alguém que ainda não existe.
                if (jogadorId is not int id) continue;
                lista.Add(new Inscrito(id, Nome(nomeDoJogador, id), Categoria(nomeDaCategoria, dupla.CategoriaId)));
            }
        }

        foreach (var inscricao in americanas)
        {
            if (inscricao.EmListaDeEspera) continue;
            lista.Add(new Inscrito(inscricao.JogadorId,
                Nome(nomeDoJogador, inscricao.JogadorId),
                Categoria(nomeDaCategoria, inscricao.CategoriaId)));
        }

        return lista;
    }

    // O agrupamento que É a regra: uma linha por pessoa, com as categorias dela juntas.
    public static List<PessoaCobrada> Cobradas(IEnumerable<Inscrito> inscritos) =>
        inscritos
            .GroupBy(i => i.JogadorId)
            .Select(g => new PessoaCobrada(
                g.Key,
                g.First().Nome,
                // Distinct porque a chave direta repete a pessoa, e às vezes na mesma
                // categoria — listar "6ª Masculina" duas vezes só confundiria a conferência.
                g.Select(i => i.Categoria).Distinct().OrderBy(c => c).ToList()))
            .OrderBy(p => p.Nome)
            .ToList();

    public static decimal Valor(int pessoas, decimal custoPorInscrito) =>
        Math.Round(pessoas * custoPorInscrito, 2);

    // Registrar acerto de torneio sem ninguém inscrito lançaria uma despesa de R$ 0,00.
    public static string? ProblemaParaAcertar(Torneio? torneio, int pessoas, AcertoRankingRs? jaAcertado)
    {
        if (torneio == null) return "Torneio não encontrado.";
        if (!torneio.ValidarPeloRankingRs)
            return $"Este torneio não usa o {MarcaDoRanking.Nome} — não há o que acertar com eles.";
        if (jaAcertado != null)
            return $"Este torneio já foi acertado em {jaAcertado.AcertadoEm:dd/MM/yyyy}.";
        if (pessoas <= 0)
            return "Nenhuma pessoa inscrita — não há o que cobrar.";
        return null;
    }

    private static string Nome(IReadOnlyDictionary<int, string> nomes, int id) =>
        nomes.TryGetValue(id, out var nome) ? nome : $"Jogador #{id}";

    private static string Categoria(IReadOnlyDictionary<int, string> nomes, int id) =>
        nomes.TryGetValue(id, out var nome) ? nome : $"Categoria #{id}";
}
