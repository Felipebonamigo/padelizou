using Padelizou.Models;

namespace Padelizou.Services;

// Traduz os quatro turnos que a dupla marca na inscrição (Dupla.ImpedimentoQuintaNoite etc.)
// em janelas de data/hora reais — as que GradeDeJogos.Encaixar precisa pra NÃO sortear a dupla
// dentro delas.
//
// ⚠️ Até 21/08/2026 isso nunca acontecia: Torneio.TaxaPorImpedimento cobra pela janela ("cada
// um deles tira uma janela da grade", Models/Torneio.cs:59-62), mas nenhum código de sorteio
// lia Dupla.ImpedimentoQuintaNoite/SextaNoite/SabadoManha/SabadoTarde — grep confirma que os
// quatro campos só alimentavam preço e exibição. A dupla pagava pela flexibilidade e podia ser
// escalada exatamente na janela que pagou pra evitar.
public static class JanelasDeImpedimento
{
    // Sábado é o único dia ABERTO do padrão de torneio (GradeDeJogos: sexta só tem turno da
    // noite a partir de HoraInicioDoDia=18h; sábado vai das 8h às 23h/23h50) — manhã e tarde
    // são dois pedaços do MESMO dia, e nem a tela de criação (Create.cshtml) nem o modelo
    // definem a hora de corte entre eles; os rótulos são só "Sábado de manhã" / "Sábado à
    // tarde", sem hora nenhuma.
    //
    // Meio-dia é a convenção adotada aqui. É um valor razoável, não um que o Felipe tenha
    // escolhido — se o corte real for outro (13h é comum em torneio de clube), é só mudar esta
    // constante; o resto do cálculo segue igual.
    public static readonly TimeSpan CorteSabadoManhaTarde = new(12, 0, 0);

    // As janelas que ESTA dupla não pode jogar, neste torneio. Vazio se ela não marcou
    // impedimento nenhum, ou se o torneio não tem data (sem DataInicio não dá pra achar
    // "a próxima quinta/sexta/sábado" — mesma trava que QuintaEhDiaDoTorneio usa).
    public static IEnumerable<(DateTime Inicio, DateTime Fim)> Da(Torneio torneio, Dupla dupla)
    {
        if (torneio.DataInicio is not DateTime inicioTorneio) yield break;

        // Quinta e sexta bloqueiam o dia INTEIRO: nos dois só existe o turno da noite (não há
        // sessão de tarde antes de HoraInicioDoDia), então "à noite" e "o dia inteiro" coincidem.
        //
        // ⚠️ A busca do dia é PRA FRENTE, mas CURTA (no máximo os 3 dias que seguem
        // DataInicio) — nunca "a próxima ocorrência daquele dia da semana", que foi o primeiro
        // jeito que isto foi escrito e tinha um bug: num torneio que começa no SÁBADO (sem
        // sexta nenhuma), buscar "a próxima sexta" sem limite achava a sexta da SEMANA
        // SEGUINTE — e geraria uma janela que não corresponde a jogo nenhum do torneio, mas
        // também não é o "sem janela" correto (torneio sem sexta não deveria gerar janela
        // nenhuma pra este campo). Limitado aos 3 dias que seguem o início, um torneio de
        // sábado-domingo simplesmente não acha sexta nenhuma — devolve null, não bloqueia
        // nada, que é o comportamento certo pra um dia que não existe no calendário dele.
        DateTime? DiaDoTorneio(DayOfWeek alvo)
        {
            for (int offset = 0; offset < 3; offset++)
            {
                var dia = inicioTorneio.Date.AddDays(offset);
                if (dia.DayOfWeek == alvo) return dia;
            }
            return null;
        }

        if (dupla.ImpedimentoQuintaNoite && torneio.QuintaEhDiaDoTorneio
            && DiaDoTorneio(DayOfWeek.Thursday) is DateTime quinta)
        {
            yield return (quinta, quinta.AddDays(1));
        }

        if (dupla.ImpedimentoSextaNoite && DiaDoTorneio(DayOfWeek.Friday) is DateTime sexta)
        {
            yield return (sexta, sexta.AddDays(1));
        }

        if ((dupla.ImpedimentoSabadoManha || dupla.ImpedimentoSabadoTarde)
            && DiaDoTorneio(DayOfWeek.Saturday) is DateTime sabado)
        {
            var corte = sabado.Add(CorteSabadoManhaTarde);

            if (dupla.ImpedimentoSabadoManha) yield return (sabado, corte);
            if (dupla.ImpedimentoSabadoTarde) yield return (corte, sabado.AddDays(1));
        }
    }

    // Mesmo padrão de RoboDoChaveamento.OcupantesPorDupla: um mapa pronto pra passar direto
    // pra GradeDeJogos.Encaixar. Dupla sem impedimento nenhum fica de fora do mapa (array
    // vazio é descartado) — não sobra entrada inútil pra Encaixar checar à toa.
    public static Dictionary<int, (DateTime Inicio, DateTime Fim)[]> PorDupla(
        Torneio torneio, IEnumerable<Dupla> duplas)
    {
        var mapa = new Dictionary<int, (DateTime, DateTime)[]>();
        foreach (var dupla in duplas)
        {
            var janelas = Da(torneio, dupla).ToArray();
            if (janelas.Length > 0) mapa[dupla.Id] = janelas;
        }
        return mapa;
    }

    public static Dictionary<int, (DateTime Inicio, DateTime Fim)[]> PorDupla(Torneio torneio) =>
        PorDupla(torneio, torneio.Categorias.SelectMany(c => c.Duplas));
}
