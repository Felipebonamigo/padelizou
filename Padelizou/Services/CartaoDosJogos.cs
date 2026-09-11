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

    // ⚠️ O ALTO DO CARD ENCOLHEU PRA CABER MAIS JOGO (11/09/2026). 🗣️ Felipe: *"tente fazer com
    // que na imagem caiba mais jogos, para que não precise varias imagens do mesmo conteudo"*.
    // O cabeçalho antigo gastava 600 dos 1350px em logo grande + "JOGOS" em corpo 96 + pílula do
    // dia + recorte — quatro blocos pra dizer duas coisas. Agora o TÍTULO É O DIA (que é a
    // pergunta de quem vê o story) e o recorte vem embaixo, numa linha só: 260px no lugar de 600,
    // e a lista ganhou os outros 340.
    //
    // ⚠️ A PÍLULA LIME SAIU DAQUI, e é a única peça da família que este card não tem. O dia em
    // corpo 76 dentro de uma pílula viraria uma faixa de 120px — justamente o espaço que o pedido
    // manda devolver pra lista. A assinatura da marca continua na faixa lime do topo, na logo, no
    // subtítulo lime e no rodapé.
    private const float TituloY = 236;
    private const float SubtituloY = 292;

    // A faixa onde a lista mora. Fixa, pra o rodapé não subir nem descer entre as artes de um
    // mesmo dia — a sequência no story sai com a mesma cara.
    private const float PrimeiraLinhaY = 336;
    private const float UltimaLinhaY = 1120;

    private const float DivisoriaY = 1160;
    private const float TorneioY = 1205;
    private const float LegendaY = 1248;

    // Catorze jogos com DUAS linhas cada (contexto em cima, confronto embaixo). Era OITO até o
    // cabeçalho encolher; com 784px de faixa, catorze ainda dá corpo 22 no confronto — legível
    // de relance no celular, que é a régua. Acima disso o contexto cairia pro corpo mínimo e a
    // arte viraria uma tabela que ninguém lê no story.
    //
    // ⚠️ O TETO É DE LEGIBILIDADE, NÃO DE ESPAÇO: quem tem mais que isso num dia recebe partes
    // equilibradas (ver Dividir), e a tela oferece todas. Um dia com trinta jogos não cabe numa
    // imagem só sem virar ilegível — e ilegível não resolve o pedido, só o esconde.
    public const int MaximoDeJogos = 14;

    // O TETO DA ALTURA DE CADA LINHA. Sem ele, um jogo sozinho viraria uma caixa de 784px.
    //
    // ⚠️ E ELE É GENEROSO (160) DE PROPÓSITO: com a faixa maior, um teto apertado deixava três
    // jogos como três frases perdidas no meio de um card vazio — visto na prévia, não no teste, e
    // é o mesmo defeito do pódio da panelinha (25/08/2026). Com 160 a lista curta sai com linhas
    // GRANDES, que é o que se faz com o espaço que sobra; a lista cheia cai pros 56 da conta.
    private const float AlturaMaximaDoJogo = 160;

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

                CartaoCompartilhavel.Logo(canvas, logo, CartaoCompartilhavel.Largura / 2f, 86, 64);
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, "P A D E L I Z O U", 152, fontes.Media, 26,
                    CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 160);

                // O DIA é o título: "SEX 11/09". É a única pergunta que quem vê o story faz antes
                // de procurar o próprio nome na lista.
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, TituloDoDia(arte), TituloY, fontes.Forte, 76,
                    CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140, tamanhoMinimo: 40);

                // Embaixo, o recorte (quais filtros estavam ligados) e a parte, quando há mais de
                // uma. Sem recorte a lista é o torneio inteiro, e aí a linha diz "JOGOS": um card
                // que abre sem dizer do que se trata é um card que não se compartilha.
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, Subtitulo(grade.Recorte, arte), SubtituloY, fontes.Media, 30,
                    CartaoCompartilhavel.Lime, CartaoCompartilhavel.Largura - 140, tamanhoMinimo: 18);

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

    /// <summary>"SEX 11/09" — o dia da arte, em maiúsculas, como o card escreve.</summary>
    public static string TituloDoDia(ArteDeJogos arte) => TextoDaLista.Dia(arte.Dia).ToUpperInvariant();

    /// <summary>
    /// A linha abaixo do dia: o recorte (ou "JOGOS", quando a lista é o torneio inteiro) e a
    /// parte, quando o dia não coube numa arte só.
    /// </summary>
    public static string Subtitulo(string? recorte, ArteDeJogos arte)
    {
        var texto = string.IsNullOrWhiteSpace(recorte) ? "JOGOS" : recorte.ToUpperInvariant();
        return arte.Partes > 1 ? $"{texto}  ·  {arte.Parte} DE {arte.Partes}" : texto;
    }

    private static void Jogos(SKCanvas canvas, FonteDoCartao fontes, List<JogoDaLista> jogos)
    {
        if (jogos.Count == 0) return;

        float faixa = UltimaLinhaY - PrimeiraLinhaY;
        float alturaDoJogo = Math.Min(faixa / jogos.Count, AlturaMaximaDoJogo);
        // O corpo acompanha a altura da linha, entre 20 (catorze jogos) e 40 (a lista curta, com
        // espaço de sobra). O `Texto` ainda encolhe sozinho o nome de dupla que não couber.
        float corpo = Math.Clamp(alturaDoJogo * 0.40f, 20f, 40f);
        float corpoDoContexto = Math.Max(16f, corpo * 0.62f);
        float corpoDaHora = corpo * 1.15f;

        // A hora à esquerda, numa coluna fixa; o texto começa depois dela. "por ordem" é a
        // palavra mais larga que a coluna recebe, e o TamanhoQueCabe a encolhe se precisar.
        float xHora = MargemH + 20;
        float larguraDaHora = 132;
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
