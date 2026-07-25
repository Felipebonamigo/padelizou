namespace Padelizou.ViewModels;

public class AgendaItem
{
    public DateTime Data { get; set; }
    public string Tipo { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public string Subtitulo { get; set; } = null!;
    public string Icone { get; set; } = null!;
    public string? LinkController { get; set; }
    public string? LinkAction { get; set; }
    public int? LinkId { get; set; }
}
