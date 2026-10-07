using Padelizou.Models;

namespace Padelizou.Services;

// Quanto JÁ ENTROU numa inscrição, quanto falta, e o que a pessoa escolhe pagar.
//
// 🗣️ Nasceu de um relato do Lucas Almeida, organizador do NATA PADEL TOUR (06/10/2026): ele se
// inscreveu sozinho, pagou a parte dele, e ao puxar o parceiro a dupla ficou marcada como
// PAGA — com metade do dinheiro dentro. A régua de 08/08 já corrigia o VALOR quando o parceiro
// entra (PrecoDaInscricao.AoEntrarOParceiro); o que faltava era alguém olhar pro `Pago`.
//
// ⚠️ `Dupla.Pago` continua querendo dizer QUITADA, e é de propósito: ele é lido em 99 lugares
// — chave, lembrete de não pagos, financeiro, Pix do organizador, lista de pendências. Mudar o
// SIGNIFICADO dele seria mexer em todos; o que mudou foi quem tem o direito de marcá-lo.
//
// ⚠️ E "parcial" não é o mesmo que "não paga": parcial é dinheiro que ENTROU e não fechou.
// Inscrição sem pagamento nenhum marcada como paga é a palavra do organizador (ele acertou por
// fora), e o sistema não contradiz quem tem a informação que ele não tem.
public static class QuitacaoDaInscricao
{
    // O que o formulário manda. É a ESCOLHA que viaja, nunca o valor: valor vindo do cliente
    // seria o jogador dizendo quanto quer pagar (Regra 0 — o servidor é quem calcula).
    public const string MinhaParte = "minha";
    public const string ADupla = "dupla";

    public static decimal JaPago(IEnumerable<Pagamento> confirmados) => confirmados.Sum(p => p.Valor);

    // Nunca negativo: estorno parcial e ajuste do organizador fazem o pago passar o valor, e
    // "falta -R$ 30,00" na tela é pior que número nenhum.
    public static decimal Falta(decimal valorDaInscricao, decimal jaPago) =>
        Math.Max(0m, valorDaInscricao - jaPago);

    public static bool Quitada(decimal valorDaInscricao, decimal jaPago) => jaPago >= valorDaInscricao;

    public static bool Parcial(decimal valorDaInscricao, decimal jaPago) =>
        jaPago > 0m && jaPago < valorDaInscricao;

    // Quanto cobrar de quem clicou, pela escolha dela.
    //
    // "Minha parte" é o preço de UMA pessoa pela régua de sempre (Services/PrecoDaInscricao) —
    // e não metade do total: com desconto de 2ª categoria os dois lados pagam valores
    // diferentes, e metade cobraria do lado errado. O teto é o que ainda falta.
    //
    // ⚠️ Escolha ausente ou desconhecida cobra a inscrição INTEIRA (formulário antigo em cache,
    // requisição montada à mão). Errar pra cá cobra o que já se cobrava antes desta mudança;
    // errar pro outro lado cobraria metade de quem queria quitar, e a dupla ficaria devendo sem
    // ninguém perceber — que é exatamente o defeito que isto veio consertar.
    public static decimal ValorDaEscolha(Torneio torneio, string? escolha, bool euRepitoNoTorneio,
        decimal falta) =>
        escolha == MinhaParte
            ? Math.Min(PrecoDaInscricao.PorPessoa(torneio, euRepitoNoTorneio), falta)
            : falta;

    // A pergunta só faz sentido numa inscrição de DUPLA que ainda deve mais que a parte de uma
    // pessoa. Sozinho não há o que dividir, e quando falta só uma parte o botão é um só.
    public static bool DaPraEscolher(Torneio torneio, bool inscricaoDeDupla, bool euRepitoNoTorneio,
        decimal falta) =>
        inscricaoDeDupla && falta > PrecoDaInscricao.PorPessoa(torneio, euRepitoNoTorneio);
}
