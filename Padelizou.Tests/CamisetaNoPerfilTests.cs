using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 04/11/2026 — O TAMANHO DA CAMISETA FICA NO PERFIL. 🗣️ Felipe: *"já deixe salvo no perfil da
// pessoa o tamanho da camiseta, para uso futuro em outros torneios"*.
//
// ⚠️ QUEM ESCREVE NO PERFIL: a própria pessoa sobrescreve; quem escolhe POR ELA (o parceiro na
// inscrição da dupla) só preenche perfil vazio — mesma régua do celular e da cidade que a
// inscrição já completa (DuplasController.Create). Sem isso, o palpite de um parceiro apagaria
// o tamanho que a pessoa mesma escolheu.
public class CamisetaNoPerfilTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador a, Jogador b)> MontarAsync(
        bool ofereceCamiseta = true)
    {
        var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio
        {
            Nome = "4ª Etapa EPT", Codigo = "EPT4", Status = "Inscrições Abertas",
            DataInicio = new DateTime(2026, 12, 12),
            OfereceCamiseta = ofereceCamiseta,
            TamanhosDeCamiseta = ofereceCamiseta ? "P,M,G,GG,XG,BLP,BLM" : null,
        };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        var a = TestInfra.NovoJogador(81);
        a.Cpf = "11144477735"; a.Nome = "Felipe Bonamigo";
        var b = TestInfra.NovoJogador(82);
        b.Cpf = "52998224725"; b.Nome = "Maickel Klein";
        ctx.Jogadores.AddRange(a, b);
        await ctx.SaveChangesAsync();
        // A dupla dos testes é inscrita por "a"; "b" só aparece no formulário.
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = a.Id });
        await ctx.SaveChangesAsync();
        return (ctx, torneio, categoria, a, b);
    }

    private static Task Inscrever(DbPadelContext ctx, Torneio t, Categoria c, Jogador a, Jogador b, string? camiseta1, string? camiseta2) =>
        TestInfra.NovoDuplasController(ctx, a.Id).Create(
            torneioId: t.Id, categoriaId: c.Id,
            nome1: a.Nome, cpf1: a.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            camiseta1: camiseta1, camiseta2: camiseta2);

    private static async Task<Jogador> Recarregar(DbPadelContext ctx, int id)
    {
        ctx.ChangeTracker.Clear();
        return await ctx.Jogadores.SingleAsync(j => j.Id == id);
    }

    private static async Task<string?> Escolhida(DbPadelContext ctx, int torneioId, int jogadorId)
    {
        ctx.ChangeTracker.Clear();
        return (await ctx.CamisetasDosAtletas.SingleOrDefaultAsync(c => c.TorneioId == torneioId && c.JogadorId == jogadorId))?.Tamanho;
    }

    // ── Gravar no perfil ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_escolhe_o_proprio_tamanho_grava_no_perfil()
    {
        var (ctx, t, c, a, b) = await MontarAsync();
        a.TamanhoCamiseta = "P";
        await ctx.SaveChangesAsync();

        await Inscrever(ctx, t, c, a, b, "G", null);

        Assert.Equal("G", (await Recarregar(ctx, a.Id)).TamanhoCamiseta);
    }

    [Fact]
    public async Task Tamanho_escolhido_pelo_parceiro_so_preenche_perfil_vazio()
    {
        var (ctx, t, c, a, b) = await MontarAsync();
        await Inscrever(ctx, t, c, a, b, null, "BLM");

        Assert.Equal("BLM", (await Recarregar(ctx, b.Id)).TamanhoCamiseta);
    }

    [Fact]
    public async Task Parceiro_nao_sobrescreve_o_tamanho_que_a_pessoa_ja_tem_no_perfil()
    {
        var (ctx, t, c, a, b) = await MontarAsync();
        b.TamanhoCamiseta = "GG";
        await ctx.SaveChangesAsync();

        await Inscrever(ctx, t, c, a, b, null, "M");

        Assert.Equal("GG", (await Recarregar(ctx, b.Id)).TamanhoCamiseta);
        // A escolha DESTE torneio é a que o parceiro marcou — o perfil é só o padrão.
        Assert.Equal("M", await Escolhida(ctx, t.Id, b.Id));
    }

    [Fact]
    public async Task Escolher_no_card_do_torneio_grava_no_perfil()
    {
        var (ctx, t, c, a, b) = await MontarAsync();
        ctx.Duplas.Add(new Dupla { CategoriaId = c.Id, Jogador1Id = a.Id, Jogador2Id = b.Id });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, b.Id).EscolherCamiseta(t.Id, "XG");

        Assert.Equal("XG", (await Recarregar(ctx, b.Id)).TamanhoCamiseta);
    }

    // ── Usar o perfil ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Inscricao_sem_tamanho_marcado_usa_o_do_perfil()
    {
        var (ctx, t, c, a, b) = await MontarAsync();
        a.TamanhoCamiseta = "M";
        b.TamanhoCamiseta = "BLP";
        await ctx.SaveChangesAsync();

        await Inscrever(ctx, t, c, a, b, null, null);

        Assert.Equal("M", await Escolhida(ctx, t.Id, a.Id));
        Assert.Equal("BLP", await Escolhida(ctx, t.Id, b.Id));
    }

    [Fact]
    public async Task Tamanho_do_perfil_que_o_torneio_nao_oferece_nao_e_anotado()
    {
        var (ctx, t, c, a, b) = await MontarAsync();
        a.TamanhoCamiseta = "XGG";
        await ctx.SaveChangesAsync();

        await Inscrever(ctx, t, c, a, b, null, null);

        Assert.Null(await Escolhida(ctx, t.Id, a.Id));
    }

    [Fact]
    public async Task Ligar_o_brinde_ja_anota_o_tamanho_do_perfil_de_quem_esta_inscrito()
    {
        var (ctx, t, c, a, b) = await MontarAsync(ofereceCamiseta: false);
        a.TamanhoCamiseta = "G";
        ctx.Duplas.Add(new Dupla { CategoriaId = c.Id, Jogador1Id = a.Id, Jogador2Id = b.Id });
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoTorneiosController(ctx, a.Id, push: push)
            .ConfigurarCamisetas(t.Id, true, new[] { "M", "G" }, null);

        Assert.Equal("G", await Escolhida(ctx, t.Id, a.Id));
        Assert.Null(await Escolhida(ctx, t.Id, b.Id));
        // Quem já foi anotado é avisado do tamanho, pra trocar se quiser.
        await push.Received(1).EnviarParaJogadorAsync(a.Id, Arg.Any<string>(),
            Arg.Is<string>(corpo => corpo != null && corpo.Contains("G")), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── Editar no perfil ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Tamanho_do_perfil_so_aceita_codigo_da_tabela()
    {
        Assert.True(CamisetaDeBrinde.CodigoValido("BLXG"));
        Assert.False(CamisetaDeBrinde.CodigoValido("GIGANTE"));
        Assert.False(CamisetaDeBrinde.CodigoValido(null));
    }

    [Fact]
    public void Salvar_preferencias_troca_limpa_ou_mantem_o_tamanho()
    {
        Assert.Equal("G", CamisetaDeBrinde.TamanhoDoPerfil(enviado: "G", atual: "M"));
        // "nenhum" é o "não quero informar": o vazio o binder já transforma em nulo.
        Assert.Null(CamisetaDeBrinde.TamanhoDoPerfil(enviado: CamisetaDeBrinde.SemTamanho, atual: "M"));
        // Nulo = aba aberta antes do deploy, sem o campo: mantém o gravado.
        Assert.Equal("M", CamisetaDeBrinde.TamanhoDoPerfil(enviado: null, atual: "M"));
        // Lixo não apaga nada.
        Assert.Equal("M", CamisetaDeBrinde.TamanhoDoPerfil(enviado: "GIGANTE", atual: "M"));
    }

    [Fact]
    public void Minhas_preferencias_tem_o_campo()
    {
        var tela = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Auth", "Preferencias.cshtml"));

        Assert.Contains("name=\"tamanhoCamiseta\"", tela);
    }
}
