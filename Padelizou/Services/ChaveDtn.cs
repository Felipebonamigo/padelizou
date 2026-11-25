using Padelizou.Models;

namespace Padelizou.Services;

// TRETA (25/11/2026): os papéis de categoria da chave DTN e da Finalíssima — ver TRETA.md.
//
// Categoria com papel é festa, não campanha: fica fora do ranking, do Padelímetro, do MVP e
// das taxas, e ninguém se inscreve nela (quem entra é o robô ou o organizador).
public static class ChaveDtn
{
    public const string Dtn = "DTN";
    public const string Finalissima = "Finalissima";

    // A fase gravada em `Dupla.UltimaFase` de quem perde a final decisiva — o lugar do
    // "Campeao" numa chave invertida.
    public const string Perdedor = "DTN";

    public static bool TemPapel(Categoria? categoria) => categoria?.PapelDaChave != null;
    public static bool EhDtn(Categoria? categoria) => categoria?.PapelDaChave == Dtn;
    public static bool EhFinalissima(Categoria? categoria) => categoria?.PapelDaChave == Finalissima;
}
