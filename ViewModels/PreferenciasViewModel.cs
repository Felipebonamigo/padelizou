namespace Padelizou.ViewModels;

public class PreferenciasViewModel
{
    public string? LadoQuadra { get; set; }
    public bool NotificarEmail { get; set; } = true;
    public bool NotificarWhatsApp { get; set; } = true;
    public HashSet<int> CategoriasSelecionadas { get; set; } = new();
    public HashSet<int> ClubesSelecionados { get; set; } = new();
    public HashSet<string> DiasHorariosSelecionados { get; set; } = new();
}
