namespace Padelizou.Services;

public class EmailSettings
{
    public string SmtpHost { get; set; } = null!;
    public int SmtpPort { get; set; }
    public string RemetenteEmail { get; set; } = null!;
    public string RemetenteSenhaApp { get; set; } = null!;
    public string RemetenteNome { get; set; } = null!;

    // O Gmail autentica com o próprio endereço de quem envia, então por anos usuário e
    // remetente foram a mesma coisa. Um serviço de envio não funciona assim: no Resend o
    // usuário é a palavra fixa `resend` e a senha é a chave de API, enquanto o remetente é
    // `nao-responda@padelizou.com.br`. Vazio = usa o remetente, que é o jeito antigo.
    public string SmtpUsuario { get; set; } = "";

    public string UsuarioDeLogin =>
        string.IsNullOrWhiteSpace(SmtpUsuario) ? RemetenteEmail : SmtpUsuario;

    // Quem recebe um e-mail do sistema responde por reflexo, e hoje a resposta cai na caixa
    // do remetente porque ela é uma caixa de verdade. Saindo de `nao-responda@`, sem isto a
    // resposta da pessoa some. Vazio = não manda o cabeçalho.
    public string ResponderPara { get; set; } = "";
}
