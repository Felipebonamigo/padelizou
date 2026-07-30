namespace Padelizou.Services;

public interface IPushNotificationService
{
    // Manda a notificação pra todos os dispositivos inscritos do jogador. Remove do banco
    // as subscriptions que o navegador já invalidou (410/404).
    Task EnviarParaJogadorAsync(int jogadorId, string titulo, string corpo, string? url = null);

    // Manda pra todo mundo que tem pelo menos uma inscrição de push ativa (instalou o app
    // e aceitou notificações). Retorna quantos jogadores distintos foram notificados.
    Task<int> EnviarParaTodosInscritosAsync(string titulo, string corpo, string? url = null);

    // Teste dirigido a UM jogador, com canais escolhidos e diagnóstico do que aconteceu em
    // cada um. Separado do envio normal de propósito: o envio normal é "tenta e segue a vida",
    // e quem está testando precisa saber por que não chegou.
    Task<ResultadoTesteNotificacao> EnviarTesteAsync(int jogadorId, bool porPush, bool porWhatsApp,
        string titulo, string corpo, string? url = null);
}
