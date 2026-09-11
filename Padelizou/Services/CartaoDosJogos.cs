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

    // ───────────────────────── A LISTA INTEIRA NUMA IMAGEM SÓ ─────────────────────────

    // 🗣️ Felipe, 11/09/2026: *"da para por a opção, para o jogador selecionar se nao quer todos os
    // jogos na lista em uma imagem apenas, dividindo e cabendo, por que é mais facil"* — mandar uma
    // imagem no grupo é mais fácil do que mandar três em sequência.
    //
    // ⚠️ E A RESSALVA DELE É A REGRA DESTE BLOCO: *"a menos que tenha muitos jogos que nao ficariam
    // visiveis se diminuisse ou organizasse"*. Por isso a imagem **CRESCE PRA BAIXO** e nunca
    // espreme: espremer 40 jogos nos 1350px do story daria linha de 19px — a lista inteira numa
    // imagem que ninguém lê. Passando do teto de altura, a opção deixa de existir (ver
    // <see cref="CabeNumaImagemSo"/>) e quem precisa dela continua com as partes.
    //
    // ⚠️ ESTE FORMATO NÃO É O DO STORY. 1080×4000 no story sai cortado; ele é feito pro WhatsApp e
    // pro Telegram, onde a imagem abre e dá pra dar zoom. É por isso que a tela oferece os dois, e
    // não troca um pelo outro.

    /// <summary>Um dia dentro da imagem única: a faixa com o dia e os jogos dele.</summary>
    public sealed record BlocoDoDia(string Rotulo, List<JogoDaLista> Jogos);

    public const float AlturaDaLinhaNaImagemUnica = 64;

    // A faixa do dia já traz o respiro de cima — assim a conta da altura é uma soma simples, sem
    // "mais 16 entre blocos" que ninguém lembra de incluir.
    public const float AlturaDaFaixaDoDia = 76;

    private const float TopoDaListaNaImagemUnica = 350;

    // O pé, medido a partir da BASE — as mesmas distâncias do card de sempre, pra os dois formatos
    // terminarem igual.
    private const float RodapeDaImagemUnica = 230;

    /// <summary>
    /// O teto de altura do PNG. Acima disso a imagem vira um arquivo que o WhatsApp reamostra e o
    /// celular engasga pra abrir — e uma imagem que não abre não é mais fácil que três que abrem.
    /// </summary>
    public const int AlturaMaxima = 10000;

    /// <summary>Os dias da lista, na ordem da fila. "Sem data" (torneio por ordem) fica no fim.</summary>
    public static List<BlocoDoDia> BlocosPorDia(IReadOnlyList<JogoDaLista> jogos) =>
        jogos.GroupBy(j => j.Horario?.Date)
            .Select(g => new BlocoDoDia(TextoDaLista.Dia(g.Key).ToUpperInvariant(), g.ToList()))
            .ToList();

    /// <summary>
    /// A altura do PNG da lista inteira. Nunca menor que o card de sempre: uma lista de quatro
    /// jogos continua saindo 1080×1350, que é o formato que o Instagram espera.
    /// </summary>
    public static int AlturaDaImagemUnica(IReadOnlyList<JogoDaLista> jogos)
    {
        var blocos = BlocosPorDia(jogos);
        float conteudo = blocos.Sum(b => AlturaDaFaixaDoDia + b.Jogos.Count * AlturaDaLinhaNaImagemUnica);
        return Math.Max(CartaoCompartilhavel.Altura,
            (int)Math.Ceiling(TopoDaListaNaImagemUnica + conteudo + RodapeDaImagemUnica));
    }

    /// <summary>A lista inteira cabe numa imagem só sem virar ilegível?</summary>
    public static bool CabeNumaImagemSo(IReadOnlyList<JogoDaLista> jogos) =>
        jogos.Count > 0 && AlturaDaImagemUnica(jogos) <= AlturaMaxima;

    public static byte[] DesenharTudo(
        GradeDesenhavel grade, IReadOnlyList<JogoDaLista> jogos, FonteDoCartao fontes, string webRootPath)
    {
        var logo = CartaoCompartilhavel.LerDaMarca(webRootPath, CartaoDeCampeao.LogoDaMarca);
        var blocos = BlocosPorDia(jogos);
        int altura = AlturaDaImagemUnica(jogos);

        try
        {
            return CartaoCompartilhavel.EmPng(canvas =>
            {
                CartaoCompartilhavel.Fundo(canvas, altura);
                CartaoCompartilhavel.FaixaDoTopo(canvas);

                CartaoCompartilhavel.Logo(canvas, logo, CartaoCompartilhavel.Largura / 2f, 86, 64);
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, "P A D E L I Z O U", 152, fontes.Media, 26,
                    CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 160);

                // Aqui o título NÃO é o dia (a imagem tem vários), é o que a lista é.
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, "JOGOS", 236, fontes.Forte, 76,
                    CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140, tamanhoMinimo: 40);

                CartaoCompartilhavel.TextoCentralizado(
                    canvas, string.IsNullOrWhiteSpace(grade.Recorte) ? "TODOS OS JOGOS" : grade.Recorte.ToUpperInvariant(),
                    292, fontes.Media, 30, CartaoCompartilhavel.Lime,
                    CartaoCompartilhavel.Largura - 140, tamanhoMinimo: 18);

                // Sobrando espaço (lista curta no formato mínimo), o bloco desce até o meio em vez
                // de ficar pendurado no alto com um vão embaixo.
                float conteudo = blocos.Sum(b => AlturaDaFaixaDoDia + b.Jogos.Count * AlturaDaLinhaNaImagemUnica);
                float disponivel = altura - TopoDaListaNaImagemUnica - RodapeDaImagemUnica;
                float y = TopoDaListaNaImagemUnica + Math.Max(0, (disponivel - conteudo) / 2f);

                foreach (var bloco in blocos)
                {
                    FaixaDoDia(canvas, fontes, bloco.Rotulo, y);
                    y += AlturaDaFaixaDoDia;

                    foreach (var jogo in bloco.Jogos)
                    {
                        Linha(canvas, fontes, jogo, y, AlturaDaLinhaNaImagemUnica);
                        y += AlturaDaLinhaNaImagemUnica;
                    }
                }

                Evento(canvas, fontes, grade, altura);
                CartaoCompartilhavel.Rodape(canvas, fontes, altura: altura);
            }, altura);
        }
        finally
        {
            logo?.Dispose();
        }
    }

    // A pílula lime com o dia, à esquerda — é ela que separa "sexta" de "sábado" no meio de uma
    // imagem longa. Volta aqui a pílula que o card do story perdeu pro espaço.
    private static void FaixaDoDia(SKCanvas canvas, FonteDoCartao fontes, string rotulo, float topo)
    {
        const float alturaDaPilula = 50;
        float centroY = topo + AlturaDaFaixaDoDia - alturaDaPilula / 2f - 4;

        using var fonte = new SKFont(fontes.Forte, 30);
        float largura = fonte.MeasureText(rotulo) + 56;

        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Lime, IsAntialias = true })
        {
            var pilula = new SKRect(MargemH, centroY - alturaDaPilula / 2f,
                MargemH + largura, centroY + alturaDaPilula / 2f);
            canvas.DrawRoundRect(pilula, alturaDaPilula / 2f, alturaDaPilula / 2f, tinta);
        }

        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Navy, IsAntialias = true })
        {
            var metricas = fonte.Metrics;
            canvas.DrawText(rotulo, MargemH + 28, centroY - (metricas.Ascent + metricas.Descent) / 2f,
                SKTextAlign.Left, fonte, tinta);
        }
    }

    private static void Jogos(SKCanvas canvas, FonteDoCartao fontes, List<JogoDaLista> jogos)
    {
        if (jogos.Count == 0) return;

        float faixa = UltimaLinhaY - PrimeiraLinhaY;
        float alturaDoJogo = Math.Min(faixa / jogos.Count, AlturaMaximaDoJogo);

        // Centralizado na faixa: a arte de três jogos não fica com o bloco no alto e um vão
        // embaixo (o defeito do pódio da panelinha, 25/08/2026).
        float y = PrimeiraLinhaY + (faixa - alturaDoJogo * jogos.Count) / 2f;

        foreach (var jogo in jogos)
        {
            Linha(canvas, fontes, jogo, y, alturaDoJogo);
            y += alturaDoJogo;
        }
    }

    // UMA LINHA DA LISTA — a caixa, a hora à esquerda, o contexto pequeno em cima e o confronto
    // embaixo.
    //
    // ⚠️ É UM MÉTODO SÓ porque os DOIS formatos desenham a mesma linha: o card do story (uma arte
    // por dia) e a imagem única da lista inteira. Duas cópias divergiriam na primeira mudança — e
    // a lista postada num formato passaria a não bater com a do outro.
    private static void Linha(SKCanvas canvas, FonteDoCartao fontes, JogoDaLista jogo, float topo, float altura)
    {
        // O corpo acompanha a altura da linha, entre 20 (catorze jogos) e 40 (a lista curta, com
        // espaço de sobra). O `Texto` ainda encolhe sozinho o nome de dupla que não couber.
        float corpo = Math.Clamp(altura * 0.40f, 20f, 40f);
        float corpoDoContexto = Math.Max(16f, corpo * 0.62f);
        float corpoDaHora = corpo * 1.15f;

        // A hora à esquerda, numa coluna fixa; o texto começa depois dela. "por ordem" é a
        // palavra mais larga que a coluna recebe, e o TamanhoQueCabe a encolhe se precisar.
        float xHora = MargemH + 20;
        float larguraDaHora = 132;
        float xTexto = xHora + larguraDaHora + 14;
        float larguraDoTexto = CartaoCompartilhavel.Largura - MargemH - xTexto - 20;

        float centroY = topo + altura / 2f;

        // A caixa da linha. A prévia é mais apagada: ela ainda não existe.
        using (var fundo = new SKPaint
        {
            Color = CartaoCompartilhavel.Branco.WithAlpha((byte)(jogo.Previa ? 7 : 14)),
            IsAntialias = true,
        })
        {
            var caixa = new SKRect(
                MargemH, topo + altura * 0.06f,
                CartaoCompartilhavel.Largura - MargemH, topo + altura * 0.94f);
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
    }

    // As etiquetas da linha, SEM a hora (ela tem coluna própria aqui).
    private static string Contexto(JogoDaLista jogo)
    {
        var partes = new List<string> { jogo.Categoria, jogo.Fase };
        if (!string.IsNullOrWhiteSpace(jogo.Lugar)) partes.Add(jogo.Lugar);
        if (jogo.Previa) partes.Add("prévia");
        return string.Join("  ·  ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static void Evento(SKCanvas canvas, FonteDoCartao fontes, GradeDesenhavel grade,
        int altura = CartaoCompartilhavel.Altura)
    {
        // As distâncias são medidas da BASE, e não do topo: é o que faz a imagem única terminar
        // igual ao card de sempre, por mais alta que ela seja.
        float divisoriaY = altura - 190, torneioY = altura - 145, legendaY = altura - 102;

        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(90), IsAntialias = true })
        {
            var meio = CartaoCompartilhavel.Largura / 2f;
            canvas.DrawRect(new SKRect(meio - 90, divisoriaY, meio + 90, divisoriaY + 2), tinta);
        }

        CartaoCompartilhavel.TextoCentralizado(
            canvas, grade.Torneio.ToUpperInvariant(), torneioY, fontes.Media, 40,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140, tamanhoMinimo: 24);

        if (!string.IsNullOrWhiteSpace(grade.Clube))
        {
            CartaoCompartilhavel.TextoCentralizado(
                canvas, grade.Clube, legendaY, fontes.Normal, 32,
                CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 140);
        }
    }
}
