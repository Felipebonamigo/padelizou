using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Em 03/08/2026 entrou no torneio real um jogador chamado "." — alguém sem os dados do
// parceiro que digitou um ponto pra passar do campo obrigatório, com um CPF de 11 números
// quaisquer. O `required` do HTML aceita qualquer caractere e o servidor não olhava.
public class NomeECpfDeVerdadeTests
{
    // ---- Nome ----

    [Theory]
    [InlineData(".")]
    [InlineData("-")]
    [InlineData("x")]
    [InlineData("  ")]
    [InlineData("")]
    [InlineData(null)]
    public void Isso_nao_e_nome_de_gente(string? nome) => Assert.False(NomeDePessoa.Parece(nome));

    [Theory]
    [InlineData("Jonatas")]
    [InlineData("Ana")]
    [InlineData("João da Silva")]
    [InlineData("Otávio Wunsch Junior")]
    [InlineData("Maria D'Ávila")]
    [InlineData("Jean-Pierre")]
    [InlineData("Bo Li")]                 // nome curto de verdade existe
    [InlineData("J. Silva")]              // abreviado também
    public void Isso_e_nome_de_gente(string nome) => Assert.True(NomeDePessoa.Parece(nome));

    [Fact]
    public void Nome_com_numero_e_recusado()
    {
        // Quase sempre é CPF colado no campo errado.
        Assert.False(NomeDePessoa.Parece("12345678900"));
        Assert.False(NomeDePessoa.Parece("Joao 2"));
    }

    [Fact]
    public void A_mensagem_diz_QUAL_campo_corrigir()
    {
        // O formulário da dupla tem dois nomes; "nome inválido" sozinho não ajuda ninguém.
        var problema = NomeDePessoa.Problema(".", "O nome do parceiro");

        Assert.NotNull(problema);
        Assert.Contains("O nome do parceiro", problema);
    }

    [Fact]
    public void Espaco_repetido_some_pra_o_mesmo_nome_nao_virar_duas_pessoas()
    {
        Assert.Equal("João da Silva", NomeDePessoa.Arrumar("  João   da  Silva "));
        Assert.Equal("Ana", NomeDePessoa.Arrumar("Ana"));
        Assert.Equal("", NomeDePessoa.Arrumar(null));
    }

    // ---- CPF ----

    [Theory]
    [InlineData("11144477735")]
    [InlineData("111.444.777-35")]   // com máscara também
    [InlineData("22255588846")]
    [InlineData("33366699957")]
    public void CPF_de_verdade_passa(string cpf) => Assert.True(Documentos.CpfEhValido(cpf));

    [Theory]
    [InlineData("12345678900")]      // 11 números quaisquer
    [InlineData("99900000001")]      // o padrão dos seeds antigos
    [InlineData("11144477734")]      // um dígito verificador errado
    [InlineData("22255588896")]      // parece CPF de teste, mas o dígito não fecha
    [InlineData("1114447773")]       // curto
    [InlineData("111444777351")]     // longo
    [InlineData("")]
    [InlineData(null)]
    public void CPF_inventado_nao_passa(string? cpf) => Assert.False(Documentos.CpfEhValido(cpf));

    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("99999999999")]
    public void Todos_os_digitos_iguais_nao_passam(string cpf)
    {
        // Estes fecham a conta dos dígitos por acidente matemático — e são justamente o que
        // se digita quando se quer enrolar.
        Assert.False(Documentos.CpfEhValido(cpf));
    }

    [Fact]
    public void A_checagem_frouxa_continua_existindo_e_e_outra_coisa()
    {
        // CpfTemFormatoValido é pra consultar e normalizar (11 números). Quem GRAVA gente
        // nova usa CpfEhValido. Trocar uma pela outra sem querer é o jeito de reabrir o furo.
        Assert.True(Documentos.CpfTemFormatoValido("12345678900"));
        Assert.False(Documentos.CpfEhValido("12345678900"));
    }
}
