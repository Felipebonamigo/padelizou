using SkiaSharp;

namespace Padelizou.Services;

// O PNG DOS RESULTADOS DO DIA — a arte do fim de tarde no clube.
//
// Cada jogo ocupa DUAS linhas: o contexto pequeno em cima (a fase) e o resultado embaixo. Uma
// linha só com fase, dois nomes de dupla e o placar passaria de setenta caracteres e cairia
// pro corpo mínimo — legível na tela do desenvolvedor, ilegível no story de quem rola rápido.
//
// ⚠️ SEM EMOJI, como todo card daqui: a Poppins não tem glifo de emoji e não há fonte de
// fallback (ver FonteDoCartao). Quem venceu é marcado por COR e peso, não por símbolo.
public static class CartaoDosResultados
{
    private const float MargemH = 140;
    private const float TituloY = 388;
    private const float PilulaCentroY = 468;

    private const float PrimeiraLinhaY = 620;
    private const float UltimaLinhaY = 1080;

    private const float RestoY = 1128;
    private const float DivisoriaY = 1158;
    private const float TorneioY = 1206;
    private const float LegendaY = 1254;

    // Cinco jogos: com TRÊS linhas cada (fase, vencedor, perdedor) é o que cabe na faixa com
    // corpo ainda legível de relance. Passar disso não é "mostrar mais" — é publicar uma lista
    // que ninguém lê. O que sobra é dito em "e mais N jogos".
    public const int MaximoDeJogos = 5;

    // Nulo quando não sobrou nada — a linha inteira some, em vez de sair "e mais 0 jogos".
    public static string? FraseDoResto(int quantos) =>
        quantos <= 0 ? null : quantos == 1 ? "e mais 1 jogo" : $"e mais {quantos} jogos";

    public static byte[] Desenhar(DiaDeResultados dia, FonteDoCartao fontes, string webRootPath)
    {
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
                    canvas, "RESULTADOS", TituloY, fontes.Forte, 96,
                    CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - MargemH);

                // ⚠️ `dd/MM/yyyy` e não o dia por extenso: a fonte do card não tem fallback, e
                // um "sábado" vindo de uma cultura invariante sairia "Saturday" no meio da arte.
                CartaoCompartilhavel.Pilula(
                    canvas, dia.Dia.ToString("dd/MM/yyyy"), PilulaCentroY, fontes, 40);

                // A categoria é a MESMA pra todos os jogos do card (ele é por categoria), então
                // ela sai uma vez aqui em vez de repetir em cada linha — era ruído que roubava
                // espaço da informação que muda.
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, CategoriaNaTela.Curto(dia.Categoria).ToUpperInvariant(), 548,
                    fontes.Media, 34, CartaoCompartilhavel.Apagado,
                    CartaoCompartilhavel.Largura - MargemH);

                Jogos(canvas, fontes, dia);
                Evento(canvas, fontes, dia);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            logo?.Dispose();
        }
    }

    private static void Jogos(SKCanvas canvas, FonteDoCartao fontes, DiaDeResultados dia)
    {
        if (dia.Jogos.Count == 0) return;

        // ⚠️ TRÊS LINHAS POR JOGO, e a forma foi decidida OLHANDO A ARTE: a primeira versão
        // punha o jogo inteiro numa linha só, colorida de uma cor só — e uma linha inteira em
        // lime não diz QUEM venceu, ela diz que os dois venceram. Aqui o vencedor vai EM CIMA,
        // em lime e em peso; o perdedor embaixo, apagado. Sem vencedor carimbado, os dois saem
        // brancos: o card não inventa um resultado que o placar não tem.
        float faixa = UltimaLinhaY - PrimeiraLinhaY;
        float alturaDoJogo = faixa / dia.Jogos.Count;
        float corpo = Math.Clamp(alturaDoJogo * 0.26f, 22f, 38f);
        float corpoDaFase = Math.Max(18f, corpo * 0.62f);

        var y = PrimeiraLinhaY;
        foreach (var jogo in dia.Jogos)
        {
            CartaoCompartilhavel.TextoCentralizado(
                canvas, jogo.Fase.ToUpperInvariant(), y, fontes.Media, corpoDaFase,
                CartaoCompartilhavel.Lime, CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 14);

            bool houveVencedor = jogo.Dupla1Venceu || jogo.Dupla2Venceu;
            bool primeiroEmCima = !jogo.Dupla2Venceu;

            var emCima = primeiroEmCima
                ? (Nome: jogo.Dupla1, Games: jogo.Games1)
                : (Nome: jogo.Dupla2, Games: jogo.Games2);
            var embaixo = primeiroEmCima
                ? (Nome: jogo.Dupla2, Games: jogo.Games2)
                : (Nome: jogo.Dupla1, Games: jogo.Games1);

            CartaoCompartilhavel.TextoCentralizado(
                canvas, $"{emCima.Nome}   {emCima.Games}", y + corpo * 1.15f,
                houveVencedor ? fontes.Forte : fontes.Media, corpo,
                houveVencedor ? CartaoCompartilhavel.LimeClaro : CartaoCompartilhavel.Branco,
                CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 16);

            CartaoCompartilhavel.TextoCentralizado(
                canvas, $"{embaixo.Nome}   {embaixo.Games}", y + corpo * 2.25f,
                fontes.Media, corpo,
                houveVencedor ? CartaoCompartilhavel.Apagado : CartaoCompartilhavel.Branco,
                CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 16);

            y += alturaDoJogo;
        }

        var resto = FraseDoResto(dia.QuantosFicaramDeFora);
        if (resto != null)
        {
            CartaoCompartilhavel.TextoCentralizado(
                canvas, resto, RestoY, fontes.Normal, 30,
                CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - MargemH);
        }
    }

    private static void Evento(SKCanvas canvas, FonteDoCartao fontes, DiaDeResultados dia)
    {
        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(90), IsAntialias = true })
        {
            var meio = CartaoCompartilhavel.Largura / 2f;
            canvas.DrawRect(new SKRect(meio - 90, DivisoriaY, meio + 90, DivisoriaY + 2), tinta);
        }

        CartaoCompartilhavel.TextoCentralizado(
            canvas, dia.Torneio.ToUpperInvariant(), TorneioY, fontes.Media, 40,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 24);

        if (!string.IsNullOrWhiteSpace(dia.Clube))
        {
            CartaoCompartilhavel.TextoCentralizado(
                canvas, dia.Clube, LegendaY, fontes.Normal, 32,
                CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - MargemH);
        }
    }
}
