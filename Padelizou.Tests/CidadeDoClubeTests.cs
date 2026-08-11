using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A cidade do clube entrou em 11/08/2026 porque ela é o ÚNICO jeito de o sistema saber onde
// um torneio acontece (ver Services/UfDoTorneio). Antes disso o campo existia e ninguém
// perguntava: conferido em produção, os 3 clubes que sediam torneio real tinham `CidadeId`
// NULO — e por isso a mira do "Novo torneio aberto" dependia do estado de quem organiza.
public class CidadeDoClubeTests
{
    [Fact]
    public async Task Clube_novo_nasce_com_a_cidade_que_foi_escrita()
    {
        var ctx = TestInfra.NovoContexto();

        var clube = await CatalogoLocais.AcharOuCriarClubeAsync(ctx, "Radar", "Porto Alegre", "RS");

        Assert.NotNull(clube!.CidadeId);
        var cidade = await ctx.Cidades.FindAsync(clube.CidadeId);
        Assert.Equal("Porto Alegre", cidade!.Nome);
        Assert.Equal("RS", cidade.Estado);
    }

    // ⚠️ A cidade é OPCIONAL, e tem que continuar sendo. Campo obrigatório aqui trava a
    // criação do torneio e o cadastro de quem só quer marcar onde jogou — e a mira tem plano
    // B pra esse caso.
    [Fact]
    public async Task Sem_cidade_o_clube_e_criado_do_mesmo_jeito()
    {
        var ctx = TestInfra.NovoContexto();

        var clube = await CatalogoLocais.AcharOuCriarClubeAsync(ctx, "Radar");

        Assert.NotNull(clube);
        Assert.Null(clube!.CidadeId);
    }

    // O passivo se resolve sozinho: os clubes que nasceram antes desta pergunta recebem a
    // cidade na primeira vez que alguém informar uma.
    [Fact]
    public async Task Clube_antigo_sem_cidade_e_preenchido_quando_alguem_informa()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Radar", Endereco = "", Contato = "" });
        await ctx.SaveChangesAsync();

        var clube = await CatalogoLocais.AcharOuCriarClubeAsync(ctx, "radar", "Porto Alegre", "RS");

        Assert.Equal(1, clube!.Id);                       // é o MESMO clube, não um segundo
        Assert.NotNull(clube.CidadeId);
        Assert.Equal(1, await ctx.Clubes.CountAsync());
    }

    // ⚠️ Só preenche vazio. Quem digita o nome de um clube que já existe não pode mudar a
    // cidade dele pra todo mundo — um erro de digitação viraria mudança de endereço alheio.
    [Fact]
    public async Task Cidade_ja_cadastrada_nao_e_sobrescrita()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Cidades.Add(new Cidade { Id = 1, Nome = "Porto Alegre", Estado = "RS" });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Radar", Endereco = "", Contato = "", CidadeId = 1 });
        await ctx.SaveChangesAsync();

        var clube = await CatalogoLocais.AcharOuCriarClubeAsync(ctx, "Radar", "São Paulo", "SP");

        Assert.Equal(1, clube!.CidadeId);
    }

    // A ponta a ponta que dá sentido ao resto: com a cidade preenchida, o torneio passa a
    // saber o próprio estado sem depender de quem organiza.
    [Fact]
    public async Task Com_a_cidade_no_clube_o_torneio_sabe_o_estado_sozinho()
    {
        var ctx = TestInfra.NovoContexto();

        var clube = await CatalogoLocais.AcharOuCriarClubeAsync(ctx, "Chakra Padel", "Gravataí", "rs");
        ctx.Torneios.Add(new Torneio { Id = 5, Nome = "Copa", Codigo = "T5", ClubeId = clube!.Id });
        await ctx.SaveChangesAsync();

        // Sem nenhum organizador cadastrado: a resposta vem do clube.
        Assert.Equal("RS", await UfDoTorneio.DescobrirAsync(ctx, 5));
    }
}
