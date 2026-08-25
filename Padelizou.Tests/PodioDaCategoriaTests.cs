using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O PÓDIO DE UMA CATEGORIA — campeão, vice e semifinalistas.
//
// ⚠️ ELE NÃO CONTA NADA: lê os carimbos que o mata-mata JÁ deixou em `Dupla.UltimaFase`
// (perdedor recebe a fase em que caiu — ver PartidasController e TorneiosController.Placar).
// Recalcular quem perdeu a final a partir das partidas seria a segunda régua a decidir
// pódio, e este projeto tem a cicatriz de ter feito isso com "quem venceu" (05/08/2026).
//
// ⚠️ AMERICANO NÃO TEM PÓDIO AQUI, e é decisão, não esquecimento: lá o campeão sai da
// CLASSIFICAÇÃO, não de uma final, e ninguém carimba "Final" em ninguém — as outras duplas
// ficam em "Grupos". Um pódio montado no braço a partir da tabela seria uma terceira régua
// de classificação. O card de CAMPEÃO já cobre esse formato, e é o formato certo pra ele
// (no Americano quem ganha é uma pessoa, não uma dupla).
public class PodioDaCategoriaTests
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
            Status = "Finalizado",
        };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var categoria = new Categoria { TorneioId = torneio.Id, Nome = "Open Masculina", Codigo = "OPENM" };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        return (ctx, torneio, categoria);
    }

    private static async Task<Dupla> DuplaAsync(
        DbPadelContext ctx, Categoria categoria, string nome1, string nome2, string fase)
    {
        var j1 = new Jogador { Nome = nome1, Cpf = Guid.NewGuid().ToString("N")[..11] };
        var j2 = new Jogador { Nome = nome2, Cpf = Guid.NewGuid().ToString("N")[..11] };
        ctx.Jogadores.AddRange(j1, j2);
        await ctx.SaveChangesAsync();

        var dupla = new Dupla
        {
            CategoriaId = categoria.Id,
            Jogador1Id = j1.Id,
            Jogador2Id = j2.Id,
            UltimaFase = fase,
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        return dupla;
    }

    [Fact]
    public async Task Le_campeao_vice_e_semifinalistas_dos_carimbos_que_ja_existem()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", "Campeao");
        await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "Final");
        await DuplaAsync(ctx, categoria, "Eva Prado", "Fabi Cruz", "Semifinal");
        await DuplaAsync(ctx, categoria, "Gabi Melo", "Hana Dias", "Semifinal");
        // Quem caiu nos grupos não é pódio.
        await DuplaAsync(ctx, categoria, "Iva Nunes", "Julia Paz", "Grupos");

        var podio = await PodioDaCategoria.DaCategoriaAsync(ctx, torneio.Id, categoria.Id);

        Assert.NotNull(podio);
        Assert.Equal("Ana Souza  &  Bia Lima", podio.Campeao);
        Assert.Equal("Carla Reis  &  Dani Alves", podio.Vice);
        Assert.Equal(2, podio.Semifinalistas.Count);
        Assert.Contains("Eva Prado  &  Fabi Cruz", podio.Semifinalistas);
        Assert.Equal("NATA PADEL TOUR", podio.Torneio);
        Assert.Equal("Arena Beira Rio", podio.Clube);
        Assert.True(podio.TemOQueMostrar);
    }

    // ⚠️ Sem vice não vira arte — é o caso do Americano e o da categoria que parou no meio.
    // Um "pódio" com um degrau só é o card de campeão, que já existe.
    [Fact]
    public async Task Sem_vice_nao_vira_podio()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", "Campeao");

        var podio = await PodioDaCategoria.DaCategoriaAsync(ctx, torneio.Id, categoria.Id);

        Assert.NotNull(podio);
        Assert.False(podio.TemOQueMostrar);
    }

    [Fact]
    public async Task Sem_campeao_nao_vira_podio()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "Final");

        var podio = await PodioDaCategoria.DaCategoriaAsync(ctx, torneio.Id, categoria.Id);

        Assert.NotNull(podio);
        Assert.False(podio.TemOQueMostrar);
    }

    // A mesma régua do card de campeão: torneio cancelado não anuncia nada, mesmo tendo tido
    // final jogada antes do cancelamento.
    [Fact]
    public async Task Torneio_cancelado_nao_tem_podio()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", "Campeao");
        await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "Final");

        torneio.Status = CancelamentoDoTorneio.Status;
        await ctx.SaveChangesAsync();

        var podio = await PodioDaCategoria.DaCategoriaAsync(ctx, torneio.Id, categoria.Id);

        Assert.Null(podio);
    }

    // ⚠️ Linha de TIME não estampa nome de jogador: no time o `Jogador1Id` aponta pro
    // organizador que cadastrou, e coroá-lo seria premiar quem não jogou. Mesma armadilha
    // que já tirou a dupla-TIME de todo somatório de ranking.
    [Fact]
    public async Task Dupla_de_time_aparece_pelo_nome_do_time()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        var organizador = new Jogador { Nome = "Quem Cadastrou", Cpf = "99999999999" };
        ctx.Jogadores.Add(organizador);
        await ctx.SaveChangesAsync();

        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id,
            Jogador1Id = organizador.Id,
            NomeTime = "Target.it",
            UltimaFase = "Campeao",
        });
        await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "Final");
        await ctx.SaveChangesAsync();

        var podio = await PodioDaCategoria.DaCategoriaAsync(ctx, torneio.Id, categoria.Id);

        Assert.NotNull(podio);
        Assert.Equal("Target.it", podio.Campeao);
        Assert.DoesNotContain("Quem Cadastrou", podio.Campeao);
    }

    // A PÁGINA do torneio precisa saber se mostra o botão, e ela já tem categorias e duplas
    // carregadas — a pergunta não pode custar uma segunda ida ao banco nem virar um
    // `UltimaFase == "Campeao"` escrito na view (a segunda cópia da regra que decide pódio).
    [Fact]
    public async Task A_pagina_do_torneio_so_mostra_o_botao_quando_ha_campeao_E_vice()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", "Campeao");

        var comCampeaoSo = await ctx.Torneios
            .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
            .FirstAsync(t => t.Id == torneio.Id);
        Assert.False(PodioDaCategoria.TemPodio(comCampeaoSo));

        await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "Final");

        ctx.ChangeTracker.Clear();
        var comVice = await ctx.Torneios
            .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
            .FirstAsync(t => t.Id == torneio.Id);
        Assert.True(PodioDaCategoria.TemPodio(comVice));

        // Cancelado não anuncia nada, mesma régua do card de campeão.
        comVice.Status = CancelamentoDoTorneio.Status;
        Assert.False(PodioDaCategoria.TemPodio(comVice));
    }

    // O nome da dupla tem UMA régua no sistema, e o card de campeão passou a comer dela.
    [Fact]
    public void A_regua_do_nome_da_dupla_e_uma_so()
    {
        var ana = new Jogador { Nome = "Ana Souza" };
        var bia = new Jogador { Nome = "Bia Lima" };

        Assert.Equal("Ana Souza  &  Bia Lima", NomeDaDupla.De(null, ana, bia));
        Assert.Equal("Ana Souza", NomeDaDupla.De(null, ana, null));
        Assert.Equal("Target.it", NomeDaDupla.De("Target.it", ana, bia));
        Assert.Equal("", NomeDaDupla.De(null, null, null));

        // E o card de campeão continua respondendo o mesmo, pelo mesmo caminho.
        var campeao = new CampeaoDeCategoria(1, "Open", 1, null, null, ana, bia, "T", null, null);
        Assert.Equal("Ana Souza  &  Bia Lima", campeao.Nomes);
    }
}

// O DESENHO DO PÓDIO. Mesma prova dos outros cards: pixels de LETRA na imagem final, porque
// fonte sem glifo desenha um card inteiro, mudo, sem erro nenhum.
public class CartaoDoPodioTests
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

    private static int PixelsClaros(byte[] png)
    {
        using var imagem = SkiaSharp.SKBitmap.Decode(png);
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

    private static PodioDeCategoria Podio(params string[] semifinalistas) => new(
        CategoriaId: 1,
        Categoria: "Open Masculina",
        Campeao: "Ana Souza  &  Bia Lima",
        Vice: "Carla Reis  &  Dani Alves",
        Semifinalistas: semifinalistas.ToList(),
        Torneio: "NATA PADEL TOUR",
        Clube: "Arena Beira Rio",
        Data: new DateTime(2026, 8, 22));

    [Fact]
    public void Os_semifinalistas_saem_numa_linha_so()
    {
        Assert.Equal(
            "Eva & Fabi   ·   Gabi & Hana",
            CartaoDoPodio.LinhaDosSemifinalistas(new List<string> { "Eva & Fabi", "Gabi & Hana" }));
    }

    // Categoria de 4 duplas não tem semifinal — a seção some inteira em vez de sair com um
    // rótulo em cima do nada.
    [Fact]
    public void Sem_semifinalista_nao_ha_linha()
    {
        Assert.Null(CartaoDoPodio.LinhaDosSemifinalistas(new List<string>()));
    }

    [Fact]
    public void O_card_do_podio_desenha_letra_de_verdade()
    {
        var png = CartaoDoPodio.Desenhar(Podio("Eva & Fabi", "Gabi & Hana"), Fontes(), WebRoot());
        Assert.True(PixelsClaros(png) > 500);
    }

    [Fact]
    public void Sem_semifinalistas_e_sem_clube_o_card_continua_inteiro()
    {
        var enxuto = Podio() with { Clube = null };
        var png = CartaoDoPodio.Desenhar(enxuto, Fontes(), WebRoot());
        Assert.True(PixelsClaros(png) > 300);
    }
}
