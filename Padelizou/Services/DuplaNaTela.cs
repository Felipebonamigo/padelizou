using Padelizou.Models;

namespace Padelizou.Services;

// Como uma dupla de desafio é ESCRITA — "Felipe & Lucas".
//
// Num lugar só porque o mesmo par aparece no cartão do mural, na lista de desafios, no texto do
// aviso que sai por WhatsApp e no histórico. Cada tela montando o nome do seu jeito é como a
// mesma dupla vira duas na cabeça de quem lê — e, no aviso, é como o push chega dizendo um
// nome que não bate com o da tela que ele abre.
//
// Usa o nome CURTO (primeiro nome + apelido quando existe), que é como as pessoas se chamam na
// quadra. Nome completo faria o cartão do mural quebrar em duas linhas por dupla.
public static class DuplaNaTela
{
    public static string Nome(Jogador? a, Jogador? b)
    {
        var primeiro = Quem(a);
        var segundo = Quem(b);

        // Anúncio ainda em rascunho não tem parceiro. Dizer "Felipe & ?" seria confuso; a tela
        // que mostra isso é a de quem publicou, e ela já explica que falta o convite.
        if (segundo.Length == 0) return primeiro;

        return $"{primeiro} & {segundo}";
    }

    // Pré-cadastro pode não ter nome preenchido — e "Alguém & Lucas" é melhor que " & Lucas".
    private static string Quem(Jogador? jogador)
    {
        if (jogador == null) return "";

        var curto = NomeBonito.Curto(jogador.Nome);
        if (!string.IsNullOrWhiteSpace(jogador.Apelido)) return jogador.Apelido.Trim();

        return curto.Length > 0 ? curto : "Alguém";
    }
}
