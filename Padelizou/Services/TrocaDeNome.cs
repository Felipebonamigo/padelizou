namespace Padelizou.Services;

// Com que frequência dá pra trocar o próprio nome e apelido.
//
// O nome é como as pessoas te acham no Padelizou: ele está na lista de inscritos, no placar
// da mesa, no ranking, na ficha do professor e no histórico de todo torneio que você jogou.
// Trocar toda semana faz o parceiro de terça não reconhecer quem entrou na dupla dele, e
// apaga a ligação entre a pessoa de hoje e os resultados dela de seis meses atrás.
//
// Por isso: NOME a cada 6 meses, APELIDO a cada 1 mês. O apelido é mais solto de propósito —
// ele existe justamente pra ser o "como me chamam agora", e mudar de apelido é bem mais
// inocente do que mudar de nome.
//
// A primeira troca é sempre livre: quem se cadastrou com o nome errado (ou com "asdf" na
// pressa) não pode ficar seis meses preso a ele.
public static class TrocaDeNome
{
    public const int MesesParaTrocarNome = 6;
    public const int MesesParaTrocarApelido = 1;

    public record Resultado(bool Pode, DateTime? LiberaEm)
    {
        // Quantos dias faltam, arredondando pra cima: "faltam 0 dias" não é resposta.
        public int DiasQueFaltam(DateTime agora) =>
            LiberaEm is { } q && q > agora ? (int)Math.Ceiling((q - agora).TotalDays) : 0;
    }

    public static Resultado PodeTrocarNome(DateTime? ultimaTroca, DateTime agora) =>
        Avaliar(ultimaTroca, agora, MesesParaTrocarNome);

    public static Resultado PodeTrocarApelido(DateTime? ultimaTroca, DateTime agora) =>
        Avaliar(ultimaTroca, agora, MesesParaTrocarApelido);

    private static Resultado Avaliar(DateTime? ultimaTroca, DateTime agora, int meses)
    {
        // Nunca trocou: livre. É o caso de quem se cadastrou com erro de digitação.
        if (ultimaTroca is not { } ultima) return new Resultado(true, null);

        var libera = ultima.AddMonths(meses);
        return new Resultado(agora >= libera, libera);
    }

    // Só conta como troca se o texto MUDOU de verdade. Salvar o perfil pra corrigir o
    // telefone não pode consumir a troca de nome do semestre — e é o que aconteceria se a
    // gente carimbasse a data toda vez que o formulário passa por aqui.
    public static bool Mudou(string? antes, string? depois) =>
        !string.Equals((antes ?? "").Trim(), (depois ?? "").Trim(), StringComparison.Ordinal);

    // ── O que a pessoa lê ─────────────────────────────────────────────────────────────────

    // Antes de trocar: a regra dita na tela, pra ela decidir com a informação na mão.
    public static string AvisoAntesDeTrocar(string oQue, int meses) =>
        $"Depois de salvar, você só poderá trocar {oQue} de novo daqui a {meses} " +
        (meses == 1 ? "mês." : "meses.");

    // Depois de trocar: a confirmação com a data, que é o "avise-o ao alterar" do pedido.
    public static string AvisoDepoisDeTrocar(string oQue, DateTime liberaEm) =>
        $"{oQue} atualizado. A próxima troca só a partir de {liberaEm:dd/MM/yyyy}.";

    // Na recusa: quando libera e quanto falta — negar sem dizer "até quando" faz a pessoa
    // tentar de novo amanhã, e no outro dia, sem nunca entender.
    public static string Recusa(string oQue, Resultado resultado, DateTime agora)
    {
        int dias = resultado.DiasQueFaltam(agora);
        var quando = resultado.LiberaEm?.ToString("dd/MM/yyyy") ?? "";

        return $"{oQue} só pode ser trocado de novo em {quando}" +
               (dias > 0 ? $" — faltam {dias} dia{(dias == 1 ? "" : "s")}." : ".") +
               " O resto do perfil você salva normalmente.";
    }
}
