namespace Padelizou.Services;

public interface IWhatsAppService
{
    // O canal está ligado neste ambiente? Serve pro diagnóstico do teste do painel: sem isto,
    // "não chegou" no dev e "não chegou" com o chip caído dariam a mesma tela muda.
    bool Configurado { get; }

    // Envia uma mensagem de WhatsApp automaticamente via Z-API. Retorna false (sem lançar exceção)
    // se as credenciais não estiverem configuradas ou se o envio falhar — quem chama decide se tenta
    // de novo depois.
    Task<bool> EnviarAsync(string? celular, string mensagem);
}
