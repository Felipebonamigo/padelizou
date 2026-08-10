using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// A página de privacidade ficou até 10/08/2026 com o TEXTO DE EXEMPLO do ASP.NET ("Use this
// page to detail your site's privacy policy") — linkada no rodapé, em produção, com CPF,
// e-mail, telefone e endereço de 154 pessoas no banco. Ninguém percebeu porque nada quebra:
// a página abre, o link funciona, e o defeito é o CONTEÚDO.
//
// Estes testes existem pra isso não voltar, e pra ela não virar promessa vazia: cada serviço
// externo que o código usa tem que estar declarado lá. Quando alguém plugar o próximo
// (e vai), é aqui que a conta chega.
public class PoliticaDePrivacidadeTests
{
    [Fact]
    public void A_pagina_nao_e_mais_o_texto_de_exemplo_do_framework()
    {
        var pagina = Pagina();

        Assert.DoesNotContain("Use this page to detail", pagina, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Privacy Policy</h1>", pagina, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LGPD", pagina);
    }

    // Cada linha aqui é um serviço que o código REALMENTE chama. Compartilhar dado com alguém
    // que a política não cita é exatamente o que a LGPD proíbe — e é o tipo de coisa que entra
    // sem ninguém notar, junto com a funcionalidade nova.
    [Theory]
    [InlineData("ViaCEP", "Services/ConsultaDeCep.cs")]
    [InlineData("Mundo do Atleta", "Services/RankingRsService.cs")]
    [InlineData("Google", "Services/GoogleCalendarService.cs e o envio de e-mail")]
    [InlineData("WhatsApp", "o envio de avisos pela Evolution")]
    [InlineData("Apple", "a entrega de push no iPhone")]
    public void Todo_terceiro_que_recebe_dado_esta_declarado(string terceiro, string ondeVive)
    {
        Assert.True(Pagina().Contains(terceiro, StringComparison.OrdinalIgnoreCase),
            $"A política de privacidade não cita '{terceiro}', que recebe dado de usuário em "
            + $"{ondeVive}. Compartilhar com quem a política não declara é o que a LGPD proíbe.");
    }

    // Regra antiga da casa (ver memória "Nunca citar o Asaas nas telas"): pro usuário existe
    // "meio de pagamento" e "taxa do Padelizou", nunca o nome do gateway. A política é uma
    // tela como as outras — e é a mais lida por quem está desconfiado.
    [Fact]
    public void A_politica_nao_expoe_o_nome_do_gateway_de_pagamento()
    {
        Assert.False(Pagina().Contains("asaas", StringComparison.OrdinalIgnoreCase),
            "A política de privacidade passou a citar o gateway pelo nome. Na tela ele é "
            + "'meio de pagamento' — a regra vale aqui também.");

        Assert.Contains("meio de pagamento", Pagina(), StringComparison.OrdinalIgnoreCase);
    }

    // Uma política sem canal de contato não serve pra nada: o direito de pedir cópia,
    // correção e exclusão depende de existir pra quem pedir.
    [Fact]
    public void Diz_quem_responde_e_por_onde_falar()
    {
        var pagina = Pagina();

        Assert.Contains("controlador", pagina, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SuporteOpcoes", pagina);   // contato lido da configuração, não escrito à mão
        Assert.Contains("mailto:", pagina);
    }

    // O rodapé é o único caminho até ela em todo o site.
    [Fact]
    public void Continua_alcancavel_pelo_rodape()
    {
        var layout = Arquivo(Path.Combine("Views", "Shared", "_Layout.cshtml"));

        Assert.True(Regex.IsMatch(layout, @"asp-action=""Privacy"""),
            "O link da política sumiu do _Layout — a página existe e ninguém chega nela.");
    }

    // Sem os comentários Razor, que não chegam ao navegador — e que aqui citam justamente o
    // texto de exemplo antigo e o nome do gateway, pra explicar por que os testes existem.
    // Sem este corte, a explicação reprovaria a implementação.
    private static string Pagina() =>
        Regex.Replace(Arquivo(Path.Combine("Views", "Home", "Privacy.cshtml")),
            @"@\*.*?\*@", "", RegexOptions.Singleline);

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
