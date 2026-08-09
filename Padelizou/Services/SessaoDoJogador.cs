using Microsoft.AspNetCore.Authentication;

namespace Padelizou.Services;

// Quanto tempo o login dura — respondido em UM lugar, porque são três telas que fazem login
// (entrar, cadastrar e salvar o perfil) e cada uma criava o cookie do seu jeito.
//
// O cookie nascia SEM propriedades nenhuma, e sem `IsPersistent` ele é um cookie de SESSÃO:
// existe só enquanto o navegador está aberto. No computador isso já incomoda; no celular é
// pior do que parece, porque o sistema recicla a aba sozinho pra liberar memória — a pessoa
// volta no app no dia seguinte e está deslogada, sem ter fechado nada.
//
// "Tem que ficar logando toda hora" (jogadores, 09/08/2026) era literalmente isto.
//
// ⚠️ De propósito NÃO se define `ExpiresUtc` aqui. Com ele, a validade vira uma data fixa e
// a renovação deslizante do handler não adianta o relógio — quem usa o app todo dia seria
// deslogado no nonagésimo assim mesmo. Deixando em branco, quem manda é o par
// ExpireTimeSpan + SlidingExpiration do Program.cs: 90 dias que se renovam a cada visita.
// Na prática, quem usa nunca é deslogado; quem sumiu por três meses entra de novo.
//
// O middleware de acesso antecipado já fazia isso certo desde sempre — foi a única porta de
// entrada que não esqueceu, e é de onde a regra veio.
public static class SessaoDoJogador
{
    public static AuthenticationProperties Propriedades() => new() { IsPersistent = true };
}
