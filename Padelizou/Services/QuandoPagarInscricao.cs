using Padelizou.Models;

namespace Padelizou.Services;

// "Você quer pagar agora ou depois?" — a pergunta que a inscrição faz quando o torneio aceita
// pagamento até o fechamento (Felipe, 09/08/2026: "no momento que ela for se inscrever,
// questione se ela quer pagar agora ou depois, e somente a hora que ela for pagar, gere a
// cobrança").
//
// ⚠️ ANTES DISTO A PERGUNTA NÃO EXISTIA, e as duas respostas possíveis eram ruins de um jeito
// diferente:
//
//   • No arranjo "só confirmo depois de pago", ninguém era perguntado: todo mundo ia direto pro
//     checkout, e quem não terminava sumia (ver PrazoParaPagar — foi assim que 8 pessoas
//     evaporaram no NATA PADEL TOUR).
//
//   • No arranjo "pago até o fechamento", também ninguém era perguntado — mas pro outro lado:
//     a inscrição nascia pendente e quem QUERIA resolver na hora tinha que voltar, achar a
//     faixa amarela na tela do torneio e clicar de novo. Quem quer pagar é justamente quem não
//     se deve fazer procurar.
//
// A regra que fica: **a cobrança só nasce quando alguém diz que vai pagar**. Fatura criada "por
// via das dúvidas" é fatura que fica pendurada — no painel do organizador, na conta dele e no
// e-mail de cobrança do meio de pagamento.
public static class QuandoPagarInscricao
{
    // Os valores que trafegam no formulário. Texto, não bool, porque na tela são duas escolhas
    // com peso igual — um checkbox "pagar agora" desmarcado pareceria a opção esquecida.
    public const string Agora = "agora";
    public const string Depois = "depois";

    // A pergunta só faz sentido onde as DUAS respostas são possíveis.
    //
    // Em "só confirmo depois de pago" não há escolha: o pagamento é a inscrição. Em torneio que
    // recebe por fora, ou de graça, não há o que perguntar — não existe cobrança pelo site.
    public static bool PodePerguntar(Torneio torneio, bool podeCobrar) =>
        podeCobrar
        && !PrazoParaPagar.ExigePagarNaHora(torneio)
        && torneio.PrecoInscricao > 0;

    // ⚠️ Qualquer coisa que não seja um "agora" explícito vale como DEPOIS — inclusive nulo,
    // que é o que chega de um formulário antigo em cache ou de um cliente que não mandou o
    // campo. Errar pra cá deixa a pessoa inscrita e devendo, com o botão "pagar agora" à mão;
    // errar pro outro lado cria fatura pra quem não pediu.
    public static bool VaiPagarAgora(Torneio torneio, bool podeCobrar, string? escolha) =>
        PodePerguntar(torneio, podeCobrar) && escolha == Agora;
}
