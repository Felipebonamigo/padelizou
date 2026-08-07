namespace Padelizou.Services;

// Com que frequência dá pra trocar o próprio nome e apelido.
//
// O nome é como as pessoas te acham no Padelizou: ele está na lista de inscritos, no placar
// da mesa, no ranking, na ficha do professor e no histórico de todo torneio que você jogou.
// Trocar toda semana faz o parceiro de terça não reconhecer quem entrou na dupla dele, e
// apaga a ligação entre a pessoa de hoje e os resultados dela de seis meses atrás.
//
// Por isso: o NOME muda UMA VEZ SÓ; o APELIDO, a cada 1 mês. O apelido é mais solto de
// propósito — ele existe justamente pra ser o "como me chamam agora", e mudar de apelido é
// bem mais inocente do que mudar de nome.
//
// A troca única é livre e existe pra um caso só: quem se cadastrou com o nome errado (ou com
// "asdf" na pressa) não pode ficar preso a ele pra sempre. Depois dela o nome congela —
// decisão do Felipe em 06/08/2026, quando ficou provado que o **Ranking RS casa atleta por
// nome**: nome que dança é jogador que some do ranking parceiro no meio da temporada.
public static class TrocaDeNome
{
    public const int MesesParaTrocarApelido = 1;

    // Fica pra quem consulta o histórico: era a regra até 06/08/2026.
    [Obsolete("O nome agora muda uma vez só. Use PodeTrocarNome.")]
    public const int MesesParaTrocarNome = 6;

    // `Definitivo` = não libera nunca mais, então não existe data pra prometer. Sem esse
    // sinal a tela cairia em "libera em 01/01/0001", que é pior do que não dizer nada.
    public record Resultado(bool Pode, DateTime? LiberaEm, bool Definitivo = false)
    {
        // Quantos dias faltam, arredondando pra cima: "faltam 0 dias" não é resposta.
        public int DiasQueFaltam(DateTime agora) =>
            LiberaEm is { } q && q > agora ? (int)Math.Ceiling((q - agora).TotalDays) : 0;
    }

    // Uma vez só: nunca trocou → pode; já trocou → nunca mais (o suporte resolve exceção).
    public static Resultado PodeTrocarNome(DateTime? ultimaTroca, DateTime agora) =>
        ultimaTroca == null
            ? new Resultado(true, null)
            : new Resultado(false, null, Definitivo: true);

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

    // O porquê, numa frase. Vai junto de todo aviso sobre o NOME: uma trava sem motivo
    // parece implicância do site, e é aí que a pessoa tenta driblar (enfiando o apelido no
    // campo do nome, por exemplo) em vez de colaborar.
    public const string PorQueONomeTrava =
        "para mantermos o padrão dos rankings e dos torneios — é pelo nome que você é " +
        "encontrado nas listas, na chave e no ranking estadual.";

    // Antes de trocar: a regra dita na tela, pra ela decidir com a informação na mão.
    public static string AvisoAntesDeTrocarNome() =>
        $"Você pode corrigir seu nome **uma única vez**. Depois disso ele fica fixo, {PorQueONomeTrava}";

    public static string AvisoAntesDeTrocar(string oQue, int meses) =>
        $"Depois de salvar, você só poderá trocar {oQue} de novo daqui a {meses} " +
        (meses == 1 ? "mês." : "meses.");

    // Depois de trocar: a confirmação com a data, que é o "avise-o ao alterar" do pedido.
    public static string AvisoDepoisDeTrocar(string oQue, DateTime liberaEm) =>
        $"{oQue} atualizado. A próxima troca só a partir de {liberaEm:dd/MM/yyyy}.";

    public static string AvisoDepoisDeTrocarNome() =>
        $"Nome atualizado — e agora ele fica fixo, {PorQueONomeTrava}";

    // Na recusa: quando libera e quanto falta — negar sem dizer "até quando" faz a pessoa
    // tentar de novo amanhã, e no outro dia, sem nunca entender. Quando é definitivo, diz
    // isso com todas as letras E oferece a saída, senão vira beco sem saída.
    public static string Recusa(string oQue, Resultado resultado, DateTime agora)
    {
        if (resultado.Definitivo)
            return $"{oQue} já foi corrigido uma vez e agora fica fixo, {PorQueONomeTrava} " +
                   $"Se precisa mesmo mudar, fale com a gente pelo \"Reportar problema\".";

        int dias = resultado.DiasQueFaltam(agora);
        var quando = resultado.LiberaEm?.ToString("dd/MM/yyyy") ?? "";

        return $"{oQue} só pode ser trocado de novo em {quando}" +
               (dias > 0 ? $" — faltam {dias} dia{(dias == 1 ? "" : "s")}." : ".");
    }

    // Vai UMA vez no fim do aviso, não colada em cada recusa: quem trocou nome e apelido no
    // mesmo save lia a mesma frase duas vezes seguidas, e texto repetido faz a tela parecer
    // feita às pressas — além de empurrar a informação útil pra longe.
    public const string ORestoFoiSalvo = "O resto do perfil você salva normalmente.";
}
