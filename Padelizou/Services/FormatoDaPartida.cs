using Padelizou.Models;

namespace Padelizou.Services;

// Quantos sets e quantos games vale CADA partida, conforme a fase em que ela é jogada.
//
// O torneio guarda três formatos desde sempre (grupos, mata-mata, final) e a tela de criação
// pergunta os três — mas ninguém lia: a Mesa de Controle tinha `limiteGames: 9` cravado no
// JavaScript. O organizador escolhia "4 games" no cadastro e a Mesa deixava marcar até 9,
// calada. Este é o único lugar que traduz fase → formato; se aparecer uma segunda cópia,
// volta o mesmo bug com outro número.
//
// A régua das fases:
//   • grupos e Americano  → o formato "geral" (é a maioria esmagadora dos jogos);
//   • Semifinal e Final   → o formato de decisão. Semifinal entra aqui de propósito: quem
//     escreve "as semis e a final são mais longas" está descrevendo as duas, e ninguém
//     configura uma semifinal com regra de oitavas;
//   • o resto do mata-mata (Primeira Rodada, Oitavas, Quartas) → o formato eliminatório.
public static class FormatoDaPartida
{
    public const int GamesPadrao = 9;
    public const int SetsPadrao = 1;

    public record Formato(int Sets, int Games);

    public static Formato De(Torneio? torneio, string? fase)
    {
        if (torneio == null) return new Formato(SetsPadrao, GamesPadrao);

        if (fase is "Semifinal" or "Final")
            return Valido(torneio.SetsFaseFinal, torneio.GamesFaseFinal);

        if (FasesTorneio.EhFaseDeGrupos(fase) || (fase?.StartsWith("Americano") ?? false))
            return Valido(torneio.SetsFaseGrupos, torneio.GamesFaseGrupos);

        return Valido(torneio.SetsFaseMataMata, torneio.GamesFaseMataMata);
    }

    // Torneio antigo (ou criado por formulário incompleto) tem zero gravado nessas colunas.
    // Zero viraria uma Mesa onde não dá pra marcar nem um game — melhor cair no padrão do
    // que travar a quadra.
    private static Formato Valido(int sets, int games) =>
        new(sets > 0 ? sets : SetsPadrao, games > 0 ? games : GamesPadrao);
}
