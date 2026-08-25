using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
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

// A PORTA DO CARD DO DUELO — a parte que não é desenho.
//
// ⚠️ O duelo é da família FECHADA do CartoesController: ele exige login porque a arte é a
// provocação de UM dos dois, montada do ponto de vista de quem pede. E card que exige login
// não pode sair com `Cache-Control: public`.
public class CacheDoCardDoDueloTests
{
    // Dois jogadores que já se enfrentaram uma vez — o mínimo pro duelo existir (0 × 0 não
    // vira card, por decisão de 12/08/2026).
    private static async Task<(DbPadelContext Ctx, Jogador Eu, Jogador Oponente)> MontarConfrontoAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        var eu = new Jogador { Nome = "Felipe Bonamigo", Cpf = "11111111111" };
        var meuParceiro = new Jogador { Nome = "Lucas Foka", Cpf = "22222222222" };
        var oponente = new Jogador { Nome = "Diego Martins", Cpf = "33333333333" };
        var parceiroDele = new Jogador { Nome = "Rafael Souza", Cpf = "44444444444" };
        ctx.Jogadores.AddRange(eu, meuParceiro, oponente, parceiroDele);
        await ctx.SaveChangesAsync();

        var minhaDupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = eu.Id, Jogador2Id = meuParceiro.Id };
        var aDele = new Dupla { CategoriaId = categoria.Id, Jogador1Id = oponente.Id, Jogador2Id = parceiroDele.Id };
        ctx.Duplas.AddRange(minhaDupla, aDele);
        await ctx.SaveChangesAsync();

        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Codigo = "DUELO1",
            Status = "Finalizada",
            Fase = "Final",
            Dupla1Id = minhaDupla.Id,
            Dupla2Id = aDele.Id,
            GamesDupla1 = 6,
            GamesDupla2 = 3,
            SetsDupla1 = 1,
            SetsDupla2 = 0,
            VencedorId = minhaDupla.Id,
        });
        await ctx.SaveChangesAsync();

        return (ctx, eu, oponente);
    }

    // ⚠️ O DEFEITO QUE ESTE TESTE PRENDE: o duelo saía com `Cache-Control: public`, herdado do
    // padrão do `Png(...)` — que é o certo pros cards de DIVULGAÇÃO (é dele que a prévia do
    // WhatsApp vive) e errado aqui. `public` autoriza qualquer cache no caminho (proxy, CDN,
    // navegador compartilhado) a guardar a resposta de uma página autenticada e devolvê-la a
    // OUTRA pessoa. O card da panelinha já nasceu com `private` em 25/08; este veio de 12/08 e
    // ficou pra trás.
    [Fact]
    public async Task A_imagem_do_duelo_e_cache_privado()
    {
        var (ctx, eu, oponente) = await MontarConfrontoAsync();
        using var _ = ctx;

        var controller = TestInfra.NovoCartoesController(ctx, eu.Id);
        var resposta = await controller.DueloImagem(oponente.Id);

        // Primeiro a garantia de que o teste está medindo o caminho FELIZ: um 404 aqui também
        // teria cabeçalho nenhum, e o teste passaria sem provar coisa alguma.
        var imagem = Assert.IsType<FileContentResult>(resposta);
        Assert.Equal("image/png", imagem.ContentType);
        Assert.NotEmpty(imagem.FileContents);

        var cache = controller.Response.Headers.CacheControl.ToString();
        Assert.StartsWith("private", cache);
        Assert.DoesNotContain("public", cache);
    }

    // A contraprova, pra o teste acima não passar por acidente num controller que responde
    // `private` pra tudo: o cartaz do torneio é da família de DIVULGAÇÃO e PRECISA de cache
    // compartilhado — é ele que serve a prévia do link pro servidor da Meta.
    [Fact]
    public async Task O_cartaz_do_torneio_continua_com_cache_publico()
    {
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        var controller = TestInfra.NovoCartoesController(ctx, usuarioLogadoId: null);
        controller.Url = TestInfra.UrlDeTeste();

        var resposta = await controller.CartazImagem(torneio.Id);

        Assert.IsType<FileContentResult>(resposta);
        Assert.StartsWith("public", controller.Response.Headers.CacheControl.ToString());
    }
}
