using SkiaSharp;

namespace Padelizou.Services;

// As ferramentas de desenho dos cards que o jogador posta no story: o fundo da marca, o
// cabeçalho, o rodapé, o texto que se ajusta sozinho e a foto redonda.
//
// Existe separado dos cards em si (`CartaoDeCampeao`, `CartaoDoAno`) porque a identidade é UMA:
// dois arquivos desenhando o mesmo navy com dois códigos diferentes viram dois navys diferentes
// no dia em que um for ajustado — e card de marca que não parece da mesma marca não constrói
// marca nenhuma.
//
// ⚠️ NADA AQUI GRAVA ARQUIVO. O PNG nasce, é entregue na resposta e morre. Guardar em disco
// entraria no `infra/vps/backup.sh`, que faz `tar` COMPLETO de /opt/padelizou-shared todo dia e
// guarda 14 cópias — a mesma conta que manteve a galeria de fotos fora do sistema. Um card de
// ~250 KB por campeão por categoria viraria 3,5 MB de backup por torneio, todo dia, pra sempre,
// por uma imagem que o jogador baixa uma vez.
//
// ⚠️ SEM EMOJI. A Poppins não tem glifo de emoji e o Skia daqui não tem fonte de fallback (ver
// FonteDoCartao) — um 🏆 no meio da frase sairia como espaço em branco. Troféu, raquete e
// medalha aqui são DESENHO, não caractere.
public static class CartaoCompartilhavel
{
    // 1080×1350 é o retrato 4:5. Escolhido por ser o formato que sobrevive nos três lugares em
    // que este card vai parar: é o tamanho nativo do feed do Instagram, cabe inteiro no story
    // (com margem, sem cortar nada) e ainda é aceito como prévia de link no WhatsApp. Story
    // puro (1080×1920) ganharia no story e seria cortado nos outros dois.
    public const int Largura = 1080;
    public const int Altura = 1350;

    // As cores da marca, iguais às de wwwroot/css/site.css. Ficam aqui em SKColor porque o CSS
    // não alcança o Skia — e é por isso que o comentário de lá também aponta pra cá.
    public static readonly SKColor Navy = new(0x1C, 0x27, 0x42);
    public static readonly SKColor NavyEscuro = new(0x14, 0x1D, 0x33);
    public static readonly SKColor Lime = new(0xA3, 0xD8, 0x27);
    public static readonly SKColor LimeClaro = new(0xC9, 0xEE, 0x6B);
    public static readonly SKColor Branco = new(0xFF, 0xFF, 0xFF);
    public static readonly SKColor Apagado = new(0x9A, 0xA7, 0xC4);

    // ── O papel ────────────────────────────────────────────────────────────────────────────

    // O fundo da marca: navy escurecendo pra baixo, com o brilho lime no alto à direita — o
    // mesmo gesto do `radial-gradient` do tema escuro do site.
    public static void Fundo(SKCanvas canvas)
    {
        using (var tinta = new SKPaint { IsAntialias = true })
        {
            tinta.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, Altura),
                new[] { Navy, NavyEscuro },
                new float[] { 0f, 1f },
                SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, Largura, Altura), tinta);
        }

        using (var brilho = new SKPaint { IsAntialias = true })
        {
            brilho.Shader = SKShader.CreateRadialGradient(
                new SKPoint(Largura * 0.95f, -Altura * 0.05f), Largura * 0.85f,
                new[] { Lime.WithAlpha(48), Lime.WithAlpha(0) },
                new float[] { 0f, 1f },
                SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, Largura, Altura), brilho);
        }
    }

    // A faixa lime do topo — a assinatura visual que faz o card ser reconhecido de longe no
    // meio de um feed, antes de qualquer texto ser lido.
    public static void FaixaDoTopo(SKCanvas canvas)
    {
        using var tinta = new SKPaint { Color = Lime, IsAntialias = true };
        canvas.DrawRect(new SKRect(0, 0, Largura, 14), tinta);
    }

    // O rodapé assinado. É a única parte do card que precisa converter quem VÊ em quem ENTRA,
    // então ele carrega o endereço e não o nome: "Padelizou" sozinho não é clicável na cabeça
    // de ninguém.
    public static void Rodape(SKCanvas canvas, FonteDoCartao fontes, string endereco = "padelizou.com.br")
    {
        using var tinta = new SKPaint { Color = Lime, IsAntialias = true };
        using var fonte = new SKFont(fontes.Media, 34);
        canvas.DrawText(endereco, Largura / 2f, Altura - 58, SKTextAlign.Center, fonte, tinta);
    }

    // ── Texto ──────────────────────────────────────────────────────────────────────────────

    // Escreve centralizado, ENCOLHENDO a fonte até caber na largura pedida.
    //
    // ⚠️ É o método mais importante daqui, e ele existe por causa de um nome real: "Anderson
    // Matteus Schwaab & Charls Gustavio Polese" tem 44 caracteres. Num tamanho fixo, o nome de
    // dupla longa vazaria pelas bordas do card — e o card só é conferido com nome curto, porque
    // nome curto é o que a gente digita ao testar. Encolher é feio; vazar é inutilizável.
    //
    // Devolve o tamanho de fonte usado, pra quem empilha linhas saber o quanto desceu.
    public static float TextoCentralizado(
        SKCanvas canvas, string texto, float y, SKTypeface? familia, float tamanho,
        SKColor cor, float larguraMaxima, float tamanhoMinimo = 18f)
        => Texto(canvas, texto, Largura / 2f, y, familia, tamanho, cor, larguraMaxima, tamanhoMinimo);

    // O mesmo, centrado em `x` — pras seções em duas colunas. `y` é a LINHA DE BASE.
    public static float Texto(
        SKCanvas canvas, string texto, float x, float y, SKTypeface? familia, float tamanho,
        SKColor cor, float larguraMaxima, float tamanhoMinimo = 18f)
    {
        var usado = TamanhoQueCabe(texto, familia, tamanho, larguraMaxima, tamanhoMinimo);

        using var tinta = new SKPaint { Color = cor, IsAntialias = true };
        using var fonte = new SKFont(familia, usado);
        canvas.DrawText(texto, x, y, SKTextAlign.Center, fonte, tinta);
        return usado;
    }

    // O maior tamanho de fonte em que o texto ainda cabe na largura. Separado do desenho porque
    // é pura conta — e conta pura é o que dá pra prender em teste sem abrir um canvas.
    public static float TamanhoQueCabe(
        string texto, SKTypeface? familia, float tamanho, float larguraMaxima, float tamanhoMinimo = 18f)
    {
        if (string.IsNullOrEmpty(texto) || larguraMaxima <= 0) return tamanho;

        using var fonte = new SKFont(familia, tamanho);
        var largura = fonte.MeasureText(texto);
        if (largura <= larguraMaxima) return tamanho;

        // Regra de três direta em vez de laço: a largura do texto é linear no tamanho da fonte,
        // então uma conta chega onde vinte iterações chegariam.
        var proporcional = tamanho * (larguraMaxima / largura);
        return Math.Max(tamanhoMinimo, proporcional);
    }

    // Quebra o texto em linhas que caibam na largura, sem partir palavra.
    //
    // ⚠️ EXISTE PORQUE ENCOLHER NÃO RESOLVE TUDO. `TextoCentralizado` diminui a fonte até
    // caber, e isso salva o nome de dupla longo — mas o NOME DO TORNEIO é a maior palavra do
    // cartaz e a que precisa ser lida de longe: "NATA PADEL TOUR — 2ª Etapa Gravataí" numa
    // linha só cairia pra corpo 28 e sumiria dentro da própria arte. Duas linhas grandes leem
    // melhor que uma linha pequena.
    //
    // Pura de propósito (não desenha nada), pra o teste conferir a quebra sem abrir canvas.
    public static List<string> QuebrarEmLinhas(
        string? texto, SKTypeface? familia, float tamanho, float larguraMaxima)
    {
        var linhas = new List<string>();
        var palavras = (texto ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palavras.Length == 0) return linhas;

        using var fonte = new SKFont(familia, tamanho);

        var atual = palavras[0];
        for (var i = 1; i < palavras.Length; i++)
        {
            var tentativa = $"{atual} {palavras[i]}";
            if (fonte.MeasureText(tentativa) <= larguraMaxima)
            {
                atual = tentativa;
            }
            else
            {
                linhas.Add(atual);
                atual = palavras[i];
            }
        }

        linhas.Add(atual);
        return linhas;
    }

    // Escreve em até `maximoDeLinhas`, encolhendo a fonte enquanto sobrar linha. Devolve a
    // LINHA DE BASE da última — quem empilha o próximo bloco continua de onde este parou.
    public static float TextoEmVariasLinhas(
        SKCanvas canvas, string texto, float yPrimeiraLinha, SKTypeface? familia, float tamanho,
        SKColor cor, float larguraMaxima, int maximoDeLinhas, float tamanhoMinimo = 30f,
        float entrelinha = 1.12f)
    {
        var usado = tamanho;
        var linhas = QuebrarEmLinhas(texto, familia, usado, larguraMaxima);

        // 10% por vez: passo grosso demais salta o tamanho bom, fino demais gasta iteração à
        // toa num texto que tem meia dúzia de palavras.
        while (linhas.Count > maximoDeLinhas && usado > tamanhoMinimo)
        {
            usado = Math.Max(tamanhoMinimo, usado * 0.9f);
            linhas = QuebrarEmLinhas(texto, familia, usado, larguraMaxima);
        }

        // Chegou no menor tamanho e ainda sobra linha: o excesso é EMENDADO na última, que o
        // `Texto` encolhe sozinha até caber. Cortar com reticências perderia texto em silêncio,
        // e o que se perde num cartaz é justamente o fim do nome do torneio ("... 2ª Etapa").
        if (linhas.Count > maximoDeLinhas)
        {
            var sobra = string.Join(" ", linhas.Skip(maximoDeLinhas - 1));
            linhas = linhas.Take(maximoDeLinhas - 1).Append(sobra).ToList();
        }

        var y = yPrimeiraLinha;
        foreach (var linha in linhas)
        {
            Texto(canvas, linha, Largura / 2f, y, familia, usado, cor, larguraMaxima, tamanhoMinimo: 18f);
            y += usado * entrelinha;
        }

        return y - usado * entrelinha;
    }

    // A pílula lime com texto escuro dentro — a mesma forma do `badge` do site. Usada pra
    // categoria e pro ano.
    public static void Pilula(
        SKCanvas canvas, string texto, float centroY, FonteDoCartao fontes,
        float tamanhoTexto = 40, SKColor? fundo = null, SKColor? corTexto = null)
    {
        using var fonte = new SKFont(fontes.Media, tamanhoTexto);
        var larguraTexto = fonte.MeasureText(texto);

        float folgaH = 44, alturaPilula = tamanhoTexto + 44;
        var largura = larguraTexto + folgaH * 2;
        var retangulo = new SKRect(
            (Largura - largura) / 2f, centroY - alturaPilula / 2f,
            (Largura + largura) / 2f, centroY + alturaPilula / 2f);

        using (var tinta = new SKPaint { Color = fundo ?? Lime, IsAntialias = true })
            canvas.DrawRoundRect(retangulo, alturaPilula / 2f, alturaPilula / 2f, tinta);

        using (var tinta = new SKPaint { Color = corTexto ?? Navy, IsAntialias = true })
        {
            // O `y` do DrawText é a LINHA DE BASE, não o meio. Sem descontar a métrica a
            // palavra fica encostada no topo da pílula.
            var metricas = fonte.Metrics;
            var linhaDeBase = centroY - (metricas.Ascent + metricas.Descent) / 2f;
            canvas.DrawText(texto, Largura / 2f, linhaDeBase, SKTextAlign.Center, fonte, tinta);
        }
    }

    // ── Imagens ────────────────────────────────────────────────────────────────────────────

    // Lê uma imagem que está no disco a partir do caminho gravado no banco ("/uploads/...").
    //
    // Passa por `ImagemEnviada.CaminhoFisico` de propósito: é ele que garante que o caminho
    // continua dentro de wwwroot/uploads. Uma linha adulterada no banco não vai fazer o card
    // desenhar (e publicar!) um arquivo de fora dali.
    public static SKBitmap? Ler(string webRootPath, string? caminhoRelativo)
    {
        try
        {
            var fisico = ImagemEnviada.CaminhoFisico(webRootPath, caminhoRelativo);
            if (fisico == null || !File.Exists(fisico)) return null;
            return SKBitmap.Decode(fisico);
        }
        catch
        {
            // Foto quebrada não pode derrubar o card inteiro: sem ela desenha-se o círculo com
            // as iniciais, que é melhor card do que nenhum card.
            return null;
        }
    }

    // Lê um arquivo NOSSO de wwwroot (a logo das raquetes), que o `Ler` acima recusa de
    // propósito — ele só deixa sair de /uploads.
    //
    // ⚠️ A separação é a trava. `Ler` recebe caminho VINDO DO BANCO, que um dia pode ter sido
    // adulterado, e por isso passa pela conferência de pasta; aqui o caminho é uma constante
    // escrita no código, e nenhuma entrada de usuário chega a este parâmetro. Fundir os dois
    // métodos "pra simplificar" abriria o de dados ao disco inteiro.
    public static SKBitmap? LerDaMarca(string webRootPath, string caminhoEmWwwRoot)
    {
        try
        {
            var fisico = Path.Combine(webRootPath,
                caminhoEmWwwRoot.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fisico) ? SKBitmap.Decode(fisico) : null;
        }
        catch
        {
            return null;
        }
    }

    // A foto redonda com anel lime. Sem foto, desenha o círculo com as INICIAIS — que é o que o
    // site já faz na lista de jogadores, e o que evita um buraco cinza no meio da arte.
    public static void FotoRedonda(
        SKCanvas canvas, SKBitmap? foto, float centroX, float centroY, float raio,
        FonteDoCartao fontes, string nomeParaIniciais)
    {
        var circulo = new SKRect(centroX - raio, centroY - raio, centroX + raio, centroY + raio);

        if (foto != null)
        {
            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(circulo, raio, raio), SKClipOperation.Intersect,
                antialias: true);

            // Recorta o QUADRADO CENTRAL da foto antes de encaixar no círculo. Sem isso a foto
            // retrato entra achatada — e achatar rosto é o tipo de defeito que a pessoa vê na
            // hora e não sabe nomear.
            var lado = Math.Min(foto.Width, foto.Height);
            var origem = new SKRect(
                (foto.Width - lado) / 2f, (foto.Height - lado) / 2f,
                (foto.Width + lado) / 2f, (foto.Height + lado) / 2f);

            canvas.DrawBitmap(foto, origem, circulo, Reamostragem);
            canvas.Restore();
        }
        else
        {
            using (var tinta = new SKPaint { Color = new SKColor(0x33, 0x45, 0x6E), IsAntialias = true })
                canvas.DrawCircle(centroX, centroY, raio, tinta);

            var iniciais = Iniciais(nomeParaIniciais);
            using var fonte = new SKFont(fontes.Forte, raio * 0.8f);
            using var tintaTexto = new SKPaint { Color = LimeClaro, IsAntialias = true };
            var metricas = fonte.Metrics;
            canvas.DrawText(iniciais, centroX, centroY - (metricas.Ascent + metricas.Descent) / 2f,
                SKTextAlign.Center, fonte, tintaTexto);
        }

        using var anel = new SKPaint
        {
            Color = Lime,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = raio * 0.09f,
        };
        canvas.DrawCircle(centroX, centroY, raio, anel);
    }

    // Primeira letra do primeiro e do último nome. Mesma ideia do avatar de iniciais do site.
    //
    // ⚠️ Só palavra que COMEÇA COM LETRA conta: o nome que chega aqui costuma ser o
    // `ComoChamar`, e "Diego Martins (Diguinho)" fazia a inicial do "último nome" ser o
    // PARÊNTESE — "D(" dentro do círculo, visto olhando a arte, com os testes verdes.
    public static string Iniciais(string? nome)
    {
        var partes = (nome ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetter(p[0]))
            .ToArray();
        if (partes.Length == 0) return "?";
        if (partes.Length == 1) return partes[0][..1].ToUpperInvariant();
        return (partes[0][..1] + partes[^1][..1]).ToUpperInvariant();
    }

    // As raquetes da marca, desenhadas a partir do arquivo que o site já usa. Nulo = sem logo,
    // e o card segue sem ela (nunca é ela que decide se o card existe).
    public static void Logo(SKCanvas canvas, SKBitmap? logo, float centroX, float centroY, float alturaAlvo)
    {
        if (logo == null || logo.Height == 0) return;

        var escala = alturaAlvo / logo.Height;
        var largura = logo.Width * escala;
        var destino = new SKRect(
            centroX - largura / 2f, centroY - alturaAlvo / 2f,
            centroX + largura / 2f, centroY + alturaAlvo / 2f);

        canvas.DrawBitmap(logo, destino, Reamostragem);
    }

    // O QR que leva ao torneio, sobre a plaquinha branca que o destaca do fundo navy.
    // Devolve o lado realmente desenhado — que quase nunca é o pedido, e a razão importa.
    //
    // ⚠️ ESCALA INTEIRA E SEM SUAVIZAR, as duas coisas. Um QR é uma grade de quadradinhos, e
    // ampliá-lo com o filtro bonito (o Mitchell que as fotos usam) borra a borda de cada módulo
    // — o leitor do celular passa a ler cinza onde deveria haver preto ou branco, e falha em
    // luz ruim, que é a luz do clube. Por isso a escala é um INTEIRO calculado a partir do
    // número de módulos: assim cada módulo recebe exatamente os mesmos N pixels. Escala
    // fracionária (300px em cima de 41 módulos = 7,3) daria módulos de 7 e de 8 pixels
    // alternados — legível na tela grande e falho no celular, que é o pior defeito possível,
    // porque passa no teste de quem olha e falha em quem usa.
    public static float Qr(SKCanvas canvas, string conteudo, float centroX, float centroY, float ladoAlvo)
    {
        // ECC M é o mesmo nível do QR do Pix: aguenta tela riscada e dedo em cima sem inflar o
        // código a ponto de os módulos ficarem miúdos demais pra câmera.
        using var gerador = new QRCoder.QRCodeGenerator();
        using var codigo = gerador.CreateQrCode(conteudo, QRCoder.QRCodeGenerator.ECCLevel.M);

        // 1 pixel por módulo: o desenho sai daqui do tamanho da GRADE (uns 41×41), e quem
        // amplia é o canvas, em múltiplo exato. Pedir o tamanho final ao QRCoder devolveria um
        // número que raramente casa com o espaço da arte.
        var png = new QRCoder.PngByteQRCode(codigo).GetGraphic(pixelsPerModule: 1);
        using var bitmap = SKBitmap.Decode(png);
        if (bitmap == null || bitmap.Width == 0) return 0;

        var escala = Math.Max(1, (int)(ladoAlvo / bitmap.Width));
        var lado = bitmap.Width * escala;

        // A plaquinha branca por baixo. O PNG do QR já traz a margem clara que a norma exige
        // (a "zona de silêncio"), mas ela some visualmente contra o navy; a plaquinha devolve
        // o contorno e ainda faz o QR parecer parte da arte em vez de um adesivo colado.
        var folga = Math.Max(16f, lado * 0.06f);
        var moldura = new SKRect(
            centroX - lado / 2f - folga, centroY - lado / 2f - folga,
            centroX + lado / 2f + folga, centroY + lado / 2f + folga);

        using (var tinta = new SKPaint { Color = Branco, IsAntialias = true })
            canvas.DrawRoundRect(moldura, 24, 24, tinta);

        var destino = new SKRect(
            centroX - lado / 2f, centroY - lado / 2f,
            centroX + lado / 2f, centroY + lado / 2f);

        canvas.DrawBitmap(bitmap, destino, SemSuavizar);
        return lado + folga * 2;
    }

    // O mesmo filtro cúbico que o `ImagemEnviada` usa pra encolher foto: aqui toda imagem que
    // entra no card está sendo REDUZIDA (foto de 512px num círculo de 150), que é justamente o
    // caso em que o filtro padrão serrilha.
    private static readonly SKSamplingOptions Reamostragem = new(SKCubicResampler.Mitchell);

    // O oposto, e só pro QR: vizinho mais próximo, sem mipmap. Ver a nota do `Qr`.
    private static readonly SKSamplingOptions SemSuavizar = new(SKFilterMode.Nearest, SKMipmapMode.None);

    // ── Saída ──────────────────────────────────────────────────────────────────────────────

    // Fecha o desenho em PNG.
    //
    // PNG e não WebP (que é o formato de tudo que o `ImagemEnviada` grava): aqui o arquivo não
    // fica guardado, então peso importa menos que ABRIR EM TUDO — e o destino dele é a galeria
    // do celular e o compartilhamento do WhatsApp, onde o WebP ainda tropeça em aparelho velho.
    public static byte[] EmPng(Action<SKCanvas> desenhar)
    {
        using var bitmap = new SKBitmap(Largura, Altura);
        using (var canvas = new SKCanvas(bitmap))
        {
            desenhar(canvas);
            canvas.Flush();
        }

        using var imagem = SKImage.FromBitmap(bitmap);
        using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);
        return dados.ToArray();
    }
}
