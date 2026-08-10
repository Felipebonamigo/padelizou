using Padelizou.Models;

namespace Padelizou.Services;

// "QUEM NÃO PAGAR PERDE A VAGA" — a caixinha que o organizador marcava e que o sistema NUNCA
// cumpriu (Felipe, 10/08/2026: *"arrume esse 'quem não pagar perde a vaga' que não faz nada"*).
//
// ⚠️ `Torneio.ExcluirSeNaoPagar` existia em exatamente dois lugares no código: o modelo e a tela
// de criação. **Nenhuma linha a lia.** No NATA PADEL TOUR ela está LIGADA desde o começo, e o
// Lucas seguiu o torneio inteiro achando que o sistema ia limpar sozinho no fechamento.
//
// A saída NÃO foi passar a excluir sozinho. Apagar inscrição de gente real por conta própria é
// exatamente o desastre que já aconteceu aqui uma vez — as 8 pessoas que sumiram sem deixar
// rastro (ver PrazoParaPagar). O sistema PERGUNTA, e quem decide é quem organiza:
//
//   • "Remover quem não pagou" — libera as vagas, avisa quem saiu e chama a lista de espera.
//   • "Vou acertar pessoalmente" — ninguém sai; muita inscrição se resolve no Pix da quadra.
//
// A caixinha deixou de prometer exclusão e passou a significar "me pergunte no fechamento".
public static class DecisaoSobreQuemNaoPagou
{
    // QUANDO perguntar. O Felipe pediu "na data que encerrar as inscrições" — e aqui vale a
    // MAIS TARDE entre o fechamento e o prazo de pagamento.
    //
    // ⚠️ Os dois podem divergir: o organizador pode fechar as inscrições no dia 5 e ainda aceitar
    // pagamento até o dia 10. Perguntar no dia 5 ofereceria remover gente que ainda está DENTRO
    // do prazo combinado — e o pior desfecho possível é alguém perder a vaga e pagar no dia
    // seguinte, no prazo que a própria tela prometeu.
    public static DateTime? Quando(Torneio torneio)
    {
        if (torneio.PrevisaoEncerramentoInscricoes is not { } fecha) return null;

        var prazo = torneio.PrazoPagamento ?? fecha;
        var maisTarde = prazo.Date > fecha.Date ? prazo : fecha;

        // Fim do dia: quem tem até o dia 5 pra pagar tem o dia 5 inteiro, e a pergunta é do dia
        // seguinte em diante.
        return maisTarde.Date.AddDays(1).AddTicks(-1);
    }

    // Chegou a hora de perguntar a ESTE organizador?
    //
    // ⚠️ `jaPerguntouEm` é o que garante UMA pergunta só: o varredor passa de hora em hora, e sem
    // isso o organizador levaria o mesmo aviso doze vezes no primeiro dia.
    public static bool HoraDePerguntar(Torneio torneio, DateTime agora, DateTime? jaPerguntouEm)
    {
        if (!torneio.ExcluirSeNaoPagar) return false;   // ele já disse que acerta por fora
        if (torneio.CanceladoEm != null) return false;
        if (torneio.PrecoInscricao <= 0) return false;
        if (jaPerguntouEm != null) return false;

        return Quando(torneio) is { } quando && agora > quando;
    }

    // Dá pra remover agora? A remoção de inscrito só existe com as inscrições ABERTAS — depois do
    // sorteio já pode haver Partida apontando pra dupla, e tirá-la deixaria jogo órfão.
    //
    // ⚠️ Não é hipótese remota: a pergunta chega no dia do fechamento, e o organizador pode ter
    // apertado "Encerrar inscrições" antes de responder. A tela precisa dizer isso em vez de
    // deixar o botão falhar calado.
    public static bool PodeRemoverAgora(Torneio torneio) => torneio.Status == "Inscrições Abertas";

    public const string PorQueNaoPodeRemover =
        "As inscrições deste torneio já foram encerradas, e depois disso a remoção não é mais "
        + "possível (o chaveamento pode já ter usado essas duplas). Reabra as inscrições se ainda "
        + "quiser tirar quem não pagou.";

    // O texto da caixinha na criação/edição do torneio. Fica aqui, e não solto na view, porque
    // foi justamente o texto solto que passou meses prometendo o que o código não fazia.
    public const string RotuloDaCaixinha =
        "No fechamento, me perguntar o que fazer com quem não pagou";

    public const string ExplicacaoDaCaixinha =
        "Ninguém é removido automaticamente. Na data em que as inscrições fecharem você recebe um "
        + "aviso com a lista de quem está devendo, e escolhe entre liberar as vagas ou acertar "
        + "pessoalmente.";

    public const string TituloDoAviso = "Tem gente inscrita sem pagar";
}
