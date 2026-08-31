using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 31/08/2026 — O SELO DE CAMPEÃO NA LISTA DE INSCRITOS MENTIA DUAS VEZES, num print do Felipe:
// *"nao pode ter esse trofeu aqui assim. Primeiro que nao é categoria Madeira, tem q ser a
// categoria que o atleta jogou (7a feminina, 7 masculina e assim por diante) e esse trofeu é de
// americano, esse nao deve aparecer como se fosse os torneios 'normais'/'Oficiais'"*.
//
// 1️⃣ O `title` dizia "na categoria Madeira". Madeira é o MATERIAL do troféu (a escada
//    diamante > ouro > … > plástico de Services/TrofeuDeMaterial), não uma categoria — ninguém
//    se inscreve na "Madeira", se inscreve na 6ª Feminina. O selo agrupava por material, então
//    era o material que sobrava pra escrever no rótulo.
//
// 2️⃣ E agrupar por MATERIAL vazava categoria: 6ª Masculina e 6ª Feminina são a mesma madeira,
//    então o título de uma aparecia no chip da outra. O rótulo errado escondia o vazamento.
//
// 3️⃣ O título era de AMERICANO. O rodízio não tem chave, não tem final e não tem "chegar na
//    semi" — carimbar o campeão dele com o mesmo selo do campeão de mata-mata faz o Americano
//    da 6ª se passar por título da 6ª Categoria. A prateleira do perfil já separava os dois
//    (TrofeuDeMaterial.Contar, 08/08/2026); este selo não separava.
//    ⚠️ O título do Americano NÃO some do sistema: continua na prateleira do perfil, de vidro
//    e marcado. O que ele não faz é aparecer neste selo, que é histórico de CHAVE.
public class SeloDeCampeaoDaCategoriaTests
{
    private const string SextaFeminina = "6ª Categoria Feminina";
    private const string SextaMasculina = "6ª Categoria Masculina";

    private static Torneio NovoTorneio(DbPadelContext ctx, string codigo, string formato)
    {
        var t = new Torneio
        {
            Nome = "Copa " + codigo,
            Codigo = codigo,
            Status = "Finalizado",
            Formato = formato,
            DataInicio = new DateTime(2026, 5, 1),
        };
        ctx.Torneios.Add(t);
        return t;
    }

    // Campeã de uma categoria num torneio de um formato. Devolve a jogadora coroada.
    private static Jogador CampeaDe(DbPadelContext ctx, string cpf, string categoria, string formato)
    {
        var torneio = NovoTorneio(ctx, "T" + cpf.Substring(cpf.Length - 5), formato);
        var cat = new Categoria { Nome = categoria, Codigo = "C" + cpf.Substring(cpf.Length - 4), Torneio = torneio };
        var jogadora = new Jogador { Nome = "Campeã " + cpf, Cpf = cpf };
        ctx.AddRange(cat, jogadora);
        ctx.Duplas.Add(new Dupla { Categoria = cat, Jogador1 = jogadora, Jogador2Id = null, UltimaFase = "Campeao" });
        ctx.SaveChanges();
        return jogadora;
    }

    // ── 1. O RÓTULO ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_selo_e_indexado_pela_CATEGORIA_jogada_e_nao_pelo_material_do_trofeu()
    {
        var ctx = TestInfra.NovoContexto();
        var jogadora = CampeaDe(ctx, "88800000001", SextaFeminina, FormatoDoTorneio.Padrao);

        var mapa = await new EstatisticasService(ctx).ObterMelhoresColocacoesAsync(new[] { SextaFeminina });

        // A chave do dicionário é o que o chip procura, e o rótulo é o que a pessoa lê.
        // "Madeira" não pode ser nem uma coisa nem outra.
        Assert.True(mapa[jogadora.Id].ContainsKey(SextaFeminina));
        Assert.DoesNotContain("Madeira", mapa[jogadora.Id].Keys);
        Assert.Equal(SextaFeminina, mapa[jogadora.Id][SextaFeminina].CategoriaNome);
    }

    [Fact]
    public async Task A_cor_do_selo_continua_saindo_do_material_da_categoria()
    {
        // Trocar a CHAVE não pode apagar a escada: a pílula da 6ª segue com a cor da madeira.
        var ctx = TestInfra.NovoContexto();
        var jogadora = CampeaDe(ctx, "88800000002", SextaFeminina, FormatoDoTorneio.Padrao);

        var selo = (await new EstatisticasService(ctx)
            .ObterMelhoresColocacoesAsync(new[] { SextaFeminina }))[jogadora.Id][SextaFeminina];

        Assert.Equal(TrofeuDeMaterial.Madeira.CorFundo, selo.CorFundoTier);
        Assert.Equal(TrofeuDeMaterial.Madeira.CorTexto, selo.CorTextoTier);
    }

    // ── 2. O VAZAMENTO ENTRE CATEGORIAS DO MESMO MATERIAL ──────────────────────────────────

    [Fact]
    public async Task Titulo_na_6a_FEMININA_nao_aparece_no_chip_da_6a_MASCULINA()
    {
        // As duas são madeira. Agrupadas por material, o título de uma coroava a outra.
        var ctx = TestInfra.NovoContexto();
        var jogadora = CampeaDe(ctx, "88800000003", SextaFeminina, FormatoDoTorneio.Padrao);

        var mapa = await new EstatisticasService(ctx)
            .ObterMelhoresColocacoesAsync(new[] { SextaFeminina, SextaMasculina });

        Assert.True(mapa[jogadora.Id].ContainsKey(SextaFeminina));
        Assert.False(mapa[jogadora.Id].ContainsKey(SextaMasculina));
    }

    // ── 3. O AMERICANO FORA DO SELO DE CHAVE ───────────────────────────────────────────────

    [Theory]
    [InlineData(FormatoDoTorneio.Americano)]
    [InlineData(FormatoDoTorneio.AmericanoDeDuplas)]
    public async Task Titulo_de_Americano_NAO_vira_selo_de_campeao_da_categoria(string formato)
    {
        var ctx = TestInfra.NovoContexto();
        var jogadora = CampeaDe(ctx, "88800000004", SextaFeminina, formato);

        var mapa = await new EstatisticasService(ctx).ObterMelhoresColocacoesAsync(new[] { SextaFeminina });

        // Nem selo de título, nem selo de "melhor campanha": o rodízio não tem fase de chave.
        Assert.False(mapa.ContainsKey(jogadora.Id));
    }

    [Fact]
    public async Task Titulo_de_torneio_de_CHAVE_continua_virando_selo()
    {
        // A guarda do outro lado: sem ela, "sumiu o troféu do Americano" passa junto com
        // "sumiu o troféu de todo mundo".
        var ctx = TestInfra.NovoContexto();
        var jogadora = CampeaDe(ctx, "88800000005", SextaFeminina, FormatoDoTorneio.Padrao);

        var mapa = await new EstatisticasService(ctx).ObterMelhoresColocacoesAsync(new[] { SextaFeminina });

        Assert.Equal(1, mapa[jogadora.Id][SextaFeminina].Titulos);
    }

    [Fact]
    public async Task O_Americano_nao_derruba_o_titulo_de_chave_da_MESMA_jogadora()
    {
        // O caso do print: a mesma pessoa tem os dois. Sobra um selo, o da chave, com 1 título.
        var ctx = TestInfra.NovoContexto();
        var jogadora = CampeaDe(ctx, "88800000007", SextaFeminina, FormatoDoTorneio.Padrao);

        var americano = NovoTorneio(ctx, "AMER07", FormatoDoTorneio.Americano);
        var catAmericano = new Categoria { Nome = SextaFeminina, Codigo = "CAM07", Torneio = americano };
        ctx.Add(catAmericano);
        ctx.Duplas.Add(new Dupla { Categoria = catAmericano, Jogador1Id = jogadora.Id, UltimaFase = "Campeao" });
        ctx.SaveChanges();

        var mapa = await new EstatisticasService(ctx).ObterMelhoresColocacoesAsync(new[] { SextaFeminina });

        Assert.Equal(1, mapa[jogadora.Id][SextaFeminina].Titulos);
    }

    // ── 4. AS GUARDAS DE TELA ──────────────────────────────────────────────────────────────
    // A suíte não renderiza Razor: trocar `CategoriaNome` de volta por `TierNome` no `title`
    // não deixaria nenhum teste de comportamento vermelho. A única rede é ler o arquivo.

    [Fact]
    public void O_title_do_selo_nomeia_a_categoria_e_nunca_o_material()
    {
        var view = LerDaWeb("Views", "Torneios", "_SeloHistorico.cshtml");

        Assert.Contains("na categoria @Model.CategoriaNome", view);
        Assert.DoesNotContain("@Model.TierNome", view);
    }

    [Fact]
    public void O_chip_procura_o_historico_pelo_NOME_da_categoria_atual()
    {
        var view = LerDaWeb("Views", "Torneios", "_JogadorChip.cshtml");

        Assert.Contains("TryGetValue(categoriaAtual", view);
        Assert.DoesNotContain("TierDaCategoria(categoriaAtual)", view);
    }

    // ── 5. A CONSULTA VIRA SQL DE VERDADE ──────────────────────────────────────────────────
    // O InMemory do resto da suíte não traduz nada: o filtro novo atravessa DOIS níveis de
    // navegação (Dupla → Categoria → Torneio) e passaria liso pelos 5 mil testes verdes pra
    // estourar na primeira abertura de torneio em produção — foi assim em 19/08/2026.
    // `ToQueryString()` compila sem abrir conexão nenhuma.

    [Fact]
    public void O_filtro_de_formato_atravessa_Categoria_ate_Torneio_e_vira_SQL()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        using var ctx = new DbPadelContext(options);

        var nomes = new[] { SextaFeminina }.ToHashSet();
        int? excluirTorneioId = 7;

        // Mesma forma exata de EstatisticasService.ObterMelhoresColocacoesAsync.
        var consulta = ctx.Duplas
            .Include(d => d.Categoria)
            .Where(d => d.NomeTime == null
                     && nomes.Contains(d.Categoria.Nome)
                     && d.Categoria.Torneio.Formato != FormatoDoTorneio.Americano
                     && d.Categoria.Torneio.Formato != FormatoDoTorneio.AmericanoDeDuplas
                     && (excluirTorneioId == null || d.Categoria.TorneioId != excluirTorneioId))
            .Select(d => new { d.Jogador1Id, d.Jogador2Id, d.UltimaFase, CategoriaNome = d.Categoria.Nome });

        var sql = consulta.ToQueryString();

        Assert.Contains("SELECT", sql);
        // O JOIN com Torneio precisa estar lá: sem ele o filtro de formato teria virado
        // avaliação em memória (ou nem existiria), e o Americano voltaria calado.
        Assert.Contains("Torneio", sql);
    }

    private static string LerDaWeb(params string[] caminho)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return File.ReadAllText(Path.Combine(dir.FullName, "Padelizou", Path.Combine(caminho)));
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
