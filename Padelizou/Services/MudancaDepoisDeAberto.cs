namespace Padelizou.Services;

// O QUE PODE MUDAR COM O TORNEIO JÁ ABERTO — e o que muda de significado quando muda.
//
// Felipe, 18/08/2026: "permita q os organizadores vejam todos os dados de criação, igualmente
// na edição". O motivo é bom e já apareceu três vezes neste projeto: interruptor que só existe
// na tela de CRIAÇÃO vira beco (foi assim com a forma de recebimento, com reabrir inscrições e
// com "só confirmo depois de pago").
//
// ⚠️ MAS "ver tudo" NÃO É "poder tudo". Alguns campos, mexidos com gente já inscrita, não
// causam erro nenhum — causam ESTRAGO SILENCIOSO, que é pior, porque ninguém vê acontecer.
// Esta classe é onde essa diferença fica escrita, em vez de virar um `if` solto na tela.
public static class MudancaDepoisDeAberto
{
    // ⚠️ O LIMITE DE VAGAS NÃO PODE CAIR ABAIXO DE QUEM JÁ ENTROU.
    //
    // Sem esta trava, baixar o limite de 32 pra 8 com 20 duplas inscritas não apaga ninguém —
    // é pior: `PagamentoInscricaoService` passa a responder "lotado" pra todo mundo, as 20
    // continuam lá, e a lista de espera vira a fila de um torneio que, pra ele, já estourou.
    // O organizador não vê nada acontecer; só param de chegar inscrições.
    //
    // Nulo = "sem limite", e isso é sempre permitido: tirar o teto não expulsa ninguém.
    public static string? ProblemaComOLimite(int? novoLimite, int inscricoesJaFeitas)
    {
        if (novoLimite == null) return null;

        if (novoLimite < 0)
            return "O limite de vagas não pode ser negativo.";

        if (novoLimite < inscricoesJaFeitas)
            return $"O limite não pode ficar abaixo das {inscricoesJaFeitas} inscrições que já existem — "
                + "elas não seriam apagadas, mas o torneio passaria a se comportar como lotado. "
                + "Para reduzir de verdade, cancele inscrições antes.";

        return null;
    }

    // ⚠️ TROCAR O FORMATO depois que a chave saiu é o caso que este arquivo existe pra impedir.
    //
    // Chave e Americano guardam inscrição em TABELAS diferentes (`Dupla` × `InscricaoAmericana`)
    // e produzem partidas com significados diferentes. Virar a chave com jogos já criados deixa
    // o torneio com metade dos dados de um formato e metade do outro — e nada disso aparece na
    // tela até alguém abrir o chaveamento.
    //
    // Enquanto ninguém se inscreveu, trocar é inofensivo: não há o que ficar órfão.
    public static string? ProblemaComOFormato(string? formatoAtual, string? formatoNovo, int inscricoesJaFeitas)
    {
        if (string.IsNullOrWhiteSpace(formatoNovo) || formatoNovo == formatoAtual) return null;

        if (inscricoesJaFeitas > 0)
            return "Não dá pra trocar o formato do torneio com inscrições já feitas — "
                + "chave e Americano guardam as inscrições de formas diferentes, e as que existem ficariam órfãs.";

        return null;
    }
}
