using Padelizou.Services;
using SkiaSharp;

namespace Padelizou.Tests;

// OS DOIS CARDS NOVOS (12/08/2026): a carteirinha do jogador e a provocação do duelo.
// A prova que importa é a mesma do card de campeão — pixels de LETRA na imagem final,
// porque fonte sem glifo desenha um card inteiro, mudo, sem erro nenhum.
public class CartoesDoJogadorEDueloTests
{
    private static string PastaDasFontes()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "wwwroot", "fonts");
            if (Directory.Exists(tentativa)) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("wwwroot/fonts não encontrado a partir do bin.");
    }

    private static FonteDoCartao Fontes() => new(PastaDasFontes());
    private static string WebRoot() => Path.GetDirectoryName(PastaDasFontes())!;

    private static int PixelsBrancos(byte[] png)
    {
        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);
        Assert.Equal(CartaoCompartilhavel.Largura, imagem.Width);
        Assert.Equal(CartaoCompartilhavel.Altura, imagem.Height);

        int brancos = 0;
        for (int x = 0; x < imagem.Width; x += 2)
            for (int y = 0; y < imagem.Height; y += 2)
            {
                var c = imagem.GetPixel(x, y);
                if (c.Red > 240 && c.Green > 240 && c.Blue > 240) brancos++;
            }
        return brancos;
    }

    // ─────────────────────────── AS REGRAS PURAS ───────────────────────────

    [Fact]
    public void A_pilula_de_nivel_se_recompoe_quando_falta_um_lado()
    {
        var completo = new DadosDoCardDoJogador { Faixa = "5ª categoria", Lado = "Lado esquerdo" };
        Assert.Equal("5ª categoria  ·  Lado esquerdo", CartaoDoJogador.PilulaDeNivel(completo));

        var soFaixa = new DadosDoCardDoJogador { Faixa = "Categoria Open" };
        Assert.Equal("Categoria Open", CartaoDoJogador.PilulaDeNivel(soFaixa));

        var soLado = new DadosDoCardDoJogador { Lado = "Lado direito" };
        Assert.Equal("Lado direito", CartaoDoJogador.PilulaDeNivel(soLado));

        Assert.Null(CartaoDoJogador.PilulaDeNivel(new DadosDoCardDoJogador()));

        // Em calibração o número existe mas ainda não é confiável — a pílula diz isso em vez
        // de estampar uma categoria que pode mudar amanhã.
        var calibrando = new DadosDoCardDoJogador
        {
            Faixa = "5ª categoria", EmCalibracao = true, Lado = "Lado esquerdo",
        };
        Assert.Equal("Calibrando o nível  ·  Lado esquerdo", CartaoDoJogador.PilulaDeNivel(calibrando));
    }

    [Theory]
    [InlineData("esquerda", "Lado esquerdo")]
    [InlineData("Esquerda", "Lado esquerdo")]
    [InlineData("direita", "Lado direito")]
    [InlineData("DIREITA", "Lado direito")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("tanto faz", null)]
    public void O_lado_vira_rotulo_so_quando_e_um_lado_de_verdade(string? guardado, string? esperado)
    {
        Assert.Equal(esperado, CartaoDoJogador.RotuloDoLado(guardado));
    }

    [Fact]
    public void A_linha_de_elogios_pega_no_maximo_tres()
    {
        var linha = CartaoDoJogador.LinhaDeElogios(new[]
        {
            ("Parceiro ideal", 7), ("Fair play", 4), ("Raio de quadra", 3), ("Paredão", 2),
        });
        Assert.Equal("Parceiro ideal ×7   ·   Fair play ×4   ·   Raio de quadra ×3", linha);

        Assert.Null(CartaoDoJogador.LinhaDeElogios(Array.Empty<(string, int)>()));
    }

    [Theory]
    [InlineData(1, "1 jogo entre si")]
    [InlineData(4, "4 jogos entre si")]
    public void A_frase_do_duelo_sabe_singular_e_plural(int jogos, string esperado)
    {
        Assert.Equal(esperado, CartaoDoDuelo.FraseDosJogos(jogos));
    }

    [Theory]
    [InlineData("Diego Martins (Diguinho)", "DM")]   // o apelido não vira inicial — visto na arte, "D("
    [InlineData("Rafael Souza", "RS")]
    [InlineData("Cher", "C")]
    [InlineData("(Apelido)", "?")]
    [InlineData("", "?")]
    public void As_iniciais_ignoram_o_que_nao_comeca_com_letra(string nome, string esperado)
    {
        Assert.Equal(esperado, CartaoCompartilhavel.Iniciais(nome));
    }

    // ─────────────────────────── OS PIXELS ───────────────────────────

    [Fact]
    public void O_card_do_jogador_desenha_LETRA_de_verdade_com_e_sem_os_opcionais()
    {
        // O caso cheio: tudo preenchido.
        var cheio = new DadosDoCardDoJogador
        {
            Nome = "Rafael Souza (Rafa)",
            Cidade = "Gravataí - RS",
            Faixa = "5ª categoria",
            Lado = "Lado esquerdo",
            Torneios = 8,
            Titulos = 2,
            Vitorias = 23,
            Elogios = new() { ("Parceiro ideal", 7), ("Fair play", 4) },
        };
        Assert.True(PixelsBrancos(CartaoDoJogador.Desenhar(cheio, Fontes(), WebRoot())) > 500);

        // O caso magro: conta nova, sem nível, sem lado, sem elogio — o card ainda precisa
        // sair inteiro, porque é a carteirinha de TODO MUNDO.
        var magro = new DadosDoCardDoJogador { Nome = "Novato Silva" };
        Assert.True(PixelsBrancos(CartaoDoJogador.Desenhar(magro, Fontes(), WebRoot())) > 300);
    }

    [Fact]
    public void O_card_do_duelo_desenha_LETRA_de_verdade()
    {
        var dados = new DadosDoCardDoDuelo
        {
            Nome1 = "Rafael Souza",
            Vitorias1 = 3,
            Nome2 = "Diego Martins (Diguinho)",
            Vitorias2 = 1,
            Jogos = 4,
        };

        Assert.True(PixelsBrancos(CartaoDoDuelo.Desenhar(dados, Fontes(), WebRoot())) > 500);
    }
}
