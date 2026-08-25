using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using SkiaSharp;

namespace Padelizou.Tests;

// A CHAVE DO MATA-MATA VIRANDO ÁRVORE DESENHADA.
//
// ⚠️ O CARD MOSTRA NO MÁXIMO TRÊS FASES, e é limite de física, não preguiça: 1080px de
// largura divididos em quatro colunas dariam 250px por nome de dupla, e "Anderson / Charls"
// já ocupa isso. Uma chave de 16 entra a partir das quartas — o "caminho até o título", que é
// o que se posta. Quem quer a chave inteira abre a tela, que tem rolagem.
public class CartaoDaChaveTests
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
            Status = "Mata-Mata",
        };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var categoria = new Categoria { TorneioId = torneio.Id, Nome = "Open Masculina", Codigo = "OPENM" };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        return (ctx, torneio, categoria);
    }

    private static async Task<Dupla> DuplaAsync(DbPadelContext ctx, Categoria c, string n1, string n2)
    {
        var j1 = new Jogador { Nome = n1, Cpf = Guid.NewGuid().ToString("N")[..11] };
        var j2 = new Jogador { Nome = n2, Cpf = Guid.NewGuid().ToString("N")[..11] };
        ctx.Jogadores.AddRange(j1, j2);
        await ctx.SaveChangesAsync();

        var d = new Dupla { CategoriaId = c.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id };
        ctx.Duplas.Add(d);
        await ctx.SaveChangesAsync();
        return d;
    }

    private static async Task JogoAsync(
        DbPadelContext ctx, Torneio t, Categoria c, string fase, Dupla d1, Dupla d2,
        int? g1 = null, int? g2 = null)
    {
        bool terminou = g1 != null && g2 != null;
        ctx.Partidas.Add(new Partida
        {
            TorneioId = t.Id,
            CategoriaId = c.Id,
            Codigo = $"{fase[..3]}{d1.Id}x{d2.Id}",
            Status = terminou ? "Finalizada" : "Agendada",
            Fase = fase,
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            GamesDupla1 = g1,
            GamesDupla2 = g2,
            VencedorId = terminou ? (g1 > g2 ? d1.Id : d2.Id) : null,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Monta_as_fases_da_mais_antiga_pra_final()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var duplas = new List<Dupla>();
        for (int i = 1; i <= 4; i++) duplas.Add(await DuplaAsync(ctx, c, $"Jogador{i} Silva", $"Parceiro{i} Souza"));

        await JogoAsync(ctx, t, c, "Semifinal", duplas[0], duplas[1], 6, 3);
        await JogoAsync(ctx, t, c, "Semifinal", duplas[2], duplas[3], 4, 6);
        await JogoAsync(ctx, t, c, "Final", duplas[0], duplas[3], 6, 2);

        var chave = await ChaveParaCard.DaCategoriaAsync(ctx, t.Id, c.Id);

        Assert.NotNull(chave);
        Assert.True(chave.TemOQueMostrar);
        Assert.Equal(new[] { "Semifinal", "Final" }, chave.Rodadas.Select(r => r.Fase));
        Assert.Equal(2, chave.Rodadas[0].Jogos.Count);
        Assert.Single(chave.Rodadas[1].Jogos);
        Assert.True(chave.Rodadas[1].Jogos[0].Dupla1Venceu);
    }

    // ⚠️ NO MÁXIMO TRÊS COLUNAS. Uma chave de 16 tem oitavas, quartas, semi e final — o card
    // entra nas quartas e deixa as oitavas de fora, porque quatro colunas em 1080px dão 250px
    // por nome de dupla e nenhum nome de padel cabe nisso.
    [Fact]
    public async Task Chave_grande_entra_nas_tres_ultimas_fases()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var duplas = new List<Dupla>();
        for (int i = 1; i <= 16; i++) duplas.Add(await DuplaAsync(ctx, c, $"Jogador{i} Silva", $"Parceiro{i} Souza"));

        for (int i = 0; i < 8; i++) await JogoAsync(ctx, t, c, "Oitavas de Final", duplas[i * 2], duplas[i * 2 + 1], 6, 1);
        for (int i = 0; i < 4; i++) await JogoAsync(ctx, t, c, "Quartas de Final", duplas[i * 4], duplas[i * 4 + 2], 6, 2);
        for (int i = 0; i < 2; i++) await JogoAsync(ctx, t, c, "Semifinal", duplas[i * 8], duplas[i * 8 + 4], 6, 3);
        await JogoAsync(ctx, t, c, "Final", duplas[0], duplas[8], 6, 4);

        var chave = await ChaveParaCard.DaCategoriaAsync(ctx, t.Id, c.Id);

        Assert.NotNull(chave);
        Assert.Equal(ChaveParaCard.MaximoDeFases, chave.Rodadas.Count);
        Assert.Equal(new[] { "Quartas de Final", "Semifinal", "Final" }, chave.Rodadas.Select(r => r.Fase));
        Assert.DoesNotContain(chave.Rodadas, r => r.Fase == "Oitavas de Final");
    }

    // Jogo de grupo não é chave. Sem mata-mata nenhum, não há o que desenhar.
    [Fact]
    public async Task Categoria_so_com_fase_de_grupos_nao_tem_chave()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var a = await DuplaAsync(ctx, c, "Ana Souza", "Bia Lima");
        var b = await DuplaAsync(ctx, c, "Carla Reis", "Dani Alves");
        await JogoAsync(ctx, t, c, "Grupo A", a, b, 6, 2);

        var chave = await ChaveParaCard.DaCategoriaAsync(ctx, t.Id, c.Id);

        Assert.NotNull(chave);
        Assert.False(chave.TemOQueMostrar);
    }

    // ⚠️ JOGO AINDA SEM PLACAR ENTRA NA CHAVE, ao contrário do card de resultados: chave é o
    // caminho, e "quem joga contra quem na semifinal" é justamente o que se posta ANTES de a
    // semifinal acontecer. Sem placar, sem vencedor destacado.
    [Fact]
    public async Task Jogo_marcado_e_ainda_sem_placar_aparece_na_chave()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var duplas = new List<Dupla>();
        for (int i = 1; i <= 4; i++) duplas.Add(await DuplaAsync(ctx, c, $"Jogador{i} Silva", $"Parceiro{i} Souza"));

        await JogoAsync(ctx, t, c, "Semifinal", duplas[0], duplas[1], 6, 3);
        await JogoAsync(ctx, t, c, "Semifinal", duplas[2], duplas[3]);

        var chave = await ChaveParaCard.DaCategoriaAsync(ctx, t.Id, c.Id);

        Assert.NotNull(chave);
        var semis = chave.Rodadas.Single(r => r.Fase == "Semifinal").Jogos;
        Assert.Equal(2, semis.Count);

        var semPlacar = semis[1];
        Assert.Null(semPlacar.Placar);
        Assert.False(semPlacar.Dupla1Venceu);
        Assert.False(semPlacar.Dupla2Venceu);

        Assert.Equal("6 x 3", semis[0].Placar);
        Assert.True(semis[0].Dupla1Venceu);
    }

    [Fact]
    public async Task Torneio_cancelado_nao_gera_chave()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var a = await DuplaAsync(ctx, c, "Ana Souza", "Bia Lima");
        var b = await DuplaAsync(ctx, c, "Carla Reis", "Dani Alves");
        await JogoAsync(ctx, t, c, "Final", a, b, 6, 2);

        t.Status = CancelamentoDoTorneio.Status;
        await ctx.SaveChangesAsync();

        Assert.Null(await ChaveParaCard.DaCategoriaAsync(ctx, t.Id, c.Id));
    }

    // ⚠️ A CICATRIZ DO INNER JOIN, medida aqui pra ninguém redescobrir: `Partida.Dupla1` e
    // `Dupla2` são navegações OBRIGATÓRIAS (`= null!`, FK `int`), então todo `Include` delas
    // vira INNER JOIN — e uma partida cuja dupla não existe SOME da consulta inteira, calada,
    // em vez de aparecer com um lado vazio. É o mesmo mecanismo que fez o `CampeoesDoTorneio`
    // devolver "este torneio não tem campeão" quando o clube não casava.
    //
    // Em produção a FK do Postgres impede esse estado; este teste existe pra dizer que o
    // sumiço é do EF e não do card — e pra travar o `ChaveParaCard.VagaEmAberto` como rede de
    // segurança, não como fluxo esperado.
    [Fact]
    public async Task Partida_com_dupla_inexistente_some_da_chave_em_vez_de_sair_pela_metade()
    {
        var (ctx, t, c) = await MontarAsync();
        using var _ = ctx;

        var a = await DuplaAsync(ctx, c, "Ana Souza", "Bia Lima");
        var b = await DuplaAsync(ctx, c, "Carla Reis", "Dani Alves");

        await JogoAsync(ctx, t, c, "Semifinal", a, b, 6, 2);

        ctx.Partidas.Add(new Partida
        {
            TorneioId = t.Id,
            CategoriaId = c.Id,
            Codigo = "FIN1",
            Status = "Agendada",
            Fase = "Final",
            Dupla1Id = a.Id,
            Dupla2Id = 99999,   // não existe
        });
        await ctx.SaveChangesAsync();

        var chave = await ChaveParaCard.DaCategoriaAsync(ctx, t.Id, c.Id);

        Assert.NotNull(chave);
        // A semifinal continua; a final inteira sumiu junto com a dupla que não existe.
        Assert.Equal(new[] { "Semifinal" }, chave.Rodadas.Select(r => r.Fase));
    }

    // ── O nome que cabe numa coluna de chave ───────────────────────────────────────────

    [Fact]
    public void O_nome_compacto_usa_so_os_primeiros_nomes()
    {
        var anderson = new Jogador { Nome = "Anderson Matteus Schwaab" };
        var charls = new Jogador { Nome = "Charls Gustavio Polese" };

        Assert.Equal("Anderson / Charls", NomeDaDupla.Compacto(null, anderson, charls));
        Assert.Equal("Anderson", NomeDaDupla.Compacto(null, anderson, null));
        // Time continua mandando, como em toda régua de nome de dupla.
        Assert.Equal("Target.it", NomeDaDupla.Compacto("Target.it", anderson, charls));
        Assert.Equal("", NomeDaDupla.Compacto(null, null, null));
    }

    [Fact]
    public void O_apelido_entra_no_compacto_quando_e_o_nome_pelo_qual_a_pessoa_e_conhecida()
    {
        var zeca = new Jogador { Nome = "José Carlos da Silva", Apelido = "Zeca" };
        var diguinho = new Jogador { Nome = "Diego Martins", Apelido = "Diguinho" };

        // Na chave o espaço é curto: vale o apelido sozinho, que é como a quadra chama.
        Assert.Equal("Zeca / Diguinho", NomeDaDupla.Compacto(null, zeca, diguinho));
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

    private static JogoDaChave Jogo(int n) => new(
        $"Jogador{n} / Parceiro{n}", $"Rival{n} / Colega{n}", "6 x 3", Dupla1Venceu: true, Dupla2Venceu: false);

    private static ChaveDesenhavel ChaveDe(params (string Fase, int Jogos)[] rodadas) => new(
        CategoriaId: 1,
        Categoria: "Open Masculina",
        Rodadas: rodadas.Select(r => new RodadaDaChave(
            r.Fase, Enumerable.Range(1, r.Jogos).Select(Jogo).ToList())).ToList(),
        Torneio: "NATA PADEL TOUR",
        Clube: "Arena Beira Rio",
        Data: new DateTime(2026, 8, 22));

    [Fact]
    public void A_chave_de_oito_desenha_letra_de_verdade()
    {
        var png = CartaoDaChave.Desenhar(
            ChaveDe(("Quartas de Final", 4), ("Semifinal", 2), ("Final", 1)),
            new FonteDoCartao(PastaDasFontes()), Path.GetDirectoryName(PastaDasFontes())!);

        Assert.True(PixelsClaros(png) > 500);
    }

    [Fact]
    public void A_chave_de_quatro_tambem_desenha()
    {
        var png = CartaoDaChave.Desenhar(
            ChaveDe(("Semifinal", 2), ("Final", 1)),
            new FonteDoCartao(PastaDasFontes()), Path.GetDirectoryName(PastaDasFontes())!);

        Assert.True(PixelsClaros(png) > 300);
    }

    // Só a final: o caso do torneio pequeno, e o card não pode virar uma coluna solta na
    // borda esquerda.
    [Fact]
    public void So_a_final_ainda_da_um_card()
    {
        var png = CartaoDaChave.Desenhar(
            ChaveDe(("Final", 1)),
            new FonteDoCartao(PastaDasFontes()), Path.GetDirectoryName(PastaDasFontes())!);

        Assert.True(PixelsClaros(png) > 200);
    }
}
