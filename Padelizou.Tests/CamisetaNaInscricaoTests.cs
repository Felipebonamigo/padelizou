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

// 04/11/2026 — O TAMANHO AO LADO DO NOME NA LISTA DE INSCRITOS. 🗣️ Felipe, com o print da lista
// de duplas: *"coloque a camiseta e o tamanho ao lado do nome, para saber que já selecionou"*.
//
// ⚠️ Tamanho de roupa é dado pessoal: quem vê é o ORGANIZADOR (é pra ele encomendar) e quem é
// da MESMA inscrição (o próprio atleta e o parceiro). Pra quem só passou na página, nada.
public class CamisetaAoLadoDoNomeTests
{
    [Fact]
    public void Organizador_ve_o_tamanho_de_todo_mundo()
    {
        Assert.True(CamisetaDeBrinde.PodeVerNaLista(podeGerenciar: true, quemOlha: 99, daInscricao: new int?[] { 1, 2 }));
    }

    [Fact]
    public void Atleta_ve_o_seu_e_o_do_parceiro()
    {
        Assert.True(CamisetaDeBrinde.PodeVerNaLista(false, quemOlha: 2, daInscricao: new int?[] { 1, 2 }));
    }

    [Fact]
    public void Quem_e_de_outra_dupla_ou_nao_esta_logado_nao_ve()
    {
        Assert.False(CamisetaDeBrinde.PodeVerNaLista(false, quemOlha: 3, daInscricao: new int?[] { 1, 2 }));
        // Dupla sem parceiro tem Jogador2Id nulo: o visitante deslogado (nulo) não pode casar com ele.
        Assert.False(CamisetaDeBrinde.PodeVerNaLista(false, quemOlha: null, daInscricao: new int?[] { 1, null }));
    }

    [Fact]
    public async Task A_pagina_do_torneio_entrega_o_tamanho_de_cada_atleta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.OfereceCamiseta = true;
        torneio.TamanhosDeCamiseta = "M,G";
        var dupla = ctx.Duplas.Single();
        ctx.CamisetasDosAtletas.Add(new CamisetaDoAtleta { TorneioId = torneio.Id, JogadorId = dupla.Jogador1Id, Tamanho = "G" });
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoTorneiosController(ctx, dupla.Jogador1Id).Details(torneio.Id, null, null);

        var view = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado);
        var tamanhos = Assert.IsAssignableFrom<IReadOnlyDictionary<int, string>>(view.ViewData["CamisetasPorJogador"]);
        Assert.Equal("G", tamanhos[dupla.Jogador1Id]);
    }

    [Fact]
    public void A_lista_de_duplas_e_a_do_americano_mostram_o_selo()
    {
        var details = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Torneios", "Details.cshtml"));

        Assert.Contains("_CamisetaAoLadoDoNome", details);
        Assert.True(details.Split("_CamisetaAoLadoDoNome").Length - 1 >= 3,
            "O selo precisa estar no jogador 1, no jogador 2 e na inscrição do Americano.");
    }
}
