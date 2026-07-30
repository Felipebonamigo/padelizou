using Padelizou.Models;

namespace Padelizou.ViewModels;

// Mapa de ocupação: a semana inteira, quadra por quadra, num relance. É a tela que o
// dono abre pra saber se vale abrir mais horário ou fazer promoção na terça de manhã.
// A casa do dono de clube. As telas de gestão (mapa da semana, horários e preços, financeiro,
// política) já existiam, mas soltas — não havia um lugar que respondesse "como está o meu
// clube hoje" nem mostrasse QUEM agendou, que é a primeira coisa que o dono quer ver.
public class PainelClubeVM
{
    public Clube Clube { get; set; } = null!;

    public int ReservasHoje { get; set; }
    public int ReservasNaSemana { get; set; }
    public int QuadrasAtivas { get; set; }
    public int HorariosPublicados { get; set; }
    public decimal ReceitaDoMes { get; set; }

    // Ocupação da semana corrente, mesmo cálculo do mapa — serve de termômetro na entrada.
    public int PercentualOcupacaoSemana { get; set; }

    // O DIA em ordem de hora: reservas (com selo de pagamento) e horários livres com o
    // botão de marcar. É a tela de operação — o dono trabalha o dia, não o mês.
    public List<ReservaDoPainelVM> Hoje { get; set; } = new();

    public List<ReservaDoPainelVM> Proximas { get; set; } = new();

    // Sem quadra ou sem horário publicado, o clube não aparece pra ninguém marcar — é a
    // mesma armadilha da escada do professor, e o painel precisa dizer isso na cara.
    public bool PrecisaDeQuadra => QuadrasAtivas == 0;
    public bool PrecisaDeHorario => QuadrasAtivas > 0 && HorariosPublicados == 0;
    public bool InvisivelPraMarcacao => !Clube.MarcacaoHorariosAtiva || PrecisaDeQuadra || PrecisaDeHorario;
}

public class ReservaDoPainelVM
{
    public int MarcacaoId { get; set; }
    public DateTime DataHora { get; set; }
    public string Quadra { get; set; } = "";
    public string Jogador { get; set; } = "";
    public string? Celular { get; set; }
    public bool EhMensalista { get; set; }
    public bool EhBloqueio { get; set; }
    public string? MotivoBloqueio { get; set; }
    public decimal? Valor { get; set; }

    // "pago" | "paga lá" | "" — e o balcão pra tela saber de quem é o × e o "recebi".
    public string Selo { get; set; } = "";
    public bool EhBalcao { get; set; }

    // Linha de horário LIVRE do dia (sem reserva): vira o botão de marcar do balcão.
    public bool Livre { get; set; }
    public int QuadraId { get; set; }

    public bool EhHoje => DataHora.Date == DateTime.Today;
}

public class OcupacaoClubeVM
{
    public Clube Clube { get; set; } = null!;
    public DateTime InicioSemana { get; set; }

    public List<QuadraClube> Quadras { get; set; } = new();

    // Faixas de horário que existem na agenda do clube (ex: 07:00 → 22:00).
    public List<TimeSpan> Horarios { get; set; } = new();

    // Chave: (quadraId, dia, hora) → o que ocupa aquele slot.
    public Dictionary<(int Quadra, DateTime Dia, TimeSpan Hora), SlotVM> Slots { get; set; } = new();

    public int TotalSlots { get; set; }
    public int SlotsOcupados { get; set; }
    public decimal ReceitaSemana { get; set; }

    public int PercentualOcupacao => TotalSlots == 0 ? 0 : (int)Math.Round(SlotsOcupados * 100.0 / TotalSlots);

    public DateTime FimSemana => InicioSemana.AddDays(6);
}

public class SlotVM
{
    public int MarcacaoId { get; set; }
    public string Titulo { get; set; } = "";
    public bool EhBloqueio { get; set; }
    public bool EhMensalista { get; set; }
    public string Status { get; set; } = "";

    // "pago" | "paga lá" | "" (ver ReservaDeBalcao.Selo — vazio inclui reserva antiga).
    public string Selo { get; set; } = "";
    public bool EhBalcao { get; set; }
}

// Financeiro do clube por quadra e por período.
public class FinanceiroClubeVM
{
    public Clube Clube { get; set; } = null!;
    public string Periodo { get; set; } = "mes";
    public string PeriodoRotulo { get; set; } = "";

    public decimal Receita { get; set; }
    public decimal APerder { get; set; }        // no-shows não cobrados
    public decimal Recuperado { get; set; }     // no-shows cobrados

    // Origem e pendência: quanto veio de reserva registrada no balcão (o resto é site),
    // e quanto de "paga lá" ainda não foi recebido — a fila de cobrança do dono.
    public decimal ReceitaBalcao { get; set; }
    public decimal AReceber { get; set; }
    public decimal ReceitaSite => Receita - ReceitaBalcao;
    public int Reservas { get; set; }
    public int Cancelamentos { get; set; }
    public int NoShows { get; set; }

    public List<ReceitaQuadraVM> PorQuadra { get; set; } = new();
    public List<ReceitaDiaVM> PorDiaSemana { get; set; } = new();

    public bool TemMovimento => Reservas > 0;
}

public class ReceitaQuadraVM
{
    public string Quadra { get; set; } = "";
    public int Reservas { get; set; }
    public decimal Receita { get; set; }
    public int Horas { get; set; }
}

public class ReceitaDiaVM
{
    public DayOfWeek Dia { get; set; }
    public int Reservas { get; set; }
    public decimal Receita { get; set; }

    public string Rotulo => Dia switch
    {
        DayOfWeek.Sunday => "Domingo",
        DayOfWeek.Monday => "Segunda",
        DayOfWeek.Tuesday => "Terça",
        DayOfWeek.Wednesday => "Quarta",
        DayOfWeek.Thursday => "Quinta",
        DayOfWeek.Friday => "Sexta",
        _ => "Sábado",
    };
}
