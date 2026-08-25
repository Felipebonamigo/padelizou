using SkiaSharp;

namespace Padelizou.Services;

// O PNG DA TABELA DE UM GRUPO — a arte do "fim da fase de grupos".
//
// É o card mais informativo dos quatro e o mais difícil de desenhar: tabela não é frase, e
// story não tem rolagem. A saída é a linha ÚNICA por dupla ("2º  Ana & Bia  ·  3V  ·  +8"),
// que sobrevive tanto ao grupo de três quanto ao de oito.
//
// ⚠️ SEM EMOJI e sem símbolo: a Poppins não tem glifo de emoji e o Skia daqui não tem fonte de
// fallback (ver FonteDoCartao). Posição é número, vitória é "V", saldo é sinal.
public static class CartaoDaClassificacao
{
    private const float MargemH = 140;
    private const float TituloY = 392;
    private const float PilulaCentroY = 472;

    // A faixa onde a tabela mora. Fixa: assim o rodapé nunca sobe nem desce conforme o grupo
    // tem três ou oito duplas, e a arte da categoria inteira sai com a mesma cara.
    private const float PrimeiraLinhaY = 620;
    private const float UltimaLinhaY = 1090;

    private const float DivisoriaY = 1132;
    private const float TorneioY = 1186;
    private const float LegendaY = 1240;

    // Grupo de padel tem três a cinco duplas; oito já é torneio grande. O teto existe pro dado
    // torto (importação, acerto na mão) não produzir um card de vinte linhas ilegíveis — corta
    // a lista, e o card continua sendo verdade sobre quem está em cima.
    public const int MaximoDeLinhas = 12;

    // "3V  ·  +8". O saldo vem assinado porque é o critério de desempate que decide quem passa,
    // e "8" sozinho seria lido como games. Zero não leva sinal: "+0" e "-0" são a mesma coisa.
    public static string Campanha(int vitorias, int saldo)
    {
        var saldoEscrito = saldo > 0 ? $"+{saldo}" : saldo.ToString();
        return $"{vitorias}V  ·  {saldoEscrito}";
    }

    public static byte[] Desenhar(GrupoClassificado grupo, FonteDoCartao fontes, string webRootPath)
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
                    canvas, $"GRUPO {grupo.Grupo}".ToUpperInvariant(), TituloY, fontes.Forte, 104,
                    CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - MargemH);

                CartaoCompartilhavel.Pilula(
                    canvas, CategoriaNaTela.Curto(grupo.Categoria), PilulaCentroY, fontes, 40);

                Tabela(canvas, fontes, grupo);
                Evento(canvas, fontes, grupo);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            logo?.Dispose();
        }
    }

    private static void Tabela(SKCanvas canvas, FonteDoCartao fontes, GrupoClassificado grupo)
    {
        var linhas = grupo.Linhas.Take(MaximoDeLinhas).ToList();
        if (linhas.Count == 0) return;

        // ⚠️ O PASSO E O CORPO SAEM DA CONTAGEM, e não de números escolhidos no olho: com
        // altura fixa, um grupo de oito empilharia linha por cima de linha, e um de três
        // deixaria metade do card vazia. O teto de 76 impede que o grupo de três vire três
        // frases gigantes soltas no meio da arte.
        var passo = Math.Min(76f, (UltimaLinhaY - PrimeiraLinhaY) / Math.Max(1, linhas.Count - 1));
        if (linhas.Count == 1) passo = 0;

        var corpo = Math.Min(44f, passo * 0.62f);
        if (linhas.Count == 1) corpo = 44f;

        var y = PrimeiraLinhaY;
        foreach (var linha in linhas)
        {
            // Quem passa em primeiro sai em lime; o resto em branco. Uma cor de destaque só —
            // a segunda faria a terceira posição competir com a primeira.
            var cor = linha.Posicao == 1 ? CartaoCompartilhavel.LimeClaro : CartaoCompartilhavel.Branco;

            CartaoCompartilhavel.TextoCentralizado(
                canvas, $"{linha.Posicao}º   {linha.Dupla}   ·   {Campanha(linha.Vitorias, linha.Saldo)}",
                y, linha.Posicao == 1 ? fontes.Forte : fontes.Media, corpo,
                cor, CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 20);

            y += passo;
        }
    }

    private static void Evento(SKCanvas canvas, FonteDoCartao fontes, GrupoClassificado grupo)
    {
        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(90), IsAntialias = true })
        {
            var meio = CartaoCompartilhavel.Largura / 2f;
            canvas.DrawRect(new SKRect(meio - 90, DivisoriaY, meio + 90, DivisoriaY + 2), tinta);
        }

        CartaoCompartilhavel.TextoCentralizado(
            canvas, grupo.Torneio.ToUpperInvariant(), TorneioY, fontes.Media, 42,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 26);

        var partes = new[] { grupo.Clube, grupo.Data?.ToString("dd/MM/yyyy") }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        var legenda = string.Join("  ·  ", partes);
        if (legenda.Length > 0)
        {
            CartaoCompartilhavel.TextoCentralizado(
                canvas, legenda, LegendaY, fontes.Normal, 32,
                CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - MargemH);
        }
    }
}
