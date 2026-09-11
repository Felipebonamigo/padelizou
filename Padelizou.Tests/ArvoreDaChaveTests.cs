using System.Globalization;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — A GEOMETRIA DA ÁRVORE DA PRÉVIA.
//
// A prévia do mata-mata passou de quatro colunas soltas (uma por rodada, sem nada ligando) pra
// uma ÁRVORE de verdade: no computador de cima pra baixo, no celular deitada e arrastável. Os
// dois desenhos saem do MESMO cálculo, que é este serviço — desenho é CSS, posição é conta, e
// conta duplicada entre duas views diverge no dia em que a regra mudar.
//
// A conta é uma só: a largura de um jogo é a soma das larguras dos jogos que o alimentam (1 se
// nenhum alimenta), e a coluna sai de percorrer a árvore da final pra trás. É isso que centraliza
// cada jogo sobre os dois que o produzem SEM cálculo de pixel — a grade do CSS faz o resto.
//
// ⚠️ A ORDEM DA RODADA NÃO É A NUMÉRICA, e é o ponto que mais engana: numa chave de 4, a primeira
// rodada aparece 1, 4, 2, 3, porque o jogo 5 nasce do 1 e do 4. Desenhar em ordem numérica faria
// as linhas cruzarem no meio do quadro.
public class ArvoreDaChaveTests
{
    private static ArvoreDaChave.Quadro Quadro(params string[] grupos) =>
        ArvoreDaChave.Montar(ChaveProjetada.MontarCompleta(grupos));

    private static ArvoreDaChave.Vaga Vaga(ArvoreDaChave.Quadro quadro, int numeroDoJogo) =>
        quadro.Rodadas.SelectMany(r => r.Vagas).Single(v => v.Jogo.Numero == numeroDoJogo);

    [Fact]
    public void Quatro_grupos_dao_uma_arvore_de_quatro_colunas()
    {
        var quadro = Quadro("Grupo A", "Grupo B", "Grupo C", "Grupo D");

        Assert.Equal(4, quadro.Colunas);
        Assert.Equal(new[] { "Quartas de Final", "Semifinal", "Final" },
                     quadro.Rodadas.Select(r => r.Fase));
    }

    [Fact]
    public void Cada_jogo_ocupa_as_colunas_dos_jogos_que_o_alimentam()
    {
        // 5 = vencedor do 1 x vencedor do 4; 6 = do 2 x do 3; 7 = do 5 x do 6. Então o 5 tem que
        // cobrir as colunas do 1 e do 4, e a final cobre a chave inteira — é o que põe o cartão
        // no meio dos dois que o produzem.
        var quadro = Quadro("Grupo A", "Grupo B", "Grupo C", "Grupo D");

        Assert.Equal((0, 2), (Vaga(quadro, 5).Coluna, Vaga(quadro, 5).Largura));
        Assert.Equal((2, 2), (Vaga(quadro, 6).Coluna, Vaga(quadro, 6).Largura));
        Assert.Equal((0, 4), (Vaga(quadro, 7).Coluna, Vaga(quadro, 7).Largura));
    }

    [Fact]
    public void A_ordem_dentro_da_rodada_e_a_da_arvore_e_nao_a_numerica()
    {
        var quadro = Quadro("Grupo A", "Grupo B", "Grupo C", "Grupo D");

        Assert.Equal(new[] { 1, 4, 2, 3 }, quadro.Rodadas[0].Vagas.Select(v => v.Jogo.Numero));
        Assert.Equal(new[] { 0, 1, 2, 3 }, quadro.Rodadas[0].Vagas.Select(v => v.Coluna));
    }

    [Fact]
    public void Quem_passou_direto_nao_ocupa_coluna_propria()
    {
        // 3 grupos = 6 classificados num quadro de 8: dois jogos de abertura e dois que passam
        // direto. O lado que passou direto não tem jogo alimentando, então a semifinal dele é
        // tão larga quanto o único jogo que a alimenta — a chave tem 2 colunas, não 4.
        var quadro = Quadro("Grupo A", "Grupo B", "Grupo C");

        Assert.Equal(2, quadro.Colunas);
        Assert.Equal(1, Vaga(quadro, 3).Largura);
        Assert.Equal(2, Vaga(quadro, 5).Largura);
    }

    [Fact]
    public void Um_grupo_so_da_um_quadro_de_uma_coluna()
    {
        var quadro = Quadro("Grupo A");

        Assert.Equal(1, quadro.Colunas);
        var rodada = Assert.Single(quadro.Rodadas);
        Assert.Equal("Final", rodada.Fase);
        Assert.Equal(0, Assert.Single(rodada.Vagas).Coluna);
    }

    [Fact]
    public void Sem_jogo_nenhum_o_quadro_vem_vazio()
    {
        var quadro = ArvoreDaChave.Montar(new List<ChaveProjetada.RodadaProjetada>());

        Assert.Equal(0, quadro.Colunas);
        Assert.Empty(quadro.Rodadas);
    }

    // ⚠️ A GUARDA CONTRA PERDER JOGO CALADO: a árvore é montada da final pra trás, seguindo quem
    // alimenta quem. Um jogo que ficasse fora dessa corrente sumiria da tela sem erro nenhum — o
    // pior jeito de quebrar. Isto confere que todo jogo projetado entra no quadro, uma vez só, em
    // toda quantidade de grupos que o sistema aceita.
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(11)]
    [InlineData(16)]
    public void Todo_jogo_projetado_entra_no_quadro_uma_vez_so(int quantosGrupos)
    {
        var grupos = Enumerable.Range(0, quantosGrupos).Select(i => $"Grupo {(char)('A' + i)}").ToArray();
        var rodadas = ChaveProjetada.MontarCompleta(grupos);

        var quadro = ArvoreDaChave.Montar(rodadas);

        var projetados = rodadas.SelectMany(r => r.Jogos).Select(j => j.Numero).OrderBy(n => n);
        var noQuadro = quadro.Rodadas.SelectMany(r => r.Vagas).Select(v => v.Jogo.Numero).OrderBy(n => n);
        Assert.Equal(projetados, noQuadro);
    }

    // Cada rodada preenche a faixa inteira de colunas sem buraco nem sobreposição: é o que segura
    // as linhas de ligação apontando pro cartão certo.
    [Theory]
    [InlineData(4)] [InlineData(6)] [InlineData(8)] [InlineData(16)]
    public void As_vagas_de_uma_rodada_cobrem_a_largura_sem_buraco(int quantosGrupos)
    {
        var grupos = Enumerable.Range(0, quantosGrupos).Select(i => $"Grupo {(char)('A' + i)}").ToArray();

        var quadro = ArvoreDaChave.Montar(ChaveProjetada.MontarCompleta(grupos));

        foreach (var rodada in quadro.Rodadas)
        {
            int esperada = 0;
            foreach (var vaga in rodada.Vagas)
            {
                Assert.Equal(esperada, vaga.Coluna);
                esperada += vaga.Largura;
            }
            Assert.Equal(quadro.Colunas, esperada);
        }
    }

    // O lado que vem de um jogo anterior precisa dizer QUAL — é o que liga a árvore. Sem isso a
    // geometria teria que adivinhar pelo texto do rótulo, que é tradução, não dado.
    [Fact]
    public void O_lado_que_vem_de_outro_jogo_carrega_o_numero_dele()
    {
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        var semi = rodadas[1].Jogos[0];
        Assert.Equal(1, semi.VemDoJogo1);
        Assert.Equal(4, semi.VemDoJogo2);

        var abertura = rodadas[0].Jogos[0];
        Assert.Null(abertura.VemDoJogo1);
        Assert.Null(abertura.VemDoJogo2);
    }

    // ── A LIGAÇÃO ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_vaga_da_primeira_rodada_nao_tem_ligacao()
    {
        var quadro = Quadro("Grupo A", "Grupo B", "Grupo C", "Grupo D");

        Assert.Null(quadro.Ligacao(Vaga(quadro, 1)));
    }

    [Fact]
    public void A_ligacao_entra_no_centro_de_cada_jogo_que_alimenta_a_vaga()
    {
        // O jogo 5 cobre as colunas do 1 e do 4, então os talos entram a 25% e a 75% da largura
        // dele — e a linha que os junta anda os 50% do meio.
        var quadro = Quadro("Grupo A", "Grupo B", "Grupo C", "Grupo D");

        var liga = Assert.IsType<(bool Reta, string Estilo)>(quadro.Ligacao(Vaga(quadro, 5)));
        Assert.False(liga.Reta);
        Assert.Equal("--a:25%;--hw:50%", liga.Estilo);
    }

    [Fact]
    public void Com_um_alimentador_so_a_ligacao_e_reta()
    {
        // 3 grupos: a semifinal recebe um vencedor e um que passou direto. Não há dois pontos
        // pra juntar, então o talo desce no meio e o cotovelo não é desenhado.
        var quadro = Quadro("Grupo A", "Grupo B", "Grupo C");

        var liga = Assert.IsType<(bool Reta, string Estilo)>(quadro.Ligacao(Vaga(quadro, 3)));
        Assert.True(liga.Reta);
    }

    // 🕳️ O DEFEITO QUE ESTE TESTE EXISTE PRA PEGAR É INVISÍVEL NA SUÍTE E FATAL NO AR: em pt-BR,
    // `(12.5).ToString()` sai "12,5". O navegador não entende vírgula em `calc`/porcentagem e
    // DESCARTA a declaração inteira — sem erro, sem log. O quadro apareceria em produção com os
    // cartões certos e sem nenhuma linha ligando, exatamente na cultura em que o site roda.
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("de-DE")]
    public void A_ligacao_sai_com_PONTO_decimal_em_qualquer_cultura(string cultura)
    {
        var antes = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultura);

            // ⚠️ SÃO SETE GRUPOS, e o número importa: com 4 ou 6 a chave fica simétrica e toda
            // porcentagem sai INTEIRA — o teste passaria mesmo com a formatação errada. Com 7,
            // as duas duplas que passam direto deixam a semifinal com 3 colunas de largura, e
            // a conta cai em 16,667%. Medido: é o menor caso que produz casa decimal.
            var quadro = Quadro("Grupo A", "Grupo B", "Grupo C", "Grupo D",
                                "Grupo E", "Grupo F", "Grupo G");

            var estilos = quadro.Rodadas
                .SelectMany(r => r.Vagas)
                .Select(v => quadro.Ligacao(v)?.Estilo)
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            // Sem esta linha o teste passa à toa no dia em que a chave ficar sem fração.
            Assert.Contains(estilos, e => e!.Contains("16.667%", StringComparison.Ordinal));
            Assert.All(estilos, e => Assert.DoesNotContain(",", e));
        }
        finally
        {
            CultureInfo.CurrentCulture = antes;
        }
    }

    [Fact]
    public void Quem_passou_direto_nao_vem_de_jogo_nenhum()
    {
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A", "Grupo B", "Grupo C" });

        var semi = rodadas[1].Jogos[0];
        Assert.Equal(1, semi.VemDoJogo1);
        Assert.Null(semi.VemDoJogo2);
        Assert.Contains("passou direto", semi.Lado2);
    }
}
