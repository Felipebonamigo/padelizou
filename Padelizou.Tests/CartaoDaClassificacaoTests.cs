using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using SkiaSharp;

namespace Padelizou.Tests;

// A CLASSIFICAÇÃO DA FASE DE GRUPOS VIRANDO ARTE.
//
// ⚠️ A ORDEM NÃO É CALCULADA AQUI. Ela sai de `ClassificacaoDeGrupos.Ordenar`, a régua única
// do sistema — a mesma que o chaveamento usa pra montar a chave. Uma segunda ordenação aqui
// publicaria um card dizendo que a dupla A passou e o chaveamento colocaria a B na semifinal,
// que é exatamente o defeito de 13/08/2026 (a TELA ordenava por conta própria) numa versão
// pior: impressa e postada.
public class CartaoDaClassificacaoTests
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

    private static async Task<Dupla> DuplaAsync(
        DbPadelContext ctx, Categoria categoria, string nome1, string nome2, string grupo)
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
            Grupo = grupo,
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        return dupla;
    }

    private static async Task JogoAsync(
        DbPadelContext ctx, Torneio torneio, Categoria categoria, string grupo,
        Dupla d1, Dupla d2, int games1, int games2)
    {
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Codigo = $"G{grupo}-{d1.Id}x{d2.Id}",
            Status = "Finalizada",
            Fase = $"Grupo {grupo}",
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            GamesDupla1 = games1,
            GamesDupla2 = games2,
            VencedorId = games1 > games2 ? d1.Id : d2.Id,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task A_tabela_do_grupo_sai_na_ordem_da_regua_unica()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        var ana = await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", "A");
        var carla = await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "A");
        var eva = await DuplaAsync(ctx, categoria, "Eva Prado", "Fabi Cruz", "A");

        await JogoAsync(ctx, torneio, categoria, "A", ana, carla, 6, 2);
        await JogoAsync(ctx, torneio, categoria, "A", ana, eva, 6, 4);
        await JogoAsync(ctx, torneio, categoria, "A", carla, eva, 6, 3);

        var grupos = await ClassificacaoParaCard.DaCategoriaAsync(ctx, torneio.Id, categoria.Id);

        var grupoA = Assert.Single(grupos);
        Assert.Equal("A", grupoA.Grupo);
        Assert.Equal(
            new[] { "Ana Souza  &  Bia Lima", "Carla Reis  &  Dani Alves", "Eva Prado  &  Fabi Cruz" },
            grupoA.Linhas.Select(l => l.Dupla));
        Assert.Equal(new[] { 1, 2, 3 }, grupoA.Linhas.Select(l => l.Posicao));
        Assert.Equal(2, grupoA.Linhas[0].Vitorias);
        // Ana: 12 games pró, 6 contra.
        Assert.Equal(6, grupoA.Linhas[0].Saldo);
        Assert.True(grupoA.TemOQueMostrar);
    }

    [Fact]
    public async Task Cada_grupo_vira_um_card()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        var a1 = await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", "A");
        var a2 = await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "A");
        var b1 = await DuplaAsync(ctx, categoria, "Eva Prado", "Fabi Cruz", "B");
        var b2 = await DuplaAsync(ctx, categoria, "Gabi Melo", "Hana Dias", "B");

        await JogoAsync(ctx, torneio, categoria, "A", a1, a2, 6, 1);
        await JogoAsync(ctx, torneio, categoria, "B", b1, b2, 6, 0);

        var grupos = await ClassificacaoParaCard.DaCategoriaAsync(ctx, torneio.Id, categoria.Id);

        Assert.Equal(new[] { "A", "B" }, grupos.Select(g => g.Grupo));
    }

    // ⚠️ TABELA ZERADA NÃO VIRA ARTE. A classificação existe desde o sorteio (decisão de
    // 12/08/2026 pro Americano) e é o certo NA TELA — quem entra procura o próprio nome. Mas
    // um card postado no Instagram com todo mundo em 0 anuncia uma etapa que não aconteceu.
    [Fact]
    public async Task Grupo_sem_jogo_terminado_nao_vira_arte()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", "A");
        await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "A");

        var grupos = await ClassificacaoParaCard.DaCategoriaAsync(ctx, torneio.Id, categoria.Id);

        var grupoA = Assert.Single(grupos);
        Assert.False(grupoA.TemOQueMostrar);
    }

    // Jogo lançado sem placar não conta como etapa disputada — `VencedorId` nulo é jogo que
    // ainda não terminou.
    [Fact]
    public async Task Jogo_sem_vencedor_nao_conta_como_etapa_disputada()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        var a1 = await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", "A");
        var a2 = await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "A");

        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Codigo = "GA-1",
            Status = "Agendada",
            Fase = "Grupo A",
            Dupla1Id = a1.Id,
            Dupla2Id = a2.Id,
        });
        await ctx.SaveChangesAsync();

        var grupos = await ClassificacaoParaCard.DaCategoriaAsync(ctx, torneio.Id, categoria.Id);

        Assert.False(Assert.Single(grupos).TemOQueMostrar);
    }

    // ⚠️ JOGO AO VIVO NÃO ENTRA NA TABELA DO CARD, e este é o teste que mais vale aqui: a
    // TELA de classificação filtra `Status == "Finalizada"`, e um card que somasse o placar
    // parcial de um jogo em andamento mostraria uma classificação que discorda da tela que o
    // gerou — impressa e postada, fora do nosso alcance pra corrigir. O 6x0 do primeiro set
    // de um jogo ao vivo não é campanha de ninguém.
    [Fact]
    public async Task Jogo_ao_vivo_com_placar_parcial_nao_entra_no_card()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        var a1 = await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", "A");
        var a2 = await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "A");
        var a3 = await DuplaAsync(ctx, categoria, "Eva Prado", "Fabi Cruz", "A");

        // Um jogo terminado de verdade: a Ana venceu a Carla.
        await JogoAsync(ctx, torneio, categoria, "A", a1, a2, 6, 2);

        // E um EM ANDAMENTO, com o primeiro set já 6x0 pra Eva.
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Codigo = "GA-AOVIVO",
            Status = "AoVivo",
            Fase = "Grupo A",
            Dupla1Id = a3.Id,
            Dupla2Id = a2.Id,
            GamesDupla1 = 6,
            GamesDupla2 = 0,
        });
        await ctx.SaveChangesAsync();

        var grupoA = Assert.Single(await ClassificacaoParaCard.DaCategoriaAsync(ctx, torneio.Id, categoria.Id));

        // A Eva não jogou nada que conte: sem jogos, sem saldo, e no fim da tabela.
        var eva = grupoA.Linhas.Single(l => l.Dupla.StartsWith("Eva"));
        Assert.Equal(0, eva.Jogos);
        Assert.Equal(0, eva.Saldo);
        Assert.Equal(0, eva.Vitorias);

        // E a Carla continua com o saldo só do jogo que terminou (-4), sem os 6 games que
        // levou no jogo em andamento.
        var carla = grupoA.Linhas.Single(l => l.Dupla.StartsWith("Carla"));
        Assert.Equal(-4, carla.Saldo);
    }

    // Categoria de mata-mata direto (ou Americano) não tem grupo nenhum — nada a desenhar.
    [Fact]
    public async Task Categoria_sem_grupos_nao_gera_card()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", null!);

        var grupos = await ClassificacaoParaCard.DaCategoriaAsync(ctx, torneio.Id, categoria.Id);

        Assert.Empty(grupos);
    }

    [Fact]
    public async Task Torneio_cancelado_nao_gera_card()
    {
        var (ctx, torneio, categoria) = await MontarAsync();
        using var _ = ctx;

        var a1 = await DuplaAsync(ctx, categoria, "Ana Souza", "Bia Lima", "A");
        var a2 = await DuplaAsync(ctx, categoria, "Carla Reis", "Dani Alves", "A");
        await JogoAsync(ctx, torneio, categoria, "A", a1, a2, 6, 1);

        torneio.Status = CancelamentoDoTorneio.Status;
        await ctx.SaveChangesAsync();

        Assert.Empty(await ClassificacaoParaCard.DaCategoriaAsync(ctx, torneio.Id, categoria.Id));
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

    private static GrupoClassificado GrupoDeTeste(int quantasDuplas) => new(
        Grupo: "A",
        CategoriaId: 1,
        Categoria: "Open Masculina",
        Linhas: Enumerable.Range(1, quantasDuplas).Select(i => new LinhaDaClassificacao(
            Posicao: i, Dupla: $"Dupla {i} da Silva  &  Parceiro {i} de Souza",
            Jogos: 3, Vitorias: 4 - i, Saldo: 10 - i * 4)).ToList(),
        Torneio: "NATA PADEL TOUR",
        Clube: "Arena Beira Rio",
        Data: new DateTime(2026, 8, 22));

    [Fact]
    public void A_campanha_sai_com_o_saldo_assinado()
    {
        Assert.Equal("3V  ·  +8", CartaoDaClassificacao.Campanha(vitorias: 3, saldo: 8));
        Assert.Equal("1V  ·  -4", CartaoDaClassificacao.Campanha(vitorias: 1, saldo: -4));
        // Saldo zero não leva sinal: "+0" e "-0" são a mesma coisa e as duas ficam feias.
        Assert.Equal("2V  ·  0", CartaoDaClassificacao.Campanha(vitorias: 2, saldo: 0));
    }

    [Fact]
    public void O_card_da_classificacao_desenha_letra_de_verdade()
    {
        var png = CartaoDaClassificacao.Desenhar(GrupoDeTeste(4), new FonteDoCartao(PastaDasFontes()),
            Path.GetDirectoryName(PastaDasFontes())!);

        Assert.True(PixelsClaros(png) > 500);
    }

    // ⚠️ Grupo grande é o caso que quebra card de tabela: oito duplas com nome comprido
    // precisam caber sem o texto sair pela borda nem uma linha subir por cima da outra.
    [Fact]
    public void Grupo_grande_ainda_cabe_no_card()
    {
        var png = CartaoDaClassificacao.Desenhar(GrupoDeTeste(8), new FonteDoCartao(PastaDasFontes()),
            Path.GetDirectoryName(PastaDasFontes())!);

        Assert.True(PixelsClaros(png) > 500);
    }
}
