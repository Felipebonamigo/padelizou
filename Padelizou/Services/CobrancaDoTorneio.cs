using Padelizou.Models;

namespace Padelizou.Services;

// Como a inscrição de um torneio é cobrada: qual forma a cobrança trava no meio de pagamento
// e qual taxa o Padelizou fica.
//
// Existe porque as duas respostas TÊM QUE CONCORDAR. Travar a cobrança em Pix e ficar com a
// taxa de cartão (ou o contrário) seria cobrar do organizador uma coisa e entregar outra — e
// as duas decisões nasciam em lugares diferentes do código.
//
// Regra nova (Felipe, 29/07/2026): quando o organizador aceita TODAS as formas, vale a taxa
// da forma que o jogador escolheu de verdade. Quem paga por Pix não carrega o preço do cartão
// parcelado — Pix custa centavos e cai na hora; o crédito custa até 3,29% + R$ 0,49 e demora
// 32 dias. Antes, "todas as formas" cobrava 15% mesmo de quem pagava por Pix.
//
// Por que o jogador precisa DECLARAR a forma antes: o rateio é fixado quando a cobrança nasce.
// Deixando o meio de pagamento aberto ("escolha lá"), na hora de definir a taxa ainda não se
// sabe o que ele vai usar — então a escolha é trazida pra dentro do nosso checkout.
public static class CobrancaDoTorneio
{
    // O que o formulário manda. Nomes em português porque vêm da tela; o que vai pro meio de
    // pagamento é o `BillingType`.
    public const string EscolhaPix = "Pix";
    public const string EscolhaCartao = "Cartao";

    // ⚠️ AQUI MORAVA `EscolhaBoleto` (Felipe, 10/08/2026): boleto está DESLIGADO. Nenhuma
    // cobrança do sistema nasce mais como "BOLETO" — a tela oferece só Pix e cartão.
    //
    // Se um dia alguém pedir, voltar é: a constante aqui, o case abaixo (taxa do Pix, porque
    // pro gateway os dois custam o mesmo valor fixo em centavos), o mesmo case em
    // PlanoDoProfessor.CobrancaDaAula, e a opção em _EscolhaFormaPagamento.cshtml. O
    // vencimento mais longo do boleto continua de pé em VencimentoDaCobranca, intocado.

    public sealed record Cobranca(string BillingType, decimal Percentual);

    // O AMERICANO NÃO PAGA PERCENTUAL — nunca, em nenhuma forma de recebimento (Externo,
    // Pix ou Todas as formas). A monetização dele é OUTRA: R$ 5 por pessoa se o organizador
    // comprar o Ranking Americano (PontosDoAmericano.CustoParaPontuar), acertado POR FORA em
    // /Admin/RankingAmericano — não passa pelo split da inscrição.
    //
    // ⚠️ Corrigido em 07/08/2026 depois de cobrar errado num torneio REAL (o da Carol): a
    // primeira versão isentava só o Americano SEM ranking, então quem comprava o ranking
    // pagava as DUAS coisas — os R$ 5 por pessoa E o percentual sobre cada inscrição. São
    // duas cobranças pela mesma coisa; a régua certa é o FORMATO, não a caixinha do ranking.
    // Comprar o ranking é comprar ponto, não é motivo pra passar a dever comissão.
    public static bool IsentoDeTaxa(Torneio torneio) =>
        FormatoDoTorneio.EhAmericano(torneio.Formato);

    // Só faz sentido perguntar quando o organizador abriu mais de uma forma.
    public static bool JogadorEscolheAForma(string formaDoTorneio) => formaDoTorneio == "OnlineTodas";

    // Mesma forma de pagamento, mesmo BillingType — só a taxa some pro Americano isento.
    public static Cobranca Montar(Torneio torneio, string? escolhaDoJogador, TaxasExibicao taxas)
    {
        var cobranca = Montar(torneio.FormaPagamento, escolhaDoJogador, taxas);
        return IsentoDeTaxa(torneio) ? cobranca with { Percentual = 0m } : cobranca;
    }

    // O percentual que a tela mostra ANTES de o jogador escolher a forma (Details, criação).
    // Espelha Montar: mesma isenção, sem precisar de uma Cobranca inteira pra uma prévia.
    public static decimal PercentualExibicao(Torneio torneio, TaxasExibicao taxas) =>
        IsentoDeTaxa(torneio) ? 0m : taxas.PercentualDoTorneio(torneio.FormaPagamento);

    public static Cobranca Montar(string formaDoTorneio, string? escolhaDoJogador, TaxasExibicao taxas)
    {
        // Organizador travou em Pix: não há o que escolher, e a taxa é a de Pix.
        if (formaDoTorneio == "OnlinePix")
            return new Cobranca("PIX", taxas.ComissaoPercentualSomentePix);

        if (JogadorEscolheAForma(formaDoTorneio))
        {
            return (escolhaDoJogador ?? "") switch
            {
                EscolhaPix => new Cobranca("PIX", taxas.ComissaoPercentualSomentePix),
                EscolhaCartao => new Cobranca("CREDIT_CARD", taxas.ComissaoPercentualTodasFormas),

                // Escolha ausente ou desconhecida (formulário antigo em cache, requisição
                // montada à mão): cai no comportamento de sempre — todas as formas liberadas,
                // taxa cheia. Errar pra cá nunca cobra do organizador MENOS do que ele
                // combinou; o contrário seria prejuízo silencioso.
                _ => new Cobranca("UNDEFINED", taxas.ComissaoPercentualTodasFormas),
            };
        }

        // "Externo": o dinheiro não passa pelo sistema, então não deveria chegar aqui. Se
        // chegar, a taxa é a do externo e a forma fica aberta.
        return new Cobranca("UNDEFINED", taxas.PercentualDoTorneio(formaDoTorneio));
    }

    // ⚠️ AQUI MORAVA `ExplicacaoDaEscolha`, e ela NÃO deve voltar (Felipe, 08/08/2026).
    //
    // Era o texto que a tela de pagamento punha ao lado de cada forma: "Taxa do Padelizou:
    // 10%", "o organizador recebe em 32 dias". Isso não é assunto do JOGADOR — taxa e prazo
    // de repasse são a combinação entre o Padelizou e quem ORGANIZA. Ele paga o mesmo valor
    // anunciado em qualquer forma (o modo é "Descontada", a taxa sai da fatia do organizador),
    // então o percentual só levantava uma pergunta que não era dele e expunha nossa régua de
    // comissão pra qualquer um que abrisse a inscrição.
    //
    // O que a tela do jogador mostra hoje é só o NOME das formas, um lembrete de que o Pix
    // favorece o sistema e o organizador, e — no boleto — quando o pagamento passa a contar
    // (esse prazo vem de VencimentoDaCobranca, não daqui). Ver _EscolhaFormaPagamento.cshtml.
    //
    // A régua de taxa continua existindo e aparecendo onde ela é devida: na tela de CRIAÇÃO
    // do torneio, que é do organizador — ver TaxasExibicao.
}
