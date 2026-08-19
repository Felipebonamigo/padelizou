using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Padelizou.Middleware;

namespace Padelizou.Tests;

// Clicar no nome de um professor (ou de um jogador, ou de um torneio) dentro do painel não
// abria nada: o link saía relativo, e em admin.padelizou.com.br tudo que não é /Admin ou
// /Auth morre em 404 no AdminHostMiddleware. O par host/protocolo faz o tag helper montar o
// endereço do site público — e só lá, porque no localhost e no dev o endereço fixo mandaria
// quem está testando direto pra produção.
public class LinksDoPainelProSiteTests
{
    private static HttpContext Em(string host)
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Host = new HostString(host);
        return contexto;
    }

    [Fact]
    public void No_host_do_painel_o_link_sai_pro_site_publico()
    {
        var (host, protocolo) = AdminHostMiddleware.SaidaPraSitePublico(
            Em("admin.padelizou.com.br"), ehDesenvolvimento: false);

        Assert.Equal("padelizou.com.br", host);
        Assert.Equal("https", protocolo);
    }

    [Theory]
    [InlineData("padelizou.com.br", false)]            // já estou no site: relativo resolve
    [InlineData("dev.padelizou.com.br", false)]        // instância de teste serve tudo
    [InlineData("localhost", true)]                    // rodando na minha máquina
    [InlineData("admin.padelizou.com.br", true)]       // host forjado no localhost (curl -H Host:)
    public void Onde_o_site_inteiro_e_servido_o_link_continua_relativo(string host, bool desenvolvimento)
    {
        // Nulo é o que o asp-host/asp-protocol esperam pra deixar o link como estava — pular
        // de host aqui jogaria na PRODUÇÃO quem clicou no ambiente de teste.
        var saida = AdminHostMiddleware.SaidaPraSitePublico(Em(host), desenvolvimento);

        Assert.Null(saida.Host);
        Assert.Null(saida.Protocolo);
    }

    // ---- Os links, no arquivo ----
    //
    // O defeito é mudo: a view compila, a página abre, e só o clique não vai a lugar nenhum.
    // Só o arquivo denuncia um link novo que esqueceu de sair do host.

    [Fact]
    public void Todo_link_do_painel_pro_site_publico_sai_do_host()
    {
        var faltando = new List<string>();

        foreach (var arquivo in Directory.GetFiles(Path.Combine(PastaDoProjeto(), "Views", "Admin"), "*.cshtml"))
        {
            var texto = File.ReadAllText(arquivo);
            foreach (Match link in Regex.Matches(texto, @"<a[^>]*asp-controller=""(?<c>[^""]+)""[^>]*>", RegexOptions.Singleline))
            {
                // /Admin e /Auth são justamente o que este host serve — esses ficam relativos.
                var controller = link.Groups["c"].Value;
                if (controller is "Admin" or "Auth") continue;

                if (!link.Value.Contains("asp-host=\"@hostDoSite\""))
                {
                    faltando.Add($"{Path.GetFileName(arquivo)}: asp-controller=\"{controller}\"");
                }
            }
        }

        Assert.True(faltando.Count == 0,
            "Link do painel pro site público sem asp-host/asp-protocol — em produção o clique dá "
            + "404 sem sair do lugar. Falta em: " + string.Join(", ", faltando));
    }

    [Fact]
    public void O_perfil_do_jogador_e_chamado_pelo_nome_que_o_controller_tem()
    {
        // JogadoresController.Perfil. "Jogadors" (o nome do ARQUIVO) e "Details" (ação que não
        // existe) montavam um endereço que dava 404 até no site público.
        foreach (var arquivo in Directory.GetFiles(Path.Combine(PastaDoProjeto(), "Views", "Admin"), "*.cshtml"))
        {
            Assert.DoesNotContain("asp-controller=\"Jogadors\"", File.ReadAllText(arquivo));
        }
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
