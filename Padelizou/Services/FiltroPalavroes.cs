using System.Globalization;
using System.Text;

namespace Padelizou.Services;

// Primeira linha de defesa contra ofensas nos comentários de perfil — bloqueia na hora de
// publicar. Não é (e não pretende ser) infalível: por isso o dono do perfil e o autor sempre
// podem remover um comentário depois (ver JogadoresController.RemoverComentario).
public static class FiltroPalavroes
{
    private static readonly string[] Bloqueadas =
    {
        "arrombad", "babaca", "bosta", "burro", "burra", "canalha", "corno", "cretin",
        "desgraç", "estupid", "fdp", "filho da puta", "filha da puta", "foda-se", "fodase",
        "idiota", "imbecil", "lixo", "merda", "otari", "otário", "otaria", "piranha",
        "porra", "putaria", "puta", "retardad", "vagabund", "vadia", "viad", "escrot",
        "caralho", "cuzão", "cuzao", "desgracad", "verme", "nojent",
    };

    public static bool EhOfensivo(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        var normalizado = Normalizar(texto);
        return Bloqueadas.Any(p => normalizado.Contains(Normalizar(p)));
    }

    private static string Normalizar(string texto)
    {
        var semAcento = new string(texto
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return semAcento.ToLowerInvariant();
    }
}
