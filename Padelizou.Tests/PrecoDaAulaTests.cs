using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O preço da aula tem duas dimensões que se cruzam — quantos alunos e QUEM — e cruzar sem
// regra escrita é como se cobra errado. Estes testes fixam a regra: o valor do tamanho vem
// da tabela do local, o acordo particular vale na individual daquele aluno, e nada disso
// pode divergir da conta que a tela faz em JavaScript.
public class PrecoDaAulaTests
{
    private static LocalAula Local(decimal individual, decimal? dupla = null, decimal? trio = null) =>
        new() { Nome = "Batata Padel", PrecoPadrao = individual, PrecoDupla = dupla, PrecoTrio = trio };

    [Theory]
    [InlineData(1, 110)]
    [InlineData(2, 150)]
    [InlineData(3, 180)]
    public void Cada_tamanho_cobra_o_seu_preco(int alunos, decimal esperado)
    {
        var local = Local(110, dupla: 150, trio: 180);
        Assert.Equal(esperado, PrecoDaAula.DoLocal(local, alunos));
    }

    [Fact]
    public void Tamanho_sem_preco_cai_pro_menor_mais_proximo_que_o_professor_informou()
    {
        // Preencheu dupla e deixou trio em branco: mostrar o valor da dupla (perto, dá pra
        // ajustar pra cima) é melhor que mostrar o individual, que cobraria três pessoas
        // pelo preço de uma.
        var local = Local(110, dupla: 150);
        Assert.Equal(150, PrecoDaAula.DoLocal(local, 3));

        // Sem nenhum dos dois, só existe o preço que ele informou.
        var soIndividual = Local(110);
        Assert.Equal(110, PrecoDaAula.DoLocal(soIndividual, 2));
        Assert.Equal(110, PrecoDaAula.DoLocal(soIndividual, 3));
    }

    [Fact]
    public void Preco_combinado_vale_na_individual_daquele_aluno()
    {
        var local = Local(110, dupla: 150);
        Assert.Equal(90, PrecoDaAula.Sugerido(local, quantidadeAlunos: 1, precoCombinado: 90));
    }

    [Fact]
    public void Preco_combinado_nao_derruba_o_valor_da_dupla_nem_do_trio()
    {
        // O desconto foi dado a UMA PESSOA, não à quadra inteira: se valesse aqui, o
        // professor daria de graça o lugar do acompanhante sem perceber.
        var local = Local(110, dupla: 150, trio: 180);
        Assert.Equal(150, PrecoDaAula.Sugerido(local, quantidadeAlunos: 2, precoCombinado: 90));
        Assert.Equal(180, PrecoDaAula.Sugerido(local, quantidadeAlunos: 3, precoCombinado: 90));
    }

    [Fact]
    public void Sem_acordo_a_individual_cobra_a_tabela_do_local()
    {
        var local = Local(110, dupla: 150);
        Assert.Equal(110, PrecoDaAula.Sugerido(local, quantidadeAlunos: 1, precoCombinado: null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(4)]
    [InlineData(99)]
    public void Quantidade_fora_da_faixa_vira_aula_individual(int digitado)
    {
        // O formulário manda o que o navegador quiser. O professor quer marcar a aula, não
        // discutir o campo — então o valor esquisito vira o caso normal em vez de erro.
        Assert.Equal(1, PrecoDaAula.Tamanho(digitado));
        Assert.Equal("Individual", PrecoDaAula.Rotulo(digitado));
        Assert.Equal(110, PrecoDaAula.DoLocal(Local(110, dupla: 150), digitado));
    }

    [Theory]
    [InlineData(1, "Individual")]
    [InlineData(2, "Em dupla")]
    [InlineData(3, "Em trio")]
    public void Rotulo_diz_o_tamanho_em_portugues(int alunos, string esperado) =>
        Assert.Equal(esperado, PrecoDaAula.Rotulo(alunos));

    // ---- A chave que identifica o aluno ----

    [Fact]
    public void Aluno_com_conta_e_aluno_avulso_nunca_colidem()
    {
        Assert.NotEqual(PrecoDaAula.Chave(7, null), PrecoDaAula.Chave(null, "7"));
    }

    [Fact]
    public void Nome_avulso_ignora_caixa_e_espaco_das_pontas()
    {
        // "joão", "João " e "JOÃO" são a mesma pessoa pra quem digitou — e o professor digita
        // o nome de novo a cada aula que lança.
        var referencia = PrecoDaAula.Chave(null, "João");
        Assert.Equal(referencia, PrecoDaAula.Chave(null, "joão"));
        Assert.Equal(referencia, PrecoDaAula.Chave(null, "  João  "));
        Assert.Equal(referencia, PrecoDaAula.Chave(null, "JOÃO"));
    }

    [Fact]
    public void Nome_avulso_NAO_ignora_acento()
    {
        // Chutar aqui aplicaria o desconto da Inês na Ines — duas pessoas diferentes.
        Assert.NotEqual(PrecoDaAula.Chave(null, "Ines"), PrecoDaAula.Chave(null, "Inês"));
    }

    [Fact]
    public void Quando_o_mesmo_aluno_tem_duas_linhas_o_mapa_nao_estoura()
    {
        // Não deveria acontecer (a tela grava um por aluno), mas duas linhas antigas não
        // podem derrubar a página inteira com exceção de chave repetida.
        var precos = new List<PrecoDeAluno>
        {
            new() { ProfessorId = 1, NomeAvulso = "Medina", Preco = 100 },
            new() { ProfessorId = 1, NomeAvulso = "medina", Preco = 90 },
        };

        var mapa = PrecoDaAula.PorAluno(precos);

        Assert.Single(mapa);
        Assert.Equal(90, mapa[PrecoDaAula.Chave(null, "Medina")]);
    }
}
