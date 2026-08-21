using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O mesmo jogador entrando DUAS VEZES na mesma categoria. Aconteceu de verdade no
// "Interno Los Corneteiros" (03/08/2026): o Otávio apareceu como parceiro de um e, logo
// abaixo, sozinho "procurando parceiro" — duas vagas ocupadas pela mesma pessoa, que no
// sorteio viraria uma dupla sem ninguém.
//
// A causa é o próprio jeito certo de usar o sistema: você se inscreve sozinho, e depois
// alguém te inscreve como parceiro dele. As duas ações são legítimas; o que faltava era
// alguém notar que a segunda torna a primeira obsoleta.
//
// Por isso a resposta NÃO é um "não" seco: quando a inscrição que já existe é SOLO, ela é
// justamente a que a nova veio substituir — o certo é oferecer juntar as duas. Só quando a
// inscrição existente já tem parceiro é que não há o que fazer: aí a pessoa está mesmo
// tentando jogar duas vezes na mesma categoria.
public static class InscricaoRepetida
{
    public enum Situacao
    {
        NaoInscrito,
        Sozinho,        // dá pra juntar: a nova inscrição substitui esta
        ComParceiro,    // não dá: ele já tem dupla fechada nesta categoria
    }

    // ⚠️ `CategoriaId` entrou em 21/08/2026 (o mural precisa se defender de receber achados de
    // OUTRA categoria): sem ele, uma régua que filtra por categoria não tem como conferir nada.
    public record Achado(int CategoriaId, int DuplaId, int JogadorId, string NomeJogador, Situacao Situacao, string? NomeDoParceiro);

    // Todas as inscrições que os jogadores informados já têm NESTA categoria.
    // `ignorarDuplaId` existe pra troca de parceiro: a dupla que está sendo editada não
    // pode se acusar de já existir.
    public static async Task<List<Achado>> ProcurarAsync(
        DbPadelContext context, int categoriaId, IEnumerable<int> jogadorIds, int? ignorarDuplaId = null)
    {
        var ids = jogadorIds.Distinct().ToList();
        if (ids.Count == 0) return new List<Achado>();

        // ⚠️ TIME NÃO É INSCRIÇÃO DE JOGADOR (`NomeTime == null`). A linha de time guarda o
        // ORGANIZADOR em Jogador1Id e nada em Jogador2Id — de fora, é igualzinha a uma
        // inscrição "sozinho, procurando parceiro". Sem esta exceção, o organizador que
        // cadastrou um time seria acusado de já estar inscrito na categoria, e quem juntasse
        // apagaria o TIME junto: a premissa de que nenhuma regra de jogador enxerga essa linha
        // vale aqui como vale no TrocarParceiro e no QuemJaEstaNoTorneio.
        var duplas = await context.Duplas
            .Where(d => d.CategoriaId == categoriaId
                     && d.Id != ignorarDuplaId
                     && d.NomeTime == null
                     && (ids.Contains(d.Jogador1Id) || (d.Jogador2Id != null && ids.Contains(d.Jogador2Id.Value))))
            .Select(d => new Linha(
                d.CategoriaId,
                d.Id,
                d.Jogador1Id,
                d.Jogador1.Nome,
                d.Jogador2Id,
                d.Jogador2 != null ? d.Jogador2.Nome : null))
            .ToListAsync();

        return Classificar(duplas, ids);
    }

    // A MESMA classificação, sobre duplas JÁ CARREGADAS em memória.
    //
    // Existe porque a tela de detalhes do torneio já traz `Categorias → Duplas → Jogador1/2`
    // pra desenhar a grade: perguntar "em que categorias eu já estou?" com uma consulta nova
    // rodaria em toda abertura da página mais pesada do site, e seria tradução nova pro
    // Postgres que o teste InMemory não prova. Uma régua, dois jeitos de alimentá-la.
    public static List<Achado> DasDuplasCarregadas(
        IEnumerable<Dupla> duplasDaCategoria, IEnumerable<int> jogadorIds, int? ignorarDuplaId = null)
    {
        var ids = jogadorIds.Distinct().ToList();
        if (ids.Count == 0) return new List<Achado>();

        var linhas = duplasDaCategoria
            .Where(d => d.Id != ignorarDuplaId
                     && d.NomeTime == null
                     && (ids.Contains(d.Jogador1Id) || (d.Jogador2Id != null && ids.Contains(d.Jogador2Id.Value))))
            .Select(d => new Linha(d.CategoriaId, d.Id, d.Jogador1Id, d.Jogador1?.Nome ?? "",
                                   d.Jogador2Id, d.Jogador2?.Nome))
            .ToList();

        return Classificar(linhas, ids);
    }

    private record Linha(int CategoriaId, int DuplaId, int Jogador1Id, string Nome1, int? Jogador2Id, string? Nome2);

    private static List<Achado> Classificar(IEnumerable<Linha> duplas, IReadOnlyCollection<int> ids)
    {
        var achados = new List<Achado>();
        foreach (var d in duplas)
        {
            var completa = d.Jogador2Id != null;

            if (ids.Contains(d.Jogador1Id))
            {
                achados.Add(new Achado(d.CategoriaId, d.DuplaId, d.Jogador1Id, d.Nome1,
                    completa ? Situacao.ComParceiro : Situacao.Sozinho, d.Nome2));
            }
            // ⚠️ A GUARDA É NO `Jogador2Id`, NÃO no nome. Era `&& d.Nome2 != null`, e vinda do
            // banco a diferença não aparecia (o JOIN sempre traz o nome). Vinda da memória, um
            // `Jogador2` não carregado deixa `Nome2` nulo e faria quem está na CADEIRA DE
            // PARCEIRO sumir da classificação — fail-open exatamente na metade que a régua do
            // mural existe pra pegar.
            if (d.Jogador2Id is int j2 && ids.Contains(j2))
            {
                // Quem está na posição de parceiro está, por definição, numa dupla fechada.
                achados.Add(new Achado(d.CategoriaId, d.DuplaId, j2, d.Nome2 ?? "", Situacao.ComParceiro, d.Nome1));
            }
        }

        return achados;
    }

    // Quem já tem DUPLA FECHADA nesta categoria — a pergunta do mural (21/08/2026). Filtra por
    // categoria E por jogador aqui dentro de propósito: passar a lista do torneio inteiro é
    // seguro, passar só a da categoria é seguro, e passar a categoria errada não fura a régua.
    public static Achado? DuplaFechadaDe(
        IEnumerable<Achado> achados, int categoriaId, int jogadorId) =>
        achados.FirstOrDefault(a => a.CategoriaId == categoriaId
                                 && a.JogadorId == jogadorId
                                 && a.Situacao == Situacao.ComParceiro);

    // O que impede de seguir sem conversa. Null = ou não há conflito, ou o conflito é do
    // tipo que se resolve juntando (ver PerguntaParaJuntar).
    public static string? MotivoParaRecusar(IEnumerable<Achado> achados)
    {
        var fechadas = achados.Where(a => a.Situacao == Situacao.ComParceiro).ToList();
        if (fechadas.Count == 0) return null;

        var nomes = fechadas
            .Select(a => $"{a.NomeJogador} (já está com {a.NomeDoParceiro})")
            .Distinct();

        return "Já tem dupla fechada nesta categoria: " + string.Join(", ", nomes)
             + ". Ninguém joga duas vezes na mesma categoria — se a dupla mudou, use "
             + "\"Trocar parceiro\" na inscrição que já existe.";
    }

    public static List<Achado> QuePodemSerJuntadas(IEnumerable<Achado> achados) =>
        achados.Where(a => a.Situacao == Situacao.Sozinho).ToList();

    // A pergunta que a tela faz antes de gravar. Sai daqui, e não da view, pra a mesma frase
    // valer quando o servidor precisa recusar um formulário que veio sem a confirmação.
    public static string PerguntaParaJuntar(IEnumerable<Achado> soloS)
    {
        var lista = soloS.ToList();
        if (lista.Count == 0) return "";

        var nomes = string.Join(" e ", lista.Select(a => a.NomeJogador).Distinct());
        var plural = lista.Count > 1;

        return $"{nomes} já {(plural ? "estão inscritos" : "está inscrito")} nesta categoria, "
             + $"{(plural ? "sozinhos" : "sozinho")}, procurando parceiro. "
             + $"Quer juntar? {(plural ? "As inscrições sozinhas saem" : "A inscrição sozinha sai")} "
             + "e a dupla fica valendo — a vaga é a mesma, ninguém perde lugar.";
    }
}
