using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Padelizou.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task EnviarAsync(string paraEmail, string paraNome, string assunto, string corpoHtml)
    {
        if (string.IsNullOrWhiteSpace(paraEmail))
        {
            return;
        }

        var mensagem = new MimeMessage();
        mensagem.From.Add(new MailboxAddress(_settings.RemetenteNome, _settings.RemetenteEmail));
        mensagem.To.Add(new MailboxAddress(paraNome, paraEmail));
        mensagem.Subject = assunto;
        mensagem.Body = new BodyBuilder { HtmlBody = corpoHtml }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.RemetenteEmail, _settings.RemetenteSenhaApp);
            await client.SendAsync(mensagem);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true);
            }
        }
    }
}
