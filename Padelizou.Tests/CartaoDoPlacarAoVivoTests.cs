using Padelizou.Services;
using SkiaSharp;
using Xunit;

namespace Padelizou.Tests;

// O CARD QUE A NOTIFICAÇÃO ABRE (12/09/2026).
//
// 🗣️ Felipe, com dois prints na mão — a bolha de placar do app do Google na tela inicial e o
// placar da Copa na Dynamic Island: *"as notificações estao acontecendo, mas eu queria algo tipo
// esses prints, tem como ?"*. Os dois exigem app NATIVO (a bolha é exclusiva do app do Google; a
// Dynamic Island é Live Activity/ActivityKit) — ver STATUS.md de 16/08. O que a notificação da
// web tem de imagem é a `image` do `showNotification`: puxando a notificação pra baixo no
// Android, ela mostra um PNG. É este.
//
// ⚠️ DEITADO, não retrato: a `image` da notificação é recortada em ~2:1 pelo Android. Um card
// 1080×1350 (o formato dos cards de story daqui) chegaria cortado pelo meio, e o que some no
// corte é justamente a linha de baixo — o placar da segunda dupla.
public class CartaoDoPlacarAoVivoTests
{
    [Fact]
    public void O_card_do_placar_desenha_letra_de_verdade()
    {
        var png = CartaoDoPlacarAoVivo.Desenhar(
            new PlacarParaCard("Juliano / Gabriel", "Paulo / Vitor", 4, 2, Encerrado: false,
                Contexto: "5ª Masculina · Grupo A"),
            new FonteDoCartao(PastaDasFontes()));

        Assert.True(PixelsClaros(png) > 300);
    }

    // O tamanho é o contrato com o Android: quem mexer no desenho não pode mudar a proporção
    // sem saber que está mudando o recorte da notificação.
    [Fact]
    public void O_card_e_deitado_dois_por_um()
    {
        var png = CartaoDoPlacarAoVivo.Desenhar(
            new PlacarParaCard("Ana / Bia", "Carla / Dani", 6, 6, Encerrado: true, Contexto: null),
            new FonteDoCartao(PastaDasFontes()));

        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);
        Assert.Equal(CartaoCompartilhavel.Largura, imagem.Width);
        Assert.Equal(CartaoCompartilhavel.Largura / 2, imagem.Height);
    }

    // Nome de dupla comprido é o caso normal no Americano ("Paulo Prass / Vitor Bittencourt"),
    // e o card não pode vazar pelas bordas nem quebrar.
    [Fact]
    public void Nome_comprido_de_dupla_nao_derruba_o_desenho()
    {
        var png = CartaoDoPlacarAoVivo.Desenhar(
            new PlacarParaCard(
                "Juliano Bender Bonamigo / Gabriel Souza da Silveira",
                "Paulo Prass Pujol Filho / Vitor Bittencourt de Oliveira",
                9, 8, Encerrado: false, Contexto: "5ª Categoria Masculina · Grupo A"),
            new FonteDoCartao(PastaDasFontes()));

        Assert.True(PixelsClaros(png) > 300);
    }

    // Sem a Poppins o card não é desenhado (mesma régua de todo card daqui — ver FonteDoCartao):
    // letra nenhuma num PNG é pior que notificação sem imagem.
    [Fact]
    public void Sem_fonte_nao_desenha()
    {
        var semFonte = new FonteDoCartao(Path.Combine(Path.GetTempPath(), "pasta-que-nao-existe"));

        Assert.False(semFonte.Disponivel);
    }

    private static string PastaDasFontes()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "wwwroot", "fonts");
            if (Directory.Exists(tentativa)) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("wwwroot/fonts não encontrado a partir do bin.");
    }

    private static int PixelsClaros(byte[] png)
    {
        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);

        int claros = 0;
        for (int x = 0; x < imagem.Width; x += 2)
            for (int y = 0; y < imagem.Height; y += 2)
            {
                var c = imagem.GetPixel(x, y);
                if (c.Red > 200 && c.Green > 200 && c.Blue > 200) claros++;
            }
        return claros;
    }
}
