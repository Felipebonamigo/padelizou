using Microsoft.AspNetCore.Http;
using Padelizou.Services;
using SkiaSharp;

namespace Padelizou.Tests;

// Toda imagem enviada ao Padelizou passa pelo ImagemEnviada. O que motivou isso foi uma medição
// real: uma única capa de torneio de 8 MB em produção, sozinha 60% de todo o armazenamento — e
// baixada inteira por quem abrisse aquele torneio no celular.
public class ImagemEnviadaTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "pdz-img-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
    }

    // ── Conta de redimensionar ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(4000, 3000, 1600, 1600, 1200)]  // paisagem: o lado maior vira o teto
    [InlineData(3000, 4000, 1600, 1200, 1600)]  // retrato: idem, no outro eixo
    [InlineData(2000, 2000, 512, 512, 512)]     // quadrada
    public void Encolhe_mantendo_a_proporcao(int lArg, int lAlt, int teto, int espArg, int espAlt)
    {
        Assert.Equal((espArg, espAlt), ImagemEnviada.NovoTamanho(lArg, lAlt, teto));
    }

    [Fact]
    public void Imagem_menor_que_o_teto_passa_intacta()
    {
        // Esticar um logo de 80px pra 512px não acrescenta detalhe nenhum — só peso e borrão.
        Assert.Equal((80, 60), ImagemEnviada.NovoTamanho(80, 60, 512));
        Assert.Equal((512, 512), ImagemEnviada.NovoTamanho(512, 512, 512));
    }

    [Fact]
    public void Imagem_muito_estreita_nao_vira_zero_pixel()
    {
        // 4000x3 encolhido pra caber em 1600 daria altura 1,2 → 1 se arredondasse pra baixo,
        // e 0 se truncasse. Zero pixel quebra o codificador.
        var (largura, altura) = ImagemEnviada.NovoTamanho(4000, 3, 1600);

        Assert.Equal(1600, largura);
        Assert.True(altura >= 1);
    }

    // ── Peneira de entrada ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("foto.jpg")]
    [InlineData("FOTO.JPG")]
    [InlineData("escudo.PNG")]
    [InlineData("capa.webp")]
    public void Aceita_os_formatos_que_o_celular_manda(string nome)
    {
        Assert.True(ImagemEnviada.ExtensaoAceita(nome));
    }

    [Theory]
    [InlineData("contrato.pdf")]
    [InlineData("virus.exe")]
    [InlineData("script.svg")]   // SVG é XML: carrega script e não é imagem pra esse fim
    [InlineData("semextensao")]
    [InlineData("")]
    [InlineData(null)]
    public void Recusa_o_que_nao_e_imagem(string? nome)
    {
        Assert.False(ImagemEnviada.ExtensaoAceita(nome));
    }

    [Fact]
    public void O_nome_do_arquivo_nunca_vem_de_quem_envia()
    {
        // O nome chega do navegador. Antes ele era colado no caminho em disco ("guid_" + nome),
        // e um nome com "../" sairia da pasta de uploads.
        var nome = ImagemEnviada.NomeDeArquivo();

        Assert.EndsWith(".webp", nome);
        Assert.DoesNotContain("/", nome);
        Assert.DoesNotContain("\\", nome);
        Assert.DoesNotContain("..", nome);
        Assert.NotEqual(nome, ImagemEnviada.NomeDeArquivo());
    }

    // ── Processamento de verdade ──────────────────────────────────────────────────────

    [Fact]
    public async Task Capa_gigante_encolhe_e_perde_a_maior_parte_do_peso()
    {
        // É o caso real que originou tudo: foto grande de celular virando capa de torneio.
        var original = ImagemDeRuido(2000, 1500);
        Assert.True(original.Length > 1_000_000, $"o teste precisa de uma imagem grande, veio {original.Length} bytes");

        var caminho = (await ImagemEnviada.SalvarAsync(
            Arquivo(original, "capa-do-celular.png"), _webRoot, "capas-torneio", FormatoDeImagem.CapaTorneio)).Caminho;

        Assert.NotNull(caminho);
        var (largura, altura, bytes) = LerSalva(caminho!);

        Assert.Equal(1600, largura);
        Assert.Equal(1200, altura);
        Assert.True(bytes < original.Length / 4,
            $"esperava encolher bem abaixo de 1/4; original {original.Length}, salvo {bytes}");
    }

    [Fact]
    public async Task Sai_sempre_em_webp_seja_qual_for_a_entrada()
    {
        var caminho = (await ImagemEnviada.SalvarAsync(
            Arquivo(ImagemDeRuido(300, 300), "foto.png"), _webRoot, "fotos-perfil", FormatoDeImagem.FotoPerfil)).Caminho;

        Assert.NotNull(caminho);
        Assert.EndsWith(".webp", caminho);

        using var codec = SKCodec.Create(CaminhoFisico(caminho!));
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
    }

    [Fact]
    public async Task Logo_pequeno_nao_e_esticado()
    {
        // Escudo de time costuma vir pequeno e já pronto. Aumentar só borraria.
        var caminho = (await ImagemEnviada.SalvarAsync(
            Arquivo(ImagemDeRuido(120, 90), "escudo.png"), _webRoot, "logos-time", FormatoDeImagem.LogoTime)).Caminho;

        Assert.NotNull(caminho);
        var (largura, altura, _) = LerSalva(caminho!);

        Assert.Equal(120, largura);
        Assert.Equal(90, altura);
    }

    [Fact]
    public async Task Logo_com_fundo_transparente_continua_transparente()
    {
        // Achatar o alfa deixaria um retângulo preto em volta de todo escudo de time.
        var caminho = (await ImagemEnviada.SalvarAsync(
            Arquivo(ImagemComTransparencia(200, 200), "escudo.png"), _webRoot, "logos-time", FormatoDeImagem.LogoTime)).Caminho;

        Assert.NotNull(caminho);

        using var salva = SKBitmap.Decode(CaminhoFisico(caminho!));
        Assert.True(salva.GetPixel(5, 5).Alpha < 40, "o canto devia ter continuado transparente");
        Assert.True(salva.GetPixel(100, 100).Alpha > 200, "o meio devia ter continuado opaco");
    }

    [Fact]
    public async Task Arquivo_renomeado_pra_png_e_recusado()
    {
        // Alguém renomeia contrato.pdf pra foto.png. A extensão engana a primeira peneira;
        // quem decide de verdade é o decodificador.
        var lixo = new byte[5000];
        Random.Shared.NextBytes(lixo);

        var caminho = (await ImagemEnviada.SalvarAsync(
            Arquivo(lixo, "foto.png"), _webRoot, "fotos-perfil", FormatoDeImagem.FotoPerfil)).Caminho;

        Assert.Null(caminho);
    }

    [Fact]
    public async Task Arquivo_gigante_e_recusado_antes_de_decodificar()
    {
        // O tamanho é conferido no cabeçalho do upload: uma imagem de 40 MB seguraria o
        // servidor inteiro enquanto fosse processada.
        var gordo = Arquivo(new byte[16], "foto.png", tamanhoDeclarado: ImagemEnviada.BytesMaximos + 1);

        var r = await ImagemEnviada.SalvarAsync(gordo, _webRoot, "fotos-perfil", FormatoDeImagem.FotoPerfil);

        Assert.False(r.Salvou);
        // Recusar não basta: a pessoa precisa saber por quê, senão fica tentando o mesmo arquivo.
        Assert.True(r.DeuErro);
        Assert.Contains("MB", r.Erro);
    }

    [Fact]
    public async Task Nome_com_travessia_de_pasta_nao_escapa_dos_uploads()
    {
        var caminho = (await ImagemEnviada.SalvarAsync(
            Arquivo(ImagemDeRuido(100, 100), "../../../evil.png"), _webRoot, "fotos-perfil", FormatoDeImagem.FotoPerfil)).Caminho;

        Assert.NotNull(caminho);
        Assert.StartsWith("/uploads/fotos-perfil/", caminho);

        var esperada = Path.Combine(_webRoot, "uploads", "fotos-perfil");
        var gravada = Path.GetDirectoryName(CaminhoFisico(caminho!))!;
        Assert.Equal(Path.GetFullPath(esperada), Path.GetFullPath(gravada));

        // E nada foi parar fora da pasta de uploads.
        Assert.Single(Directory.GetFiles(_webRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Sem_arquivo_nao_e_erro_e_sim_ausencia_de_foto()
    {
        // A foto é sempre opcional: derrubar um cadastro longo por causa dela custa caro.
        // E "não mandou foto" NÃO pode virar mensagem de erro — quem deixou o campo vazio de
        // propósito não fez nada de errado.
        var semNada = await ImagemEnviada.SalvarAsync(null, _webRoot, "fotos-perfil", FormatoDeImagem.FotoPerfil);
        var vazio = await ImagemEnviada.SalvarAsync(
            Arquivo(Array.Empty<byte>(), "foto.png"), _webRoot, "fotos-perfil", FormatoDeImagem.FotoPerfil);

        foreach (var r in new[] { semNada, vazio })
        {
            Assert.False(r.Salvou);
            Assert.False(r.DeuErro);   // ausência não é falha
            Assert.Null(r.Erro);
        }
    }

    [Fact]
    public async Task Arquivo_que_nao_e_imagem_de_verdade_avisa_em_vez_de_sumir_calado()
    {
        // O caso que motivou tudo: por um dia inteiro o upload de logo falhou sem dizer nada,
        // porque falha e ausência devolviam o mesmo `null` e o chamador tratava tudo como
        // ausência. Quem tenta salvar uma imagem ruim precisa ouvir isso.
        var r = await ImagemEnviada.SalvarAsync(
            Arquivo(System.Text.Encoding.UTF8.GetBytes("isto aqui é texto, não imagem"), "foto.png"),
            _webRoot, "fotos-perfil", FormatoDeImagem.FotoPerfil);

        Assert.False(r.Salvou);
        Assert.True(r.DeuErro);
        Assert.False(string.IsNullOrWhiteSpace(r.Erro));
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────────────

    private static FormFile Arquivo(byte[] conteudo, string nome, long? tamanhoDeclarado = null)
    {
        var stream = new MemoryStream(conteudo);
        return new FormFile(stream, 0, tamanhoDeclarado ?? conteudo.Length, "arquivo", nome);
    }

    // Ruído em vez de cor chapada: PNG de cor sólida comprime pra quase nada e o teste de
    // "encolheu bastante" passaria sem provar coisa alguma.
    private static byte[] ImagemDeRuido(int largura, int altura)
    {
        var info = new SKImageInfo(largura, altura, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new byte[info.BytesSize];
        new Random(42).NextBytes(pixels);
        for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255; // alfa opaco

        using var imagem = SKImage.FromPixelCopy(info, pixels);
        using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);
        return dados.ToArray();
    }

    // Círculo opaco no meio, canto transparente — o formato de um escudo de time.
    private static byte[] ImagemComTransparencia(int largura, int altura)
    {
        using var bitmap = new SKBitmap(largura, altura, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var tinta = new SKPaint { Color = SKColors.OrangeRed, IsAntialias = true };
            canvas.DrawCircle(largura / 2f, altura / 2f, largura / 3f, tinta);
        }

        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);
        return dados.ToArray();
    }

    private string CaminhoFisico(string caminhoRelativo) =>
        Path.Combine(_webRoot, caminhoRelativo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private (int Largura, int Altura, long Bytes) LerSalva(string caminhoRelativo)
    {
        var fisico = CaminhoFisico(caminhoRelativo);
        using var codec = SKCodec.Create(fisico);
        return (codec.Info.Width, codec.Info.Height, new FileInfo(fisico).Length);
    }
}
