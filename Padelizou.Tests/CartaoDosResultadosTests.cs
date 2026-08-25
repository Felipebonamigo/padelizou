using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using SkiaSharp;

namespace Padelizou.Tests;

// OS RESULTADOS DO DIA VIRANDO ARTE.
//
// ⚠️ O DIA DE UM JOGO É `HorarioFimReal ?? HorarioPrevisto`, e a ordem importa: o primeiro é
// quando a bola parou de verdade, o segundo é a grade. Um jogo lançado sem ter sido "colocado
// no ar" não tem fim real — e cair pra grade é o que impede que ele suma do resumo do próprio
// dia em que aconteceu. Sem nenhum dos dois, o jogo não tem dia e fica fora: melhor ausente
// que no dia errado.
public class CartaoDosResultadosTests
{
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria)> MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var clube = new Clube { Nome = "Arena Beira Rio" };
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();

        var torneio = new Torneio
        {
            Nome = "NATA PADEL TOUR",
            Codigo = "NATA26",
            ClubeId = clube.Id,
            DataInicio = new DateTime(2026, 8, 22),
            Status = "Fase de Grupos",
        };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var categoria = new Categoria { TorneioId = torneio.Id, Nome = "Open Masculina", Codigo = "OPENM" };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        return (ctx, torneio, categoria);
    }

    private static async Task<Dupla> DuplaAsync(DbPadelContext ctx, Categoria categoria, string n1, string n2)
    {
        var j1 = new Jogador { Nome = n1, Cpf = Guid.NewGuid().ToString("N")[..11] };
        var j2 = new Jogador { Nome = n2, Cpf = Guid.NewGuid().ToString("N")[..11] };
        ctx.Jogadores.AddRange(j1, j2);
        await ctx.SaveChangesAsync();

        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        return dupla;
    }

    private static async Task JogoAsync(
        DbPadelContext ctx, Torneio t, Categoria c, Dupla d1, Dupla d2,
        int g1, int g2, string fase = "Grupo A",
        DateTime? fimReal = null, DateTime? previsto = null, string status = "Finalizada")
    {
        ctx.Partidas.Add(new Partida
        {
            TorneioId = t.Id,
            CategoriaId = c.Id,
            Codigo = $"P{d1.Id}x{d2.Id}",
            Status = status,
            Fase = fase,
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            GamesDupla1 = g1,
            GamesDupla2 = g2,
            VencedorId = status == "Finalizada" ? (g1 > g2 ? d1.Id : d2.Id) : null,
            HorarioFimReal = fimReal,
            HorarioPrevisto = previsto,
        });
        await ctx.SaveChangesAsync();
    }

    private static readonly DateTime Sabado = new(2026, 8, 22);

    [Fact]
    public async Task Junta_os_jogos_terminados_naquele_dia()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var a = await DuplaAsync(ctx, c, "Ana Souza", "Bia Lima");
        var b = await DuplaAsync(ctx, c, "Carla Reis", "Dani Alves");
        var e = await DuplaAsync(ctx, c, "Eva Prado", "Fabi Cruz");

        await JogoAsync(ctx, t, c, a, b, 6, 2, fimReal: Sabado.AddHours(9));
        await JogoAsync(ctx, t, c, a, e, 4, 6, fimReal: Sabado.AddHours(11));

        var dia = await ResultadosDoDia.DaCategoriaAsync(ctx, t.Id, c.Id, Sabado);

        Assert.NotNull(dia);
        Assert.True(dia.TemOQueMostrar);
        Assert.Equal(2, dia.Jogos.Count);
        // Mais recente em cima: num dia de torneio os jogos que interessam (semi, final) são
        // os últimos, e é neles que o card precisa pegar quem só olha a primeira linha.
        Assert.Equal("Ana Souza  &  Bia Lima", dia.Jogos[0].Dupla1);
        Assert.Equal("Eva Prado  &  Fabi Cruz", dia.Jogos[0].Dupla2);
        Assert.Equal(4, dia.Jogos[0].Games1);
        Assert.Equal(6, dia.Jogos[0].Games2);
        Assert.False(dia.Jogos[0].Dupla1Venceu);
        Assert.True(dia.Jogos[0].Dupla2Venceu);
        Assert.True(dia.Jogos[1].Dupla1Venceu);
        Assert.False(dia.Jogos[1].Dupla2Venceu);
    }

    [Fact]
    public async Task Jogo_de_outro_dia_fica_de_fora()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var a = await DuplaAsync(ctx, c, "Ana Souza", "Bia Lima");
        var b = await DuplaAsync(ctx, c, "Carla Reis", "Dani Alves");

        await JogoAsync(ctx, t, c, a, b, 6, 2, fimReal: Sabado.AddDays(-1).AddHours(20));

        var dia = await ResultadosDoDia.DaCategoriaAsync(ctx, t.Id, c.Id, Sabado);

        Assert.NotNull(dia);
        Assert.False(dia.TemOQueMostrar);
    }

    // ⚠️ Jogo lançado sem ter sido "colocado no ar" não tem `HorarioFimReal` — e é rotina em
    // torneio pequeno, onde o organizador só digita o placar no fim. Cair pra grade é o que
    // impede que ele suma do resumo do dia em que aconteceu.
    [Fact]
    public async Task Sem_fim_real_o_dia_sai_da_grade()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var a = await DuplaAsync(ctx, c, "Ana Souza", "Bia Lima");
        var b = await DuplaAsync(ctx, c, "Carla Reis", "Dani Alves");

        await JogoAsync(ctx, t, c, a, b, 6, 2, fimReal: null, previsto: Sabado.AddHours(14));

        var dia = await ResultadosDoDia.DaCategoriaAsync(ctx, t.Id, c.Id, Sabado);

        Assert.NotNull(dia);
        Assert.Single(dia.Jogos);
    }

    [Fact]
    public async Task Jogo_sem_data_nenhuma_nao_entra_em_dia_nenhum()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var a = await DuplaAsync(ctx, c, "Ana Souza", "Bia Lima");
        var b = await DuplaAsync(ctx, c, "Carla Reis", "Dani Alves");

        await JogoAsync(ctx, t, c, a, b, 6, 2, fimReal: null, previsto: null);

        var dia = await ResultadosDoDia.DaCategoriaAsync(ctx, t.Id, c.Id, Sabado);

        Assert.NotNull(dia);
        Assert.False(dia.TemOQueMostrar);
    }

    [Fact]
    public async Task Jogo_ao_vivo_nao_e_resultado()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var a = await DuplaAsync(ctx, c, "Ana Souza", "Bia Lima");
        var b = await DuplaAsync(ctx, c, "Carla Reis", "Dani Alves");

        await JogoAsync(ctx, t, c, a, b, 6, 0, fimReal: Sabado.AddHours(10), status: "AoVivo");

        var dia = await ResultadosDoDia.DaCategoriaAsync(ctx, t.Id, c.Id, Sabado);

        Assert.NotNull(dia);
        Assert.False(dia.TemOQueMostrar);
    }

    [Fact]
    public async Task Torneio_cancelado_nao_gera_card()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var a = await DuplaAsync(ctx, c, "Ana Souza", "Bia Lima");
        var b = await DuplaAsync(ctx, c, "Carla Reis", "Dani Alves");
        await JogoAsync(ctx, t, c, a, b, 6, 2, fimReal: Sabado.AddHours(9));

        t.Status = CancelamentoDoTorneio.Status;
        await ctx.SaveChangesAsync();

        Assert.Null(await ResultadosDoDia.DaCategoriaAsync(ctx, t.Id, c.Id, Sabado));
    }

    // Dia cheio: o card mostra os primeiros e DIZ quantos ficaram de fora. Cortar calado seria
    // publicar "os resultados do dia" mostrando metade deles.
    [Fact]
    public async Task Dia_cheio_corta_no_teto_e_avisa_quantos_sobraram()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var duplas = new List<Dupla>();
        for (int i = 0; i < 2; i++) duplas.Add(await DuplaAsync(ctx, c, $"Jog {i}", $"Par {i}"));

        int quantos = CartaoDosResultados.MaximoDeJogos + 3;
        for (int i = 0; i < quantos; i++)
        {
            await JogoAsync(ctx, t, c, duplas[0], duplas[1], 6, i % 5, fase: $"Grupo A",
                fimReal: Sabado.AddHours(8).AddMinutes(i * 30));
        }

        var dia = await ResultadosDoDia.DaCategoriaAsync(ctx, t.Id, c.Id, Sabado);

        Assert.NotNull(dia);
        Assert.Equal(quantos, dia.Total);
        Assert.Equal(CartaoDosResultados.MaximoDeJogos, dia.Jogos.Count);
        Assert.Equal(3, dia.QuantosFicaramDeFora);
    }

    [Fact]
    public void A_frase_do_resto_so_existe_quando_sobra_jogo()
    {
        Assert.Null(CartaoDosResultados.FraseDoResto(0));
        Assert.Equal("e mais 1 jogo", CartaoDosResultados.FraseDoResto(1));
        Assert.Equal("e mais 7 jogos", CartaoDosResultados.FraseDoResto(7));
    }

    // ── O desenho ──────────────────────────────────────────────────────────────────────

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

    private static int PixelsClaros(byte[] png)
    {
        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);
        Assert.Equal(CartaoCompartilhavel.Largura, imagem.Width);
        Assert.Equal(CartaoCompartilhavel.Altura, imagem.Height);

        int claros = 0;
        for (int x = 0; x < imagem.Width; x += 2)
            for (int y = 0; y < imagem.Height; y += 2)
            {
                var c = imagem.GetPixel(x, y);
                if (c.Red > 200 && c.Green > 200 && c.Blue > 200) claros++;
            }
        return claros;
    }

    private static DiaDeResultados DiaDeTeste(int quantos, int total) => new(
        Dia: Sabado,
        CategoriaId: 1,
        Categoria: "Open Masculina",
        Jogos: Enumerable.Range(1, quantos).Select(i => new ResultadoDeJogo(
            Fase: i == 1 ? "Final" : "Grupo A",
            Dupla1: $"Jogador {i} da Silva  &  Parceiro {i} de Souza",
            Dupla2: $"Rival {i} Pereira  &  Colega {i} Antunes",
            Games1: 6, Games2: i % 5, Dupla1Venceu: true, Dupla2Venceu: false)).ToList(),
        Total: total,
        Torneio: "NATA PADEL TOUR",
        Clube: "Arena Beira Rio");

    // ⚠️ "Finalizada sem VencedorId" existe no banco (correção de placar no meio do caminho),
    // e é por isso que os dois lados são campos próprios em vez de um `bool` negado: com a
    // negação, o card coroaria a dupla 2 num jogo que ninguém venceu.
    [Fact]
    public void Jogo_finalizado_sem_vencedor_nao_coroa_ninguem()
    {
        var jogo = new ResultadoDeJogo("Final", "Ana & Bia", "Carla & Dani", 6, 6,
            Dupla1Venceu: false, Dupla2Venceu: false);

        // E o desenho não quebra nesse estado — os dois saem sem destaque, em branco.
        var png = CartaoDosResultados.Desenhar(
            new DiaDeResultados(Sabado, 1, "Open Masculina", new List<ResultadoDeJogo> { jogo },
                Total: 1, "NATA PADEL TOUR", "Arena Beira Rio"),
            new FonteDoCartao(PastaDasFontes()), Path.GetDirectoryName(PastaDasFontes())!);

        Assert.True(PixelsClaros(png) > 200);
    }

    [Fact]
    public void O_card_dos_resultados_desenha_letra_de_verdade()
    {
        var png = CartaoDosResultados.Desenhar(DiaDeTeste(4, 4), new FonteDoCartao(PastaDasFontes()),
            Path.GetDirectoryName(PastaDasFontes())!);

        Assert.True(PixelsClaros(png) > 500);
    }

    [Fact]
    public void Card_cheio_com_resto_ainda_desenha()
    {
        var cheio = DiaDeTeste(CartaoDosResultados.MaximoDeJogos, CartaoDosResultados.MaximoDeJogos + 5);
        var png = CartaoDosResultados.Desenhar(cheio, new FonteDoCartao(PastaDasFontes()),
            Path.GetDirectoryName(PastaDasFontes())!);

        Assert.True(PixelsClaros(png) > 500);
    }
}
