using SkiaSharp;

namespace Padelizou.Services;

// O PNG que a dupla campeã posta.
//
// A leitura tem que funcionar em três segundos e de longe, porque é assim que story se
// consome: primeiro a pessoa reconhece o formato (faixa lime, navy), depois lê CAMPEÕES,
// depois procura o rosto. O nome do torneio vem por último de propósito — quem vê já sabe
// de que torneio se trata; quem não sabe está lendo por causa do amigo, não do evento.
//
// ⚠️ Nada aqui vai pro disco: ver a nota do `CartaoCompartilhavel`.
public static class CartaoDeCampeao
{
    // Desenha e devolve o PNG. `webRootPath` é de onde saem as fotos e a logo.
    public static byte[] Desenhar(
        CampeaoDeCategoria campeao, FonteDoCartao fontes, string webRootPath)
    {
        // Carregadas fora do desenho pra serem descartadas de um jeito só, e porque uma foto
        // que não abre não pode interromper o card no meio.
        var foto1 = CartaoCompartilhavel.Ler(webRootPath, campeao.Jogador1?.FotoPerfil);
        var foto2 = CartaoCompartilhavel.Ler(webRootPath, campeao.Jogador2?.FotoPerfil);
        var escudo = CartaoCompartilhavel.Ler(webRootPath, campeao.LogoDoTime);
        var logo = CartaoCompartilhavel.LerDaMarca(webRootPath, LogoDaMarca);

        try
        {
            return CartaoCompartilhavel.EmPng(canvas =>
            {
                CartaoCompartilhavel.Fundo(canvas);
                CartaoCompartilhavel.FaixaDoTopo(canvas);

                Cabecalho(canvas, fontes, logo);
                Coroa(canvas, fontes, campeao);
                Retratos(canvas, fontes, campeao, foto1, foto2, escudo);
                Nomes(canvas, fontes, campeao);
                Evento(canvas, fontes, campeao);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            foto1?.Dispose();
            foto2?.Dispose();
            escudo?.Dispose();
            logo?.Dispose();
        }
    }

    // As raquetes que o site já usa. Arquivo NOSSO, então entra por `LerDaMarca` — o `Ler`
    // comum só deixa sair de /uploads, que é onde mora o que veio de usuário.
    public const string LogoDaMarca = "/image/logo-raquetes.webp";

    private static void Cabecalho(SKCanvas canvas, FonteDoCartao fontes, SKBitmap? logo)
    {
        CartaoCompartilhavel.Logo(canvas, logo, CartaoCompartilhavel.Largura / 2f, 150, 108);

        // Letras espaçadas à mão: `SKFont` não tem tracking, e a palavra da marca sem respiro
        // fica pesada embaixo de um desenho. Um espaço entre as letras faz o trabalho.
        CartaoCompartilhavel.TextoCentralizado(
            canvas, "P A D E L I Z O U", 248, fontes.Media, 34,
            CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 160);
    }

    private static void Coroa(SKCanvas canvas, FonteDoCartao fontes, CampeaoDeCategoria campeao)
    {
        // O tamanho cai de 128 pra caber "CAMPEÃS"/"CAMPEÕES" sem encostar nas bordas — quem
        // decide é a medida, não um número escolhido no olho.
        CartaoCompartilhavel.TextoCentralizado(
            canvas, campeao.Rotulo, 400, fontes.Forte, 128,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140);

        CartaoCompartilhavel.Pilula(
            canvas, CategoriaNaTela.Curto(campeao.Categoria), 480, fontes, 40);
    }

    private static void Retratos(
        SKCanvas canvas, FonteDoCartao fontes, CampeaoDeCategoria campeao,
        SKBitmap? foto1, SKBitmap? foto2, SKBitmap? escudo)
    {
        const float centroY = 720;
        var meio = CartaoCompartilhavel.Largura / 2f;

        if (campeao.EhTime)
        {
            // Time não tem rosto: quem representa é o escudo. Sem escudo cadastrado sobra o
            // círculo com as iniciais do nome do time, que ainda diz de quem se trata.
            CartaoCompartilhavel.FotoRedonda(
                canvas, escudo, meio, centroY, 165, fontes, campeao.NomeTime ?? "");
            return;
        }

        if (campeao.Pessoas <= 1)
        {
            // Americano: o campeão é uma pessoa só, e o retrato dela ocupa o centro.
            CartaoCompartilhavel.FotoRedonda(
                canvas, foto1, meio, centroY, 165, fontes, campeao.Jogador1?.Nome ?? "");
            return;
        }

        // A dupla: dois círculos que se encostam de leve, como quem posa junto.
        const float raio = 150;
        const float afastamento = 158;
        CartaoCompartilhavel.FotoRedonda(
            canvas, foto1, meio - afastamento, centroY, raio, fontes, campeao.Jogador1?.Nome ?? "");
        CartaoCompartilhavel.FotoRedonda(
            canvas, foto2, meio + afastamento, centroY, raio, fontes, campeao.Jogador2?.Nome ?? "");
    }

    private static void Nomes(SKCanvas canvas, FonteDoCartao fontes, CampeaoDeCategoria campeao)
    {
        // ⚠️ O nome é a linha que mais quebra card: "Anderson Schwaab (Deco) & Charls Polese"
        // passa de 45 caracteres, e num tamanho fixo vazaria pelas bordas. Quem escolhe o
        // tamanho aqui é a régua, dentro de uma margem de 80px de cada lado.
        CartaoCompartilhavel.TextoCentralizado(
            canvas, campeao.Nomes, 1000, fontes.Forte, 62,
            CartaoCompartilhavel.LimeClaro, CartaoCompartilhavel.Largura - 160, tamanhoMinimo: 30);
    }

    private static void Evento(SKCanvas canvas, FonteDoCartao fontes, CampeaoDeCategoria campeao)
    {
        // A linha fina que separa "quem ganhou" de "onde ganhou".
        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(90), IsAntialias = true })
        {
            var meio = CartaoCompartilhavel.Largura / 2f;
            canvas.DrawRect(new SKRect(meio - 90, 1058, meio + 90, 1060), tinta);
        }

        CartaoCompartilhavel.TextoCentralizado(
            canvas, campeao.Torneio.ToUpperInvariant(), 1130, fontes.Media, 44,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140, tamanhoMinimo: 26);

        // Clube e data na mesma linha; qualquer um dos dois pode faltar (torneio sem data
        // marcada, consulta sem clube) e a linha se recompõe sem sobrar separador solto.
        var partes = new[]
        {
            campeao.Clube,
            campeao.Data?.ToString("dd/MM/yyyy"),
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        var legenda = string.Join("  ·  ", partes);
        if (legenda.Length > 0)
        {
            CartaoCompartilhavel.TextoCentralizado(
                canvas, legenda, 1188, fontes.Normal, 34,
                CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 140);
        }
    }
}
