using Padelizou.Services;

namespace Padelizou.Tests;

// A prévia do mata-mata por COLOCAÇÃO ("1º do Grupo A x 2º do Grupo C"), mostrada enquanto a
// fase de grupos ainda está rolando. Ela passa pelo mesmo motor do sorteio de verdade — o
// risco é justamente esse deixar de valer e a tela prometer um cruzamento que não acontece.
public class ChaveProjetadaTests
{
    [Fact]
    public void Quatro_grupos_projetam_oito_vagas_em_quatro_jogos()
    {
        var (fase, confrontos) = ChaveProjetada.Montar(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        Assert.Equal("Quartas de Final", fase);
        Assert.Equal(4, confrontos.Count);

        // Toda vaga aparece uma vez só: 4 grupos x 2 classificados = 8 lugares no quadro.
        var vagas = confrontos.SelectMany(c => new[] { c.Lado1.Rotulo, c.Lado2.Rotulo }).ToList();
        Assert.Equal(8, vagas.Count);
        Assert.Equal(8, vagas.Distinct().Count());
    }

    [Fact]
    public void Primeiro_nunca_cruza_com_primeiro_na_estreia()
    {
        // A semeadura serve pra isso: os melhores entram por lados opostos e só se encontram
        // no fim. Se dois primeiros caíssem juntos na estreia, a prévia estaria denunciando
        // um chaveamento torto.
        var (_, confrontos) = ChaveProjetada.Montar(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        Assert.All(confrontos, c =>
            Assert.False(c.Lado1.Posicao == 1 && c.Lado2.Posicao == 1,
                $"{c.Lado1.Rotulo} x {c.Lado2.Rotulo} põe dois primeiros na estreia"));
    }

    [Fact]
    public void Ninguem_estreia_contra_o_vizinho_do_proprio_grupo()
    {
        // Quem dividiu grupo já se enfrentou; reeditar o jogo na primeira fase do mata-mata
        // é o que a semeadura evita.
        var (_, confrontos) = ChaveProjetada.Montar(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        Assert.All(confrontos, c => Assert.NotEqual(c.Lado1.Grupo, c.Lado2.Grupo));
    }

    [Fact]
    public void Tres_grupos_cortam_pro_quadro_de_quatro_e_todo_primeiro_passa()
    {
        // 3 grupos x 2 = 6 candidatos, mas o quadro é de 4: sobram os 3 primeiros e o melhor
        // segundo. Nenhum primeiro colocado pode ficar de fora.
        var (fase, confrontos) = ChaveProjetada.Montar(new[] { "Grupo A", "Grupo B", "Grupo C" });

        Assert.Equal("Semifinal", fase);
        Assert.Equal(2, confrontos.Count);

        var vagas = confrontos.SelectMany(c => new[] { c.Lado1, c.Lado2 }).ToList();
        Assert.Equal(3, vagas.Count(v => v.Posicao == 1));
        Assert.Equal(1, vagas.Count(v => v.Posicao == 2));
    }

    [Fact]
    public void Um_grupo_so_fecha_em_final_direta()
    {
        var (fase, confrontos) = ChaveProjetada.Montar(new[] { "Grupo A" });

        Assert.Equal("Final", fase);
        var jogo = Assert.Single(confrontos);
        Assert.Equal("1º do Grupo A", jogo.Lado1.Rotulo);
        Assert.Equal("2º do Grupo A", jogo.Lado2.Rotulo);
    }

    [Fact]
    public void Categoria_de_times_projeta_com_o_numero_que_o_organizador_definiu()
    {
        // A de times deixa o organizador escolher quantos passam por grupo — a prévia
        // precisa respeitar isso, senão promete um quadro que não é o dele.
        var (fase, confrontos) = ChaveProjetada.Montar(new[] { "Grupo A", "Grupo B" }, classificadosPorGrupo: 4);

        // 2 grupos x 4 = 8 classificados, que é um quadro de 8 — Quartas. (Oitavas seriam 16.)
        Assert.Equal("Quartas de Final", fase);
        Assert.Equal(4, confrontos.Count);

        var posicoes = confrontos.SelectMany(c => new[] { c.Lado1.Posicao, c.Lado2.Posicao }).Distinct().Order();
        Assert.Equal(new[] { 1, 2, 3, 4 }, posicoes);
    }

    [Fact]
    public void Sem_grupo_nenhum_nao_ha_o_que_projetar()
    {
        var (_, confrontos) = ChaveProjetada.Montar(Array.Empty<string>());
        Assert.Empty(confrontos);
    }

    // A regra escrita pelo Felipe: o 1º do primeiro grupo pega o 2º do ÚLTIMO grupo, o 2º do
    // primeiro pega o 1º do último, e assim caminhando pro meio. É o cruzamento clássico —
    // fica travado aqui porque é o que a tela promete.
    [Fact]
    public void O_primeiro_de_um_grupo_cruza_com_o_segundo_do_grupo_espelhado()
    {
        var (_, confrontos) = ChaveProjetada.Montar(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        var comoFicou = confrontos
            .Select(c => $"{c.Lado1.Rotulo} x {c.Lado2.Rotulo}")
            .ToList();

        Assert.Equal(new[]
        {
            "1º do Grupo A x 2º do Grupo D",
            "1º do Grupo B x 2º do Grupo C",
            "1º do Grupo C x 2º do Grupo B",
            "1º do Grupo D x 2º do Grupo A",
        }, comoFicou);
    }

    // ---- O caminho inteiro até a final ----

    [Fact]
    public void Quatro_grupos_mapeiam_quartas_semifinal_e_final()
    {
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        Assert.Equal(new[] { "Quartas de Final", "Semifinal", "Final" },
                     rodadas.Select(r => r.Fase));
        Assert.Equal(new[] { 4, 2, 1 }, rodadas.Select(r => r.Jogos.Count));
    }

    [Fact]
    public void Da_segunda_rodada_em_diante_o_lado_e_o_vencedor_de_um_jogo_numerado()
    {
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        // Os jogos são numerados na ordem em que nascem — a MESMA ordem que o robô lê pra
        // montar a fase seguinte. Quartas 1..4, semis 5 e 6, final 7.
        Assert.Equal(new[] { 1, 2, 3, 4 }, rodadas[0].Jogos.Select(j => j.Numero));
        Assert.Equal(new[] { 5, 6 }, rodadas[1].Jogos.Select(j => j.Numero));
        Assert.Equal(new[] { 7 }, rodadas[2].Jogos.Select(j => j.Numero));

        // Primeiro x último, como faz o ParearVencedores de verdade.
        Assert.Equal("Vencedor do jogo 1", rodadas[1].Jogos[0].Lado1);
        Assert.Equal("Vencedor do jogo 4", rodadas[1].Jogos[0].Lado2);
        Assert.Equal("Vencedor do jogo 2", rodadas[1].Jogos[1].Lado1);
        Assert.Equal("Vencedor do jogo 3", rodadas[1].Jogos[1].Lado2);

        Assert.Equal("Vencedor do jogo 5", rodadas[2].Jogos[0].Lado1);
        Assert.Equal("Vencedor do jogo 6", rodadas[2].Jogos[0].Lado2);
    }

    [Fact]
    public void Os_dois_lados_da_chave_so_se_encontram_na_final()
    {
        // O teste que dá sentido ao cruzamento: quem entra por cima e quem entra por baixo
        // não pode esbarrar antes da decisão.
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        var semi1 = rodadas[1].Jogos[0];
        var semi2 = rodadas[1].Jogos[1];
        var deCima = new[] { semi1.Lado1, semi1.Lado2 };
        var deBaixo = new[] { semi2.Lado1, semi2.Lado2 };

        Assert.Empty(deCima.Intersect(deBaixo));
        Assert.Single(rodadas[^1].Jogos);
        Assert.Equal("Final", rodadas[^1].Fase);
    }

    [Fact]
    public void Um_grupo_so_mapeia_a_final_e_para()
    {
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A" });

        var rodada = Assert.Single(rodadas);
        Assert.Equal("Final", rodada.Fase);
        Assert.Single(rodada.Jogos);
    }
}
