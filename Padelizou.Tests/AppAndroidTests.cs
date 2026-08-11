using System.Text.Json;
using Padelizou.Middleware;
using Padelizou.Services;

namespace Padelizou.Tests;

// O app da Play Store é este site dentro de uma casca (TWA). A única coisa que faz o Android
// confiar na casca é o /.well-known/assetlinks.json — e o defeito dele é MUDO: o app continua
// abrindo, só que com a barra de endereço do Chrome por cima, no celular de quem instalou.
// Ninguém reclama, nada loga, e do lado de cá está tudo verde.
//
// Estes testes existem porque cada peça disso é invisível de um jeito diferente: a impressão
// digital é uma sequência de 64 caracteres que ninguém confere a olho, o formato do arquivo é
// ditado pelo Google, e o caminho passa por um portão que pode ser religado por um botão do
// painel meses depois de tudo isto ter sido esquecido.
public class AppAndroidTests
{
    // Uma impressão digital de verdade tem 32 pares hexadecimais. Esta é inventada, mas tem o
    // formato exato — é o formato que os testes precisam exercitar.
    private const string Impressao =
        "A1:B2:C3:D4:E5:F6:07:18:29:3A:4B:5C:6D:7E:8F:90:A1:B2:C3:D4:E5:F6:07:18:29:3A:4B:5C:6D:7E:8F:90";

    private const string Outra =
        "0F:1E:2D:3C:4B:5A:69:78:87:96:A5:B4:C3:D2:E1:F0:0F:1E:2D:3C:4B:5A:69:78:87:96:A5:B4:C3:D2:E1:F0";

    // ── Enquanto não existe app, o arquivo não pode existir ────────────────────────────────

    [Fact]
    public void Sem_impressao_digital_configurada_o_arquivo_nao_e_servido()
    {
        // Hoje é este o estado: ninguém publicou nada na loja ainda. O endpoint responde 404,
        // que é o que a ferramenta de verificação do Google sabe explicar. Servir um arquivo
        // vazio daria a MESMA falha lá na frente, com uma cara muito pior de diagnosticar.
        Assert.False(new AndroidSettings().EstaConfigurado);
    }

    [Fact]
    public void So_o_nome_do_pacote_nao_basta()
    {
        var android = new AndroidSettings { PackageName = "br.com.padelizou.app" };
        Assert.False(android.EstaConfigurado);
    }

    // ── A mesma chave, copiada de lugares diferentes ───────────────────────────────────────

    [Theory]
    // Como o Play Console mostra.
    [InlineData("A1:B2:C3:D4:E5:F6:07:18:29:3A:4B:5C:6D:7E:8F:90:A1:B2:C3:D4:E5:F6:07:18:29:3A:4B:5C:6D:7E:8F:90")]
    // Como às vezes sai do keytool.
    [InlineData("a1:b2:c3:d4:e5:f6:07:18:29:3a:4b:5c:6d:7e:8f:90:a1:b2:c3:d4:e5:f6:07:18:29:3a:4b:5c:6d:7e:8f:90")]
    // Copiado da tela e colado sem os dois-pontos.
    [InlineData("A1B2C3D4E5F60718293A4B5C6D7E8F90A1B2C3D4E5F60718293A4B5C6D7E8F90")]
    // Com espaço e quebra de linha em volta, que é o que acontece copiando de um terminal.
    [InlineData("\n  A1:B2:C3:D4:E5:F6:07:18:29:3A:4B:5C:6D:7E:8F:90:A1:B2:C3:D4:E5:F6:07:18:29:3A:4B:5C:6D:7E:8F:90  \n")]
    public void A_mesma_chave_escrita_de_varios_jeitos_vira_a_mesma_impressao(string comoFoiColada)
    {
        var android = new AndroidSettings { Sha256Fingerprints = comoFoiColada };

        Assert.Equal(new[] { Impressao }, android.ImpressoesDigitais);
    }

    // ── As DUAS impressões digitais ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(",")]
    [InlineData("; ")]
    [InlineData(" ")]
    [InlineData("\n")]
    public void As_duas_chaves_cabem_no_mesmo_valor_de_configuracao(string separador)
    {
        // Na prática são sempre duas: a chave de upload (que fica na máquina do Felipe) e a que
        // o Google gera ao reassinar o pacote. Configurar só uma faz o app funcionar na hora do
        // teste local e falhar pra quem instalou pela loja — o pior jeito de descobrir.
        var android = new AndroidSettings { Sha256Fingerprints = Impressao + separador + Outra };

        Assert.Equal(new[] { Impressao, Outra }, android.ImpressoesDigitais);
    }

    [Fact]
    public void Impressao_digital_truncada_nao_entra_no_arquivo()
    {
        // Uma impressão digital com um par a menos não é "quase certa": ela invalida o arquivo
        // INTEIRO pro Google, derrubando junto a que estava correta. Por isso a inválida é
        // descartada — e listada em ImpressoesInvalidas pra virar aviso no log, não sumiço.
        var android = new AndroidSettings
        {
            Sha256Fingerprints = Impressao + "," + Outra.Substring(0, Outra.Length - 3)
        };

        Assert.Equal(new[] { Impressao }, android.ImpressoesDigitais);
        Assert.Single(android.ImpressoesInvalidas);
    }

    [Fact]
    public void Texto_que_nao_e_hexadecimal_nao_passa()
    {
        var android = new AndroidSettings { Sha256Fingerprints = "COLE:AQUI:A:IMPRESSAO:DIGITAL" };

        Assert.Empty(android.ImpressoesDigitais);
        Assert.False(android.EstaConfigurado);
    }

    // ── O formato é ditado pelo Google, não por nós ────────────────────────────────────────

    [Fact]
    public void O_arquivo_sai_no_formato_que_o_Google_exige()
    {
        var android = new AndroidSettings
        {
            PackageName = "br.com.padelizou.app",
            Sha256Fingerprints = Impressao + "," + Outra
        };

        var raiz = JsonDocument.Parse(JsonSerializer.Serialize(android.MontarAssetLinks())).RootElement;

        // É uma LISTA no topo, não um objeto — o Google recusa o arquivo se vier objeto.
        Assert.Equal(JsonValueKind.Array, raiz.ValueKind);
        var item = raiz.EnumerateArray().Single();

        Assert.Equal("delegate_permission/common.handle_all_urls",
            item.GetProperty("relation").EnumerateArray().Single().GetString());

        var alvo = item.GetProperty("target");
        Assert.Equal("android_app", alvo.GetProperty("namespace").GetString());
        Assert.Equal("br.com.padelizou.app", alvo.GetProperty("package_name").GetString());
        Assert.Equal(new[] { Impressao, Outra },
            alvo.GetProperty("sha256_cert_fingerprints").EnumerateArray().Select(f => f.GetString()));
    }

    [Fact]
    public void Os_nomes_dos_campos_sao_com_underline_e_nao_camelCase()
    {
        // O padrão de serialização do ASP.NET Core é camelCase. Estes nomes NÃO são nossos —
        // são do Google, e "packageName" faz o arquivo ser lido como vazio, em silêncio.
        var json = JsonSerializer.Serialize(new AndroidSettings
        {
            Sha256Fingerprints = Impressao
        }.MontarAssetLinks());

        Assert.Contains("\"package_name\"", json);
        Assert.Contains("\"sha256_cert_fingerprints\"", json);
        Assert.DoesNotContain("packageName", json);
        Assert.DoesNotContain("sha256CertFingerprints", json);
    }

    // ── O caminho não pode ser engolido pelo portão ────────────────────────────────────────

    [Theory]
    [InlineData("/.well-known/assetlinks.json")]
    [InlineData("/.well-known")]
    public void O_verificador_do_Google_passa_pelo_portao_de_acesso_antecipado(string caminho)
    {
        // Quem busca esse arquivo é um robô sem cookie e sem conta, e ele NÃO segue
        // redirecionamento. Se o portão for religado pelo botão do painel e este caminho sair
        // da lista, o app de todo mundo passa a mostrar a barra de endereço do Chrome — e do
        // lado de cá nada quebra, nada loga, nada avisa.
        Assert.True(AcessoAntecipadoMiddleware.EhCaminhoLiberado(caminho));
    }

    [Fact]
    public void O_endpoint_esta_declarado_no_caminho_exato_que_o_Google_busca()
    {
        // O Google busca sempre em https://<dominio>/.well-known/assetlinks.json, na raiz e
        // com esse nome. Servir em qualquer outro caminho é o mesmo que não servir.
        Assert.Contains("\"/.well-known/assetlinks.json\"", LerProgramCs());
    }

    [Fact]
    public void A_secao_de_configuracao_do_Android_esta_registrada()
    {
        // Sem o Configure<AndroidSettings> a impressão digital preenchida no systemd não chega
        // em lugar nenhum: o endpoint segue respondendo 404 com tudo aparentemente certo.
        Assert.Contains("Configure<AndroidSettings>", LerProgramCs());
    }

    private static string LerProgramCs()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Program.cs")))
            dir = dir.Parent;

        Assert.True(dir != null, "Não achei o Program.cs subindo a partir de " + AppContext.BaseDirectory);
        return File.ReadAllText(Path.Combine(dir!.FullName, "Padelizou", "Program.cs"));
    }
}
