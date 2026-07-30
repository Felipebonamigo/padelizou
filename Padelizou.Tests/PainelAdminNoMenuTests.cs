using Microsoft.AspNetCore.Http;
using Padelizou.Middleware;

namespace Padelizou.Tests;

// O item "Painel Admin" do menu. O painel mora num subdomínio só dele em produção, e era um
// endereço fixo abrindo em aba nova: no localhost isso jogava quem estava testando dentro da
// produção, e o item nunca acendia como selecionado.
public class PainelAdminNoMenuTests
{
    private static HttpContext Em(string host)
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Host = new HostString(host);
        return contexto;
    }

    [Fact]
    public void Do_site_publico_o_link_pula_de_host()
    {
        // É o único lugar onde /Admin dá 404 — aqui o pulo é obrigatório.
        Assert.Equal("https://admin.padelizou.com.br/Admin",
            AdminHostMiddleware.LinkDoPainel(Em("padelizou.com.br"), ehDesenvolvimento: false));
    }

    [Fact]
    public void Do_www_tambem_pula()
    {
        Assert.Equal("https://admin.padelizou.com.br/Admin",
            AdminHostMiddleware.LinkDoPainel(Em("www.padelizou.com.br"), ehDesenvolvimento: false));
    }

    [Theory]
    [InlineData("localhost", true)]                    // rodando na minha máquina
    [InlineData("dev.padelizou.com.br", false)]        // instância de teste, serve tudo
    [InlineData("admin.padelizou.com.br", false)]      // já estou dentro do painel
    public void Onde_slash_Admin_existe_o_link_e_relativo(string host, bool desenvolvimento)
    {
        // Endereço fixo aqui mandaria pra PRODUÇÃO quem clicou no ambiente de teste.
        Assert.Equal("/Admin", AdminHostMiddleware.LinkDoPainel(Em(host), desenvolvimento));
    }

    [Fact]
    public void So_o_host_do_painel_perde_o_menu_do_site()
    {
        // Lá dentro Torneio/Aula/Ranking dão 404 — mostrar o menu seria oferecer beco sem saída.
        Assert.True(AdminHostMiddleware.ServeSoOPainel(Em("admin.padelizou.com.br"), ehDesenvolvimento: false));
        Assert.False(AdminHostMiddleware.ServeSoOPainel(Em("padelizou.com.br"), ehDesenvolvimento: false));
        Assert.False(AdminHostMiddleware.ServeSoOPainel(Em("dev.padelizou.com.br"), ehDesenvolvimento: false));
    }

    [Fact]
    public void No_localhost_o_menu_continua_inteiro_mesmo_com_o_host_forjado()
    {
        // O middleware não roda em Development (dá pra forjar o Host com curl pra testar); se o
        // menu sumisse aqui, o site local ficaria sem barra de navegação por engano.
        Assert.False(AdminHostMiddleware.ServeSoOPainel(Em("admin.padelizou.com.br"), ehDesenvolvimento: true));
    }
}
