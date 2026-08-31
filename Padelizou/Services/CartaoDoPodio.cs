using SkiaSharp;

namespace Padelizou.Services;

// O PNG DO PÓDIO DA CATEGORIA — a arte que o ORGANIZADOR posta quando a etapa acaba.
//
// O card de campeão é do campeão: ele posta porque é sobre ele. Este é do evento — conta a
// disputa inteira e dá lugar às outras três duplas que chegaram longe e hoje não aparecem em
// arte nenhuma. São quatro nomes por categoria em vez de um, e num torneio de dez categorias
// isso é a diferença entre dez pessoas com motivo pra postar e quarenta.
//
// ⚠️ SEM EMOJI, como todo card daqui: a Poppins não tem glifo de emoji e o Skia deste projeto
// não tem fonte de fallback (ver FonteDoCartao) — uma medalha 🥇 sairia como espaço em branco
// no meio da arte. As posições são PALAVRA ("CAMPEÃO", "VICE"), não símbolo.
//
// ⚠️ AS ALTURAS SÃO CONSTANTES NOMEADAS, e isso é cicatriz: `Pilula` recebe o CENTRO e `Texto`
// recebe a LINHA DE BASE, e um `y += ...` misturando as duas convenções foi o que sobrepôs
// três blocos na primeira versão do card do ano (13/08/2026).
public static class CartaoDoPodio
{
    private const float MargemH = 140;

    private const float TituloY = 392;
    private const float PilulaCentroY = 472;
    private const float RotuloCampeaoY = 610;
    private const float NomeCampeaoY = 682;
    private const float RotuloViceY = 800;
    private const float NomeViceY = 862;
    private const float RotuloSemisY = 966;
    private const float NomeSemisY = 1022;
    private const float DivisoriaY = 1112;
    private const float TorneioY = 1176;
    private const float LegendaY = 1236;

    // ⚠️ O RÓTULO DO PRIMEIRO DEGRAU NÃO É "CAMPEÃO" FIXO, e o defeito foi visto na arte, não
    // no código: a versão com a palavra escrita na mão saiu "CAMPEÃO" em cima de uma dupla de
    // "4ª Categoria Feminina". A régua já existia no card de campeão (`CampeoesDoTorneio
    // .Rotulo`, que lê o NOME da categoria e não o sexo de ninguém) — escrevê-la de novo aqui
    // era a segunda cópia, e a segunda cópia é a que fica pra trás.
    //
    // `duasPessoas: true` porque o pódio é do mata-mata, onde o campeão é sempre uma DUPLA. O
    // caso de uma pessoa só é o Americano, e ele não tem pódio aqui (ver PodioDaCategoria).
    public static string RotuloDoCampeao(string categoria) =>
        CampeoesDoTorneio.Rotulo(categoria, duasPessoas: true);

    // Os semifinalistas numa linha só, com o mesmo separador de respiro dos outros cards.
    // Nulo = não houve semifinal (categoria de 4 duplas), e a seção inteira some — rótulo em
    // cima do nada é pior que espaço vazio.
    public static string? LinhaDosSemifinalistas(List<string> semifinalistas) =>
        semifinalistas.Count == 0 ? null : string.Join("   ·   ", semifinalistas);

    public static byte[] Desenhar(PodioDeCategoria podio, FonteDoCartao fontes, string webRootPath)
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
                    canvas, "PÓDIO", TituloY, fontes.Forte, 118,
                    CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - MargemH);

                CartaoCompartilhavel.Pilula(
                    canvas, CategoriaNaTela.Curto(podio.Categoria), PilulaCentroY, fontes, 40);

                Degrau(canvas, fontes, RotuloDoCampeao(podio.Categoria), podio.Campeao, RotuloCampeaoY, NomeCampeaoY,
                    tamanhoDoNome: 60, corDoNome: CartaoCompartilhavel.LimeClaro,
                    familiaDoNome: fontes.Forte, corDoRotulo: CartaoCompartilhavel.Lime);

                Degrau(canvas, fontes, "VICE", podio.Vice, RotuloViceY, NomeViceY,
                    tamanhoDoNome: 46, corDoNome: CartaoCompartilhavel.Branco,
                    familiaDoNome: fontes.Media, corDoRotulo: CartaoCompartilhavel.Apagado);

                var semis = LinhaDosSemifinalistas(podio.Semifinalistas);
                if (semis != null)
                {
                    Degrau(canvas, fontes, "SEMIFINALISTAS", semis, RotuloSemisY, NomeSemisY,
                        tamanhoDoNome: 34, corDoNome: CartaoCompartilhavel.Branco,
                        familiaDoNome: fontes.Normal, corDoRotulo: CartaoCompartilhavel.Apagado);
                }

                Evento(canvas, fontes, podio);

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            logo?.Dispose();
        }
    }

    // Um degrau: o rótulo pequeno em cima, o nome embaixo. O nome encolhe sozinho até caber —
    // "Anderson Matteus Schwaab  &  Charls Gustavio Polese" é nome real e tem 50 caracteres.
    private static void Degrau(
        SKCanvas canvas, FonteDoCartao fontes, string rotulo, string? nome,
        float rotuloY, float nomeY, float tamanhoDoNome, SKColor corDoNome,
        SKTypeface? familiaDoNome, SKColor corDoRotulo)
    {
        if (string.IsNullOrWhiteSpace(nome)) return;

        CartaoCompartilhavel.TextoCentralizado(
            canvas, rotulo, rotuloY, fontes.Media, 32,
            corDoRotulo, CartaoCompartilhavel.Largura - MargemH);

        CartaoCompartilhavel.TextoCentralizado(
            canvas, nome, nomeY, familiaDoNome, tamanhoDoNome,
            corDoNome, CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 24);
    }

    private static void Evento(SKCanvas canvas, FonteDoCartao fontes, PodioDeCategoria podio)
    {
        using (var tinta = new SKPaint { Color = CartaoCompartilhavel.Apagado.WithAlpha(90), IsAntialias = true })
        {
            var meio = CartaoCompartilhavel.Largura / 2f;
            canvas.DrawRect(new SKRect(meio - 90, DivisoriaY, meio + 90, DivisoriaY + 2), tinta);
        }

        CartaoCompartilhavel.TextoCentralizado(
            canvas, podio.Torneio.ToUpperInvariant(), TorneioY, fontes.Media, 44,
            CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - MargemH, tamanhoMinimo: 26);

        // Clube e data na mesma linha; qualquer um dos dois pode faltar e a linha se recompõe
        // sem sobrar separador solto. Mesmo padrão do card de campeão.
        var partes = new[] { podio.Clube, podio.Data?.ToString("dd/MM/yyyy") }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        var legenda = string.Join("  ·  ", partes);
        if (legenda.Length > 0)
        {
            CartaoCompartilhavel.TextoCentralizado(
                canvas, legenda, LegendaY, fontes.Normal, 34,
                CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - MargemH);
        }
    }
}
