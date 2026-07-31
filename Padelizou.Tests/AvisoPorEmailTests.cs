using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O push só alcança quem instalou o app; o WhatsApp depende do chip, que ainda não está
// ligado. Quem não fez nenhum dos dois não ficava sabendo de NADA — nem que foi inscrito, nem
// que saiu da lista de espera, nem que o parceiro desistiu. E-mail é o que quase todo mundo
// tem, e o cadastro já pede.
public class AvisoPorEmailTests
{
    private const string Site = "https://padelizou.com.br";

    [Theory]
    [InlineData("felipe@gmail.com", true)]
    [InlineData("felipe.bona@sub.dominio.com.br", true)]
    [InlineData("  felipe@gmail.com  ", true)]     // espaço colado do cadastro
    [InlineData("felipe", false)]                  // sem arroba
    [InlineData("@gmail.com", false)]              // sem nome
    [InlineData("felipe@", false)]                 // sem domínio
    [InlineData("felipe@gmail", false)]            // domínio sem ponto
    [InlineData("felipe@.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Peneira_minima_de_endereco(string? email, bool esperado)
        => Assert.Equal(esperado, AvisoPorEmail.PodeReceber(true, email));

    [Fact]
    public void Quem_desligou_o_aviso_por_email_nao_recebe()
    {
        // A preferência manda, mesmo com endereço válido: é opção da pessoa em Preferências.
        Assert.False(AvisoPorEmail.PodeReceber(false, "felipe@gmail.com"));
    }

    [Fact]
    public void O_link_relativo_vira_endereco_completo()
    {
        // O aviso carrega "/Torneios/Details/17" porque o navegador resolve sozinho; no
        // e-mail não há site em volta, e uma barra solta não vira link nenhum.
        var html = AvisoPorEmail.MontarHtml("Abriu vaga", "Vocês estão dentro.", "/Torneios/Details/17", Site);

        Assert.Contains("https://padelizou.com.br/Torneios/Details/17", html);
    }

    [Fact]
    public void Link_que_ja_e_absoluto_passa_intacto()
    {
        var html = AvisoPorEmail.MontarHtml("Oi", "corpo", "https://outro.com/x", Site);

        Assert.Contains("https://outro.com/x", html);
        Assert.DoesNotContain("padelizou.com.br/https", html);
    }

    [Fact]
    public void Sem_link_o_email_nao_ganha_botao_quebrado()
    {
        var html = AvisoPorEmail.MontarHtml("Oi", "corpo", null, Site);

        // Sem url o botão vira "/" — melhor não ter botão do que ter um que não leva a nada
        // útil. (A raiz do site ainda é destino válido, então o botão aparece apontando pra ela.)
        Assert.Contains("https://padelizou.com.br", html);
    }

    [Fact]
    public void Nome_de_gente_no_corpo_nao_vira_html()
    {
        // O corpo carrega nome ("Fulano inscreveu você") e nome é campo livre: sem escape,
        // um nome com marcação entraria como HTML no e-mail de outra pessoa.
        var html = AvisoPorEmail.MontarHtml(
            "<img src=x onerror=alert(1)>", "Fulano <b>Silva</b> inscreveu você", "/x", Site);

        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<b>Silva</b>", html);
        Assert.Contains("&lt;b&gt;Silva&lt;/b&gt;", html);
    }
}
