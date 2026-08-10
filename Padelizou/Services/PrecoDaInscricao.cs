using Padelizou.Models;

namespace Padelizou.Services;

// Quanto custa uma inscrição — a única régua, agora que o mesmo jogador pode pagar dois
// preços diferentes no mesmo torneio.
//
// Pedido do Felipe (08/08/2026): "normalmente nos torneios, quando o jogador joga duas
// categorias, a segunda inscrição é mais barata". É como o padel cobra de verdade — a segunda
// categoria não traz um atleta novo, traz mais jogos pro mesmo.
//
// ⚠️ O DESCONTO É POR PESSOA, NÃO POR INSCRIÇÃO. Numa dupla em que um repete e o outro é
// novo no torneio, um lado paga cheio e o outro paga o desconto — e o total da inscrição é a
// SOMA dos dois, não "o preço da dupla". Foi por isso que `Torneio.ValorCobrado` (preço ×
// número de pessoas) deixou de dar conta: ele supõe que todo mundo na inscrição paga igual.
public static class PrecoDaInscricao
{
    // O que ESTA pessoa paga nesta inscrição.
    //
    // `jaEstaNoTorneio` é "ela já tem inscrição em outra categoria deste torneio" — pergunta
    // que só o banco responde, e que tem que olhar as DUAS tabelas de inscrição (Duplas e
    // InscricoesAmericanas), porque cada formato grava na sua.
    public static decimal PorPessoa(Torneio torneio, bool jaEstaNoTorneio)
    {
        // Sem múltiplas categorias ninguém repete, então não há segundo preço a aplicar —
        // mesmo que o campo tenha ficado preenchido de uma configuração anterior.
        if (!jaEstaNoTorneio || !torneio.PermiteMultiplasCategorias)
            return torneio.PrecoInscricao;

        return torneio.PrecoSegundaInscricao ?? torneio.PrecoInscricao;
    }

    // O total da inscrição: a soma do que cada pessoa dela paga, mais os impedimentos.
    //
    // Os impedimentos são da INSCRIÇÃO (é uma janela da grade que se perde, não um atleta),
    // então não entram na conta por pessoa nem ganham desconto.
    public static decimal Total(Torneio torneio, IEnumerable<bool> pessoasJaNoTorneio, int impedimentos = 0)
    {
        decimal total = 0m;
        foreach (var jaEsta in pessoasJaNoTorneio)
            total += PorPessoa(torneio, jaEsta);

        return total + torneio.TaxaPorImpedimento * Math.Max(impedimentos, 0);
    }

    // ── QUANDO O PARCEIRO ENTRA NUMA DUPLA QUE ESTAVA SOZINHA ──────────────────────────
    //
    // O valor da inscrição sobe pra contar duas pessoas. ⚠️ Isto NÃO cobra ninguém: só ajeita
    // o número que os somatórios de dinheiro, a devolução e a base da taxa leem — a diferença
    // é acertada com o organizador.
    //
    // ⚠️ E É AQUI QUE ENTRA O TETO (Felipe, 10/08/2026: *"a dupla q já pagou 250, quando entrar
    // o novo parceiro, não cobre dele"*). Até 08/08/2026 a inscrição sem parceiro era cobrada
    // por DUAS pessoas; das 20 do NATA PADEL TOUR, uma é dessa época e pagou **R$ 250**. Somar
    // a parte do parceiro nela daria R$ 375 — pediria de novo um dinheiro que já entrou, e
    // ainda inflaria o faturamento do organizador com R$ 125 que nunca chegaram (relatório do
    // dia do jogo e caderneta leem `DaDupla`).
    //
    // O teto é o que uma dupla COMPLETA custa pagando cheio pelos dois. Quem já alcançou não
    // sobe mais.
    //
    // ⚠️ E o teto nunca ABAIXA o que já está gravado: inscrição antiga de um torneio que
    // barateou depois continua valendo o que foi cobrado dela. Corrigir pra menos aqui seria
    // reescrever história de dinheiro.
    public static decimal AoEntrarOParceiro(Torneio torneio, decimal valorAtual,
        bool segundoJaEstaNoTorneio, int impedimentos = 0)
    {
        var teto = Math.Max(valorAtual, Total(torneio, new[] { false, false }, impedimentos));

        return Math.Min(valorAtual + PorPessoa(torneio, segundoJaEstaNoTorneio), teto);
    }

    // ── O QUE OS SOMATÓRIOS DEVEM LER ──────────────────────────────────────────────────
    // Quanto uma inscrição JÁ EXISTENTE custou. É sempre o valor gravado nela; o cálculo por
    // preço do torneio só entra pra linha antiga (anterior à coluna `ValorInscricao`), quando
    // havia um preço só e a conta batia.
    //
    // ⚠️ Existe pra que relatório, devolução e base da taxa parem de recalcular por conta
    // própria: com dois preços possíveis, quem recalcula não tem como saber quem entrou pela
    // segunda vez — e passa a mentir sem avisar.
    public static decimal DaDupla(Torneio torneio, Dupla dupla) =>
        dupla.ValorInscricao ?? torneio.PrecoInscricao * (dupla.Jogador2Id != null ? 2 : 1);

    public static decimal DaInscricaoAmericana(Torneio torneio, InscricaoAmericana inscricao) =>
        inscricao.ValorInscricao ?? torneio.PrecoInscricao;

    // Faz sentido oferecer o campo na tela? Torneio de graça não tem segundo preço, e sem
    // múltiplas categorias ninguém chega a uma segunda inscrição.
    public static bool CabeSegundoPreco(Torneio torneio) =>
        torneio.PermiteMultiplasCategorias && torneio.PrecoInscricao > 0;

    // Devolve o motivo da recusa, ou null se o valor serve. Segundo preço MAIOR que o
    // primeiro quase certamente é dedo trocado — o desconto é o ponto do recurso —, mas
    // recusar seria decidir pelo organizador; quem avisa é a tela. Aqui só o que não pode:
    // valor negativo.
    public static string? ProblemaCom(decimal? precoSegunda) =>
        precoSegunda < 0
            ? "O preço da segunda inscrição não pode ser negativo. Use zero pra não cobrar nada."
            : null;
}
