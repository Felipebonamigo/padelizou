using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 04/11/2026 — A CAMISETA SE ESCOLHE NA PRÓPRIA INSCRIÇÃO.
//
// 🗣️ Felipe, com o print do formulário de inscrição: *"não achei aonde escolho minha camiseta"*.
// A primeira versão (CamisetaDeBrindeTests) só deixava escolher DEPOIS de inscrito, num card no
// topo da página — e quem está se inscrevendo procura o tamanho ali, no formulário, junto com o
// resto. Agora cada jogador do formulário tem o campo; o card de depois continua servindo pra
// trocar e pra quem foi avisado já inscrito.
//
// ⚠️ A camiseta nunca derruba a inscrição: tamanho que não é oferecido, prazo vencido ou brinde
// desligado são ignorados em silêncio. A vaga vale mais que o brinde.
public class CamisetaNaInscricaoTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador a, Jogador b)> MontarDuplasAsync(
        bool ofereceCamiseta = true)
    {
        var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio
        {
            Nome = "3ª Etapa EPT", Codigo = "EPT3", Status = "Inscrições Abertas",
            DataInicio = new DateTime(2026, 12, 12),
            OfereceCamiseta = ofereceCamiseta,
            TamanhosDeCamiseta = ofereceCamiseta ? "P,M,G,GG,XG,BLP,BLM" : null,
        };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);

        // CPF com dígito verificador de verdade: o Create recusa CPF inválido antes de tudo.
        var a = TestInfra.NovoJogador(81);
        a.Cpf = "11144477735"; a.Nome = "Felipe Bonamigo";
        var b = TestInfra.NovoJogador(82);
        b.Cpf = "52998224725"; b.Nome = "Maickel Klein";
        ctx.Jogadores.AddRange(a, b);
        await ctx.SaveChangesAsync();
        return (ctx, torneio, categoria, a, b);
    }

    private static Task InscreverDupla(DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador a, Jogador b,
        string? camiseta1, string? camiseta2) =>
        TestInfra.NovoDuplasController(ctx, a.Id).Create(
            torneioId: torneio.Id, categoriaId: categoria.Id,
            nome1: a.Nome, cpf1: a.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            camiseta1: camiseta1, camiseta2: camiseta2);

    private static async Task<string?> TamanhoDe(DbPadelContext ctx, int torneioId, int jogadorId)
    {
        ctx.ChangeTracker.Clear();
        return (await ctx.CamisetasDosAtletas.SingleOrDefaultAsync(c => c.TorneioId == torneioId && c.JogadorId == jogadorId))?.Tamanho;
    }

    [Fact]
    public async Task Inscricao_da_dupla_grava_a_camiseta_de_cada_um()
    {
        var (ctx, torneio, categoria, a, b) = await MontarDuplasAsync();

        await InscreverDupla(ctx, torneio, categoria, a, b, "XG", "BLM");

        Assert.Single(await ctx.Duplas.ToListAsync());
        Assert.Equal("XG", await TamanhoDe(ctx, torneio.Id, a.Id));
        Assert.Equal("BLM", await TamanhoDe(ctx, torneio.Id, b.Id));
    }

    [Fact]
    public async Task Sem_tamanho_marcado_a_inscricao_segue_sem_camiseta()
    {
        // Opcional: quem não escolhe se inscreve igual.
        var (ctx, torneio, categoria, a, b) = await MontarDuplasAsync();

        await InscreverDupla(ctx, torneio, categoria, a, b, "M", null);

        Assert.Single(await ctx.Duplas.ToListAsync());
        Assert.Equal("M", await TamanhoDe(ctx, torneio.Id, a.Id));
        Assert.Null(await TamanhoDe(ctx, torneio.Id, b.Id));
    }

    [Fact]
    public async Task Tamanho_que_nao_e_oferecido_e_ignorado_e_a_inscricao_entra()
    {
        // POST montado à mão (ou aba velha): a camiseta não derruba a vaga.
        var (ctx, torneio, categoria, a, b) = await MontarDuplasAsync();

        await InscreverDupla(ctx, torneio, categoria, a, b, "XGG", null);

        Assert.Single(await ctx.Duplas.ToListAsync());
        Assert.Null(await TamanhoDe(ctx, torneio.Id, a.Id));
    }

    [Fact]
    public async Task Torneio_sem_brinde_ignora_o_tamanho()
    {
        var (ctx, torneio, categoria, a, b) = await MontarDuplasAsync(ofereceCamiseta: false);

        await InscreverDupla(ctx, torneio, categoria, a, b, "M", "M");

        Assert.Single(await ctx.Duplas.ToListAsync());
        Assert.Empty(await ctx.CamisetasDosAtletas.ToListAsync());
    }

    [Fact]
    public async Task Inscricao_individual_grava_a_camiseta()
    {
        var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio
        {
            Nome = "Americano", Codigo = "AMC", Status = "Inscrições Abertas",
            Formato = "Americano", QuantidadeQuadras = 2, TempoPrevistoPartidaMinutos = 20,
            DataInicio = new DateTime(2026, 12, 12, 19, 0, 0),
            OfereceCamiseta = true, TamanhosDeCamiseta = "P,M,G",
        };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "Geral", Codigo = "GER", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        var jogador = new Jogador { Nome = "Fulano Beltrano", Cpf = "52998224725" };
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();

        await TestInfra.NovoTorneiosController(ctx, jogador.Id)
            .InscreverIndividual(torneio.Id, categoria.Id, jogador.Nome, jogador.Cpf, camiseta: "G");

        Assert.Single(await ctx.InscricoesAmericanas.ToListAsync());
        Assert.Equal("G", await TamanhoDe(ctx, torneio.Id, jogador.Id));
    }

    [Fact]
    public void O_formulario_tem_o_campo_em_cada_jogador_e_na_inscricao_individual()
    {
        var details = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Torneios", "Details.cshtml"));

        Assert.Contains("[\"CampoCamiseta\"] = \"camiseta1\"", details);
        Assert.Contains("[\"CampoCamiseta\"] = \"camiseta2\"", details);
        Assert.Contains("[\"CampoCamiseta\"] = \"camiseta\"", details);
    }
}
