namespace Padelizou.Services;

// QUEM ORGANIZA JUNTO VÊ IGUAL A QUEM CRIOU — o caixa inclusive (17/11/2026).
//
// 🗣️ Felipe: *"Os co organizadores tem q ver igual os organizadores/criadores"*. De 18/08/2026
// até aqui valia o contrário — "organiza junto, mas o caixa é de quem criou" —, e foi o que travou
// o Vini (Wallau) de marcar um jogador como pago. O que continua só de quem criou NÃO passa por
// aqui: a conta que RECEBE o Pix (PixDoOrganizador, nível "Criador").
//
// O assistente do sistema que abre a gestão sem ser organizador DESTE torneio não tem linha de
// TorneioOrganizador (nível nulo) e segue sem ver — quem decide por ele é PoderesNoSistema.
public static class AcessoAoDinheiroDoTorneio
{
    // Os níveis que enxergam dinheiro: os quatro que existem. "Criador" é quem criou; "Total" é
    // o sinônimo de uma linha ajustada na mão (torneio 19); "Organizador" é o que a caixinha
    // "Adicionar organizador" grava; "Ajudante" é o padrão do modelo.
    private static readonly string[] NiveisComDinheiro = { "Criador", "Total", "Organizador", "Ajudante" };

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
        "Quem você adicionar organiza junto com você e vê o mesmo que você: lança placares, mexe "
        + "nas configurações, marca e cobra as inscrições e enxerga o caixa — quanto entrou, quanto "
        + "falta e o líquido. O Pix das inscrições continua caindo na sua conta.";

    // O que o ajudante lê quando abre a tela: sem isso ele acha que a página quebrou.
    public const string ExplicacaoParaQuemAjuda =
        "Você ajuda a organizar este torneio: pode marcar quem já acertou o pagamento. Os valores "
        + "ficam com quem criou o torneio.";
}
