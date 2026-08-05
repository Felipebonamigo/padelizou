namespace Padelizou.Services;

// Quem organiza junto faz TUDO do torneio — placar, chaves, configuração, marcar quem já
// acertou — menos ver o dinheiro. Arrecadado, a receber, líquido e taxa ficam com quem criou.
//
// A razão é do dono do torneio, não técnica: ele chama um amigo pra ajudar na mesa no dia do
// jogo, e ajudar na mesa não é o mesmo que abrir o caixa dele. Antes era tudo ou nada — quem
// entrava pra lançar placar via quanto o torneio faturou.
public static class AcessoAoDinheiroDoTorneio
{
    // Os níveis que enxergam dinheiro.
    //
    // "Criador" é o que o código escreve hoje ao criar o torneio. "Total" existe porque uma
    // linha de produção foi ajustada na mão antes desta regra existir (torneio 19) — aceitar
    // o sinônimo custa uma palavra e evita trancar um organizador de verdade fora do próprio
    // caixa. Quem é adicionado pela caixinha entra como "Organizador"/"Ajudante" e NÃO vê.
    private static readonly string[] NiveisComDinheiro = { "Criador", "Total" };

    // Lista fechada e não o contrário ("todo mundo vê, menos o ajudante") de propósito: se um
    // nível novo aparecer amanhã, o erro seguro é ele NÃO ver dinheiro e alguém reclamar —
    // não ele ver e ninguém perceber.
    public static bool PodeVer(string? nivelAcesso, bool ehAdminDaPlataforma)
    {
        if (ehAdminDaPlataforma) return true;

        return nivelAcesso != null
            && NiveisComDinheiro.Any(n => string.Equals(n, nivelAcesso.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    // O que o criador lê ANTES de adicionar alguém — no lugar onde ele decide, não escondido
    // numa ajuda. É o aviso que o Felipe pediu.
    public const string AvisoAoAdicionar =
        "Quem você adicionar pode lançar placares, mexer nas configurações do torneio e marcar "
        + "inscrição como paga. O dinheiro — quanto entrou, quanto falta e o líquido — continua "
        + "só com você.";

    // O que o ajudante lê quando abre a tela: sem isso ele acha que a página quebrou.
    public const string ExplicacaoParaQuemAjuda =
        "Você ajuda a organizar este torneio: pode marcar quem já acertou o pagamento. Os valores "
        + "ficam com quem criou o torneio.";
}
