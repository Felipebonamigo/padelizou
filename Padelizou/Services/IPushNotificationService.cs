namespace Padelizou.Services;

public interface IPushNotificationService
{
    // Manda a notificação pra todos os dispositivos inscritos do jogador. Remove do banco
    // as subscriptions que o navegador já invalidou (410/404).
    // `alcance` é SoApp por omissão de propósito: aviso novo nasce sem WhatsApp. O contrário
    // — WhatsApp por padrão — foi o que fez a Meta restringir o número em 04/08/2026, porque
    // cada aviso novo entrava no canal sem ninguém decidir isso.
    Task EnviarParaJogadorAsync(int jogadorId, string titulo, string corpo, string? url = null,
        AlcanceDoAviso alcance = AlcanceDoAviso.SoApp);

    // Manda pra todo mundo que tem pelo menos uma inscrição de push ativa (instalou o app
    // e aceitou notificações). Retorna quantos jogadores distintos foram notificados.
    Task<int> EnviarParaTodosInscritosAsync(string titulo, string corpo, string? url = null);

    // Teste dirigido a UM jogador, com canais escolhidos e diagnóstico do que aconteceu em
    // cada um. Separado do envio normal de propósito: o envio normal é "tenta e segue a vida",
    // e quem está testando precisa saber por que não chegou.
    Task<ResultadoTesteNotificacao> EnviarTesteAsync(int jogadorId, bool porPush, bool porWhatsApp,
        string titulo, string corpo, string? url = null);

    // Atualização de placar AO VIVO. `tag` faz o navegador SUBSTITUIR a notificação anterior
    // do mesmo jogo (mesma tag = mesmo aviso trocando de conteúdo, não empilhando) — sem ela
    // cada game viraria um aviso novo na tela de bloqueio. Não passa pela Caixa de Avisos nem
    // manda e-mail: ver Services/AvisoDePlacarAoVivo pro porquê e pra quem chama isto.
    Task EnviarPlacarAoVivoAsync(int jogadorId, string titulo, string corpo, string url, string tag);
}
