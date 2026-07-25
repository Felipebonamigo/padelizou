using Padelizou.Models;

namespace Padelizou.ViewModels;

// Linha da vitrine de times.
public class TimeResumoVM
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? Logo { get; set; }
    public string? Clube { get; set; }
    public int Membros { get; set; }
    public int Pontos { get; set; }
}

public class TimeDetalheVM
{
    public Time Time { get; set; } = null!;
    public List<MembroTimeVM> Membros { get; set; } = new();

    public int TotalPontos => Membros.Sum(m => m.Pontos);
}

public class MembroTimeVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Pontos { get; set; }
    public bool EhDono { get; set; }
}
