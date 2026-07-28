using SkiaSharp;

namespace Padelizou.Services;

// Tudo que entra como imagem no Padelizou passa por aqui: foto de perfil, logo de time e capa
// de torneio. Antes cada controller gravava o arquivo cru do jeito que veio do celular, e o
// resultado apareceu na medição de backup — uma capa de torneio de 8 MB, sozinha 60% de todo o
// armazenamento de produção, baixada por toda pessoa que abrisse aquele torneio no 4G.
//
// Quatro coisas acontecem aqui, e cada uma resolve um problema diferente:
//   1. REDIMENSIONA — ninguém precisa de 4000px numa tela de celular.
//   2. RECODIFICA em WebP — mesma imagem, uma fração do peso, transparência preservada.
//   3. APAGA os metadados — foto de celular carrega coordenada de GPS embutida. Publicar a
//      foto de perfil de alguém junto com o lugar onde ela foi tirada é vazar endereço.
//      Aqui isso sai de graça: decodificar e recodificar não leva EXIF junto.
//   4. NÃO CONFIA no nome do arquivo — o nome vem do navegador, e o nome antigo ia direto pro
//      caminho em disco.
public static class ImagemEnviada
{
    // Extensões que a gente aceita receber. A saída é sempre .webp, independentemente do que
    // entrou — a extensão é só a primeira peneira, quem decide de verdade é o decodificador.
    private static readonly string[] ExtensoesAceitas = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };

    public const string ExtensaoDeSaida = ".webp";

    // Teto do arquivo que chega. Acima disso nem tentamos decodificar: uma imagem de 40 MB
    // travaria o servidor pra todo mundo enquanto é processada.
    public const long BytesMaximos = 25 * 1024 * 1024;

    // Teto das dimensões declaradas no cabeçalho. Um PNG de 2 MB pode declarar 50000x50000 e
    // estourar a memória do servidor ao ser aberto (o clássico "decompression bomb"). Lemos só
    // o cabeçalho antes de decidir decodificar.
    public const int PixelsMaximos = 12000;

    public static bool ExtensaoAceita(string? nomeArquivo)
    {
        var extensao = Path.GetExtension(nomeArquivo ?? "").ToLowerInvariant();
        return extensao.Length > 0 && ExtensoesAceitas.Contains(extensao);
    }

    // Quanto a imagem deve encolher pra caber no lado máximo, mantendo a proporção.
    //
    // Nunca AUMENTA: esticar um logo de 80px pra 512px não acrescenta detalhe nenhum, só peso e
    // borrão. Imagem já pequena passa intacta.
    public static (int Largura, int Altura) NovoTamanho(int largura, int altura, int ladoMaximo)
    {
        if (largura <= 0 || altura <= 0 || ladoMaximo <= 0) return (largura, altura);
        if (largura <= ladoMaximo && altura <= ladoMaximo) return (largura, altura);

        var escala = (double)ladoMaximo / Math.Max(largura, altura);

        // Arredonda pra cima pra nunca devolver 0 numa imagem muito estreita (ex.: 4000x3).
        return (Math.Max(1, (int)Math.Ceiling(largura * escala)),
                Math.Max(1, (int)Math.Ceiling(altura * escala)));
    }

    // Nome do arquivo gravado no disco.
    //
    // O nome que o navegador manda NÃO é aproveitado. Antes ele era concatenado no caminho
    // ("guid_" + arquivo.FileName), e um nome com "../" escaparia da pasta de uploads.
    public static string NomeDeArquivo() => Guid.NewGuid().ToString("N") + ExtensaoDeSaida;

    // Processa e grava.
    //
    // Nunca lança: a imagem é sempre opcional e derrubar um cadastro inteiro por causa dela custa
    // caro — a pessoa preenche um formulário longo, escolhe a foto e perde tudo. Mas "não derrubar"
    // não pode virar "não contar": o cadastro segue, e a pessoa é avisada de que a foto ficou pra trás.
    //
    // Devolve um RESULTADO, e não só o caminho, porque "não mandou foto" e "a foto não pôde ser
    // salva" são coisas diferentes que antes viravam o mesmo `null`. O chamador não tinha como
    // distinguir, então tratava tudo como ausência — e o cadastro seguia sem a imagem, calado.
    //
    // Isso escondeu um problema real por um dia inteiro: a pasta de logos pertencia a outro
    // usuário do sistema, nenhum upload gravava, e ninguém via erro nenhum. O sintoma só apareceu
    // quando um botão de manutenção não fez efeito.
    public static async Task<ResultadoDaImagem> SalvarAsync(
        IFormFile? arquivo,
        string webRootPath,
        string subpasta,
        FormatoDeImagem formato,
        ILogger? logger = null)
    {
        if (arquivo == null || arquivo.Length == 0) return ResultadoDaImagem.SemArquivo;

        if (arquivo.Length > BytesMaximos)
            return ResultadoDaImagem.Falhou(
                $"A imagem tem {arquivo.Length / 1024 / 1024} MB e o limite é {BytesMaximos / 1024 / 1024} MB. "
                + "Tente uma foto menor.");

        if (!ExtensaoAceita(arquivo.FileName))
            return ResultadoDaImagem.Falhou(
                "Esse arquivo não é uma imagem que a gente aceita. Use JPG, PNG ou WEBP.");

        try
        {
            byte[] bytes;
            await using (var entrada = arquivo.OpenReadStream())
            using (var buffer = new MemoryStream())
            {
                await entrada.CopyToAsync(buffer);
                bytes = buffer.ToArray();
            }

            var recodificada = Recodificar(bytes, formato, logger);
            if (recodificada == null)
                return ResultadoDaImagem.Falhou(
                    "Não conseguimos ler essa imagem. Ela pode estar corrompida ou não ser uma imagem de verdade.");

            var pasta = Path.Combine(webRootPath, "uploads", subpasta);
            Directory.CreateDirectory(pasta);
            var nomeArquivo = NomeDeArquivo();

            await File.WriteAllBytesAsync(Path.Combine(pasta, nomeArquivo), recodificada);

            return ResultadoDaImagem.Ok("/uploads/" + subpasta + "/" + nomeArquivo);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Falha ao processar imagem enviada pra {Subpasta}.", subpasta);

            // A pessoa não precisa saber que foi permissão de pasta ou disco cheio, mas precisa
            // saber que NÃO salvou — senão vai embora achando que a foto está lá.
            return ResultadoDaImagem.Falhou("Não deu pra salvar a imagem agora. Tente de novo em instantes.");
        }
    }

    // Já está no formato e no tamanho que a gente quer?
    //
    // Existe porque a conta ingênua ("ficou menor? então troca") degrada a imagem: WebP com
    // perdas recodificado em WebP com perdas encolhe MAIS a cada passada, sempre perdendo
    // qualidade. Rodar a otimização três vezes borraria a foto de todo mundo. A pergunta certa
    // não é "ficou menor", é "já está no padrão".
    public static bool JaEstaNoPadrao(byte[] bytes, FormatoDeImagem formato)
    {
        try
        {
            using var dados = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(dados);
            if (codec == null) return false;

            return codec.EncodedFormat == SKEncodedImageFormat.Webp
                && codec.Info.Width <= formato.LadoMaximo
                && codec.Info.Height <= formato.LadoMaximo;
        }
        catch
        {
            return false;
        }
    }

    // Decodifica, encolhe e recodifica em WebP. Devolve null quando o conteúdo não é imagem de
    // verdade ou é grande demais pra ser aberto com segurança.
    //
    // Separado do SalvarAsync porque o mesmo trabalho serve pras imagens que JÁ estão no disco:
    // é assim que a capa de 8 MB que já estava em produção foi consertada.
    public static byte[]? Recodificar(byte[] entrada, FormatoDeImagem formato, ILogger? logger = null)
    {
        using var dados = SKData.CreateCopy(entrada);

        // Só o cabeçalho, antes de alocar memória pro conteúdo inteiro.
        using (var codec = SKCodec.Create(dados))
        {
            if (codec == null)
            {
                // Extensão de imagem mas conteúdo que não é imagem — arquivo renomeado.
                logger?.LogWarning("O conteúdo enviado não é uma imagem válida.");
                return null;
            }

            if (codec.Info.Width > PixelsMaximos || codec.Info.Height > PixelsMaximos)
            {
                logger?.LogWarning("Imagem recusada por dimensão ({Largura}x{Altura}).",
                    codec.Info.Width, codec.Info.Height);
                return null;
            }
        }

        using var original = SKBitmap.Decode(dados);
        if (original == null) return null;

        var (largura, altura) = NovoTamanho(original.Width, original.Height, formato.LadoMaximo);

        var redimensionada = (largura == original.Width && altura == original.Height)
            ? null
            : original.Resize(new SKImageInfo(largura, altura), Reamostragem);

        try
        {
            using var imagem = SKImage.FromBitmap(redimensionada ?? original);
            using var codificada = imagem.Encode(SKEncodedImageFormat.Webp, formato.Qualidade);
            return codificada?.ToArray();
        }
        finally
        {
            redimensionada?.Dispose();
        }
    }

    // Mitchell é o filtro cúbico que o Skia recomenda pra diminuir foto: não serrilha a borda
    // nem deixa o halo que o Lanczos deixa em logo com contorno duro.
    private static readonly SKSamplingOptions Reamostragem = new(SKCubicResampler.Mitchell);
}

// Cada lugar onde a imagem aparece tem uma necessidade diferente de tamanho.
public sealed record FormatoDeImagem(int LadoMaximo, int Qualidade)
{
    // Aparece em miniatura na maior parte do site e grande só no próprio perfil.
    public static readonly FormatoDeImagem FotoPerfil = new(512, 80);

    // Escudo de time: quase sempre PNG com fundo transparente. O WebP preserva a
    // transparência sozinho; a qualidade alta é porque contorno duro e texto pequeno são
    // justamente o que aparece primeiro quando se comprime demais.
    public static readonly FormatoDeImagem LogoTime = new(512, 95);

    // Banner no topo da página do torneio — o único que ocupa a largura toda da tela.
    public static readonly FormatoDeImagem CapaTorneio = new(1600, 82);
}

// O que aconteceu com a imagem que a pessoa mandou.
//
// Existe pra separar dois casos que antes eram o mesmo `null`: não veio arquivo nenhum (normal,
// o campo é opcional) e veio mas não deu pra salvar (precisa avisar). Tratar os dois igual faz o
// sistema mentir — a pessoa escolhe a foto, salva, e o cadastro segue sem ela sem dizer nada.
public sealed record ResultadoDaImagem(string? Caminho, string? Erro)
{
    public static readonly ResultadoDaImagem SemArquivo = new(null, null);

    public static ResultadoDaImagem Ok(string caminho) => new(caminho, null);
    public static ResultadoDaImagem Falhou(string motivo) => new(null, motivo);

    public bool Salvou => Caminho != null;
    public bool DeuErro => Erro != null;
}
