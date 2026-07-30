using Padelizou.Services;

namespace Padelizou.Tests;

// Grade de horários que o clube publica pra marcação. O pedido do Felipe (30/07) foi poder
// pôr preço em vários de uma vez e registrar se o jogo é de 1h ou 1h30; ao ligar isso apareceu
// que a criação nunca conferiu se a duração CABE na janela publicada.
public class HorarioDoClubeTests
{
    private static TimeSpan H(int hora, int min = 0) => new(hora, min, 0);

    [Fact]
    public void Zero_e_nulo_sao_a_mesma_coisa_de_graca()
    {
        // Dois jeitos de dizer "de graça" fariam um R$ 0,00 aparecer na tela do jogador.
        Assert.Null(HorarioDoClube.NormalizarPreco(null));
        Assert.Null(HorarioDoClube.NormalizarPreco(0));
        Assert.Equal(80m, HorarioDoClube.NormalizarPreco(80m));
    }

    [Fact]
    public void Janela_que_comporta_o_jogo_passa()
    {
        Assert.Null(HorarioDoClube.ProblemaCom(H(7), H(17), 60));
        Assert.Null(HorarioDoClube.ProblemaCom(H(19), H(20, 30), 90));
        Assert.Null(HorarioDoClube.ProblemaCom(H(19), H(20), 60));   // cabe exato
    }

    [Fact]
    public void Jogo_que_nao_cabe_na_janela_e_recusado()
    {
        // O caso que morde: 19h–20h publicado com jogo de 1h30 não oferece horário NENHUM
        // pro jogador — sem erro, parecendo clube sem vaga.
        var problema = HorarioDoClube.ProblemaCom(H(19), H(20), 90);

        Assert.NotNull(problema);
        Assert.Contains("1h30", problema);
    }

    [Fact]
    public void Fim_antes_do_inicio_e_recusado()
    {
        Assert.NotNull(HorarioDoClube.ProblemaCom(H(22), H(7), 60));
        Assert.NotNull(HorarioDoClube.ProblemaCom(H(10), H(10), 60));
    }

    [Fact]
    public void Duracao_ridicula_e_recusada()
    {
        Assert.NotNull(HorarioDoClube.ProblemaCom(H(7), H(23), 0));
        Assert.NotNull(HorarioDoClube.ProblemaCom(H(7), H(23), 5));
    }

    [Theory]
    [InlineData(60, "1h")]
    [InlineData(90, "1h30")]
    [InlineData(120, "2h")]
    [InlineData(30, "30 min")]
    [InlineData(45, "45 min")]
    public void O_rotulo_fala_como_a_gente_fala(int minutos, string esperado)
    {
        Assert.Equal(esperado, HorarioDoClube.Rotulo(minutos));
    }

    [Fact]
    public void O_seletor_oferece_as_duracoes_comuns()
    {
        Assert.Equal(new[] { 30, 60, 90, 120 }, HorarioDoClube.Opcoes());
    }

    [Fact]
    public void Duracao_fora_do_comum_nao_some_do_seletor()
    {
        // Editar o preço de um horário de 45 min não pode convertê-lo pra 60 sem ninguém
        // pedir — o valor atual precisa existir entre as opções pra continuar selecionado.
        Assert.Contains(45, HorarioDoClube.Opcoes(45));
        Assert.Equal(new[] { 30, 45, 60, 90, 120 }, HorarioDoClube.Opcoes(45));
    }

    [Fact]
    public void Duracao_comum_nao_aparece_duplicada()
    {
        Assert.Equal(new[] { 30, 60, 90, 120 }, HorarioDoClube.Opcoes(60));
    }
}
