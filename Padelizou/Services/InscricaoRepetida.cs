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

    public record Achado(int DuplaId, int JogadorId, string NomeJogador, Situacao Situacao, string? NomeDoParceiro);

    // Todas as inscrições que os jogadores informados já têm NESTA categoria.
    // `ignorarDuplaId` existe pra troca de parceiro: a dupla que está sendo editada não
    // pode se acusar de já existir.
    public static async Task<List<Achado>> ProcurarAsync(
        DbPadelContext context, int categoriaId, IEnumerable<int> jogadorIds, int? ignorarDuplaId = null)
    {
        var ids = jogadorIds.Distinct().ToList();
        if (ids.Count == 0) return new List<Achado>();

        var duplas = await context.Duplas
            .Where(d => d.CategoriaId == categoriaId
                     && d.Id != ignorarDuplaId
                     && (ids.Contains(d.Jogador1Id) || (d.Jogador2Id != null && ids.Contains(d.Jogador2Id.Value))))
            .Select(d => new
            {
                d.Id,
                d.Jogador1Id,
                Nome1 = d.Jogador1.Nome,
                d.Jogador2Id,
                Nome2 = d.Jogador2 != null ? d.Jogador2.Nome : null,
            })
            .ToListAsync();

        var achados = new List<Achado>();
        foreach (var d in duplas)
        {
            var completa = d.Jogador2Id != null;

            if (ids.Contains(d.Jogador1Id))
            {
                achados.Add(new Achado(d.Id, d.Jogador1Id, d.Nome1,
                    completa ? Situacao.ComParceiro : Situacao.Sozinho, d.Nome2));
            }
            if (d.Jogador2Id is int j2 && ids.Contains(j2) && d.Nome2 != null)
            {
                // Quem está na posição de parceiro está, por definição, numa dupla fechada.
                achados.Add(new Achado(d.Id, j2, d.Nome2, Situacao.ComParceiro, d.Nome1));
            }
        }

        return achados;
    }

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
