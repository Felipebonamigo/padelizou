using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Padelizou.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly VolumeDoEmail _volume;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, VolumeDoEmail volume, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _volume = volume;
        _logger = logger;
    }

    // Separado do envio pra poder conferir em teste quem assina e pra onde volta a resposta —
    // montar a mensagem não precisa de servidor nenhum, conectar precisa.
    public static MimeMessage MontarMensagem(EmailSettings settings, string paraEmail, string paraNome, string assunto, string corpoHtml)
    {
        var mensagem = new MimeMessage();
        mensagem.From.Add(new MailboxAddress(settings.RemetenteNome, settings.RemetenteEmail));
        mensagem.To.Add(new MailboxAddress(paraNome, paraEmail));
        mensagem.Subject = assunto;
        mensagem.Body = new BodyBuilder { HtmlBody = corpoHtml }.ToMessageBody();

        if (!string.IsNullOrWhiteSpace(settings.ResponderPara))
        {
            mensagem.ReplyTo.Add(MailboxAddress.Parse(settings.ResponderPara));
        }

        return mensagem;
    }

    public async Task EnviarAsync(string paraEmail, string paraNome, string assunto, string corpoHtml)
    {
        if (string.IsNullOrWhiteSpace(paraEmail))
        {
            return;
        }

        var mensagem = MontarMensagem(_settings, paraEmail, paraNome, assunto, corpoHtml);

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.UsuarioDeLogin, _settings.RemetenteSenhaApp);
            await client.SendAsync(mensagem);

            // Contado DEPOIS do envio dar certo — este é o único ponto por onde todo e-mail do
            // sistema passa, o do funil de avisos e os diretos (senha, aula, parceiro).
            _volume.RegistrarEnvio(DateTime.Now);
        }
        catch (Exception ex)
        {
            _volume.RegistrarFalha(DateTime.Now);

            // Falha de SMTP (provedor fora do ar, chave revogada, cota do dia estourada, timeout) NÃO pode quebrar
            // a ação do usuário que disparou o e-mail (inscrição, aula, etc.) — igual ao que já
            // fazem o WhatsApp e o push. Registra no log e segue; o e-mail simplesmente não sai.
            _logger.LogError(ex, "Falha ao enviar e-mail para {ParaEmail} (assunto: {Assunto}).", paraEmail, assunto);
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
