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
    // Os tamanhos vêm como pares (quantos alunos, quanto custa a aula inteira) porque a
    // tabela do local deixou de ser duas colunas: agora é uma linha por tamanho, até seis.
    private static LocalAula Local(decimal individual, params (int Alunos, decimal Preco)[] turmas) =>
        new()
        {
            Nome = "Batata Padel",
            PrecoPadrao = individual,
            PrecosDeTurma = turmas
                .Select(t => new PrecoDeTurma { QuantidadeAlunos = t.Alunos, Preco = t.Preco })
                .ToList(),
        };

    [Theory]
    [InlineData(1, 110)]
    [InlineData(2, 150)]
    [InlineData(3, 180)]
    [InlineData(4, 200)]
    [InlineData(5, 220)]
    [InlineData(6, 240)]
    public void Cada_tamanho_cobra_o_seu_preco(int alunos, decimal esperado)
    {
        // Até seis por causa da turma de beach: era três, e o professor lançava o sexteto
        // como trio pra depois corrigir o valor na mão — em toda aula.
        var local = Local(110, (2, 150), (3, 180), (4, 200), (5, 220), (6, 240));
        Assert.Equal(esperado, PrecoDaAula.DoLocal(local, alunos));
    }

    [Fact]
    public void Tamanho_sem_preco_cai_pro_menor_mais_proximo_que_o_professor_informou()
    {
        // Preencheu dupla e deixou trio em branco: mostrar o valor da dupla (perto, dá pra
        // ajustar pra cima) é melhor que mostrar o individual, que cobraria três pessoas
        // pelo preço de uma.
        var local = Local(110, (2, 150));
        Assert.Equal(150, PrecoDaAula.DoLocal(local, 3));

        // Sem nenhum dos dois, só existe o preço que ele informou.
        var soIndividual = Local(110);
        Assert.Equal(110, PrecoDaAula.DoLocal(soIndividual, 2));
        Assert.Equal(110, PrecoDaAula.DoLocal(soIndividual, 3));
    }

    [Fact]
    public void A_queda_e_degrau_a_degrau_ate_o_maior_tamanho_que_ele_anunciou()
    {
        // Anunciou dupla e quarteto, nada acima. O sexteto vale o quarteto — que é o vizinho
        // de baixo —, e não o individual: seis pessoas pelo preço de uma seria o professor
        // dando cinco lugares de graça sem perceber.
        var local = Local(110, (2, 150), (4, 200));

        Assert.Equal(200, PrecoDaAula.DoLocal(local, 6));
        Assert.Equal(200, PrecoDaAula.DoLocal(local, 5));
        Assert.Equal(200, PrecoDaAula.DoLocal(local, 4));
        Assert.Equal(150, PrecoDaAula.DoLocal(local, 3));
        Assert.Equal(150, PrecoDaAula.DoLocal(local, 2));
        Assert.Equal(110, PrecoDaAula.DoLocal(local, 1));
    }

    [Fact]
    public void Local_sem_Include_dos_precos_nao_estoura()
    {
        // Um local carregado do banco sem o Include chega com a coleção vazia. Isso é BUG de
        // quem consultou — e o preço sai errado, o que é ruim —, mas não pode ser uma página
        // fora do ar na mão do professor no meio da quadra.
        var local = new LocalAula { Nome = "Sem include", PrecoPadrao = 110, PrecosDeTurma = null! };

        Assert.Equal(110, PrecoDaAula.DoLocal(local, 6));
    }

    [Fact]
    public void Os_tamanhos_de_turma_vao_de_dois_ate_o_teto()
    {
        // A individual fica de fora: ela não é turma, o preço dela é o do local.
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, PrecoDaAula.TamanhosDeTurma);
    }

    [Fact]
    public void Tamanho_repetido_na_tabela_nao_derruba_a_leitura()
    {
        // O índice único do banco impede, mas a leitura não pode depender disso pra não
        // estourar com "chave repetida" — mesma escolha que PorAluno já fazia.
        var mapa = PrecoDaAula.PorTamanho(new List<PrecoDeTurma>
        {
            new() { QuantidadeAlunos = 6, Preco = 240 },
            new() { QuantidadeAlunos = 6, Preco = 260 },
        });

        Assert.Equal(260, Assert.Single(mapa).Value);
    }

    [Fact]
    public void Preco_combinado_vale_na_individual_daquele_aluno()
    {
        var local = Local(110, (2, 150));
        Assert.Equal(90, PrecoDaAula.Sugerido(local, quantidadeAlunos: 1, precoCombinado: 90));
    }

    [Fact]
    public void Preco_combinado_nao_derruba_o_valor_da_turma()
    {
        // O desconto foi dado a UMA PESSOA, não à quadra inteira: se valesse aqui, o
        // professor daria de graça o lugar dos acompanhantes sem perceber — e num sexteto
        // seriam cinco lugares.
        var local = Local(110, (2, 150), (3, 180), (6, 240));
        Assert.Equal(150, PrecoDaAula.Sugerido(local, quantidadeAlunos: 2, precoCombinado: 90));
        Assert.Equal(180, PrecoDaAula.Sugerido(local, quantidadeAlunos: 3, precoCombinado: 90));
        Assert.Equal(240, PrecoDaAula.Sugerido(local, quantidadeAlunos: 6, precoCombinado: 90));
    }

    [Fact]
    public void Sem_acordo_a_individual_cobra_a_tabela_do_local()
    {
        var local = Local(110, (2, 150));
        Assert.Equal(110, PrecoDaAula.Sugerido(local, quantidadeAlunos: 1, precoCombinado: null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(7)]
    [InlineData(99)]
    public void Quantidade_fora_da_faixa_vira_aula_individual(int digitado)
    {
        // O formulário manda o que o navegador quiser. O professor quer marcar a aula, não
        // discutir o campo — então o valor esquisito vira o caso normal em vez de erro.
        // ⚠️ 7 e não 4: com o teto em seis, quatro alunos é aula de verdade agora.
        Assert.Equal(1, PrecoDaAula.Tamanho(digitado));
        Assert.Equal("Individual", PrecoDaAula.Rotulo(digitado));
        Assert.Equal(110, PrecoDaAula.DoLocal(Local(110, (2, 150)), digitado));
    }

    [Theory]
    [InlineData(1, "Individual")]
    [InlineData(2, "Em dupla")]
    [InlineData(3, "Em trio")]
    [InlineData(4, "Em quarteto")]
    [InlineData(5, "Em quinteto")]
    [InlineData(6, "Em sexteto")]
    public void Rotulo_diz_o_tamanho_em_portugues(int alunos, string esperado) =>
        Assert.Equal(esperado, PrecoDaAula.Rotulo(alunos));

    [Theory]
    [InlineData(1, "Só eu")]
    [InlineData(2, "Nós dois")]
    [InlineData(6, "Nós seis")]
    [InlineData(99, "Só eu")]
    public void O_aluno_le_o_mesmo_tamanho_da_cadeira_dele(int alunos, string esperado) =>
        // Ele não marca "em sexteto", ele marca "nós seis" — e a primeira pessoa é ele.
        Assert.Equal(esperado, PrecoDaAula.RotuloDoAluno(alunos));

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
