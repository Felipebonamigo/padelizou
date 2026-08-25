using SkiaSharp;

namespace Padelizou.Services;

// O PNG DA CHAVE — a árvore do mata-mata, da primeira coluna até a final.
//
// É o card mais difícil dos seis: árvore é desenho, não texto, e um story em retrato tem
// 1080px de largura pra caber três colunas de nome de dupla mais os conectores. Duas escolhas
// fazem ele funcionar:
//
//   · o nome vem COMPACTO ("Anderson / Charls", não "Anderson Schwaab & Charls Polese") — ver
//     NomeDaDupla.Compacto, que existe por causa deste card;
//   · o alinhamento de árvore sai de UMA conta, e não de posições escritas à mão: o centro do
//     jogo `j` da rodada `r` é `topo + faixa * (j + 0,5) / jogosDaRodada`. Como cada rodada
//     tem metade dos jogos da anterior, o jogo da semifinal cai exatamente no meio dos dois
//     jogos de quartas que o alimentam, sem nenhum caso especial.
//
// ⚠️ SEM EMOJI, como todo card daqui (a Poppins não tem glifo e não há fallback). Quem venceu
// é marcado por COR e peso.
public static class CartaoDaChave
{
    private const float MargemH = 60;
    private const float TituloY = 388;
    private const float PilulaCentroY = 468;

    private const float TopoDaArvore = 592;
    private const float BaseDaArvore = 1120;

    private const float DivisoriaY = 1158;
    private const float TorneioY = 1206;
    private const float LegendaY = 1254;

    private const float GapEntreColunas = 26;

    public static byte[] Desenhar(ChaveDesenhavel chave, FonteDoCartao fontes, string webRootPath)
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
                    canvas, "CHAVE", TituloY, fontes.Forte, 110,
                    CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140);

                CartaoCompartilhavel.Pilula(
                    canvas, CategoriaNaTela.Curto(chave.Categoria), PilulaCentroY, fontes, 40);

                Arvore(canvas, fontes, chave);
                Evento(canvas, fontes, chave);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            logo?.Dispose();
        }
    }

    private static void Arvore(SKCanvas canvas, FonteDoCartao fontes, ChaveDesenhavel chave)
    {
        var rodadas = chave.Rodadas.Where(r => r.Jogos.Count > 0).ToList();
        if (rodadas.Count == 0) return;

        float larguraUtil = CartaoCompartilhavel.Largura - MargemH * 2;
        float larguraColuna = larguraUtil / rodadas.Count;
        float larguraCaixa = larguraColuna - GapEntreColunas;
        float faixa = BaseDaArvore - TopoDaArvore;

        // O corpo sai da coluna MAIS CHEIA: é ela que decide se o texto cabe. Uma final
        // sozinha não pode ganhar corpo de título só porque sobra espaço em volta dela.
        int maisJogos = rodadas.Max(r => r.Jogos.Count);
        float alturaPorJogo = faixa / maisJogos;
        float corpo = Math.Clamp(alturaPorJogo * 0.20f, 16f, 30f);

        float CentroDe(int rodada, int jogo) =>
            TopoDaArvore + faixa * (jogo + 0.5f) / rodadas[rodada].Jogos.Count;

        float CentroXDe(int rodada) => MargemH + larguraColuna * rodada + larguraColuna / 2f;

        // Os conectores primeiro, pra as caixas ficarem por cima das linhas.
        using (var traco = new SKPaint
        {
            Color = CartaoCompartilhavel.Apagado.WithAlpha(110),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
        })
        {
            for (int r = 0; r < rodadas.Count - 1; r++)
            {
                float xDireitaDaCaixa = CentroXDe(r) + larguraCaixa / 2f;
                float xEsquerdaDaProxima = CentroXDe(r + 1) - larguraCaixa / 2f;
                float xMeio = (xDireitaDaCaixa + xEsquerdaDaProxima) / 2f;

                for (int j = 0; j < rodadas[r].Jogos.Count; j++)
                {
                    float y = CentroDe(r, j);
                    canvas.DrawLine(xDireitaDaCaixa, y, xMeio, y, traco);

                    // O irmão de par sobe/desce até o mesmo x; o jogo que ficou sem par (chave
                    // com bye) só ganha o traço horizontal, e é o certo — não há confronto
                    // seguinte pra unir.
                    if (j % 2 == 0 && j + 1 < rodadas[r].Jogos.Count)
                    {
                        float yIrmao = CentroDe(r, j + 1);
                        canvas.DrawLine(xMeio, y, xMeio, yIrmao, traco);

                        int destino = j / 2;
                        if (destino < rodadas[r + 1].Jogos.Count)
                        {
                            float yDestino = CentroDe(r + 1, destino);
                            canvas.DrawLine(xMeio, yDestino, xEsquerdaDaProxima, yDestino, traco);
                        }
                    }
                }
            }
        }

        for (int r = 0; r < rodadas.Count; r++)
        {
            var rodada = rodadas[r];
            float x = CentroXDe(r);

            // O nome da fase em cima da coluna — sem ele a árvore não diz onde começa.
            // ⚠️ 46px acima da árvore, e não 34: com 34 o rótulo da fase ENCOSTAVA na pílula
            // da categoria — defeito visto na arte, não no código.
            CartaoCompartilhavel.Texto(
                canvas, RotuloDaFase(rodada.Fase), x, TopoDaArvore - 46, fontes.Media, 26,
                CartaoCompartilhavel.Lime, larguraCaixa, tamanhoMinimo: 14);

            for (int j = 0; j < rodada.Jogos.Count; j++)
            {
                Confronto(canvas, fontes, rodada.Jogos[j], x, CentroDe(r, j), larguraCaixa, corpo);
            }
        }
    }

    // "Quartas de Final" não cabe numa coluna de 300px em corpo 26 sem encolher até sumir.
    // O rótulo curto é o que a quadra usa de qualquer jeito.
    public static string RotuloDaFase(string fase) => fase switch
    {
        "Quartas de Final" => "QUARTAS",
        "Oitavas de Final" => "OITAVAS",
        ChaveamentoMataMata.PrimeiraRodada => "1ª RODADA",
        _ => fase.ToUpperInvariant(),
    };

    private static void Confronto(
        SKCanvas canvas, FonteDoCartao fontes, JogoDaChave jogo,
        float centroX, float centroY, float largura, float corpo)
    {
        float alturaDaCaixa = corpo * (jogo.Placar == null ? 3.4f : 4.6f);

        using (var fundo = new SKPaint { Color = CartaoCompartilhavel.Branco.WithAlpha(14), IsAntialias = true })
        {
            var caixa = new SKRect(
                centroX - largura / 2f, centroY - alturaDaCaixa / 2f,
                centroX + largura / 2f, centroY + alturaDaCaixa / 2f);
            canvas.DrawRoundRect(caixa, 12, 12, fundo);
        }

        // As duas duplas, uma sobre a outra. Quem venceu em lime e em peso; quem perdeu
        // apagado. Sem placar, os dois ficam brancos: ninguém venceu ainda.
        var corDeCima = jogo.Dupla1Venceu ? CartaoCompartilhavel.LimeClaro
            : jogo.Dupla2Venceu ? CartaoCompartilhavel.Apagado : CartaoCompartilhavel.Branco;
        var corDeBaixo = jogo.Dupla2Venceu ? CartaoCompartilhavel.LimeClaro
            : jogo.Dupla1Venceu ? CartaoCompartilhavel.Apagado : CartaoCompartilhavel.Branco;

        float folga = corpo * 0.15f;

        CartaoCompartilhavel.Texto(
            canvas, jogo.Dupla1, centroX, centroY - folga, jogo.Dupla1Venceu ? fontes.Forte : fontes.Media,
            corpo, corDeCima, largura - 16, tamanhoMinimo: 12);

        CartaoCompartilhavel.Texto(
            canvas, jogo.Dupla2, centroX, centroY + corpo + folga, jogo.Dupla2Venceu ? fontes.Forte : fontes.Media,
            corpo, corDeBaixo, largura - 16, tamanhoMinimo: 12);

        if (jogo.Placar != null)
        {
            CartaoCompartilhavel.Texto(
                canvas, jogo.Placar, centroX, centroY + corpo * 2.2f + folga, fontes.Normal,
                corpo * 0.82f, CartaoCompartilhavel.Apagado, largura - 16, tamanhoMinimo: 11);
        }
    }

    private static void Evento(SKCanvas canvas, FonteDoCartao fontes, ChaveDesenhavel chave)
    {
        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(90), IsAntialias = true })
        {
            var meio = CartaoCompartilhavel.Largura / 2f;
            canvas.DrawRect(new SKRect(meio - 90, DivisoriaY, meio + 90, DivisoriaY + 2), tinta);
        }

        CartaoCompartilhavel.TextoCentralizado(
            canvas, chave.Torneio.ToUpperInvariant(), TorneioY, fontes.Media, 40,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140, tamanhoMinimo: 24);

        var partes = new[] { chave.Clube, chave.Data?.ToString("dd/MM/yyyy") }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        var legenda = string.Join("  ·  ", partes);
        if (legenda.Length > 0)
        {
            CartaoCompartilhavel.TextoCentralizado(
                canvas, legenda, LegendaY, fontes.Normal, 32,
                CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 140);
        }
    }
}
