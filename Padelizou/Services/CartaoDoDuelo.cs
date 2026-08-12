using SkiaSharp;

namespace Padelizou.Services;

// O PNG do confronto direto: "eu × ele, 4 jogos, 3 a 1". A provocação é o motor de
// compartilhamento mais barato que existe — o retrospecto já morava no perfil; o que
// faltava era a peça pra mandar no grupo.
//
// O card sai SEMPRE do ponto de vista de quem pediu: as vitórias DELE à esquerda. Quem
// perde o confronto dificilmente gera a arte — e tudo bem, o outro lado gera a dele.
public sealed class DadosDoCardDoDuelo
{
    public string Nome1 { get; set; } = "";
    public string? Foto1 { get; set; }
    public int Vitorias1 { get; set; }

    public string Nome2 { get; set; } = "";
    public string? Foto2 { get; set; }
    public int Vitorias2 { get; set; }

    public int Jogos { get; set; }
}

public static class CartaoDoDuelo
{
    // A frase da pílula. Pura pra o teste conferir singular/plural sem canvas.
    public static string FraseDosJogos(int jogos) =>
        jogos == 1 ? "1 jogo entre si" : $"{jogos} jogos entre si";

    public static byte[] Desenhar(DadosDoCardDoDuelo dados, FonteDoCartao fontes, string webRootPath)
    {
        var foto1 = CartaoCompartilhavel.Ler(webRootPath, dados.Foto1);
        var foto2 = CartaoCompartilhavel.Ler(webRootPath, dados.Foto2);
        var logo = CartaoCompartilhavel.LerDaMarca(webRootPath, CartaoDeCampeao.LogoDaMarca);

        try
        {
            return CartaoCompartilhavel.EmPng(canvas =>
            {
                CartaoCompartilhavel.Fundo(canvas);
                CartaoCompartilhavel.FaixaDoTopo(canvas);

                CartaoCompartilhavel.Logo(canvas, logo, CartaoCompartilhavel.Largura / 2f, 150, 108);
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, "P A D E L I Z O U", 248, fontes.Media, 34,
                    CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 160);

                CartaoCompartilhavel.TextoCentralizado(
                    canvas, "DUELO", 420, fontes.Forte, 128,
                    CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140);

                CartaoCompartilhavel.Pilula(canvas, FraseDosJogos(dados.Jogos), 500, fontes, 40);

                // Os dois, frente a frente — e o VS pequeno no meio, porque o placar logo
                // abaixo é quem grita.
                var meio = CartaoCompartilhavel.Largura / 2f;
                const float centroY = 750;
                const float afastamento = 300;
                const float raio = 140;

                CartaoCompartilhavel.FotoRedonda(
                    canvas, foto1, meio - afastamento, centroY, raio, fontes, dados.Nome1);
                CartaoCompartilhavel.FotoRedonda(
                    canvas, foto2, meio + afastamento, centroY, raio, fontes, dados.Nome2);
                CartaoCompartilhavel.Texto(
                    canvas, "VS", meio, centroY + 18, fontes.Forte, 52,
                    CartaoCompartilhavel.Lime, 120);

                // Os nomes, cada um centrado embaixo da própria foto.
                CartaoCompartilhavel.Texto(
                    canvas, dados.Nome1, meio - afastamento, 952, fontes.Forte, 40,
                    CartaoCompartilhavel.Branco, 420, tamanhoMinimo: 24);
                CartaoCompartilhavel.Texto(
                    canvas, dados.Nome2, meio + afastamento, 952, fontes.Forte, 40,
                    CartaoCompartilhavel.Branco, 420, tamanhoMinimo: 24);

                // O placar do confronto — o número da provocação.
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, $"{dados.Vitorias1}  ×  {dados.Vitorias2}", 1120, fontes.Forte, 130,
                    CartaoCompartilhavel.LimeClaro, CartaoCompartilhavel.Largura - 200);

                CartaoCompartilhavel.TextoCentralizado(
                    canvas, "vitórias no confronto direto", 1188, fontes.Normal, 32,
                    CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 200);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            foto1?.Dispose();
            foto2?.Dispose();
            logo?.Dispose();
        }
    }
}
