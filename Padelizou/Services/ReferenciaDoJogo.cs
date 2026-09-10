using System.Globalization;

namespace Padelizou.Services;

// "QUAL JOGO" NO FORMULÁRIO DE TROCAR HORÁRIO — o jogo real (Id) ou o previsto (categoria,
// fase, número), num texto só (10/09/2026).
//
// O modal de trocar horário passou a oferecer as eliminatórias PREVISTAS, que não existem no
// banco e por isso não têm Id. A linha da prévia escreve a referência dela no botão; o POST lê.
// Os dois lados passam pelo MESMO tipo, pelo mesmo motivo da AncoraDoJogo: duas fórmulas
// separadas viram um clique que não chega a lugar nenhum no dia em que uma delas mudar.
//
// Texto que não é referência devolve nulo — nunca exceção, nunca jogo inventado. É a defesa
// contra o POST montado à mão.
public sealed record ReferenciaDoJogo(int? PartidaId, int CategoriaId, string Fase, int Numero)
{
    private const string Prefixo = "previa";

    public bool EhPrevia => PartidaId == null;

    public static ReferenciaDoJogo Real(int partidaId) => new(partidaId, 0, "", 0);

    public static ReferenciaDoJogo Prevista(int categoriaId, string fase, int numero) =>
        new(null, categoriaId, fase, numero);

    public static ReferenciaDoJogo? Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        if (int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            return Real(id);

        // "previa:<categoria>:<fase>:<numero>". A fase é a última coisa que pode ter ":" — nomes
        // de fase são constantes nossas sem ele, mas o Split limitado protege do caso mesmo assim.
        var partes = texto.Split(':', 4);
        if (partes.Length != 4 || partes[0] != Prefixo) return null;
        if (!int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out var categoriaId)) return null;
        if (string.IsNullOrWhiteSpace(partes[2])) return null;
        if (!int.TryParse(partes[3], NumberStyles.None, CultureInfo.InvariantCulture, out var numero)) return null;

        return Prevista(categoriaId, partes[2], numero);
    }

    public override string ToString() =>
        PartidaId is int id
            ? id.ToString(CultureInfo.InvariantCulture)
            : $"{Prefixo}:{CategoriaId}:{Fase}:{Numero}";
}
