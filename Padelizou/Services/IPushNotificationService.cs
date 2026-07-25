namespace Padelizou.Services;

public interface IPushNotificationService
{
    // Manda a notificação pra todos os dispositivos inscritos do jogador. Remove do banco
    // as subscriptions que o navegador já invalidou (410/404).
    Task EnviarParaJogadorAsync(int jogadorId, string titulo, string corpo, string? url = null);

    // Manda pra todo mundo que tem pelo menos uma inscrição de push ativa (instalou o app
    // e aceitou notificações). Retorna quantos jogadores distintos foram notificados.
    Task<int> EnviarParaTodosInscritosAsync(string titulo, string corpo, string? url = null);
}
