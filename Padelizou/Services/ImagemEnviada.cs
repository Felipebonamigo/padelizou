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
//   5. ENDIREITA a foto antes de recodificar — e isto existe POR CAUSA do item 3. Ver Endireitar.
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
                && codec.Info.Height <= formato.LadoMaximo
                // Arquivo que ainda pede pra ser girado na hora de mostrar NÃO está no padrão:
                // é a foto antiga, de antes deste serviço existir, que continua deitada.
                && codec.EncodedOrigin == SKEncodedOrigin.TopLeft;
        }
        catch
        {
            return false;
        }
    }

    // Decodifica, endireita, encolhe e recodifica em WebP. Devolve null quando o conteúdo não é
    // imagem de verdade ou é grande demais pra ser aberto com segurança.
    //
    // Separado do SalvarAsync porque o mesmo trabalho serve pras imagens que JÁ estão no disco:
    // é assim que a capa de 8 MB que já estava em produção foi consertada.
    //
    // `quartosDeVolta` é o giro PEDIDO À MÃO, somado por cima do que o arquivo já pedia: 1 é um
    // quarto de volta pra direita. Serve pra foto que veio torta sem o EXIF dizer nada — foto
    // tirada com o celular deitado pro lado errado, ou print de tela girado.
    public static byte[]? Recodificar(byte[] entrada, FormatoDeImagem formato, ILogger? logger = null,
        int quartosDeVolta = 0)
    {
        using var dados = SKData.CreateCopy(entrada);

        SKEncodedOrigin orientacaoDoArquivo;

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

            orientacaoDoArquivo = codec.EncodedOrigin;
        }

        using var decodificada = SKBitmap.Decode(dados);
        if (decodificada == null) return null;

        // Endireita ANTES de medir: numa foto de celular em pé, largura e altura vêm trocadas no
        // arquivo, e encolher pelo lado errado deixaria a foto fora do teto depois de girada.
        using var endireitada = Endireitar(decodificada, orientacaoDoArquivo, quartosDeVolta);
        var original = endireitada ?? decodificada;

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

    // Gira os PIXELS pra foto ficar de pé. Devolve null quando não havia nada a fazer — o caso
    // comum, que não merece uma cópia inteira da imagem na memória.
    //
    // Por que é obrigatório: o sensor do celular grava SEMPRE deitado e anota num campo do EXIF
    // quanto o visualizador deve girar na hora de mostrar. Só que o item 3 lá em cima joga o EXIF
    // fora de propósito (é lá que mora a coordenada de GPS) — então, se ninguém girar os pixels
    // aqui, a instrução some e a foto fica deitada PARA SEMPRE. Foi exatamente isso que aconteceu
    // com a foto de perfil de um jogador real: subiu em pé no celular dele e apareceu de lado no
    // site, sem nada no arquivo salvo que denunciasse o motivo.
    //
    // `quartosDeVolta` entra por cima: é o giro que a pessoa pede à mão, quando nem o EXIF sabia.
    public static SKBitmap? Endireitar(SKBitmap original, SKEncodedOrigin orientacao, int quartosDeVolta = 0)
    {
        var giro = Combinar(orientacao, quartosDeVolta);
        if (giro == SKEncodedOrigin.TopLeft) return null;

        // Um quarto de volta troca largura por altura; meia volta e espelho, não.
        var deitou = giro is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                          or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        var largura = deitou ? original.Height : original.Width;
        var altura = deitou ? original.Width : original.Height;

        // Alfa não-multiplicado não aceita canvas no Skia — o destino sairia em branco.
        var alfa = original.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul;
        var destino = new SKBitmap(new SKImageInfo(largura, altura, original.ColorType, alfa));

        using (var canvas = new SKCanvas(destino))
        {
            canvas.SetMatrix(MatrizDe(giro, largura, altura));

            // Vizinho mais próximo de propósito: girar um quarto de volta só troca pixel de
            // lugar, nenhum precisa ser inventado. Filtrar aqui só borraria a imagem de graça —
            // o filtro bom entra depois, na hora de ENCOLHER.
            canvas.DrawBitmap(original, 0, 0, SemFiltro);
        }

        return destino;
    }

    // Soma o giro pedido à mão à orientação que veio no arquivo.
    //
    // Só os quatro giros puros entram nessa conta. Orientação espelhada (o celular virado ao
    // contrário na selfie) é rara e combinar espelho com giro daria uma tabela de 32 casos pra
    // ganhar quase nada: nesse caso o giro manual manda sozinho, porque é o que a pessoa está
    // vendo na tela na hora que aperta o botão.
    private static SKEncodedOrigin Combinar(SKEncodedOrigin orientacao, int quartosDeVolta)
    {
        var quartos = (((quartosDeVolta % 4) + 4) % 4);
        if (quartos == 0) return orientacao;

        var doArquivo = orientacao switch
        {
            SKEncodedOrigin.RightTop => 1,
            SKEncodedOrigin.BottomRight => 2,
            SKEncodedOrigin.LeftBottom => 3,
            SKEncodedOrigin.TopLeft => 0,
            _ => -1,   // espelhada: fica de fora da soma
        };

        if (doArquivo < 0) return QuartosEmOrigem(quartos);

        return QuartosEmOrigem((doArquivo + quartos) % 4);
    }

    // Giro em quartos de volta pra direita, escrito como orientação — assim existe UM só lugar
    // que sabe girar imagem, e o giro manual reaproveita a mesma conta do EXIF.
    private static SKEncodedOrigin QuartosEmOrigem(int quartos) => quartos switch
    {
        1 => SKEncodedOrigin.RightTop,
        2 => SKEncodedOrigin.BottomRight,
        3 => SKEncodedOrigin.LeftBottom,
        _ => SKEncodedOrigin.TopLeft,
    };

    // A matriz que leva cada pixel de onde ele foi gravado pra onde ele deve aparecer.
    // `largura` e `altura` são as do DESTINO (já com os lados trocados, quando for o caso).
    private static SKMatrix MatrizDe(SKEncodedOrigin origem, int largura, int altura) => origem switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, largura, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, largura, 0, -1, altura, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, altura, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, largura, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, largura, -1, 0, altura, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, altura, 0, 0, 1),
        _ => SKMatrix.Identity,
    };

    // Gira uma imagem que JÁ está no disco e grava o resultado como arquivo NOVO.
    //
    // Grava com nome novo — e não por cima — porque essas imagens são servidas como arquivo
    // estático e ficam em cache em três camadas: no navegador, no Service Worker do PWA e no
    // Caddy. Reescrever o mesmo caminho deixaria a pessoa vendo a foto torta por dias, achando
    // que o botão não funcionou. Quem chamar atualiza o banco e só depois apaga o arquivo velho.
    public static async Task<ResultadoDaImagem> GirarNoDiscoAsync(
        string? caminhoRelativo,
        string webRootPath,
        FormatoDeImagem formato,
        int quartosDeVolta,
        ILogger? logger = null)
    {
        var fisico = CaminhoFisico(webRootPath, caminhoRelativo);
        if (fisico == null || !File.Exists(fisico))
            return ResultadoDaImagem.Falhou("Não encontramos o arquivo dessa imagem pra girar.");

        try
        {
            var atual = await File.ReadAllBytesAsync(fisico);
            if (atual.Length > BytesMaximos)
                return ResultadoDaImagem.Falhou("Essa imagem é grande demais pra girar aqui.");

            var girada = Recodificar(atual, formato, logger, quartosDeVolta);
            if (girada == null)
                return ResultadoDaImagem.Falhou("Não conseguimos ler essa imagem pra girar.");

            var pasta = Path.GetDirectoryName(fisico)!;
            var nomeArquivo = NomeDeArquivo();
            await File.WriteAllBytesAsync(Path.Combine(pasta, nomeArquivo), girada);

            return ResultadoDaImagem.Ok("/uploads/" + Path.GetFileName(pasta) + "/" + nomeArquivo);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Falha ao girar a imagem {Caminho}.", caminhoRelativo);
            return ResultadoDaImagem.Falhou("Não deu pra girar a imagem agora. Tente de novo em instantes.");
        }
    }

    // O caminho vem do banco no formato "/uploads/pasta/arquivo". Confere que ele continua dentro
    // de wwwroot/uploads antes de qualquer coisa — uma linha adulterada no banco não vai fazer o
    // sistema ler nem apagar arquivo de fora dali.
    public static string? CaminhoFisico(string webRootPath, string? caminhoRelativo)
    {
        if (string.IsNullOrWhiteSpace(caminhoRelativo)) return null;

        var raizUploads = Path.GetFullPath(Path.Combine(webRootPath, "uploads"));
        var completo = Path.GetFullPath(Path.Combine(webRootPath,
            caminhoRelativo.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)));

        return completo.StartsWith(raizUploads + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? completo
            : null;
    }

    // Mitchell é o filtro cúbico que o Skia recomenda pra diminuir foto: não serrilha a borda
    // nem deixa o halo que o Lanczos deixa em logo com contorno duro.
    private static readonly SKSamplingOptions Reamostragem = new(SKCubicResampler.Mitchell);

    // Girar não reamostra nada — ver Endireitar.
    private static readonly SKSamplingOptions SemFiltro = new(SKFilterMode.Nearest);
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
