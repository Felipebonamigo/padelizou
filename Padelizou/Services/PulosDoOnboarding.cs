using Padelizou.Models;

namespace Padelizou.Services;

// QUAL PASSO DOS "PRIMEIROS PASSOS" TEM SAÍDA — 14/09/2026. Ver PassoPuladoDoOnboarding.
//
// ⚠️ UMA RÉGUA SÓ, lida pela TELA (o botão só aparece onde há saída) e pelo POST (que recusa o
// resto). Esconder não é fechar: o botão some do HTML, o endpoint não. Foi por isso que o
// alcance do palpitômetro precisou da trava no RegistrarVotoAsync além do `if` da view.
public static class PulosDoOnboarding
{
    // ⚖️ ESCOLHA DO FELIPE, PERGUNTADO (14/09/2026): só estes dois.
    //
    //   Seguir      — "se eu não quero seguir ninguém" é o exemplo do próprio feedback; seguir
    //                 é preferência social, não configuração de que o app depende.
    //   InstalarApp — o passo que NUNCA conclui pra quem usa só no computador: ele só fecha com
    //                 `InstalouAppEm` ou uma PushSubscription, e nenhum dos dois existe no
    //                 desktop. Era ele que deixava o cartão eterno.
    //
    // Perfil, Categoria e Torneio ficam de fora de propósito: são o que faz o app saber o que
    // sugerir (dupla, nível, convite) — e são justamente o que o cartão existe pra cobrar.
    public static bool PodeSerPulado(PassoDoOnboarding passo) =>
        passo is PassoDoOnboarding.Seguir or PassoDoOnboarding.InstalarApp;
}
