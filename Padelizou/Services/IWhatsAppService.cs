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

    // O chip está pareado AGORA? O canal já caiu duas vezes em cinco dias sem avisar ninguém —
    // e a queda de 03/08 durou 17h porque nada perguntava isso.
    Task<EstadoDoCanal> ConsultarEstadoAsync();

    // Levanta o socket sem pedir QR de novo. Resolveu a queda de 03/08 (o pareamento estava
    // vivo, só a conexão tinha morrido); NÃO resolve quando o aparelho foi removido no celular.
    Task<bool> ReligarAsync();
}
