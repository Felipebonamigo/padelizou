using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Sair do Gmail pessoal e passar a enviar por um serviço de envio (Resend) muda duas coisas
// que o código antigo tratava como uma só: quem faz login no SMTP e quem assina a mensagem.
// No Gmail eram o mesmo endereço; no Resend o usuário é a palavra fixa `resend`, a senha é a
// chave de API e o remetente é `nao-responda@padelizou.com.br`.
public class EnvioPeloProvedorDeEmailTests
{
    private static EmailSettings Resend() => new()
    {
        SmtpHost = "smtp.resend.com",
        SmtpPort = 587,
        SmtpUsuario = "resend",
        RemetenteEmail = "nao-responda@padelizou.com.br",
        RemetenteSenhaApp = "re_chave_de_api",
        RemetenteNome = "Padelizou",
        ResponderPara = "contato@padelizou.com.br",
    };

    [Fact]
    public void Sem_usuario_proprio_o_login_continua_sendo_o_remetente()
    {
        // É como o Gmail sempre funcionou. Quem não mexer em nada não pode parar de enviar.
        var gmail = new EmailSettings { RemetenteEmail = "padelizou@gmail.com" };

        Assert.Equal("padelizou@gmail.com", gmail.UsuarioDeLogin);
    }

    [Fact]
    public void Com_usuario_proprio_o_login_ignora_o_remetente()
    {
        // Autenticar como `nao-responda@padelizou.com.br` no Resend simplesmente não passa.
        Assert.Equal("resend", Resend().UsuarioDeLogin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Usuario_em_branco_conta_como_ausente(string usuario)
    {
        // Variável de ambiente esvaziada no systemd chega como string vazia, não como null.
        var s = new EmailSettings { RemetenteEmail = "padelizou@gmail.com", SmtpUsuario = usuario };

        Assert.Equal("padelizou@gmail.com", s.UsuarioDeLogin);
    }

    [Fact]
    public void A_resposta_da_pessoa_volta_pra_caixa_de_contato()
    {
        // Quem recebe "sua inscrição foi confirmada" responde por reflexo. Com remetente
        // `nao-responda@`, sem este cabeçalho a resposta cai num buraco.
        var mensagem = EmailService.MontarMensagem(Resend(), "jogador@teste.local", "Jogador", "Inscrição confirmada", "<p>oi</p>");

        Assert.Equal("contato@padelizou.com.br", Assert.Single(mensagem.ReplyTo.Mailboxes).Address);
        Assert.Equal("nao-responda@padelizou.com.br", Assert.Single(mensagem.From.Mailboxes).Address);
    }

    [Fact]
    public void Sem_endereco_de_resposta_nao_vai_cabecalho_vazio()
    {
        // No Gmail o próprio remetente é caixa de verdade: a resposta já chega no lugar certo.
        var gmail = new EmailSettings { RemetenteEmail = "padelizou@gmail.com", RemetenteNome = "Padelizou" };

        var mensagem = EmailService.MontarMensagem(gmail, "jogador@teste.local", "Jogador", "Oi", "<p>oi</p>");

        Assert.Empty(mensagem.ReplyTo);
    }
}
