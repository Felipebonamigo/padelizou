using Padelizou.Models;
using padelizou.Models;

namespace Padelizou.Services;

// Uma linha da lista "Todos" da aba de inscritos.
public record InscritoNaLista(
    string Dupla,
    string Categoria,
    DateTime? Quando,
    bool EmListaDeEspera);

// TODOS OS INSCRITOS DO TORNEIO, NUMA LISTA SÓ.
//
// 🗣️ Pedido do Felipe, 04/09/2026: "na primeira vez que abrir a aba de inscritos, uma aba com
// todos, sem separar por categoria, uma lista dizendo o nome da dupla e a categoria que foi
// inscrito, do mais recente pro mais antigo — e aí se o usuário quiser, ele seleciona a
// categoria".
//
// 🕳️ O que motivou: o ER PADEL TOUR tem 49 duplas e a aba abria na 3ª Masculina, que tem 2.
// Quem chega na página não sabe qual categoria olhar, e a primeira da lista não responde
// pergunta nenhuma — ela é só a primeira.
//
// ⚠️ Quem está INSCRITO continua abrindo na própria categoria. Isso já existia no controller,
// é proposital, e pra essa pessoa é melhor do que "Todos": ela abriu a página pra ver a
// categoria dela.
public static class InscritosDeTodasAsCategorias
{
    // O valor da opção "Todos" no seletor. Zero porque nenhuma categoria tem Id 0 — e porque
    // era justamente o que `CategoriaSelecionadaId` já devolvia quando não achava nada.
    public const int Todos = 0;

    public static List<InscritoNaLista> Montar(
        Torneio torneio, IEnumerable<InscricaoAmericana> inscricoesAmericanas)
    {
        var americanasPorCategoria = inscricoesAmericanas.ToLookup(i => i.CategoriaId);

        var linhas = new List<InscritoNaLista>();

        foreach (var categoria in torneio.Categorias)
        {
            foreach (var dupla in categoria.Duplas)
            {
                linhas.Add(new InscritoNaLista(
                    NomeDaDupla(dupla), categoria.Nome, dupla.CriadoEm, dupla.EmListaDeEspera));
            }

            foreach (var inscricao in americanasPorCategoria[categoria.Id])
            {
                linhas.Add(new InscritoNaLista(
                    inscricao.Jogador?.ComoChamar ?? "Inscrito",
                    categoria.Nome, inscricao.CriadoEm, inscricao.EmListaDeEspera));
            }
        }

        // `CriadoEm` é ANULÁVEL: inscrição anterior à coluna existe em produção, e ela é das
        // mais velhas — tem que ficar no FIM de uma lista "mais recente primeiro".
        //
        // ⚠️ E fica, de graça: em `OrderByDescending` o `null` de um `DateTime?` ordena como o
        // menor valor e cai no fim sozinho. Eu tinha escrito aqui um `?? DateTime.MinValue`
        // "pra garantir", com um comentário afirmando que sem ele as linhas subiriam pro topo.
        // A afirmação era falsa: quebrei o coalesce de propósito e o teste continuou verde.
        // Saiu, porque proteção que não protege de nada é só ruído que a próxima sessão lê
        // como regra.
        return linhas
            .OrderByDescending(l => l.Quando)
            .ToList();
    }

    // O mesmo formato que os cards de dupla já usam na tela, pra a lista "Todos" e a lista por
    // categoria não chamarem a mesma dupla de dois jeitos.
    private static string NomeDaDupla(Dupla dupla)
    {
        var primeiro = dupla.Jogador1?.ComoChamar ?? "Inscrito";

        // Quem se inscreveu sozinho ESTÁ inscrito e ocupa vaga: sumir da lista mentiria sobre
        // o tamanho do torneio. A frase diz o estado em vez de fingir uma dupla completa.
        return dupla.Jogador2?.ComoChamar is { } segundo
            ? $"{primeiro} e {segundo}"
            : $"{primeiro} (procura parceiro)";
    }
}
