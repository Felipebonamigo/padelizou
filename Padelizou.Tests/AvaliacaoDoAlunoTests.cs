using Padelizou.Services;
using static Padelizou.Services.EvolucaoDoAluno;

namespace Padelizou.Tests;

// A avaliação técnica que o professor faz do aluno, do jeito que a planilha dele funciona:
// duas réguas independentes (execução A/B/C/D e acertos 1–10) e a evolução entre duas fichas.
public class AvaliacaoDoAlunoTests
{
    // ── A régua padrão ────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_catalogo_padrao_tem_os_tres_modulos_e_nao_veio_vazio()
    {
        Assert.Equal(new[] { "Iniciante", "Intermediário", "Avançado" }, CatalogoDeFundamentos.Modulos);
        Assert.True(CatalogoDeFundamentos.Todos.Count > 100,
            "a planilha tinha ~146 fundamentos; muito menos que isso é sinal de lista truncada");
    }

    [Fact]
    public void Nenhum_fundamento_repetido_dentro_do_mesmo_modulo()
    {
        // A planilha tinha "testes" três vezes e "utilização 5 taticas" duas. Item repetido
        // vira duas linhas idênticas na ficha, e o professor não sabe em qual dar nota.
        foreach (var modulo in CatalogoDeFundamentos.Modulos)
        {
            var nomes = CatalogoDeFundamentos.DoModulo(modulo).Select(f => f.Nome).ToList();
            Assert.Equal(nomes.Count, nomes.Distinct().Count());
        }
    }

    [Fact]
    public void O_intermediario_e_agrupado_por_familia_e_os_outros_nao()
    {
        // É como a planilha organiza: Iniciante e Avançado são listas corridas.
        Assert.NotEmpty(CatalogoDeFundamentos.FamiliasDe(CatalogoDeFundamentos.Intermediario));
        Assert.Empty(CatalogoDeFundamentos.FamiliasDe(CatalogoDeFundamentos.Iniciante));
        Assert.Empty(CatalogoDeFundamentos.FamiliasDe(CatalogoDeFundamentos.Avancado));
    }

    // ── As duas réguas ────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_ordem_da_execucao_e_D_C_B_A_do_pior_pro_melhor()
    {
        // É disto que a evolução depende pra dizer "subiu" ou "caiu".
        Assert.True(EscalaDeAvaliacao.PesoDaExecucao("A") > EscalaDeAvaliacao.PesoDaExecucao("B"));
        Assert.True(EscalaDeAvaliacao.PesoDaExecucao("B") > EscalaDeAvaliacao.PesoDaExecucao("C"));
        Assert.True(EscalaDeAvaliacao.PesoDaExecucao("C") > EscalaDeAvaliacao.PesoDaExecucao("D"));
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" A ")]
    public void A_letra_aceita_caixa_e_espaco(string digitado)
    {
        Assert.Equal("A", EscalaDeAvaliacao.LimparExecucao(digitado));
    }

    [Theory]
    [InlineData("E")]
    [InlineData("")]
    [InlineData(null)]
    public void Letra_que_nao_existe_vira_null_e_nao_nota(string? digitado)
    {
        Assert.Null(EscalaDeAvaliacao.LimparExecucao(digitado));
        Assert.False(EscalaDeAvaliacao.ExecucaoValida(digitado));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(null)]
    public void Acertos_fora_de_1_a_10_nao_valem(int? valor)
    {
        Assert.Null(EscalaDeAvaliacao.LimparAcertos(valor));
    }

    [Theory]
    [InlineData(9, "Aplicável em jogo")]
    [InlineData(6, "Bom")]
    [InlineData(4, "Médio")]
    [InlineData(2, "Baixo")]
    public void A_faixa_de_acertos_e_a_da_planilha(int acertos, string esperado)
    {
        Assert.Equal(esperado, EscalaDeAvaliacao.FaixaDeAcertos(acertos));
    }

    [Fact]
    public void Linha_em_branco_NAO_e_nota()
    {
        // O professor passa por 146 fundamentos e marca meia dúzia. Gravar o resto como "sem
        // nota" faria a evolução comparar nada com nada e anunciar mudança onde não houve.
        Assert.False(EscalaDeAvaliacao.TemNota(null, null));
        Assert.False(EscalaDeAvaliacao.TemNota("", null));
        Assert.True(EscalaDeAvaliacao.TemNota("B", null));
        Assert.True(EscalaDeAvaliacao.TemNota(null, 7));
    }

    // ── A evolução ────────────────────────────────────────────────────────────────────────

    private static NotaLida Nota(int id, string? execucao, int? acertos = null) =>
        new(id, $"Fundamento {id}", "", execucao, acertos);

    [Fact]
    public void Subir_de_C_pra_B_e_evolucao()
    {
        var m = Assert.Single(Comparar(new[] { Nota(1, "C") }, new[] { Nota(1, "B") }));

        Assert.Equal(Rumo.Subiu, m.Rumo);
        Assert.Equal("C", m.Antes);
        Assert.Equal("B", m.Agora);
    }

    [Fact]
    public void Cair_de_B_pra_C_e_queda()
    {
        Assert.Equal(Rumo.Caiu, Comparar(new[] { Nota(1, "B") }, new[] { Nota(1, "C") }).Single().Rumo);
    }

    [Fact]
    public void Mesma_letra_com_mais_acertos_TAMBEM_e_evolucao()
    {
        // É o progresso mais comum entre duas aulas seguidas: a execução ainda não mudou de
        // patamar, mas a bola passou a entrar. Ignorar isso apagaria quase toda a evolução
        // real do aluno.
        var m = Comparar(new[] { Nota(1, "B", 5) }, new[] { Nota(1, "B", 8) }).Single();

        Assert.Equal(Rumo.Subiu, m.Rumo);
        Assert.Equal(5, m.AcertosAntes);
        Assert.Equal(8, m.AcertosAgora);
    }

    [Fact]
    public void A_execucao_manda_mais_que_os_acertos()
    {
        // Subiu de C pra B errando mais: continua sendo evolução técnica. É a régua que o
        // professor usa — a letra é o que ele ensina.
        var m = Comparar(new[] { Nota(1, "C", 9) }, new[] { Nota(1, "B", 4) }).Single();

        Assert.Equal(Rumo.Subiu, m.Rumo);
    }

    [Fact]
    public void Fundamento_avaliado_pela_primeira_vez_e_NOVO_e_nao_subiu()
    {
        // O aluno não melhorou nada ali — o professor é que passou a avaliar aquilo. Chamar
        // de "subiu" seria dar uma boa notícia que não aconteceu.
        var m = Comparar(Array.Empty<NotaLida>(), new[] { Nota(1, "A") }).Single();

        Assert.Equal(Rumo.Novo, m.Rumo);
        Assert.Null(m.Antes);
    }

    [Fact]
    public void Fundamento_que_sumiu_da_ficha_nova_NAO_vira_queda()
    {
        // O professor avalia o que trabalhou na aula. Ausência não é piora — inventar uma
        // notícia ruim a partir de silêncio é o pior erro que esta tela poderia cometer.
        var movimentos = Comparar(new[] { Nota(1, "A"), Nota(2, "B") }, new[] { Nota(1, "A") });

        Assert.Single(movimentos);
        Assert.Equal("Fundamento 1", movimentos[0].Fundamento);
    }

    [Fact]
    public void Nota_igual_e_igual()
    {
        Assert.Equal(Rumo.Igual, Comparar(new[] { Nota(1, "B", 6) }, new[] { Nota(1, "B", 6) }).Single().Rumo);
    }

    [Fact]
    public void O_resumo_conta_o_que_mudou()
    {
        var movimentos = Comparar(
            new[] { Nota(1, "C"), Nota(2, "B"), Nota(3, "A") },
            new[] { Nota(1, "B"), Nota(2, "C"), Nota(3, "A"), Nota(4, "D") });

        var resumo = EvolucaoDoAluno.Resumo(movimentos);

        Assert.Contains("1 subiu", resumo);
        Assert.Contains("1 caiu", resumo);
        Assert.Contains("1 novo", resumo);
    }

    [Fact]
    public void Sem_mudanca_o_resumo_diz_isso_em_vez_de_ficar_vazio()
    {
        var movimentos = Comparar(new[] { Nota(1, "B") }, new[] { Nota(1, "B") });

        Assert.Equal("Sem mudanças desde a ficha anterior", EvolucaoDoAluno.Resumo(movimentos));
    }
}
