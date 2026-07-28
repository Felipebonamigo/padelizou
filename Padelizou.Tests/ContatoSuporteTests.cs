using Padelizou.Services;

namespace Padelizou.Tests;

// O número de contato vira link clicável em toda página do site. Um dígito a mais, um traço
// que sobrou ou um "55" duplicado não quebram nada visivelmente: o link simplesmente abre uma
// conversa com ninguém, e a pessoa que queria relatar um bug desiste sem que a gente saiba.
public class ContatoSuporteTests
{
    [Fact]
    public void Numero_do_suporte_vira_link_valido_do_whatsapp()
    {
        var link = WhatsAppLinkHelper.GerarLink(new SuporteSettings().WhatsApp, "Olá!");

        // 55 (Brasil) + 51 (DDD) + o número. Sem parênteses, traço ou espaço — o wa.me recusa.
        Assert.StartsWith("https://wa.me/5551992395650?text=", link);
    }

    [Theory]
    [InlineData("(51) 99239-5650")]
    [InlineData("51 99239 5650")]
    [InlineData("51992395650")]
    public void Numero_digitado_com_qualquer_mascara_chega_limpo_no_link(string comoFoiDigitado)
    {
        // O número vem de configuração e pode ser editado à mão um dia, com máscara ou sem.
        Assert.StartsWith("https://wa.me/5551992395650?", WhatsAppLinkHelper.GerarLink(comoFoiDigitado, "oi"));
    }

    [Fact]
    public void Mensagem_com_acento_e_espaco_chega_inteira_do_outro_lado()
    {
        const string mensagem = "Olá! Aqui é o João, do Padelizou.";
        var link = WhatsAppLinkHelper.GerarLink("51992395650", mensagem);

        Assert.DoesNotContain(" ", link);     // espaço cru cortaria a URL no meio
        Assert.Contains("Ol%C3%A1", link);    // acento vai codificado, não cru

        // O que importa não é o link ser bonito, é a mensagem voltar idêntica quando o
        // WhatsApp a decodifica. Testar a ida e a volta pega o que "parece certo" e não é.
        var texto = new Uri(link).Query.Replace("?text=", "");
        Assert.Equal(mensagem, System.Net.WebUtility.UrlDecode(texto));
    }

    [Fact]
    public void Numero_aparece_formatado_pra_quem_le_na_tela()
    {
        Assert.Equal("(51) 99239-5650", new SuporteSettings().WhatsAppFormatado);
        Assert.Equal("(51) 3333-4444", WhatsAppLinkHelper.Formatar("5133334444"));
    }

    [Fact]
    public void Numero_de_tamanho_estranho_e_devolvido_como_esta_em_vez_de_embaralhado()
    {
        // Melhor mostrar sem máscara do que aplicar uma regra que não previa aquele caso e
        // exibir um número errado — que é pior que um número feio.
        Assert.Equal("123", WhatsAppLinkHelper.Formatar("123"));
        Assert.Equal("", WhatsAppLinkHelper.Formatar(null));
    }
}
