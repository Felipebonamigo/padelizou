namespace Padelizou.Services;

public interface IPushNotificationService
{
    // Manda a notificação pra todos os dispositivos inscritos do jogador. Remove do banco
    // as subscriptions que o navegador já invalidou (410/404).
    Task EnviarParaJogadorAsync(int jogadorId, string titulo, string corpo, string? url = null);
}
