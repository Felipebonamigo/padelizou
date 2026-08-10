using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O "Novo torneio aberto" é o único aviso proporcional ao TAMANHO DA BASE — foi o disparo de
// 87 e-mails que estourou a cota do Gmail em 09/08/2026. Mirar por estado corta o que não
// interessa, mas o risco de mirar errado é pior que o de não mirar: quem for cortado por
// engano simplesmente para de saber dos torneios da própria cidade, e **não tem como
// perceber que parou**. Nenhum erro aparece em lugar nenhum.
//
// Por isso o que estes testes guardam são as portas de escape, não o corte.
public class AvisoDeTorneioPorEstadoTests
{
    // Os valores abaixo são os que EXISTEM no banco de produção (10/08/2026):
    // RS 87 · em branco 44 · Rs 32 · rs 6 · "Rio Grande do Sul (RS)" 1 · Sp 1 · DF 1
    [Theory]
    [InlineData("RS", "RS")]
    [InlineData("Rs", "RS")]                       // 32 pessoas
    [InlineData("rs", "RS")]                       // 6 pessoas
    [InlineData("  RS  ", "RS")]
    [InlineData("Rio Grande do Sul (RS)", "RS")]   // 1 pessoa
    [InlineData("Rio Grande do Sul", "RS")]
    [InlineData("São Paulo", "SP")]                // com acento
    [InlineData("Sp", "SP")]
    [InlineData("DF", "DF")]
    [InlineData("Distrito Federal", "DF")]
    [InlineData("RIO GRANDE DO SUL - RS", "RS")]
    public void Estado_escrito_de_qualquer_jeito_vira_a_sigla(string escrito, string esperado)
    {
        Assert.Equal(esperado, UnidadeFederativa.Normalizar(escrito));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("qualquer coisa")]
    [InlineData("XX")]
    public void O_que_nao_da_pra_entender_vira_NULL_e_nao_um_palpite(string? escrito)
    {
        Assert.Null(UnidadeFederativa.Normalizar(escrito));
    }

    // ⚠️ O TESTE QUE NÃO PODE SER APAGADO. 44 das 172 contas ativas estão com o estado em
    // branco — o campo nunca foi obrigatório. Se "em branco" passar a significar "não é
    // daqui", um quarto da base para de saber que existe torneio novo, calada.
    [Fact]
    public void Quem_nao_preencheu_o_estado_continua_recebendo()
    {
        Assert.True(UnidadeFederativa.Combina(null, "RS"));
        Assert.True(UnidadeFederativa.Combina("", "RS"));
        Assert.True(UnidadeFederativa.Combina("   ", "RS"));
        // E o contrário também: torneio sem UF conhecida alcança todo mundo.
        Assert.True(UnidadeFederativa.Combina("RS", null));
    }

    [Fact]
    public void Estados_diferentes_nao_combinam()
    {
        Assert.False(UnidadeFederativa.Combina("RS", "SP"));
        Assert.False(UnidadeFederativa.Combina("rs", "Sp"));
    }

    [Fact]
    public void Maiuscula_e_minuscula_nao_separam_quem_e_do_mesmo_estado()
    {
        // Os 39 do banco de produção que uma comparação na lata deixaria de fora.
        Assert.True(UnidadeFederativa.Combina("Rs", "RS"));
        Assert.True(UnidadeFederativa.Combina("rs", "RS"));
        Assert.True(UnidadeFederativa.Combina("Rio Grande do Sul (RS)", "RS"));
    }

    // ── DE ONDE SAI O ESTADO DO TORNEIO ──────────────────────────────────────────────────

    [Fact]
    public async Task O_clube_manda_quando_ele_tem_cidade()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Cidades.Add(new Cidade { Id = 1, Nome = "Porto Alegre", Estado = "RS" });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Radar", CidadeId = 1 });
        ctx.Torneios.Add(new Torneio { Id = 5, Nome = "Copa", Codigo = "T5", ClubeId = 1 });
        // Organizador de OUTRO estado: o lugar de verdade tem que ganhar de onde alguém mora.
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Org", Cpf = "10000000091", Estado = "SP" });
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = 5, JogadorId = 9 });
        await ctx.SaveChangesAsync();

        Assert.Equal("RS", await UfDoTorneio.DescobrirAsync(ctx, 5));
    }

    // Hoje é ESTE caminho que responde: os 3 torneios reais têm clube sem cidade cadastrada.
    [Fact]
    public async Task Sem_cidade_no_clube_vale_o_estado_de_quem_organiza()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Radar" });   // sem CidadeId, como em produção
        ctx.Torneios.Add(new Torneio { Id = 5, Nome = "Copa", Codigo = "T5", ClubeId = 1 });
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Lucas", Cpf = "10000000092", Estado = "Rs" });
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = 5, JogadorId = 9 });
        await ctx.SaveChangesAsync();

        Assert.Equal("RS", await UfDoTorneio.DescobrirAsync(ctx, 5));
    }

    // Torneio com vários organizadores (o 23 de produção tem nove, um deles sem estado):
    // pega o primeiro PREENCHIDO, em ordem estável.
    [Fact]
    public async Task Organizador_sem_estado_nao_apaga_a_resposta_dos_outros()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(new Torneio { Id = 5, Nome = "Copa", Codigo = "T5" });
        ctx.Jogadores.Add(new Jogador { Id = 8, Nome = "Sem UF", Cpf = "10000000081", Estado = null });
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Com UF", Cpf = "10000000093", Estado = "RS" });
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = 5, JogadorId = 8 });
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = 5, JogadorId = 9 });
        await ctx.SaveChangesAsync();

        Assert.Equal("RS", await UfDoTorneio.DescobrirAsync(ctx, 5));
    }

    // ⚠️ Sem nenhuma pista, a resposta é NULL — e null obriga quem chama a avisar a base
    // inteira. Devolver um palpite aqui seria calar gente de verdade por causa de um dado
    // que ninguém preencheu.
    [Fact]
    public async Task Sem_pista_nenhuma_a_resposta_e_nao_sei()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(new Torneio { Id = 5, Nome = "Copa", Codigo = "T5" });
        await ctx.SaveChangesAsync();

        Assert.Null(await UfDoTorneio.DescobrirAsync(ctx, 5));
    }
}
