using Padelizou.Models;

namespace Padelizou.Services;

// O sexo com que a pessoa se identifica, e a única regra que depende dele: Mista e Casais são
// categorias de UM HOMEM E UMA MULHER.
//
// Até 08/08/2026 isso era honra pura — o sistema não guardava o dado, então não conferia nem
// avisava. O campo nasceu por causa dessa regra, e não o contrário: ele existe pra decidir
// inscrição nessas duas categorias, e nada mais lê isso.
public static class SexoDoJogador
{
    public const string Masculino = "Masculino";
    public const string Feminino = "Feminino";

    public static bool Existe(string? sexo) => sexo == Masculino || sexo == Feminino;

    // O que vem do formulário. Devolve null pro que não reconhece — assim um valor estranho
    // vira "não informou" em vez de virar uma terceira categoria silenciosa no banco.
    public static string? Normalizar(string? sexo)
    {
        var texto = (sexo ?? "").Trim();

        if (string.Equals(texto, Masculino, StringComparison.OrdinalIgnoreCase)) return Masculino;
        if (string.Equals(texto, Feminino, StringComparison.OrdinalIgnoreCase)) return Feminino;

        return null;
    }

    public static bool Informou(Jogador? jogador) => Existe(jogador?.Sexo);

    // A categoria pede um de cada?
    //
    // ⚠️ Casa pelo NOME, e não por um campo, porque a Categoria DO TORNEIO não guarda tipo —
    // ela copia só nome e código do catálogo. Lê `EhMista`/`EhCasal` DIRETO, e não
    // `ForaDaEscada`: Lendas também é fora da escada de nível (não gradua A/B/C), mas não é
    // par misto nenhum — dois veteranos do mesmo sexo jogam juntos numa boa. "Fora da escada"
    // e "exige um de cada" são duas perguntas diferentes que só coincidem em Mista e Casal.
    public static bool ExigeUmDeCada(string? nomeCategoria) =>
        FaixasDePadelimetro.EhMista(nomeCategoria) || FaixasDePadelimetro.EhCasal(nomeCategoria);

    public const string ComoInformar =
        "Abra Perfil › Editar perfil e informe o sexo — leva dez segundos.";

    // Devolve o motivo da recusa, ou null quando a dupla pode entrar.
    //
    // `segundo` nulo é a dupla aberta ("procurando parceiro"): não dá pra conferir um par que
    // ainda não existe, então aqui só se cobra que QUEM ESTÁ ENTRANDO tenha informado — a
    // conferência do par acontece quando o parceiro é definido.
    public static string? MotivoParaNaoEntrar(string? nomeCategoria, Jogador primeiro, Jogador? segundo)
    {
        if (!ExigeUmDeCada(nomeCategoria)) return null;

        var categoria = string.IsNullOrWhiteSpace(nomeCategoria) ? "Esta categoria" : nomeCategoria;

        // ⚠️ Quem não informou é barrado ANTES da comparação, e com o nome dele na frase: sem
        // isso a recusa sairia como "vocês são do mesmo sexo" pra uma dupla que talvez esteja
        // certa, e ninguém saberia o que fazer.
        if (!Informou(primeiro))
            return $"{NomeBonito.Curto(primeiro.Nome)} ainda não informou o sexo, e {categoria} "
                 + $"é para um homem e uma mulher. {ComoInformar}";

        if (segundo == null) return null;

        if (!Informou(segundo))
            return $"{NomeBonito.Curto(segundo.Nome)} ainda não informou o sexo, e {categoria} "
                 + $"é para um homem e uma mulher. {ComoInformar}";

        if (primeiro.Sexo == segundo.Sexo)
            return $"{categoria} é para um homem e uma mulher — e vocês dois marcaram "
                 + $"{(primeiro.Sexo == Masculino ? "masculino" : "feminino")}.";

        return null;
    }
}
