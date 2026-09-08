using Padelizou.Models;

namespace Padelizou.Services;

// "A 5ª CATEGORIA FEMININA NÃO PODE TER JOGO SÁBADO À NOITE" — a régua POR CATEGORIA.
//
// 🗣️ Pedido do Felipe, 08/09/2026: "colocar por categoria, se vai ter jogos de eliminatórias
// no sábado a noite ainda ou não. por exemplo, a 5a categoria feminina nao pode ter jogo
// sabado a noite, ai passaria para domingo de manha".
//
// ⚠️ SÓ AS ELIMINATÓRIAS (decisão do Felipe): os jogos de GRUPO da categoria continuam podendo
// cair no sábado à noite. Quem aplica esse recorte é GradeDeJogos.Encaixar (parâmetro
// `janelasSoNaEliminatoriaPorCategoria`) — aqui só nasce a janela.
//
// ⚠️ "PASSA PRO DOMINGO DE MANHÃ" NÃO É REGRA ESCRITA EM LUGAR NENHUM, e é de propósito:
// bloqueada a noite de sábado, a próxima vaga que a grade oferece já é a abertura do dia
// seguinte (Torneio.HoraInicioDiasSeguintes, 8h por padrão). Uma regra explícita de "mande pro
// domingo" seria uma segunda opinião sobre a grade, e discordaria dela no primeiro torneio que
// tivesse expediente diferente.
public static class EliminatoriaNoSabado
{
    // A janela proibida desta categoria: das 18h de sábado até a virada do dia. Vazia quando a
    // categoria pode jogar à noite (o padrão, e o comportamento de todo torneio até hoje), ou
    // quando o torneio não tem sábado nenhum no calendário.
    public static IEnumerable<(DateTime Inicio, DateTime Fim)> Da(Torneio torneio, Categoria categoria)
    {
        if (categoria.EliminatoriaNoSabadoANoite) yield break;
        if (JanelasDeImpedimento.DiaDoTorneio(torneio, DayOfWeek.Saturday) is not DateTime sabado) yield break;

        yield return (sabado.Add(JanelasDeImpedimento.CorteSabadoTardeNoite), sabado.AddDays(1));
    }

    // CategoriaId → janelas, pronto pro Encaixar. Categoria que pode jogar à noite fica de fora
    // do mapa, mesmo padrão de JanelasDeImpedimento.PorDupla.
    public static Dictionary<int, (DateTime Inicio, DateTime Fim)[]> PorCategoria(
        Torneio torneio, IEnumerable<Categoria> categorias)
    {
        var mapa = new Dictionary<int, (DateTime Inicio, DateTime Fim)[]>();
        foreach (var categoria in categorias)
        {
            var janelas = Da(torneio, categoria).ToArray();
            if (janelas.Length > 0) mapa[categoria.Id] = janelas;
        }
        return mapa;
    }

    public static Dictionary<int, (DateTime Inicio, DateTime Fim)[]> PorCategoria(Torneio torneio) =>
        PorCategoria(torneio, torneio.Categorias);
}
