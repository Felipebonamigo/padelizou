using SkiaSharp;

namespace Padelizou.Services;

// Uma linha do pódio da noite. `Posicao` já vem resolvida (com empate), pra o desenho não
// precisar contar nada.
public sealed class LinhaDoPodio
{
    public int Posicao { get; set; }
    public string Nome { get; set; } = "";
    public int Pontos { get; set; }
}

public sealed class DadosDoCardDaPanelinha
{
    public string Panelinha { get; set; } = "";
    public DateTime Data { get; set; }
    public string? Clube { get; set; }
    public int Jogos { get; set; }
    public List<LinhaDoPodio> Podio { get; set; } = new();
}

// O PNG DA NOITE DA PANELINHA — o que a turma manda no grupo do WhatsApp depois de jogar.
//
// Os outros cinco cards são sobre torneio ou sobre uma pessoa; este é sobre a terça-feira de
// um grupo, que é o padel que a maioria joga de verdade. Ele fecha o ciclo que o registro de
// jogo abriu: alguém lança o resultado no app e sai daqui com a arte pronta, em vez de digitar
// o ranking na mão na conversa.
//
// ⚠️ ELE NÃO É PÚBLICO COMO O CARTAZ. A panelinha é fechada por `CodigoConvite`, e este card
// leva nome de gente. Quem gera precisa estar logado e ser do grupo — a trava mora no
// `CartoesController`, e a página NÃO declara `og:image` (mesmo precedente do Duelo, 12/08):
// prévia de link é o servidor da Meta buscando a imagem sem sessão nenhuma, e é exatamente o
// caminho por onde o roster de um grupo privado vazaria.
//
// ⚠️ Nada aqui vai pro disco: ver a nota do `CartaoCompartilhavel`.
public static class CartaoDaPanelinha
{
    // O corte é por POSIÇÃO, não por quantidade de nomes. Com 3 pontos por vitória e 1 por
    // derrota (Services/PontuacaoDaPanelinha), empate é o caso NORMAL de uma noite curta —
    // "os 5 primeiros nomes" partiria empate no meio e publicaria um pódio que mente sobre
    // quem ficou em segundo.
    public const int PosicoesNoPodio = 3;

    // O teto existe pro dia em que a panelinha inteira empata: vinte nomes num story viram
    // corpo 12 que ninguém lê. Corta a lista, não a verdade — as posições continuam certas.
    public const int MaximoDeLinhas = 8;

    // Monta o pódio a partir de quem pontuou. Pura, pra o teste conferir empate sem canvas.
    public static List<LinhaDoPodio> Podio(IEnumerable<(string Nome, int Pontos)> pontuados)
    {
        // ⚠️ Zero fora. Quem não pontuou não é "último colocado" — ele não jogou, e "0 pts"
        // num card que a pessoa vai postar é motivo pra ela não postar (a régua do card do ano).
        var ordenados = pontuados
            .Where(p => p.Pontos > 0)
            .OrderByDescending(p => p.Pontos)
            .ThenBy(p => p.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var linhas = new List<LinhaDoPodio>();
        int posicao = 0, lidos = 0, pontosDaPosicao = int.MaxValue;

        foreach (var p in ordenados)
        {
            lidos++;
            // Posição de competição (1, 2, 2, 4): quem empata divide o lugar, e o próximo
            // recebe a posição que a contagem alcançou — não a seguinte.
            if (p.Pontos < pontosDaPosicao)
            {
                posicao = lidos;
                pontosDaPosicao = p.Pontos;
            }

            if (posicao > PosicoesNoPodio) break;
            if (linhas.Count == MaximoDeLinhas) break;

            linhas.Add(new LinhaDoPodio { Posicao = posicao, Nome = p.Nome, Pontos = p.Pontos });
        }

        return linhas;
    }

    public static string FraseDosJogos(int jogos) =>
        jogos == 1 ? "1 jogo na semana" : $"{jogos} jogos na semana";

    // Semana sem jogo ou sem ninguém pontuando não vira arte — a tela explica, a imagem
    // simplesmente não existe. Mesma régua do duelo 0×0.
    public static bool TemOQueMostrar(DadosDoCardDaPanelinha dados) =>
        dados.Jogos > 0 && dados.Podio.Count > 0;

    public static byte[] Desenhar(DadosDoCardDaPanelinha dados, FonteDoCartao fontes, string webRootPath)
    {
        var logo = CartaoCompartilhavel.LerDaMarca(webRootPath, CartaoDeCampeao.LogoDaMarca);

        try
        {
            return CartaoCompartilhavel.EmPng(canvas =>
            {
                CartaoCompartilhavel.Fundo(canvas);
                CartaoCompartilhavel.FaixaDoTopo(canvas);

                Cabecalho(canvas, fontes, logo);
                Titulo(canvas, fontes, dados);
                Podio(canvas, fontes, dados);
                Legenda(canvas, fontes, dados);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            logo?.Dispose();
        }
    }

    private static void Cabecalho(SKCanvas canvas, FonteDoCartao fontes, SKBitmap? logo)
    {
        CartaoCompartilhavel.Logo(canvas, logo, CartaoCompartilhavel.Largura / 2f, 150, 108);
        CartaoCompartilhavel.TextoCentralizado(
            canvas, "P A D E L I Z O U", 248, fontes.Media, 34,
            CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 160);
    }

    private static void Titulo(SKCanvas canvas, FonteDoCartao fontes, DadosDoCardDaPanelinha dados)
    {
        // O nome da panelinha é o que faz o grupo reconhecer o post como DELE. Vem grande e em
        // até duas linhas — "Los Corneteiros da Quinta" numa linha só cairia pra corpo 40.
        CartaoCompartilhavel.TextoEmVariasLinhas(
            canvas, dados.Panelinha.ToUpperInvariant(), 420, fontes.Forte, 86,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 140,
            maximoDeLinhas: 2, tamanhoMinimo: 46);

        // ⚠️ `dd/MM/yyyy` e não o dia da semana por extenso: a cultura do processo é pt-BR no
        // Program.cs, mas a fonte do card não tem fallback e um "ter." vindo de uma cultura
        // invariante sairia como "Tue" no meio da arte. Número não depende de cultura nenhuma.
        CartaoCompartilhavel.Pilula(canvas, dados.Data.ToString("dd/MM/yyyy"), 620, fontes, 40);

        CartaoCompartilhavel.TextoCentralizado(
            canvas, "RANKING DA SEMANA", 730, fontes.Media, 38,
            CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 200);
    }

    private static void Podio(SKCanvas canvas, FonteDoCartao fontes, DadosDoCardDaPanelinha dados)
    {
        // A altura de cada linha sai do espaço disponível dividido pelo TETO, e não pela
        // quantidade de linhas desta noite: assim três nomes e oito nomes ocupam a mesma
        // faixa, e o card não muda de proporção conforme a semana rende mais ou menos.
        const float primeiraLinha = 830;
        const float alturaDaFaixa = 320;
        var passo = alturaDaFaixa / MaximoDeLinhas;

        var y = primeiraLinha;
        foreach (var linha in dados.Podio)
        {
            // O primeiro lugar em lime; o resto em branco. Uma cor só de destaque — duas
            // fariam a terceira posição competir com a primeira.
            var cor = linha.Posicao == 1 ? CartaoCompartilhavel.LimeClaro : CartaoCompartilhavel.Branco;

            CartaoCompartilhavel.TextoCentralizado(
                canvas, $"{linha.Posicao}º   {linha.Nome}   ·   {linha.Pontos} pts", y,
                linha.Posicao == 1 ? fontes.Forte : fontes.Media, 44,
                cor, CartaoCompartilhavel.Largura - 160, tamanhoMinimo: 24);

            y += passo;
        }
    }

    private static void Legenda(SKCanvas canvas, FonteDoCartao fontes, DadosDoCardDaPanelinha dados)
    {
        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(90), IsAntialias = true })
        {
            var meio = CartaoCompartilhavel.Largura / 2f;
            canvas.DrawRect(new SKRect(meio - 90, 1178, meio + 90, 1180), tinta);
        }

        // Jogos e clube na mesma linha, e a linha se recompõe sozinha quando o clube falta —
        // panelinha sem clube configurado é caso real (ver GrupoPrivado.ClubeId anulável).
        var partes = new[] { FraseDosJogos(dados.Jogos), dados.Clube }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        CartaoCompartilhavel.TextoCentralizado(
            canvas, string.Join("  ·  ", partes), 1240, fontes.Normal, 34,
            CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 140);
    }
}
