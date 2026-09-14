namespace Padelizou.Models;

// Os passos dos "Primeiros passos" (Services/EstatisticasService.ObterOnboardingAsync), com
// nome estável — 14/09/2026.
//
// ⚠️ ISTO É GRAVADO NO BANCO PELO NOME, NÃO PELO NÚMERO (ver a configuração do
// PassoPuladoDoOnboarding em DbPadelContext). Com `int`, inserir um passo no meio deste enum
// amanhã reescreveria, calado, o que cada pessoa pulou: quem pulou "Seguir" acordaria tendo
// pulado outra coisa. O nome sobrevive à reordenação e à mudança de título na tela.
public enum PassoDoOnboarding
{
    Perfil,
    Categoria,
    Seguir,
    Torneio,
    InstalarApp,
}

// Um passo dos "Primeiros passos" que a pessoa mandou tirar da lista — 14/09/2026.
//
// 🗣️ Feedback de usuário repassado pelo Felipe: *"nos primeiros passos, por exemplo se eu não
// quero seguir ninguém, posso dar um 'Skip' no item"*.
//
// 🕳️ Os 5 passos são DERIVADOS DO DADO e não tinham saída. O "Instale o app no celular" só
// conclui com `InstalouAppEm` ou uma PushSubscription — quem usa só no computador NUNCA
// concluía, e o cartão ficava na Home e no Perfil para sempre.
//
// ⚠️ A CHAVE COMPOSTA (JogadorId, Passo) **É** a regra "pular duas vezes é pular uma" — ela não
// está num `if` de C#. O toque duplo num alvo pequeno manda dois POSTs, e foi exatamente assim
// que o `DbUpdateException em POST /Partidas/Votar` apareceu em produção em 10/09. É o degrau 4
// da escada do CLAUDE.md, o mesmo molde de TorneioMarcador, PresencaNoJogo e ReacaoDaPartida.
//
// ⚠️ NEM TODO PASSO PODE SER PULADO, e quem responde isso é Services/PulosDoOnboarding — uma
// régua só, lida pela tela E pelo POST. Esconder não é fechar.
public class PassoPuladoDoOnboarding
{
    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    public PassoDoOnboarding Passo { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
