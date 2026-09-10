using SkiaSharp;

namespace Padelizou.Services;

// Uma arte: os jogos de UM dia (ou de uma parte dele, quando o dia não cabe numa arte só).
public sealed record ArteDeJogos(DateTime? Dia, int Parte, int Partes, List<JogoDaLista> Jogos)
{
    // "sex 11/09", "sex 11/09 (1 de 2)", "sem data" — o nome da arte na tela e no arquivo.
    public string Rotulo => Partes > 1
        ? $"{TextoDaLista.Dia(Dia)} ({Parte} de {Partes})"
        : TextoDaLista.Dia(Dia);
}

public sealed record GradeDesenhavel(string Torneio, string? Recorte, string? Clube, List<ArteDeJogos> Artes)
{
    public bool TemOQueMostrar => Artes.Count > 0;
}

// O PNG DA LISTA DE JOGOS — a grade do dia, pro story e pro feed.
//
// 🗣️ *"Algo que fique bom para compartilhar no insta tambem"*. É o único card da família que
// nasce de uma LISTA FILTRADA e não de uma entidade (categoria, grupo, chave): o que ele mostra
// é o que a aba Jogos estava mostrando, e por isso ele pode ter mais de uma arte.
//
// ⚠️ UMA ARTE POR DIA, E DIA CHEIO VIRA DUAS. Um sábado de torneio grande passa de cinquenta
// jogos, e não existe card legível com cinquenta linhas — a régua é a do card de resultados
// (que corta em cinco e diz "e mais N"). Aqui cortar não serve: uma grade pela metade é uma
// grade errada, e quem procura o próprio jogo não o acha. Então a lista é DIVIDIDA, e a tela
// oferece todas as partes; no story elas viram uma sequência.
//
// ⚠️ SEM EMOJI, como todo card daqui: a Poppins não tem glifo e não há fallback. O confronto é
// "x" e não "×" pelo mesmo motivo — o teste do card confere cada caractere que ele escreve.
public static class CartaoDosJogos
{
    private const float MargemH = 60;
    private const float TituloY = 388;
    private const float PilulaCentroY = 468;
    private const float RecorteY = 548;

    // A faixa onde a lista mora. Fixa, pra o rodapé não subir nem descer entre as artes de um
    // mesmo dia — a sequência no story sai com a mesma cara.
    private const float PrimeiraLinhaY = 596;
    private const float UltimaLinhaY = 1116;

    private const float DivisoriaY = 1158;
    private const float TorneioY = 1206;
    private const float LegendaY = 1254;

    // Oito jogos com DUAS linhas cada (contexto em cima, confronto embaixo): é o que cabe na
    // faixa com corpo ainda legível de relance no celular. O card de resultados cabe cinco com
    // três linhas; a conta é a mesma.
    public const int MaximoDeJogos = 8;

    // A lista com menos de cinco jogos NÃO estica as linhas até encher a faixa: três jogos com
    // 170px cada parecem um card vazio com três frases perdidas. Abaixo disto o bloco é
    // centralizado na faixa, com a altura de linha de cinco.
    private const int LinhasMinimasNaConta = 5;

    /// <summary>
    /// As artes de uma lista: agrupa por dia, na ordem em que os dias aparecem na fila (que já
    /// vem ordenada por hora), e parte cada dia em blocos de <see cref="MaximoDeJogos"/>.
    /// Jogo sem horário (torneio por ordem, sem hora nenhuma) cai numa arte "sem data" — e, como
    /// a fila põe os sem hora no fim, ela é a última.
    /// </summary>
    public static List<ArteDeJogos> Dividir(IReadOnlyList<JogoDaLista> jogos)
    {
        var artes = new List<ArteDeJogos>();

        // GroupBy preserva a ordem da primeira aparição de cada dia — e a fila já chega
        // ordenada, então os dias saem em ordem sem um OrderBy que discordasse dela.
        foreach (var dia in jogos.GroupBy(j => j.Horario?.Date))
        {
            var doDia = dia.ToList();

            // ⚠️ EQUILIBRADO, e não "oito e o resto": 9 jogos em 8 + 1 deixava a segunda arte com
            // uma linha perdida no meio do card — visto na prévia, não no teste. O número de
            // partes é o mínimo que respeita o teto; o tamanho de cada uma é a divisão justa
            // (9 → 5 + 4, 11 → 6 + 5, 17 → 6 + 6 + 5). As primeiras levam a sobra, pra a última
            // nunca ser a mais magra.
            var partes = (doDia.Count + MaximoDeJogos - 1) / MaximoDeJogos;
            var porParte = (doDia.Count + partes - 1) / partes;
            for (int parte = 0, inicio = 0; parte < partes; parte++, inicio += porParte)
            {
                artes.Add(new ArteDeJogos(
                    dia.Key, parte + 1, partes,
                    doDia.Skip(inicio).Take(porParte).ToList()));
            }
        }

        return artes;
    }

    public static byte[] Desenhar(GradeDesenhavel grade, ArteDeJogos arte, FonteDoCartao fontes, string webRootPath)
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
                    canvas, "JOGOS", TituloY, fontes.Forte, 96,
                    CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140);

                // O dia na pílula, com a parte quando há mais de uma: "SEX 11/09 · 1/2". Em
                // maiúsculas porque é a assinatura das pílulas da família ("OPEN MASCULINA").
                CartaoCompartilhavel.Pilula(canvas, PilulaDoDia(arte), PilulaCentroY, fontes, 40);

                // O recorte — quais filtros estavam ligados. Sem ele, a lista é o torneio inteiro
                // e a linha simplesmente não existe.
                if (!string.IsNullOrWhiteSpace(grade.Recorte))
                {
                    CartaoCompartilhavel.TextoCentralizado(
                        canvas, grade.Recorte.ToUpperInvariant(), RecorteY, fontes.Media, 34,
                        CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 140, tamanhoMinimo: 20);
                }

                Jogos(canvas, fontes, arte.Jogos);
                Evento(canvas, fontes, grade);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            logo?.Dispose();
        }
    }

    public static string PilulaDoDia(ArteDeJogos arte)
    {
        var dia = TextoDaLista.Dia(arte.Dia).ToUpperInvariant();
        return arte.Partes > 1 ? $"{dia}  ·  {arte.Parte}/{arte.Partes}" : dia;
    }

    private static void Jogos(SKCanvas canvas, FonteDoCartao fontes, List<JogoDaLista> jogos)
    {
        if (jogos.Count == 0) return;

        float faixa = UltimaLinhaY - PrimeiraLinhaY;
        float alturaDoJogo = faixa / Math.Max(jogos.Count, LinhasMinimasNaConta);
        float corpo = Math.Clamp(alturaDoJogo * 0.40f, 20f, 34f);
        float corpoDoContexto = Math.Max(16f, corpo * 0.66f);
        float corpoDaHora = corpo * 1.15f;

        // A hora à esquerda, numa coluna fixa; o texto começa depois dela. "por ordem" é a
        // palavra mais larga que a coluna recebe, e o TamanhoQueCabe a encolhe se precisar.
        float xHora = MargemH + 26;
        float larguraDaHora = 160;
        float xTexto = xHora + larguraDaHora + 14;
        float larguraDoTexto = CartaoCompartilhavel.Largura - MargemH - xTexto - 20;

        // Centralizado na faixa: a arte de três jogos não fica com o bloco no alto e um vão
        // embaixo (o defeito do pódio da panelinha, 25/08/2026).
        float y = PrimeiraLinhaY + (faixa - alturaDoJogo * jogos.Count) / 2f;

        foreach (var jogo in jogos)
        {
            float centroY = y + alturaDoJogo / 2f;

            // A caixa da linha. A prévia é mais apagada: ela ainda não existe.
            using (var fundo = new SKPaint
            {
                Color = CartaoCompartilhavel.Branco.WithAlpha((byte)(jogo.Previa ? 7 : 14)),
                IsAntialias = true,
            })
            {
                var caixa = new SKRect(
                    MargemH, y + alturaDoJogo * 0.06f,
                    CartaoCompartilhavel.Largura - MargemH, y + alturaDoJogo * 0.94f);
                canvas.DrawRoundRect(caixa, 14, 14, fundo);
            }

            // A hora, na linha de base do centro da caixa.
            using (var fonteDaHora = new SKFont(fontes.Forte, corpoDaHora))
            {
                var metricas = fonteDaHora.Metrics;
                float linhaDeBase = centroY - (metricas.Ascent + metricas.Descent) / 2f;
                CartaoCompartilhavel.TextoAEsquerda(
                    canvas, TextoDaLista.Quando(jogo), xHora, linhaDeBase, fontes.Forte, corpoDaHora,
                    jogo.Previa ? CartaoCompartilhavel.Apagado : CartaoCompartilhavel.Lime,
                    larguraDaHora, tamanhoMinimo: 16);
            }

            // Duas linhas: o contexto pequeno em cima, o confronto embaixo — a forma do card de
            // resultados, pelo mesmo motivo: uma linha só com hora, categoria, fase, lugar e
            // quatro nomes passa de setenta caracteres e cai pro corpo mínimo.
            CartaoCompartilhavel.TextoAEsquerda(
                canvas, Contexto(jogo).ToUpperInvariant(), xTexto, centroY - corpo * 0.30f,
                fontes.Media, corpoDoContexto, CartaoCompartilhavel.Apagado, larguraDoTexto, tamanhoMinimo: 12);

            CartaoCompartilhavel.TextoAEsquerda(
                canvas, $"{jogo.Lado1Curto}  x  {jogo.Lado2Curto}", xTexto, centroY + corpo * 0.92f,
                fontes.Media, corpo,
                jogo.Previa ? CartaoCompartilhavel.Apagado : CartaoCompartilhavel.Branco,
                larguraDoTexto, tamanhoMinimo: 14);

            y += alturaDoJogo;
        }
    }

    // As etiquetas da linha, SEM a hora (ela tem coluna própria aqui).
    private static string Contexto(JogoDaLista jogo)
    {
        var partes = new List<string> { jogo.Categoria, jogo.Fase };
        if (!string.IsNullOrWhiteSpace(jogo.Lugar)) partes.Add(jogo.Lugar);
        if (jogo.Previa) partes.Add("prévia");
        return string.Join("  ·  ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static void Evento(SKCanvas canvas, FonteDoCartao fontes, GradeDesenhavel grade)
    {
        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(90), IsAntialias = true })
        {
            var meio = CartaoCompartilhavel.Largura / 2f;
            canvas.DrawRect(new SKRect(meio - 90, DivisoriaY, meio + 90, DivisoriaY + 2), tinta);
        }

        CartaoCompartilhavel.TextoCentralizado(
            canvas, grade.Torneio.ToUpperInvariant(), TorneioY, fontes.Media, 40,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140, tamanhoMinimo: 24);

        if (!string.IsNullOrWhiteSpace(grade.Clube))
        {
            CartaoCompartilhavel.TextoCentralizado(
                canvas, grade.Clube, LegendaY, fontes.Normal, 32,
                CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 140);
        }
    }
}
