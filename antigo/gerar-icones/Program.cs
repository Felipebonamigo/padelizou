using SkiaSharp;

// Gera todo o conjunto de icones a partir da arte nova (raquetes verdes sobre fundo escuro,
// com a palavra "Padelizou" embaixo). A palavra NAO entra em nada: as raquetes sao recortadas
// por mascara de cor e recompostas sobre um disco/placa escura sintetizada.

var origem = args.Length > 0 ? args[0] : @"C:\Users\Felip\source\repos\Padelizou\antigo\logo-novo2.jpeg";
var destino = args.Length > 1 ? args[1] : @"C:\Users\Felip\source\repos\Padelizou\Padelizou\wwwroot";
// 3o argumento (opcional): pasta pra gravar TAMBEM o logo em alta, PNG sem perda, pra uso fora
// do site (camiseta, banner, Instagram, apresentacao). Nao e servido, nao entra no deploy.
var alta = args.Length > 2 ? args[2] : null;

using var arte = SKBitmap.Decode(origem);
int L = arte.Width, A = arte.Height;
Console.WriteLine($"arte: {L}x{A}");

// O verde da arte e amarelado (#b5d33a): G - max(R,B) da so ~30, e uma rampa em cima disso
// deixaria o MIOLO da raquete meio transparente. G - media(R,B) separa de verdade — verde ~90,
// fundo escuro ~-4, furos pretos ~0, branco 0.
static int Verdor(SKColor c) => c.Green - (c.Red + c.Blue) / 2;

// A palavra fica abaixo da faixa vazia; so a metade de cima interessa.
const double FimDasRaquetes = 0.77;
int limiteY = (int)(A * FimDasRaquetes);

// --- fundo: mediana do que NAO e verde, no topo e na base da faixa das raquetes ---
static SKColor MedianaDeFundo(SKBitmap b, int y0, int y1, int x0, int x1)
{
    var r = new List<byte>(); var g = new List<byte>(); var bl = new List<byte>();
    for (int y = y0; y < y1; y++)
        for (int x = x0; x < x1; x++)
        {
            var c = b.GetPixel(x, y);
            if (Verdor(c) > 5 || (c.Red > 200 && c.Green > 200 && c.Blue > 200)) continue;
            r.Add(c.Red); g.Add(c.Green); bl.Add(c.Blue);
        }
    r.Sort(); g.Sort(); bl.Sort();
    return new SKColor(r[r.Count / 2], g[g.Count / 2], bl[bl.Count / 2]);
}

var fundoTopo = MedianaDeFundo(arte, (int)(A * 0.10), (int)(A * 0.18), (int)(L * 0.25), (int)(L * 0.75));
var fundoBase = MedianaDeFundo(arte, (int)(A * 0.70), (int)(A * 0.76), (int)(L * 0.25), (int)(L * 0.75));
var fundoMeio = MedianaDeFundo(arte, (int)(A * 0.40), (int)(A * 0.46), (int)(L * 0.02), (int)(L * 0.11));
Console.WriteLine($"fundo topo={fundoTopo} meio={fundoMeio} base={fundoBase}");

// --- recorte das raquetes: alfa pela rampa de verdor, cor limpa do fundo (des-mistura) ---
const int RampaIni = 15, RampaFim = 55;
using var sprite = new SKBitmap(L, limiteY, SKColorType.Rgba8888, SKAlphaType.Unpremul);
int minX = L, maxX = -1, minY = limiteY, maxY = -1;
for (int y = 0; y < limiteY; y++)
    for (int x = 0; x < L; x++)
    {
        var c = arte.GetPixel(x, y);
        double a = (Verdor(c) - RampaIni) / (double)(RampaFim - RampaIni);
        a = Math.Clamp(a, 0, 1);
        if (a <= 0) { sprite.SetPixel(x, y, SKColors.Transparent); continue; }

        // A cor observada vai como esta, SEM des-misturar do fundo. Dividir por alfa na borda
        // clareia o contorno e desenha um halo — e todo lugar que usa a versao transparente tem
        // fundo escuro (barra, rodape, capa, disco), onde franja escura some e halo claro apareceria.
        sprite.SetPixel(x, y, new SKColor(c.Red, c.Green, c.Blue, (byte)(a * 255)));

        if (a > 0.35)
        {
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
    }
Console.WriteLine($"raquetes: x {minX}..{maxX} ({maxX - minX + 1})  y {minY}..{maxY} ({maxY - minY + 1})");

var caixa = new SKRectI(minX, minY, maxX + 1, maxY + 1);
using var raquetes = new SKBitmap(caixa.Width, caixa.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
sprite.ExtractSubset(raquetes, caixa);
double proporcao = caixa.Width / (double)caixa.Height;
Console.WriteLine($"proporcao das raquetes: {proporcao:0.000}");

using var imgRaquetes = SKImage.FromBitmap(raquetes);
var amostragem = new SKSamplingOptions(SKCubicResampler.Mitchell);

// --- composicao ---
SKBitmap Compor(int lado, double fracaoLargura, bool circulo, bool opaco)
{
    var bmp = new SKBitmap(lado, lado, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);

    using var fundo = new SKPaint { IsAntialias = true };
    fundo.Shader = SKShader.CreateLinearGradient(
        new SKPoint(0, 0), new SKPoint(0, lado),
        new[] { fundoTopo, fundoBase }, null, SKShaderTileMode.Clamp);

    if (circulo && !opaco) canvas.DrawCircle(lado / 2f, lado / 2f, lado / 2f, fundo);
    else canvas.DrawRect(new SKRect(0, 0, lado, lado), fundo);

    float larg = (float)(lado * fracaoLargura);
    float alt = (float)(larg / proporcao);
    var alvo = new SKRect((lado - larg) / 2f, (lado - alt) / 2f, (lado + larg) / 2f, (lado + alt) / 2f);
    canvas.DrawImage(imgRaquetes, alvo, amostragem);
    canvas.Flush();
    return bmp;
}

void Gravar(SKBitmap bmp, string caminho, SKEncodedImageFormat formato, int qualidade)
{
    using var img = SKImage.FromBitmap(bmp);
    using var dados = img.Encode(formato, qualidade);
    Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
    using var arquivo = File.Create(caminho);
    dados.SaveTo(arquivo);
    Console.WriteLine($"  {Path.GetFileName(caminho),-24} {bmp.Width}x{bmp.Height}  {dados.Size / 1024.0:0.0} KB");
}

string Img(string nome) => Path.Combine(destino, "image", nome);

// 1) Raquetes soltas (barra, rodape, capa de torneio) - so o desenho, fundo transparente.
{
    int larg = 400, alt = (int)Math.Round(larg / proporcao);
    using var bmp = new SKBitmap(larg, alt, SKColorType.Rgba8888, SKAlphaType.Premul);
    using (var canvas = new SKCanvas(bmp))
    {
        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(imgRaquetes, new SKRect(0, 0, larg, alt), amostragem);
    }
    Gravar(bmp, Img("logo-raquetes.webp"), SKEncodedImageFormat.Webp, 92);
}

// 2) Logo completo em disco (login, portao, relatorio impresso) - fundo claro.
using (var bmp = Compor(256, 0.70, circulo: true, opaco: false))
    Gravar(bmp, Img("logo-icon.webp"), SKEncodedImageFormat.Webp, 92);

// 3) Favicon: aproximacao maior, senao a 32px vira mancha.
using (var bmp = Compor(64, 0.86, circulo: true, opaco: false))
    Gravar(bmp, Img("favicon-32.png"), SKEncodedImageFormat.Png, 100);

// 4) iOS pinta preto atras de transparencia: placa cheia.
using (var bmp = Compor(180, 0.72, circulo: false, opaco: true))
    Gravar(bmp, Img("apple-touch-icon.png"), SKEncodedImageFormat.Png, 100);

// 5) PWA
using (var bmp = Compor(192, 0.68, circulo: false, opaco: true))
    Gravar(bmp, Img("icon-192.png"), SKEncodedImageFormat.Png, 100);

// 6) Tambem serve de maskable: o Android recorta num circulo, raquetes nos 60% centrais.
using (var bmp = Compor(512, 0.60, circulo: false, opaco: true))
    Gravar(bmp, Img("icon-512.png"), SKEncodedImageFormat.Png, 100);

// 7) favicon.ico da raiz (o navegador pede sozinho): container ICO com PNG dentro.
{
    var tamanhos = new[] { 16, 32, 48 };
    var pngs = new List<byte[]>();
    foreach (var t in tamanhos)
    {
        using var bmp = Compor(t, 0.86, circulo: true, opaco: false);
        using var img = SKImage.FromBitmap(bmp);
        using var dados = img.Encode(SKEncodedImageFormat.Png, 100);
        pngs.Add(dados.ToArray());
    }
    using var ms = new MemoryStream();
    using var w = new BinaryWriter(ms);
    w.Write((short)0); w.Write((short)1); w.Write((short)tamanhos.Length);
    int deslocamento = 6 + 16 * tamanhos.Length;
    for (int i = 0; i < tamanhos.Length; i++)
    {
        w.Write((byte)(tamanhos[i] == 256 ? 0 : tamanhos[i]));
        w.Write((byte)(tamanhos[i] == 256 ? 0 : tamanhos[i]));
        w.Write((byte)0); w.Write((byte)0);
        w.Write((short)1); w.Write((short)32);
        w.Write(pngs[i].Length); w.Write(deslocamento);
        deslocamento += pngs[i].Length;
    }
    foreach (var p in pngs) w.Write(p);
    w.Flush();
    var caminhoIco = Path.Combine(destino, "favicon.ico");
    File.WriteAllBytes(caminhoIco, ms.ToArray());
    Console.WriteLine($"  {"favicon.ico",-24} 16/32/48    {ms.Length / 1024.0:0.0} KB");
}

// --- versoes em alta, so quando pedidas ---
if (alta is not null)
{
    Console.WriteLine("alta (PNG sem perda):");

    // Raquetes soltas no tamanho NATIVO do recorte: e todo o detalhe que a arte tem. Ampliar
    // daqui pra cima nao inventa nitidez nenhuma, so aumenta o arquivo.
    //
    // Aqui a borda e ERODIDA (alfa remapeado 0,45..0,80), o que nao acontece nos arquivos do
    // site: a arte tem um brilho claro no contorno das raquetes, que sobre o azul do site vira
    // um contorno discreto e sobre fundo CLARO vira halo de recorte mal feito. Este arquivo e o
    // unico que pode cair em qualquer fundo, entao a borda encolhe ~1px pra cortar o brilho.
    using (var bmp = new SKBitmap(caixa.Width, caixa.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul))
    {
        for (int y = 0; y < caixa.Height; y++)
            for (int x = 0; x < caixa.Width; x++)
            {
                var c = raquetes.GetPixel(x, y);
                double a = Math.Clamp((c.Alpha / 255.0 - 0.45) / 0.35, 0, 1);
                bmp.SetPixel(x, y, new SKColor(c.Red, c.Green, c.Blue, (byte)(a * 255)));
            }
        Gravar(bmp, Path.Combine(alta, "padelizou-raquetes.png"), SKEncodedImageFormat.Png, 100);
    }

    using (var bmp = Compor(1024, 0.70, circulo: true, opaco: false))
        Gravar(bmp, Path.Combine(alta, "padelizou-logo-redondo.png"), SKEncodedImageFormat.Png, 100);

    using (var bmp = Compor(1024, 0.62, circulo: false, opaco: true))
        Gravar(bmp, Path.Combine(alta, "padelizou-logo-quadrado.png"), SKEncodedImageFormat.Png, 100);
}

Console.WriteLine("pronto.");
