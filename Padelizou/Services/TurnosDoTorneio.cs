using Padelizou.Models;

namespace Padelizou.Services;

// QUAIS TURNOS ESTE TORNEIO TEM — a lista que as três telas do impedimento oferecem.
//
// 🗣️ Felipe, 07/10/2026, com o print do painel do NATA PADEL TOUR: *"aqui está marcando quinta
// e sexta, mas o torneio só inicia no sábado"*.
//
// ⚠️ NÃO É COSMÉTICO, E QUEM PROVA É O `JanelasDeImpedimento.Da`: ele só devolve janela quando
// `DiaDoTorneio` acha aquele dia da semana dentro dos 3 dias que seguem o início. Num torneio
// que começa no sábado, "Sexta à noite" não gera janela nenhuma — a dupla marca, PAGA a
// `TaxaPorImpedimento` por ela, e o sorteio ignora. Dinheiro por uma janela que não existe,
// sem erro em lugar nenhum. É a mesma nulidade que `ConcentracaoDeJogos.JanelaDoTurno` já
// tratava do lado da concentração desde 08/09; faltava a tela não oferecer.
//
// ⚠️ A RÉGUA ANTIGA NÃO SEGURAVA NADA: `Torneio.QuintaEhDiaDoTorneio` é
// `DataInicio é quinta || PermiteImpedimentoQuintaNoite`, e a tela a usava como
// `QuintaEhDiaDoTorneio && PermiteImpedimentoQuintaNoite` — `A && (B || A)` é `A`, ou seja, a
// checagem de dia era um `no-op` desde sempre. A sexta nunca teve checagem nenhuma, e as
// outras duas telas (inscrição e painel do organizador) não tinham nem a aparência de uma.
public static class TurnosDoTorneio
{
    // O dia da semana que cada turno nomeia. `Nenhum` não nomeia dia nenhum — e é por isso que
    // ele é o único que nunca precisa existir no calendário.
    private static DayOfWeek? DiaDe(TurnoDoImpedimento turno) => turno switch
    {
        TurnoDoImpedimento.QuintaNoite => DayOfWeek.Thursday,
        TurnoDoImpedimento.SextaNoite => DayOfWeek.Friday,
        TurnoDoImpedimento.SabadoManha => DayOfWeek.Saturday,
        TurnoDoImpedimento.SabadoTarde => DayOfWeek.Saturday,
        _ => null,
    };

    private static bool OOrganizadorPermite(Torneio torneio, TurnoDoImpedimento turno) => turno switch
    {
        TurnoDoImpedimento.QuintaNoite => torneio.PermiteImpedimentoQuintaNoite,
        TurnoDoImpedimento.SextaNoite => torneio.PermiteImpedimentoSextaNoite,
        TurnoDoImpedimento.SabadoManha => torneio.PermiteImpedimentoSabadoManha,
        TurnoDoImpedimento.SabadoTarde => torneio.PermiteImpedimentoSabadoTarde,
        _ => true,
    };

    // DUAS CONDIÇÕES, e as duas precisam valer: o organizador permitiu o turno na criação E o
    // dia que ele nomeia é dia deste torneio.
    //
    // ⚠️ SEM `DataInicio` VALE SÓ A PRIMEIRA. Torneio em montagem ainda não tem dia nenhum, e
    // esconder os quatro turnos ali seria trocar um defeito por outro: a seção de impedimentos
    // sumiria da tela de inscrição de um torneio que ainda vai ganhar data.
    public static bool Tem(Torneio torneio, TurnoDoImpedimento turno)
    {
        if (turno == TurnoDoImpedimento.Nenhum) return true;
        if (!OOrganizadorPermite(torneio, turno)) return false;
        if (torneio.DataInicio is null) return true;

        return DiaDe(turno) is DayOfWeek dia
            && JanelasDeImpedimento.DiaDoTorneio(torneio, dia) != null;
    }

    // ⚠️ `atual` ENTRA NA LISTA MESMO QUE O TORNEIO NÃO TENHA O TURNO. Sem isso, a dupla que
    // marcou "Sexta à noite" antes desta regra fica PRESA nela: a opção some do seletor e não
    // há como escolher outra coisa — nem pelo jogador, nem pelo organizador no painel.
    public static IReadOnlyList<TurnoDoImpedimento> Oferecidos(
        Torneio torneio, TurnoDoImpedimento atual = TurnoDoImpedimento.Nenhum)
    {
        var lista = new List<TurnoDoImpedimento> { TurnoDoImpedimento.Nenhum };
        foreach (var turno in new[]
                 {
                     TurnoDoImpedimento.QuintaNoite, TurnoDoImpedimento.SextaNoite,
                     TurnoDoImpedimento.SabadoManha, TurnoDoImpedimento.SabadoTarde,
                 })
        {
            if (Tem(torneio, turno) || turno == atual) lista.Add(turno);
        }
        return lista;
    }

    // A MESMA RÉGUA PRO SELETOR VIZINHO (os dois ficam lado a lado no card do organizador).
    // Aqui não há interruptor de criação: quem decide é só o calendário, e quem já sabe
    // responder é `ConcentracaoDeJogos.JanelaDoTurno` — ele devolve null justamente quando o
    // turno não existe no torneio.
    public static IReadOnlyList<TurnoDeConcentracao> OferecidosParaConcentrar(
        Torneio torneio, TurnoDeConcentracao atual = TurnoDeConcentracao.Nenhuma)
    {
        var lista = new List<TurnoDeConcentracao> { TurnoDeConcentracao.Nenhuma };
        foreach (var turno in new[]
                 {
                     TurnoDeConcentracao.SextaNoite, TurnoDeConcentracao.SabadoManha,
                     TurnoDeConcentracao.SabadoTarde,
                 })
        {
            var existe = torneio.DataInicio is null
                || ConcentracaoDeJogos.JanelaDoTurno(torneio, turno) != null;

            if (existe || turno == atual) lista.Add(turno);
        }
        return lista;
    }
}
