using Padelizou.Models;

namespace Padelizou.ViewModels;

// Mapa de ocupação: a semana inteira, quadra por quadra, num relance. É a tela que o
// dono abre pra saber se vale abrir mais horário ou fazer promoção na terça de manhã.
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
