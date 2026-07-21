namespace Padelizou.Services;

public interface IEmailService
{
    Task EnviarAsync(string paraEmail, string paraNome, string assunto, string corpoHtml);
}
