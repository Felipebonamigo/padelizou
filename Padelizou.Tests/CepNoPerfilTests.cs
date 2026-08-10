using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O CEP passando pelo controller de verdade: é ali que se decide o que vai pro banco, e o
// motivo do campo existir é a cidade chegar escrita de um jeito só.
public class CepNoPerfilTests
{
    private static readonly EnderecoDoCep Gravatai =
        new("94010020", "Rua Dom Feliciano", "Centro", "Gravataí", "RS");

    private static async Task<(DbPadelContext ctx, Jogador jogador)> ComJogadorAsync(
        string? cidade = null, string? estado = null)
    {
        var ctx = TestInfra.NovoContexto();
        var jogador = new Jogador { Nome = "Fulano", Cpf = "99900000001", Email = "f@x.com", Cidade = cidade, Estado = estado };
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();
        return (ctx, jogador);
    }

    [Fact]
    public async Task Informar_o_CEP_preenche_a_cidade_sem_a_pessoa_digitar()
    {
        var (ctx, jogador) = await ComJogadorAsync();
        using var _ = ctx;
        var c = TestInfra.NovoAuthController(ctx, jogador.Id, cep: TestInfra.CepQueResponde(Gravatai));

        await c.EditarPerfil("Fulano", "f@x.com", null, cidade: null, estado: null, isProfessor: false, foto: null,
            cep: "94010-020", enderecoPublico: false);

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.Equal("Gravataí", salvo!.Cidade);
        Assert.Equal("RS", salvo.Estado);
        Assert.Equal("Centro", salvo.Bairro);
        Assert.Equal("94010020", salvo.Cep);
    }

    [Fact]
    public async Task A_cidade_do_CEP_corrige_a_grafia_que_a_pessoa_tinha_digitado()
    {
        // Este é o motivo do campo existir: "GRAVATAI" digitado à mão vira mais uma opção no
        // filtro do ranking. Com o CEP, vira "Gravataí" — a mesma de todo mundo.
        var (ctx, jogador) = await ComJogadorAsync(cidade: "GRAVATAI", estado: "RS");
        using var _ = ctx;
        var c = TestInfra.NovoAuthController(ctx, jogador.Id, cep: TestInfra.CepQueResponde(Gravatai));

        await c.EditarPerfil("Fulano", "f@x.com", null, cidade: "GRAVATAI", estado: "RS", isProfessor: false, foto: null,
            cep: "94010-020", enderecoPublico: false);

        Assert.Equal("Gravataí", (await ctx.Jogadores.FindAsync(jogador.Id))!.Cidade);
    }

    [Fact]
    public async Task CEP_e_cidade_que_nao_combinam_terminam_na_cidade_do_CEP()
    {
        // O formulário preenche a cidade pelo JavaScript, mas o POST pode vir de qualquer
        // lugar. Quem manda é a consulta do servidor.
        var (ctx, jogador) = await ComJogadorAsync();
        using var _ = ctx;
        var c = TestInfra.NovoAuthController(ctx, jogador.Id, cep: TestInfra.CepQueResponde(Gravatai));

        await c.EditarPerfil("Fulano", "f@x.com", null, cidade: "Rio de Janeiro", estado: "RJ", isProfessor: false, foto: null,
            cep: "94010-020", enderecoPublico: false);

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.Equal("Gravataí", salvo!.Cidade);
        Assert.Equal("RS", salvo.Estado);
    }

    [Fact]
    public async Task Sem_CEP_o_perfil_continua_funcionando_como_sempre()
    {
        // O campo é OPCIONAL de verdade: quem não informa nada segue digitando a cidade.
        var (ctx, jogador) = await ComJogadorAsync();
        using var _ = ctx;
        var c = TestInfra.NovoAuthController(ctx, jogador.Id);

        await c.EditarPerfil("Fulano", "f@x.com", null, cidade: "Canoas", estado: "rs", isProfessor: false, foto: null);

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.Equal("Canoas", salvo!.Cidade);
        Assert.Equal("RS", salvo.Estado);
        Assert.Null(salvo.Cep);
    }

    [Fact]
    public async Task ViaCEP_fora_do_ar_nao_impede_ninguem_de_salvar_o_perfil()
    {
        // A consulta acontece DENTRO do POST. Servidor deles pendurado não pode virar
        // "não consigo salvar meu cadastro".
        var (ctx, jogador) = await ComJogadorAsync();
        using var _ = ctx;
        var c = TestInfra.NovoAuthController(ctx, jogador.Id);   // o dublê padrão é o "fora do ar"

        await c.EditarPerfil("Fulano", "f@x.com", null, cidade: "Canoas", estado: "RS", isProfessor: false, foto: null,
            cep: "94010-020", enderecoPublico: false);

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.Equal("94010020", salvo!.Cep);    // guardado pra próxima gravação resolver
        Assert.Equal("Canoas", salvo.Cidade);    // o que a pessoa digitou não se perde
    }

    [Fact]
    public async Task O_endereco_nasce_privado()
    {
        // ⚠️ Endereço é dado pessoal: publicar por padrão publicaria a rua de todo mundo sem
        // ninguém ter tocado em nada.
        var (ctx, jogador) = await ComJogadorAsync();
        using var _ = ctx;

        Assert.False(jogador.EnderecoPublico);

        var c = TestInfra.NovoAuthController(ctx, jogador.Id, cep: TestInfra.CepQueResponde(Gravatai));
        await c.EditarPerfil("Fulano", "f@x.com", null, null, null, isProfessor: false, foto: null,
            cep: "94010-020");

        Assert.False((await ctx.Jogadores.FindAsync(jogador.Id))!.EnderecoPublico);
    }

    [Fact]
    public async Task Quem_liga_a_chave_publica_rua_e_bairro()
    {
        var (ctx, jogador) = await ComJogadorAsync();
        using var _ = ctx;
        var c = TestInfra.NovoAuthController(ctx, jogador.Id, cep: TestInfra.CepQueResponde(Gravatai));

        await c.EditarPerfil("Fulano", "f@x.com", null, null, null, isProfessor: false, foto: null,
            cep: "94010-020", enderecoPublico: true);

        Assert.True((await ctx.Jogadores.FindAsync(jogador.Id))!.EnderecoPublico);
    }

    [Fact]
    public async Task Apagar_o_CEP_tira_o_endereco_do_ar()
    {
        var (ctx, jogador) = await ComJogadorAsync();
        using var _ = ctx;
        var comCep = TestInfra.NovoAuthController(ctx, jogador.Id, cep: TestInfra.CepQueResponde(Gravatai));
        await comCep.EditarPerfil("Fulano", "f@x.com", null, null, null, isProfessor: false, foto: null,
            cep: "94010-020", enderecoPublico: true);

        var semCep = TestInfra.NovoAuthController(ctx, jogador.Id);
        await semCep.EditarPerfil("Fulano", "f@x.com", null, "Gravataí", "RS", isProfessor: false, foto: null,
            cep: "", enderecoPublico: false);

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.Null(salvo!.Cep);
        Assert.Null(salvo.Logradouro);
        Assert.Null(salvo.Bairro);
        Assert.Equal("Gravataí", salvo.Cidade);   // a cidade é do perfil, não do endereço
    }

    [Fact]
    public async Task CEP_inventado_avisa_a_pessoa_mas_salva_o_resto()
    {
        // Recusar o formulário inteiro por causa de um campo OPCIONAL faria ele custar mais
        // caro que os obrigatórios.
        var (ctx, jogador) = await ComJogadorAsync();
        using var _ = ctx;
        var c = TestInfra.NovoAuthController(ctx, jogador.Id, cep: TestInfra.CepInexistente());

        await c.EditarPerfil("Novo Nome", "f@x.com", null, "Canoas", "RS", isProfessor: false, foto: null,
            cep: "99999-999", enderecoPublico: false);

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.Null(salvo!.Cep);
        Assert.Equal("Canoas", salvo.Cidade);
        Assert.Contains(EnderecoPeloCep.NaoEncontrado, (c.TempData["Sucesso"] as string) ?? "");
    }

    [Fact]
    public async Task A_cidade_que_veio_do_CEP_vira_UMA_opcao_no_filtro_do_ranking()
    {
        // O fecho do ciclo: duas pessoas que digitaram grafias diferentes mais uma que usou o
        // CEP dão UMA opção — e a que aparece é a do CEP, que é a escrita certa.
        var (ctx, jogador) = await ComJogadorAsync();
        using var _ = ctx;
        ctx.Jogadores.AddRange(
            new Jogador { Nome = "A", Cpf = "99900000002", Cidade = "GRAVATAI", Estado = "RS" },
            new Jogador { Nome = "B", Cpf = "99900000003", Cidade = "gravatai", Estado = "RS" });
        await ctx.SaveChangesAsync();

        var c = TestInfra.NovoAuthController(ctx, jogador.Id, cep: TestInfra.CepQueResponde(Gravatai));
        await c.EditarPerfil("Fulano", "f@x.com", null, null, null, isProfessor: false, foto: null,
            cep: "94010-020");

        var (_, cidades) = await new EstatisticasService(ctx).ObterLocaisDisponiveisAsync(null);

        Assert.Equal(new[] { "Gravataí" }, cidades);
    }
}
