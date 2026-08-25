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

    private const float PrimeiraLinhaY = 600;
    private const float UltimaLinhaY = 1080;

    private const float RestoY = 1128;
    private const float DivisoriaY = 1158;
    private const float TorneioY = 1206;
    private const float LegendaY = 1254;

    // Oito jogos é o que cabe em duas linhas cada dentro da faixa, com corpo ainda legível.
    // Passar disso não é "mostrar mais": é publicar uma lista que ninguém lê.
    public const int MaximoDeJogos = 8;

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

        // Passo e corpo saem da CONTAGEM, não de números escolhidos no olho: com altura fixa,
        // oito jogos empilhariam linha sobre linha e três deixariam metade do card vazia.
        var passo = Math.Min(120f, (UltimaLinhaY - PrimeiraLinhaY) / Math.Max(1, dia.Jogos.Count));
        var corpo = Math.Min(40f, passo * 0.42f);
        var corpoDaFase = Math.Max(20f, corpo * 0.62f);

        var y = PrimeiraLinhaY;
        foreach (var jogo in dia.Jogos)
        {
            CartaoCompartilhavel.TextoCentralizado(
                canvas, $"{CategoriaNaTela.Curto(dia.Categoria)}  ·  {jogo.Fase}".ToUpperInvariant(),
                y, fontes.Media, corpoDaFase,
                CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 16);

            // Quem venceu em lime; quem perdeu em branco apagado. A cor é a única marca de
            // resultado — no card não cabe "venceu por" nem seta nenhuma.
            var venceu = jogo.Dupla1Venceu;
            CartaoCompartilhavel.TextoCentralizado(
                canvas, $"{jogo.Dupla1}   {jogo.Games1} x {jogo.Games2}   {jogo.Dupla2}",
                y + corpo * 1.15f, fontes.Media, corpo,
                venceu ? CartaoCompartilhavel.LimeClaro : CartaoCompartilhavel.Branco,
                CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 18);

            y += passo;
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
