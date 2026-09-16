using SkiaSharp;

namespace Padelizou.Services;

// O PNG DO RANKING — o topo de qualquer uma das tabelas da tela do Ranking, pronto pro story.
//
// 🗣️ Felipe, 14/09/2026: *"crie um botão para compartilhar o ranking"*.
//
// É o segundo card de TABELA do sistema, e por isso ele nasce com a forma que o primeiro já
// provou (`CartaoDaClassificacao`): a LINHA ÚNICA por colocado — "1º  Los Corneteiros  ·  1301
// pts". Tabela não é frase e story não tem rolagem; coluna de verdade pediria uma largura que o
// 1080 não tem e uma régua de alinhamento que o primeiro card já tinha decidido não ter.
//
// ⚠️ SEM EMOJI e sem símbolo: a Poppins não tem glifo de emoji e o Skia daqui não tem fonte de
// fallback (ver FonteDoCartao). Posição é número, e a medida vai escrita por extenso ("pts",
// "vitórias", "títulos") — o troféu 🏆 da tela sairia como um retângulo vazio.
public static class CartaoDoRanking
{
    private const float MargemH = 140;
    private const float TituloY = 392;
    private const float PilulaCentroY = 472;

    // A faixa onde a tabela mora. Fixa, pelo mesmo motivo do card de classificação: assim o
    // rodapé não sobe nem desce conforme a aba tem três ou dez colocados, e as artes das oito
    // abas saem com a mesma cara.
    private const float PrimeiraLinhaY = 620;
    private const float UltimaLinhaY = 1090;

    private const float DivisoriaY = 1132;
    private const float LegendaY = 1190;

    // ⚠️ O PISO É DAQUI, E NÃO DO `TamanhoQueCabe` (que devolve o mínimo mesmo quando ele não
    // cabe — o buraco do pódio de 14/09/2026, em que o Skia pintou até a borda e sumiu com o
    // resto do nome). 22 foi escolhido contra o pior caso REAL desta arte: dupla com nome
    // comprido e apelido nos dois lados, na 10ª posição. Quem prova que ele basta é o gate de
    // margem do `CartaoDoRankingTests`, que mede tinta no PNG pronto — e é ele que tem que ser
    // consultado antes de mexer neste número.
    private const float CorpoMinimo = 22;

    public static byte[] Desenhar(ListaDoRanking lista, FonteDoCartao fontes, string webRootPath)
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

                // 88 e não os 104 do card de grupo: "TROFÉUS DO AMERICANO EM DUPLAS" é o título
                // mais longo que existe aqui, e num corpo de 104 ele encolheria mais do que o
                // título de uma arte deveria encolher.
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, lista.Titulo, TituloY, fontes.Forte, 88,
                    CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 40);

                CartaoCompartilhavel.Pilula(canvas, lista.Recorte, PilulaCentroY, fontes, 40);

                Tabela(canvas, fontes, lista);
                Rodape(canvas, fontes, lista);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            logo?.Dispose();
        }
    }

    // "1º   Los Corneteiros   ·   1301 pts" — a linha inteira de um colocado.
    //
    // Pura de propósito (não desenha nada), pra o teste conferir o texto sem abrir canvas.
    public static string Linha(LinhaDoCartaoDeRanking linha) =>
        $"{linha.Posicao}º   {linha.Nome}   ·   {linha.Valor}";

    private static void Tabela(SKCanvas canvas, FonteDoCartao fontes, ListaDoRanking lista)
    {
        var linhas = lista.Linhas;
        if (linhas.Count == 0) return;

        var larguraUtil = CartaoCompartilhavel.Largura - MargemH;

        // O passo e o corpo saem da CONTAGEM, e não de números escolhidos no olho — a mesma
        // conta do card de grupo. O teto de 76 impede que um ranking de três colocados vire
        // três frases gigantes soltas no meio da arte.
        float faixa = UltimaLinhaY - PrimeiraLinhaY;
        var passo = linhas.Count == 1 ? 0 : Math.Min(76f, faixa / (linhas.Count - 1));
        var corpoBase = linhas.Count == 1 ? 44f : Math.Min(44f, passo * 0.62f);

        // ⚠️ UM TAMANHO SÓ PRA TODAS AS LINHAS, e ele é o da MAIS LONGA. Deixar cada linha
        // encolher por conta própria é o que o `TextoCentralizado` faria sozinho — e o
        // resultado não parece tabela: o 3º colocado sairia com a letra menor que a do 4º só
        // porque o parceiro dele tem apelido. Numa lista ordenada, tamanho de letra é lido
        // como importância.
        //
        // ⚠️ E cada linha é medida COM A FONTE EM QUE ELA VAI SAIR: a do líder é a Bold, mais
        // larga que a SemiBold no mesmo corpo. Medir tudo na SemiBold deixaria justamente a
        // primeira linha — a que todo mundo lê — passando da margem.
        var corpo = corpoBase;
        foreach (var linha in linhas)
        {
            corpo = Math.Min(corpo, CartaoCompartilhavel.TamanhoQueCabe(
                Linha(linha), Familia(fontes, linha), corpoBase, larguraUtil, CorpoMinimo));
        }

        // O bloco fica centrado no que sobra: com início fixo, um ranking de três encostaria no
        // topo e deixaria um vão antes do rodapé, como se a arte estivesse cortada.
        var y = PrimeiraLinhaY + (faixa - passo * (linhas.Count - 1)) / 2f;
        foreach (var linha in linhas)
        {
            // Quem lidera sai em lime; o resto em branco. Uma cor de destaque só — a segunda
            // faria a terceira posição competir com a primeira.
            var cor = linha.Posicao == 1 ? CartaoCompartilhavel.LimeClaro : CartaoCompartilhavel.Branco;

            CartaoCompartilhavel.TextoCentralizado(
                canvas, Linha(linha), y, Familia(fontes, linha), corpo, cor, larguraUtil,
                tamanhoMinimo: CorpoMinimo);

            y += passo;
        }
    }

    private static SKTypeface? Familia(FonteDoCartao fontes, LinhaDoCartaoDeRanking linha)
        => linha.Posicao == 1 ? fontes.Forte : fontes.Media;

    // O recorte por extenso, embaixo da divisória: lugar, torneio e período.
    //
    // ⚠️ SEM ELE A ARTE MENTE POR OMISSÃO. O ranking de "Este mês" de Porto Alegre sai com os
    // mesmos nomes e outra ordem que o de "Sempre" do Brasil todo — e quem posta "sou o 1º"
    // estaria dizendo uma coisa diferente da que a tela dizia, sem ter mentido.
    private static void Rodape(SKCanvas canvas, FonteDoCartao fontes, ListaDoRanking lista)
    {
        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(90), IsAntialias = true })
        {
            var meio = CartaoCompartilhavel.Largura / 2f;
            canvas.DrawRect(new SKRect(meio - 90, DivisoriaY, meio + 90, DivisoriaY + 2), tinta);
        }

        CartaoCompartilhavel.TextoCentralizado(
            canvas, lista.Legenda, LegendaY, fontes.Media, 38,
            CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 24);
    }
}
