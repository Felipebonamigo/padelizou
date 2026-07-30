using Padelizou.Models;

namespace Padelizou.Services;

// A reserva de balcão: o clube registra quem ligou ou chamou no WhatsApp.
//
// Sem ela, o dono tinha duas saídas ruins pro cliente de telefone: bloquear o horário (a
// reserva virava um cadeado sem nome e sem valor, fora da receita) ou deixar livre e torcer
// pra ninguém marcar pelo site por cima. Ou seja: era obrigado a manter o caderninho JUNTO
// com o sistema — e ninguém paga mensalidade pra ter dois controles.
//
// O cliente NÃO precisa de conta (decisão do Felipe, 30/07/2026): nome e celular bastam,
// como o acompanhante da aula. Se o celular bater com uma conta existente, a reserva é
// ligada a ela por baixo — mas o nome digitado continua mandando na tela.
public static class ReservaDeBalcao
{
    // Devolve o motivo da recusa, ou null quando serve.
    public static string? ProblemaCom(string? nome, DateTime dataHora, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return "Diga o nome de quem reservou — é ele que aparece na agenda.";

        // Margem de 1h pra trás: registrar o jogo que está ROLANDO agora (cliente chegou
        // e pagou na hora) é caso real de balcão; ontem não é.
        if (dataHora < agora.AddHours(-1))
            return "Esse horário já passou.";

        return null;
    }

    // O selo de pagamento da reserva, em texto de tela.
    //
    // Vazio = sem selo, e cobre dois casos de propósito: horário sem preço (não há o que
    // acertar) e reserva de antes do selo existir (não dá pra saber como foi acertada —
    // chutar "pago" ou "deve" seria mentir pra um lado ou pro outro).
    public static string Selo(MarcacaoJogo m, decimal? preco)
    {
        if (m.EhBloqueio) return "";
        if (m.PagoEm != null) return "pago";
        if (m.PagaNoLocal && preco > 0) return "paga lá";
        return "";
    }

    // Quem aparece na agenda: o nome do balcão manda quando existe (é o nome que o dono
    // conhece); senão, o nome da conta de quem marcou pelo site.
    public static string NomeNaAgenda(MarcacaoJogo m) =>
        !string.IsNullOrWhiteSpace(m.NomeClienteBalcao) ? m.NomeClienteBalcao!.Trim()
        : m.Jogador?.Nome ?? "";

    // O celular de contato: no balcão é o digitado; no site, o do cadastro.
    public static string? CelularDeContato(MarcacaoJogo m) =>
        !string.IsNullOrWhiteSpace(m.CelularClienteBalcao) ? m.CelularClienteBalcao
        : m.Jogador?.Celular;

    public static bool EhBalcao(MarcacaoJogo m) => !string.IsNullOrWhiteSpace(m.NomeClienteBalcao);
}
