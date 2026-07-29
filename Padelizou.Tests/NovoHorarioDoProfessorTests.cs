using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Horário do professor em vários dias de uma vez. As três validações existem porque a tela
// antiga engolia tudo: fim antes do início e aula que não cabe na janela viravam agenda que
// SÓ o aluno via quebrada — como lista vazia, sem erro pra ninguém.
public class NovoHorarioDoProfessorTests
{
    private static readonly TimeSpan Oito = TimeSpan.FromHours(8);
    private static readonly TimeSpan Meiodia = TimeSpan.FromHours(12);

    private static HorarioDisponivel Existente(int local, int dia, TimeSpan inicio, TimeSpan fim, bool ativo = true) =>
        new() { LocalAulaId = local, DiaSemana = dia, HoraInicio = inicio, HoraFim = fim, Ativo = ativo };

    [Fact]
    public void Segunda_quarta_e_sexta_num_pedido_so()
    {
        var plano = NovoHorarioDoProfessor.Planejar(
            new[] { 1, 3, 5 }, Oito, Meiodia, 60, localAulaId: 7, Array.Empty<HorarioDisponivel>());

        Assert.True(plano.Valido);
        Assert.Equal(new[] { 1, 3, 5 }, plano.DiasParaCriar);
        Assert.Empty(plano.DiasQueJaExistiam);
    }

    [Fact]
    public void Nenhum_dia_marcado_e_erro_com_instrucao()
    {
        var plano = NovoHorarioDoProfessor.Planejar(
            Array.Empty<int>(), Oito, Meiodia, 60, 7, Array.Empty<HorarioDisponivel>());

        Assert.False(plano.Valido);
        Assert.Contains("pelo menos um dia", plano.Erro);
    }

    [Fact]
    public void Fim_antes_do_inicio_e_recusado()
    {
        var plano = NovoHorarioDoProfessor.Planejar(
            new[] { 1 }, Meiodia, Oito, 60, 7, Array.Empty<HorarioDisponivel>());

        Assert.False(plano.Valido);
    }

    [Fact]
    public void Aula_que_nao_cabe_na_janela_e_recusada_com_a_conta_na_mensagem()
    {
        // O erro mais traiçoeiro: o horário aparece na lista do professor como se estivesse
        // tudo certo, e o aluno nunca encontra vaga nenhuma.
        var plano = NovoHorarioDoProfessor.Planejar(
            new[] { 1 }, Oito, Oito.Add(TimeSpan.FromMinutes(60)), 90, 7, Array.Empty<HorarioDisponivel>());

        Assert.False(plano.Valido);
        Assert.Contains("90 min", plano.Erro);
        Assert.Contains("08:00", plano.Erro);
        Assert.Contains("09:00", plano.Erro);
    }

    [Fact]
    public void Dia_repetido_no_pedido_conta_uma_vez_e_dia_invalido_e_ignorado()
    {
        var plano = NovoHorarioDoProfessor.Planejar(
            new[] { 1, 1, 9, -2, 3 }, Oito, Meiodia, 60, 7, Array.Empty<HorarioDisponivel>());

        Assert.True(plano.Valido);
        Assert.Equal(new[] { 1, 3 }, plano.DiasParaCriar);
    }

    [Fact]
    public void Dia_que_ja_tem_horario_identico_fica_como_esta()
    {
        var existentes = new[] { Existente(7, 3, Oito, Meiodia) };

        var plano = NovoHorarioDoProfessor.Planejar(new[] { 1, 3 }, Oito, Meiodia, 60, 7, existentes);

        Assert.Equal(new[] { 1 }, plano.DiasParaCriar);
        Assert.Equal(new[] { 3 }, plano.DiasQueJaExistiam);
    }

    [Fact]
    public void Horario_identico_mas_pausado_e_religado_em_vez_de_duplicado()
    {
        // O professor pausou a quarta nas férias e agora recadastra a semana: religar é o
        // que ele quis; criar outra linha igual seria bug na cabeça de quem olha a lista.
        var existentes = new[] { Existente(7, 3, Oito, Meiodia, ativo: false) };

        var plano = NovoHorarioDoProfessor.Planejar(new[] { 3 }, Oito, Meiodia, 60, 7, existentes);

        Assert.Equal(new[] { 3 }, plano.DiasParaReativar);
        Assert.Empty(plano.DiasParaCriar);
    }

    [Fact]
    public void Mesma_janela_em_OUTRO_local_nao_e_duplicata()
    {
        var existentes = new[] { Existente(local: 99, dia: 1, Oito, Meiodia) };

        var plano = NovoHorarioDoProfessor.Planejar(new[] { 1 }, Oito, Meiodia, 60, 7, existentes);

        Assert.Equal(new[] { 1 }, plano.DiasParaCriar);
    }

    [Fact]
    public void O_resumo_fala_como_gente()
    {
        var existentes = new[] { Existente(7, 5, Oito, Meiodia) };
        var plano = NovoHorarioDoProfessor.Planejar(new[] { 1, 3, 5 }, Oito, Meiodia, 60, 7, existentes);

        var resumo = NovoHorarioDoProfessor.Resumo(plano);

        Assert.Contains("segunda e quarta", resumo);
        Assert.Contains("Sexta já tinha esse horário", resumo);
    }
}
