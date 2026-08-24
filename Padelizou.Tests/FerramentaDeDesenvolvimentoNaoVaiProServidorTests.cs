using System.Text.Json;
using System.Xml.Linq;

namespace Padelizou.Tests;

/// <summary>
/// Auditoria de supply chain de 21/08/2026: o `Microsoft.VisualStudio.Web.CodeGeneration.Design`
/// estava referenciado SEM `PrivateAssets=all` — ao contrário do `EntityFrameworkCore.Tools`, três
/// linhas acima, que já estava certo. Sem essa marcação um pacote de ferramenta de mesa vira
/// dependência de RUNTIME e viaja inteiro pro VPS, com a árvore dele junto: o compilador Roslyn,
/// o MSBuild, o cliente NuGet completo, um Razor de .NET 6 (fora de suporte desde nov/2024) e o
/// `System.Data.DataSetExtensions` de 2018. Eram 46 DLLs de terceiros e 50 pastas de idioma no
/// servidor — 66 DLLs publicadas viraram 20 quando saíram.
///
/// Não é faxina: tudo isso roda com os privilégios do app (Postgres, tokens do Google, chaves do
/// Asaas), e Roslyn + MSBuild + cliente NuGet num servidor web é ferramental de pós-exploração —
/// compilar e rodar código novo e baixar pacote sem sair do processo.
///
/// Estes testes são o guarda: quebram se algum pacote de ferramenta voltar a entrar como
/// dependência de runtime. Detalhes em `SUPPLY-CHAIN.md` (SC-01).
/// </summary>
public class FerramentaDeDesenvolvimentoNaoVaiProServidorTests
{
    // Pacotes que só existem pra rodar na MÁQUINA de quem desenvolve. Nenhum deles tem o que
    // fazer no processo que atende padelizou.com.br.
    private static readonly string[] SoDeDesenvolvimento =
    [
        "Microsoft.CodeAnalysis",                     // compilador Roslyn
        "Microsoft.Build",                            // MSBuild
        "NuGet.",                                     // cliente NuGet (baixa e instala pacote)
        "Microsoft.VisualStudio.Web.CodeGeneration",  // scaffolding
        "Microsoft.DotNet.Scaffolding",
        "Microsoft.EntityFrameworkCore.Design",       // o Tools puxa; é design-time
        "Mono.TextTemplating",
        "Microsoft.DiaSymReader",
        "Microsoft.AspNetCore.Razor.Language",        // compilação de Razor em tempo de desenho
    ];

    [Fact]
    public void O_grafo_de_runtime_nao_carrega_ferramenta_de_desenvolvimento()
    {
        // O `deps.json` é a verdade do que o .NET vai CARREGAR — não o que o .csproj pediu. E como
        // este projeto de testes referencia o app, tudo que vazar do app aparece aqui também.
        // Antes da correção esta lista tinha mais de 130 entradas; hoje tem 38.
        var deps = Path.Combine(AppContext.BaseDirectory, "Padelizou.Tests.deps.json");
        Assert.True(File.Exists(deps), "Não achei o deps.json em " + AppContext.BaseDirectory);

        using var doc = JsonDocument.Parse(File.ReadAllText(deps));
        var bibliotecas = doc.RootElement.GetProperty("libraries")
            .EnumerateObject()
            .Select(p => p.Name.Split('/')[0])
            .ToList();

        var intrusos = bibliotecas
            .Where(b => SoDeDesenvolvimento.Any(f => b.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(b => b)
            .ToList();

        Assert.True(intrusos.Count == 0,
            "Ferramenta de desenvolvimento entrou no grafo de runtime e vai pro servidor: "
            + string.Join(", ", intrusos)
            + ". Marque o PackageReference com <PrivateAssets>all</PrivateAssets> — ver SUPPLY-CHAIN.md (SC-01).");
    }

    [Fact]
    public void Pacote_de_ferramenta_no_csproj_precisa_de_PrivateAssets_all()
    {
        var pedidos = XDocument.Load(CaminhoDoCsproj())
            .Descendants("PackageReference")
            .Select(p => new
            {
                Id = p.Attribute("Include")?.Value ?? "",
                Privado = (p.Element("PrivateAssets")?.Value ?? p.Attribute("PrivateAssets")?.Value ?? "")
                          .Equals("all", StringComparison.OrdinalIgnoreCase)
            })
            .Where(p => SoDeDesenvolvimento.Any(f => p.Id.StartsWith(f, StringComparison.OrdinalIgnoreCase))
                        || p.Id.EndsWith(".Tools", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var soltos = pedidos.Where(p => !p.Privado).Select(p => p.Id).ToList();

        Assert.True(soltos.Count == 0,
            "PackageReference de ferramenta sem <PrivateAssets>all</PrivateAssets> — vai virar "
            + "dependência de runtime e ser publicado no VPS: " + string.Join(", ", soltos));
    }

    [Fact]
    public void O_app_nao_referencia_o_cliente_do_NuGet()
    {
        // `NuGet.Packaging` e `NuGet.Protocol` estavam listados como dependência DIRETA do app e não
        // apareciam em uma linha de código — nem em .cs, nem em .cshtml. Traziam 9 DLLs pro servidor.
        var doNuGet = XDocument.Load(CaminhoDoCsproj())
            .Descendants("PackageReference")
            .Select(p => p.Attribute("Include")?.Value ?? "")
            .Where(id => id.StartsWith("NuGet.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(doNuGet.Count == 0,
            "O app não usa o cliente do NuGet em lugar nenhum do código, mas ele está referenciado: "
            + string.Join(", ", doNuGet));
    }

    private static string CaminhoDoCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Padelizou.csproj");
            if (File.Exists(alvo)) return alvo;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Não achei o Padelizou.csproj subindo a partir de " + AppContext.BaseDirectory);
    }
}
