using Padelizou.Models;

namespace Padelizou.Services;

// Recebimento por Pix direto na conta do Padelizou, sem passar pelo gateway.
//
// ── A regra que sustenta tudo isto ────────────────────────────────────────────────────────
// Só entra aqui o pagamento cujo valor é INTEIRAMENTE do Padelizou. Hoje são três: a
// mensalidade do professor assinante, a taxa de 5% do torneio "por fora" e a assinatura do
// clube (Gestão/Fiscal) — os três nascem com RecebedorId nulo e Comissao == Valor.
//
// ⚠️ Inscrição de torneio, aula e quadra NUNCA podem vir por aqui, e não é preferência de
// arquitetura: nesses o dinheiro é do organizador/professor e só uma fatia é nossa. Recebê-lo
// na nossa conta faria (a) o valor cheio virar faturamento do MEI, que estoura o teto anual em
// poucos torneios, e (b) o Padelizou movimentar dinheiro de terceiro, que é atividade regulada.
// O split do gateway existe exatamente pra isso. AceitaPix é a trava, e tem teste em cima.
//
// ── O que este caminho não tem ────────────────────────────────────────────────────────────
// Confirmação automática. O código Pix estático não avisa ninguém quando é pago (ver
// PixCopiaECola), então o dinheiro entra e alguém precisa DIZER que entrou. O fluxo é:
//
//   Pendente  →  (o pagador clica "já paguei")  →  AguardandoConfirmacao
//             →  (o admin confere o extrato)    →  Confirmado   ... ou Cancelado
//
// "AguardandoConfirmacao" existe pra separar "gerei o código e sumi" de "paguei, confere aí".
// Sem essa distinção o admin abriria a fila e não saberia o que ainda vale a pena conferir.
public static class PixDireto
{
    // Vai na coluna MetodoPagamento do Pagamento.
    public const string Metodo = "PixDireto";
    public const string MetodoGateway = "Gateway";

    // O status a mais que este caminho traz. Os outros são os de sempre.
    public const string AguardandoConfirmacao = "AguardandoConfirmacao";

    public const string TipoAssinatura = "AssinaturaProfessor";
    public const string TipoTaxaTorneio = "TaxaTorneio";

    // A assinatura do clube (planos Gestão e Fiscal) entrou aqui em 19/08/2026 e passa no
    // mesmo teste dos outros dois: é software nosso, vendido pelo Padelizou ao clube, sem uma
    // única fatia de terceiro. Nasce com RecebedorId nulo e Comissao == Valor, igual.
    public const string TipoAssinaturaClube = "AssinaturaClube";

    // A trava. Qualquer tipo fora destes três tem repasse a fazer — ver o comentário do topo.
    public static bool AceitaPix(string? tipo) =>
        tipo is TipoAssinatura or TipoTaxaTorneio or TipoAssinaturaClube;

    // O identificador que vai no BR Code e (com sorte) aparece no extrato do banco. Só letras
    // e números porque o campo do EMV não aceita outra coisa; o Id do pagamento é o que casa
    // a linha do extrato com a cobrança daqui.
    public static string TxId(int pagamentoId) => $"PDZ{pagamentoId:D8}";

    // O pagador está abrindo a tela do código. Só ele e o admin — é uma cobrança nominal.
    public static string? ProblemaParaVer(Pagamento? pagamento, int jogadorId, bool ehAdmin)
    {
        if (pagamento == null) return "Cobrança não encontrada.";
        if (pagamento.MetodoPagamento != Metodo) return "Esta cobrança não é por Pix.";
        if (!ehAdmin && pagamento.JogadorId != jogadorId) return "Esta cobrança não é sua.";
        return null;
    }

    // "Já paguei": o pagador avisa que mandou o Pix, e a cobrança entra na fila do admin.
    public static string? ProblemaParaDeclarar(Pagamento? pagamento, int jogadorId)
    {
        if (pagamento == null) return "Cobrança não encontrada.";
        if (pagamento.MetodoPagamento != Metodo) return "Esta cobrança não é por Pix.";
        if (pagamento.JogadorId != jogadorId) return "Esta cobrança não é sua.";

        if (pagamento.Status == AguardandoConfirmacao)
            return "Você já avisou — está na fila pra conferência.";
        if (pagamento.Status == "Confirmado")
            return "Este pagamento já foi confirmado.";
        if (pagamento.Status != "Pendente")
            return "Esta cobrança não está mais aberta.";

        return null;
    }

    // O admin conferiu o extrato e está dando baixa. É o momento em que o dinheiro passa a
    // existir pro sistema, então a guarda mora aqui e não na tela.
    public static string? ProblemaParaConfirmar(Pagamento? pagamento)
    {
        if (pagamento == null) return "Cobrança não encontrada.";
        if (pagamento.MetodoPagamento != Metodo) return "Esta cobrança não é por Pix.";

        if (pagamento.Status == "Confirmado")
            return "Este pagamento já estava confirmado.";

        // Aceita tanto a fila normal quanto a cobrança que o pagador nunca declarou: o Pix
        // pode ter caído mesmo sem ninguém clicar em nada, e o extrato é a fonte da verdade.
        if (pagamento.Status is not ("Pendente" or AguardandoConfirmacao))
            return "Esta cobrança não está mais aberta.";

        // Cinto e suspensório: mesmo que uma cobrança com repasse chegasse aqui marcada como
        // Pix, confirmar registraria dinheiro de terceiro como receita nossa.
        if (!AceitaPix(pagamento.Tipo) || pagamento.RecebedorId != null || pagamento.ValorRepasse != 0m)
            return "Esta cobrança tem repasse a terceiro e não pode ser recebida por Pix direto.";

        return null;
    }

    // Recusar é pra quando o Pix não apareceu no extrato — some da fila sem virar dinheiro.
    public static string? ProblemaParaRecusar(Pagamento? pagamento)
    {
        if (pagamento == null) return "Cobrança não encontrada.";
        if (pagamento.MetodoPagamento != Metodo) return "Esta cobrança não é por Pix.";
        if (pagamento.Status == "Confirmado")
            return "Este pagamento já foi confirmado — recusar agora não desfaz o que ele liberou.";
        if (pagamento.Status is not ("Pendente" or AguardandoConfirmacao))
            return "Esta cobrança não está mais aberta.";
        return null;
    }

    // O texto que a tela do pagador mostra, por status. Fica aqui junto das regras pra tela e
    // fila nunca contarem histórias diferentes sobre o mesmo pagamento.
    public static string Situacao(Pagamento pagamento) => pagamento.Status switch
    {
        "Pendente" => "Aguardando o seu Pix.",
        AguardandoConfirmacao => "Recebemos seu aviso — estamos conferindo o extrato.",
        "Confirmado" => "Pagamento confirmado.",
        "Cancelado" => "Cobrança cancelada.",
        _ => pagamento.Status,
    };
}
