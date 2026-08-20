using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// DEFINIR O PARCEIRO PELO NOME (pedido do Felipe, 20/08/2026) — e o buraco que ele quase abriu.
//
// O painel do organizador só aceitava CPF. Passar a aceitar nome é fácil; o que NÃO é óbvio é
// que a caixa "juntar com a inscrição que já existe" aparecia por causa do CPF digitado. Sem
// mais nada, escolher pelo nome alguém já inscrito sozinho levaria de volta ao beco sem saída
// que OrganizadorJuntaInscricoesTests documenta: o servidor recusa mandando marcar uma caixa
// que a tela nunca mostrou.
//
// A saída foi a busca por nome já dizer quem, entre os achados, está inscrito sozinho NAQUELA
// categoria. Este arquivo protege esse campo — sem ele o front não tem como oferecer a junção.
public class BuscaPorNomeAvisaInscricaoSoloTests
{
    private static async Task<(DbPadelContext ctx, Categoria cat, Jogador organizador, Jogador gabriel)>
        MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio { Nome = "Interno", Codigo = "INT1", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var cat = new Categoria { Nome = "4ª Masculina", Codigo = "C4M", TorneioId = torneio.Id };
        ctx.Categorias.Add(cat);

        var organizador = new Jogador { Nome = "Organizador", Cpf = "11144477735" };
        var gabriel = new Jogador { Nome = "Gabriel Moreira", Cpf = "33366699957" };
        ctx.Jogadores.AddRange(organizador, gabriel);
        await ctx.SaveChangesAsync();

        return (ctx, cat, organizador, gabriel);
    }

    private static async Task<JsonElement> BuscarAsync(
        DbPadelContext ctx, int logadoComo, string termo, int? categoriaId = null, int? ignorarDuplaId = null)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: logadoComo);
        var corpo = Assert.IsType<JsonResult>(
            await controller.BuscarJogadorParaOrganizador(termo, categoriaId, ignorarDuplaId)).Value;

        // Serializado como o navegador recebe — é a resposta que o script lê. camelCase de
        // propósito: é o que o MVC manda, e o script procura `jaInscritoSozinho`, não `Id`.
        var comoOMvcManda = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonDocument.Parse(JsonSerializer.Serialize(corpo, comoOMvcManda)).RootElement;
    }

    private static JsonElement Achado(JsonElement resposta, int id) =>
        resposta.EnumerateArray().Single(j => j.GetProperty("id").GetInt32() == id);

    [Fact]
    public async Task Quem_esta_inscrito_sozinho_na_categoria_vem_marcado()
    {
        var (ctx, cat, organizador, gabriel) = await MontarAsync();
        using var _ = ctx;
        ctx.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = gabriel.Id, Codigo = "DG" });
        await ctx.SaveChangesAsync();

        var resposta = await BuscarAsync(ctx, organizador.Id, "gab", cat.Id);

        Assert.True(Achado(resposta, gabriel.Id).GetProperty("jaInscritoSozinho").GetBoolean());
    }

    [Fact]
    public async Task Sem_inscricao_na_categoria_nao_ha_nada_a_juntar()
    {
        var (ctx, cat, organizador, gabriel) = await MontarAsync();
        using var _ = ctx;

        var resposta = await BuscarAsync(ctx, organizador.Id, "gab", cat.Id);

        Assert.False(Achado(resposta, gabriel.Id).GetProperty("jaInscritoSozinho").GetBoolean());
    }

    // Juntar só vale pra inscrição SOZINHA. Quem já fechou dupla sairia de uma inscrição que
    // não é só dele — o servidor recusa, e oferecer a junção aqui seria prometer o que não vai
    // acontecer.
    [Fact]
    public async Task Quem_ja_tem_parceiro_nao_e_oferecido_pra_juntar()
    {
        var (ctx, cat, organizador, gabriel) = await MontarAsync();
        using var _ = ctx;
        var outro = new Jogador { Nome = "Outro", Cpf = "52998224725" };
        ctx.Jogadores.Add(outro);
        await ctx.SaveChangesAsync();
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = cat.Id, Jogador1Id = gabriel.Id, Jogador2Id = outro.Id, Codigo = "DUP",
        });
        await ctx.SaveChangesAsync();

        var resposta = await BuscarAsync(ctx, organizador.Id, "gab", cat.Id);

        Assert.False(Achado(resposta, gabriel.Id).GetProperty("jaInscritoSozinho").GetBoolean());
    }

    // ⚠️ A DUPLA QUE ESTÁ SENDO EDITADA NÃO PODE SE ACUSAR. Sem `ignorarDuplaId`, trocar o
    // parceiro de uma dupla faria o próprio jogador 1 dela aparecer como "inscrito sozinho".
    [Fact]
    public async Task A_dupla_em_edicao_fica_de_fora_da_conta()
    {
        var (ctx, cat, organizador, gabriel) = await MontarAsync();
        using var _ = ctx;
        var duplaDele = new Dupla { CategoriaId = cat.Id, Jogador1Id = gabriel.Id, Codigo = "DG" };
        ctx.Duplas.Add(duplaDele);
        await ctx.SaveChangesAsync();

        var resposta = await BuscarAsync(ctx, organizador.Id, "gab", cat.Id, ignorarDuplaId: duplaDele.Id);

        Assert.False(Achado(resposta, gabriel.Id).GetProperty("jaInscritoSozinho").GetBoolean());
    }

    // Sem categoria (a busca de organizador do topo da tela de criar torneio) nada disso é
    // perguntado — e a resposta continua servindo, só sem a marca.
    [Fact]
    public async Task Sem_categoria_a_busca_continua_funcionando()
    {
        var (ctx, cat, organizador, gabriel) = await MontarAsync();
        using var _ = ctx;
        ctx.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = gabriel.Id, Codigo = "DG" });
        await ctx.SaveChangesAsync();

        var resposta = await BuscarAsync(ctx, organizador.Id, "gab");

        Assert.False(Achado(resposta, gabriel.Id).GetProperty("jaInscritoSozinho").GetBoolean());
    }

    // O CPF de terceiro não sai da busca por nome (Services/BuscaJogador). A marca nova não
    // pode ter aberto essa porta de lado.
    [Fact]
    public async Task A_resposta_continua_sem_CPF_de_terceiro()
    {
        var (ctx, cat, organizador, _g) = await MontarAsync();
        using var _ = ctx;

        var resposta = await BuscarAsync(ctx, organizador.Id, "gab", cat.Id);
        var json = resposta.GetRawText();

        Assert.DoesNotContain("33366699957", json);
        Assert.DoesNotContain("cpf", json, StringComparison.OrdinalIgnoreCase);
    }
}
