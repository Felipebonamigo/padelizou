namespace Padelizou.ViewModels;

public class PreferenciasViewModel
{
    public string? LadoQuadra { get; set; }
    public string? Lateralidade { get; set; }
    public bool PerfilPrivado { get; set; }
    public string? Instagram { get; set; }
    public bool NotificarEmail { get; set; } = true;

    // Desmarcado por padrão desde 04/08/2026, ao contrário de todos os outros. O WhatsApp é o
    // único canal em que mandar pra quem não pediu tem consequência: a Meta restringiu o
    // número do Padelizou justamente por isso. Marcado por omissão, a pessoa "aceitava" sem
    // nunca ter decidido — e é exatamente essa mensagem que vira denúncia.
    public bool NotificarWhatsApp { get; set; }
    public bool AceitaConvitesJogo { get; set; } = true;
    public bool NotificarTorneiosAbertos { get; set; } = true;
    public bool NotificarSeguidosTorneio { get; set; } = true;
    public bool NotificarAvisoJogo { get; set; } = true;
    public bool NotificarJogoAula { get; set; } = true;
    public bool NotificarRaqueteLivre { get; set; } = true;
    public bool NotificarHorarioVagoRegiao { get; set; }
    public HashSet<int> CategoriasSelecionadas { get; set; } = new();
    public HashSet<int> ClubesSelecionados { get; set; } = new();
    public HashSet<string> DiasHorariosSelecionados { get; set; } = new();
    public HashSet<int> CidadesSelecionadas { get; set; } = new();
}
