namespace Padelizou.Services;

public interface IWhatsAppService
{
    // Envia uma mensagem de WhatsApp automaticamente via Z-API. Retorna false (sem lançar exceção)
    // se as credenciais não estiverem configuradas ou se o envio falhar — quem chama decide se tenta
    // de novo depois.
    Task<bool> EnviarAsync(string? celular, string mensagem);
}
