using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Apelido opcional + busca unificada por nome, apelido ou CPF.
public class ApelidoBuscaTests
{
    private static Jogador Novo(string nome, string cpf, string? apelido = null) =>
        new() { Nome = nome, Cpf = cpf, Apelido = apelido };

    // ---------- Exibição ----------

    [Fact]
    public void Sem_apelido_o_sistema_chama_pelo_nome_curto()
    {
        var j = Novo("José Carlos da Silva", "1");

        // Primeiro e último (05/08/2026): nome de três+ palavras estourava as listas densas.
        Assert.Equal("José Silva", j.ComoChamar);
        // O nome completo segue onde ele importa: perfil e busca.
        Assert.Equal("José Carlos da Silva", j.NomeComApelido);
    }

    [Fact]
    public void Com_apelido_a_lista_usa_o_apelido_e_o_perfil_mostra_os_dois()
    {
        var j = Novo("José Carlos da Silva", "1", "Zeca");

        Assert.Equal("Zeca", j.ComoChamar);
        Assert.Equal("José Carlos da Silva (Zeca)", j.NomeComApelido);
    }

    [Fact]
    public void Apelido_so_com_espacos_conta_como_sem_apelido()
    {
        // O controller grava null pra branco, mas dado antigo pode ter vindo com espaço.
        var j = Novo("Maria", "1", "   ");

        Assert.Equal("Maria", j.ComoChamar);
        Assert.Equal("Maria", j.NomeComApelido);
    }

    // ---------- O termo é CPF ou nome? ----------

    [Fact]
    public void So_CPF_completo_conta_como_busca_por_documento()
    {
        Assert.True(BuscaJogador.PareceCpf("11144477735"));
        Assert.True(BuscaJogador.PareceCpf("111.444.777-35"));  // com máscara

        // Parcial NÃO vale: com "111444" dava pra varrer os documentos da base aos
        // poucos. Quem procura por CPF já tem o número inteiro na mão.
        Assert.False(BuscaJogador.PareceCpf("111444"));
        Assert.False(BuscaJogador.PareceCpf("11"));
    }

    [Fact]
    public void Termo_com_letra_e_nome_ou_apelido_mesmo_tendo_numero()
    {
        Assert.False(BuscaJogador.PareceCpf("Zeca"));
        Assert.False(BuscaJogador.PareceCpf("Ana 2"));   // tem gente cadastrada assim
        Assert.False(BuscaJogador.PareceCpf(""));
    }

    [Fact]
    public async Task CPF_parcial_nao_lista_a_faixa_inteira()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            Novo("Um", "11144477735"),
            Novo("Dois", "11144477736"),
            Novo("Tres", "11144477737"));
        ctx.SaveChanges();

        // Antes isto devolvia os três — era o vetor de varredura de CPF.
        Assert.Empty(await BuscaJogador.BuscarAsync(ctx, "111444"));

        // Com o número inteiro, acha uma pessoa só.
        Assert.Single(await BuscaJogador.BuscarAsync(ctx, "11144477736"));
    }

    // ---------- Busca ----------

    [Fact]
    public async Task Acha_pelo_apelido_pelo_nome_e_pelo_CPF()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            Novo("José Carlos da Silva", "11144477735", "Zeca"),
            Novo("Mariana Souza", "22255588896"),
            Novo("Pedro Alves", "33366699957", "Pedrão"));
        ctx.SaveChanges();

        var porApelido = await BuscaJogador.BuscarAsync(ctx, "Zeca");
        var porNome = await BuscaJogador.BuscarAsync(ctx, "Mariana");
        var porCpf = await BuscaJogador.BuscarAsync(ctx, "333.666.999-57");

        Assert.Equal("José Carlos da Silva", porApelido.Single().Nome);
        Assert.Equal("Mariana Souza", porNome.Single().Nome);
        Assert.Equal("Pedro Alves", porCpf.Single().Nome);
    }

    [Fact]
    public async Task Quem_comeca_com_o_termo_vem_antes_de_quem_so_contem()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            Novo("Mariana Souza", "1"),   // contém "ana" no meio
            Novo("Ana Paula", "2"));      // começa com "Ana"
        ctx.SaveChanges();

        var achados = await BuscaJogador.BuscarAsync(ctx, "Ana");

        Assert.Equal(2, achados.Count);
        Assert.Equal("Ana Paula", achados[0].Nome);
    }

    [Fact]
    public async Task Apelido_que_comeca_com_o_termo_ganha_do_nome_que_so_contem()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            Novo("Roberto Zecarias", "1"),          // "Zec" no meio do sobrenome
            Novo("José Carlos", "2", "Zeca"));      // apelido começa com "Zec"
        ctx.SaveChanges();

        var achados = await BuscaJogador.BuscarAsync(ctx, "Zec");

        // Quem procura "Zec" quer o Zeca, não o Zecarias.
        Assert.Equal("José Carlos", achados[0].Nome);
    }

    [Fact]
    public async Task Termo_vazio_nao_devolve_o_banco_inteiro()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(Novo("Alguém", "1"));
        ctx.SaveChanges();

        Assert.Empty(await BuscaJogador.BuscarAsync(ctx, null));
        Assert.Empty(await BuscaJogador.BuscarAsync(ctx, "   "));

        // Já o Filtrar devolve a consulta intocada, pra combinar com outros filtros.
        Assert.Single(BuscaJogador.Filtrar(ctx.Jogadores, null));
    }
}
