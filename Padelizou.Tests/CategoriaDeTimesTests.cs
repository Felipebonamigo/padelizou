using Padelizou.Services;

namespace Padelizou.Tests;

// A estrutura da categoria de TIMES: o organizador digita quantos times, quantos grupos e
// quantos classificam — e a conta TEM que fechar um quadro de mata-mata na criação, porque
// descobrir na hora do sorteio que um classificado não cabe na chave é promessa quebrada
// com o time já na quadra.
public class CategoriaDeTimesTests
{
    // ---- Configurações que fecham ----

    [Theory]
    [InlineData(4, 2, 2)]    // 2 grupos de 2, semifinal
    [InlineData(8, 2, 2)]    // 2 grupos de 4, semifinal
    [InlineData(8, 4, 2)]    // 4 grupos de 2, quartas
    [InlineData(16, 4, 2)]   // clássico: 4 grupos de 4, quartas
    [InlineData(16, 4, 4)]   // todos passam: oitavas
    [InlineData(12, 4, 1)]   // só os campeões de grupo: semifinal
    public void Configuracao_que_fecha_quadro_e_aceita(int times, int grupos, int classificados)
        => Assert.Null(CategoriaDeTimes.ProblemaNaConfiguracao(times, grupos, classificados));

    [Fact]
    public void Um_grupo_unico_com_dois_classificados_fecha_em_final_direta()
    {
        Assert.Null(CategoriaDeTimes.ProblemaNaConfiguracao(times: 4, grupos: 1, classificados: 2));
    }

    // ---- Recusas, cada uma com a explicação certa ----

    [Fact]
    public void Menos_de_dois_times_nao_tem_torneio()
    {
        Assert.NotNull(CategoriaDeTimes.ProblemaNaConfiguracao(1, 1, 1));
        Assert.NotNull(CategoriaDeTimes.ProblemaNaConfiguracao(null, 2, 2));
    }

    [Fact]
    public void Grupo_de_um_time_so_nao_tem_jogo()
    {
        // 5 times em 3 grupos: alguém fica sozinho.
        var problema = CategoriaDeTimes.ProblemaNaConfiguracao(5, 3, 1);
        Assert.NotNull(problema);
        Assert.Contains("grupo de 1", problema, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Classificar_mais_do_que_o_grupo_tem_e_recusado()
    {
        // 6 times em 2 grupos = grupos de 3; classificar 4 de um grupo de 3 é impossível.
        var problema = CategoriaDeTimes.ProblemaNaConfiguracao(6, 2, 4);
        Assert.NotNull(problema);
    }

    [Fact]
    public void Conta_que_nao_fecha_quadro_e_recusada_com_a_conta_na_mensagem()
    {
        // 3 grupos × 2 classificados = 6 vagas: não é potência de 2 — o pior 2º "se
        // classificaria" e ficaria de fora na hora da chave.
        var problema = CategoriaDeTimes.ProblemaNaConfiguracao(9, 3, 2);
        Assert.NotNull(problema);
        Assert.Contains("6", problema);
        Assert.Contains("quadro", problema, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_sorteio_vale_o_numero_de_times_que_existe_de_verdade()
    {
        // Prometeu 8, cadastrou 3, quer 2 grupos: um grupo teria 1 time só.
        Assert.NotNull(CategoriaDeTimes.ProblemaNoSorteio(3, 2, 2));
        // Cadastrou os 8: fecha.
        Assert.Null(CategoriaDeTimes.ProblemaNoSorteio(8, 2, 2));
    }

    // ---- Distribuição ----

    [Fact]
    public void Resto_da_divisao_engorda_os_primeiros_grupos()
    {
        Assert.Equal(new[] { 4, 3, 3 }, CategoriaDeTimes.TamanhosDosGrupos(10, 3));
        Assert.Equal(new[] { 2, 2 }, CategoriaDeTimes.TamanhosDosGrupos(4, 2));
    }

    [Fact]
    public void Distribuir_nao_perde_nem_repete_ninguem()
    {
        var times = Enumerable.Range(1, 10).ToList();
        var grupos = CategoriaDeTimes.Distribuir(times, 3);

        Assert.Equal(3, grupos.Count);
        Assert.Equal(new[] { 4, 3, 3 }, grupos.Select(g => g.Count).ToArray());
        Assert.Equal(times, grupos.SelectMany(g => g).OrderBy(x => x).ToList());
    }

    // ---- Previsão de jogos (alimenta a grade de horários) ----

    [Fact]
    public void Jogos_de_grupo_sao_todos_contra_todos_em_cada_grupo()
    {
        // 2 grupos de 4: C(4,2) × 2 = 12 jogos.
        Assert.Equal(12, CategoriaDeTimes.JogosDeGrupo(8, 2));
        // 10 em 3 grupos (4,3,3): 6 + 3 + 3 = 12.
        Assert.Equal(12, CategoriaDeTimes.JogosDeGrupo(10, 3));
    }

    [Fact]
    public void Mata_mata_tem_quadro_menos_um_jogos()
    {
        Assert.Equal(3, CategoriaDeTimes.JogosDeMataMata(2, 2));   // semifinal: 4 → 3 jogos
        Assert.Equal(7, CategoriaDeTimes.JogosDeMataMata(4, 2));   // quartas: 8 → 7 jogos
        Assert.Equal(15, CategoriaDeTimes.JogosDeMataMata(4, 4));  // oitavas: 16 → 15 jogos
        Assert.Equal(1, CategoriaDeTimes.JogosDeMataMata(1, 2));   // final direta
    }
}
