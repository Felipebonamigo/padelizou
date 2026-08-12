using SkiaSharp;

namespace Padelizou.Services;

// O PNG de identidade do jogador: foto, nível, lado da quadra, números e o que os outros
// dizem dele. O card de campeão é de quem GANHOU; este é de TODO MUNDO — é a carteirinha
// que a pessoa põe no grupo quando procura dupla ou quando quer se apresentar.
//
// ⚠️ Sem emoji e sem ícone de fonte: a Poppins não tem esses glifos, e no Linux eles sairiam
// invisíveis — a mesma armadilha da fonte que já mordeu uma vez.
public sealed class DadosDoCardDoJogador
{
    public string Nome { get; set; } = "";
    public string? Foto { get; set; }
    public string? Cidade { get; set; }

    // O rótulo pronto da faixa ("5ª categoria", "Open") — nulo quando não há padelímetro
    // ainda, e aí a linha simplesmente não aparece.
    public string? Faixa { get; set; }
    public bool EmCalibracao { get; set; }
    public string? Lado { get; set; }   // rótulo pronto ("Lado esquerdo"), nulo sem preferência

    public int Torneios { get; set; }
    public int Titulos { get; set; }
    public int Vitorias { get; set; }

    // Os mais recebidos, já com título do catálogo. Até 3 — mais que isso vira lista, e
    // lista não se lê num story.
    public List<(string Titulo, int Quantidade)> Elogios { get; set; } = new();
}

public static class CartaoDoJogador
{
    // O texto da pílula sob o nome: faixa e lado juntos, qualquer um pode faltar. Pura pra
    // o teste conferir as combinações sem abrir canvas.
    public static string? PilulaDeNivel(DadosDoCardDoJogador d)
    {
        var faixa = d.EmCalibracao ? "Calibrando o nível" : d.Faixa;
        var partes = new[] { faixa, d.Lado }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return partes.Count == 0 ? null : string.Join("  ·  ", partes);
    }

    // "Parceiro ideal ×7 · Fair play ×4" — a linha dos elogios, pura pelo mesmo motivo.
    public static string? LinhaDeElogios(IEnumerable<(string Titulo, int Quantidade)> elogios)
    {
        var partes = elogios.Take(3).Select(e => $"{e.Titulo} ×{e.Quantidade}").ToList();
        return partes.Count == 0 ? null : string.Join("   ·   ", partes);
    }

    // O rótulo do lado a partir do que o perfil guarda ("esquerda"/"direita", como veio do
    // formulário). Qualquer outra coisa (vazio, "tanto faz") não vira rótulo.
    public static string? RotuloDoLado(string? ladoQuadra)
    {
        var lado = (ladoQuadra ?? "").Trim().ToLowerInvariant();
        if (lado.StartsWith("esq")) return "Lado esquerdo";
        if (lado.StartsWith("dir")) return "Lado direito";
        return null;
    }

    public static byte[] Desenhar(DadosDoCardDoJogador dados, FonteDoCartao fontes, string webRootPath)
    {
        var foto = CartaoCompartilhavel.Ler(webRootPath, dados.Foto);
        var logo = CartaoCompartilhavel.LerDaMarca(webRootPath, CartaoDeCampeao.LogoDaMarca);

        try
        {
            return CartaoCompartilhavel.EmPng(canvas =>
            {
                CartaoCompartilhavel.Fundo(canvas);
                CartaoCompartilhavel.FaixaDoTopo(canvas);

                // O mesmo cabeçalho da família de cards — é ele que faz a marca ser
                // reconhecida antes de qualquer leitura.
                CartaoCompartilhavel.Logo(canvas, logo, CartaoCompartilhavel.Largura / 2f, 150, 108);
                CartaoCompartilhavel.TextoCentralizado(
                    canvas, "P A D E L I Z O U", 248, fontes.Media, 34,
                    CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 160);

                var meio = CartaoCompartilhavel.Largura / 2f;

                // O rosto é o centro do card — maior que no card de campeão, porque aqui ele
                // é o assunto, não o prêmio.
                CartaoCompartilhavel.FotoRedonda(canvas, foto, meio, 480, 165, fontes, dados.Nome);

                CartaoCompartilhavel.TextoCentralizado(
                    canvas, dados.Nome, 744, fontes.Forte, 68,
                    CartaoCompartilhavel.LimeClaro, CartaoCompartilhavel.Largura - 160, tamanhoMinimo: 34);

                if (PilulaDeNivel(dados) is { } pilula)
                {
                    CartaoCompartilhavel.Pilula(canvas, pilula, 812, fontes, 36);
                }

                if (!string.IsNullOrWhiteSpace(dados.Cidade))
                {
                    CartaoCompartilhavel.TextoCentralizado(
                        canvas, dados.Cidade, 892, fontes.Normal, 32,
                        CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 200);
                }

                Numeros(canvas, fontes, dados);

                // A linha fina antes do que os outros dizem — só quando há o que dizer.
                if (LinhaDeElogios(dados.Elogios) is { } elogios)
                {
                    using (var tinta = new SKPaint
                    {
                        Color = CartaoCompartilhavel.Apagado.WithAlpha(90),
                        IsAntialias = true,
                    })
                    {
                        canvas.DrawRect(new SKRect(meio - 90, 1128, meio + 90, 1130), tinta);
                    }

                    CartaoCompartilhavel.TextoCentralizado(
                        canvas, "O QUE A QUADRA DIZ", 1176, fontes.Media, 26,
                        CartaoCompartilhavel.Apagado, CartaoCompartilhavel.Largura - 200);
                    CartaoCompartilhavel.TextoCentralizado(
                        canvas, elogios, 1224, fontes.Media, 34,
                        CartaoCompartilhavel.Branco, CartaoCompartilhavel.Largura - 120, tamanhoMinimo: 22);
                }

                CartaoCompartilhavel.Rodape(canvas, fontes);
            });
        }
        finally
        {
            foto?.Dispose();
            logo?.Dispose();
        }
    }

    private static void Numeros(SKCanvas canvas, FonteDoCartao fontes, DadosDoCardDoJogador d)
    {
        // Três colunas: o número grande em cima, o rótulo miúdo embaixo — a mesma leitura
        // dos cards de estatística do site.
        var colunas = new (string Rotulo, int Valor)[]
        {
            ("TORNEIOS", d.Torneios),
            ("TÍTULOS", d.Titulos),
            ("VITÓRIAS", d.Vitorias),
        };

        for (var i = 0; i < colunas.Length; i++)
        {
            var x = CartaoCompartilhavel.Largura * (i + 1) / 4f;
            CartaoCompartilhavel.Texto(
                canvas, colunas[i].Valor.ToString(), x, 1020, fontes.Forte, 88,
                CartaoCompartilhavel.Branco, 220);
            CartaoCompartilhavel.Texto(
                canvas, colunas[i].Rotulo, x, 1068, fontes.Media, 28,
                CartaoCompartilhavel.Apagado, 220);
        }
    }
}
