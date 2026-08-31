using Padelizou.Models;

namespace Padelizou.Services;

// UMA linha da lista misturada de "Todos" — dupla, time ou inscrição individual, com o que a
// tela precisa dizer sobre ela fora do card.
//
// `Dupla` e `Jogador` são excludentes: torneio de chave enche o primeiro, Americano o segundo.
// Os dois vão INTEIROS de propósito, e não achatados em nome: os parciais que já desenham
// inscrito (`_LadoDaPartida` e `_JogadorChip`) recebem exatamente esses tipos — e é o
// `_LadoDaPartida` que já sabe que um TIME não se desenha pelo Jogador1 dele (que é o
// organizador que cadastrou).
public record InscritoNoTorneio(
    int Id,
    string Categoria,
    DateTime? Quando,
    bool Pago,
    bool EmListaDeEspera,
    Dupla? Dupla,
    Jogador? Jogador);

// A aba de inscritos SEM separar por categoria, do mais recente pro mais antigo (Felipe,
// 31/08/2026). A aba sempre obrigou a escolher uma categoria pra ver alguém, e quem organiza
// responde "quem entrou hoje?" o dia inteiro — a resposta exigia abrir as nove e comparar de
// cabeça, que é a mesma queixa que já tinha gerado o total no cabeçalho.
public static class TodosOsInscritos
{
    // O valor da opção "Todos" no seletor. Zero porque nenhuma categoria tem Id 0 — e porque
    // é o que o controller já devolvia quando não havia categoria nenhuma pra escolher.
    public const int TodasAsCategorias = 0;

    public static IReadOnlyList<InscritoNoTorneio> MaisRecentesPrimeiro(
        Torneio torneio,
        IEnumerable<Categoria> categorias,
        IEnumerable<InscricaoAmericana> inscricoesAmericanas)
    {
        var cats = categorias.ToList();

        // Americano cobra e joga por PESSOA: as duplas nem existem ali, e ler `cat.Duplas`
        // daria lista vazia num torneio cheio.
        var itens = torneio.Formato == "Americano"
            ? inscricoesAmericanas.Select(i => new InscritoNoTorneio(
                i.Id,
                CategoriaNaTela.Curto(cats.FirstOrDefault(c => c.Id == i.CategoriaId)?.Nome),
                i.CriadoEm, i.Pago, i.EmListaDeEspera, null, i.Jogador))
            : cats.SelectMany(c => (c.Duplas ?? new List<Dupla>()).Select(d => new InscritoNoTorneio(
                d.Id, CategoriaNaTela.Curto(c.Nome), d.CriadoEm, d.Pago, d.EmListaDeEspera, d, null)));

        // ⚠️ `CriadoEm` NULO É "DAS MAIS ANTIGAS QUE EXISTEM" — inscrição anterior a
        // 25/07/2026, quando a coluna nasceu. O `?? MinValue` está escrito porque é essa a
        // intenção, e não porque a ordenação dependa dele: em memória o `DateTime?` nulo já
        // desce pro fim sozinho (nulo é o menor de todos pro comparador padrão). Ele é a
        // trava de quem tentar o oposto sem perceber (`?? DateTime.MaxValue`, "sem data =
        // trate como agora", que tem teste) e de um dia isto virar consulta ao banco, onde o
        // Postgres ordena NULLS FIRST no DESC e mandaria as mais velhas pro topo de uma lista
        // chamada "mais recentes".
        //
        // O desempate pelo Id não é sobra: duas inscrições do mesmo instante existem (a dupla
        // e o parceiro que entra junto), e sem ele a ordem seria a do banco — que não é ordem
        // nenhuma, e faria duas telas iguais mostrarem listas diferentes.
        return itens
            .OrderByDescending(i => i.Quando ?? DateTime.MinValue)
            .ThenByDescending(i => i.Id)
            .ToList();
    }
}
