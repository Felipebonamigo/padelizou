using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Aviso de quadra atrasada: atraso É um fato de relógio — ao contrário do "seu jogo é o
// próximo", que é disparado pelo fim do jogo anterior. Quem espera fora do ginásio não tem
// como saber que a grade escorregou; o aviso diz quanto, e que a vez não foi perdida.
public class QuadraAtrasadaTests
{
    private static readonly DateTime Agora = new(2026, 7, 29, 15, 0, 0);

    private static Partida Nova(string status = "Agendada", DateTime? previsto = null,
        DateTime? inicioReal = null, DateTime? fimReal = null,
        DateTime? avisoAtraso = null, DateTime? avisoProximo = null, string? quadra = null)
    {
        return new Partida
        {
            Id = Random.Shared.Next(1, 1_000_000),
            Codigo = "T",
            Status = status,
            HorarioPrevisto = previsto,
            HorarioInicioReal = inicioReal,
            HorarioFimReal = fimReal,
            AvisoAtrasoEnviadoEm = avisoAtraso,
            AvisoProximoEnviadoEm = avisoProximo,
            NomeQuadra = quadra,
        };
    }

    // Uma partida ao vivo agora: prova que o dia do torneio começou.
    private static Partida DiaRolando() => Nova(status: "AoVivo", previsto: Agora.AddHours(-1));

    [Fact]
    public void Jogo_20_minutos_atrasado_com_torneio_rolando_recebe_o_aviso()
    {
        var atrasada = Nova(previsto: Agora.AddMinutes(-20));

        var quem = QuadraAtrasada.Atrasadas(new[] { DiaRolando(), atrasada }, Agora);

        Assert.Single(quem);
        Assert.Equal(atrasada.Id, quem[0].Id);
    }

    [Fact]
    public void Atraso_menor_que_a_tolerancia_nao_e_atraso()
    {
        // Grade de torneio escorrega minutos o tempo todo. Avisar no primeiro minuto
        // ensinaria todo mundo a ignorar o aviso.
        var quase = Nova(previsto: Agora.AddMinutes(-14));

        Assert.Empty(QuadraAtrasada.Atrasadas(new[] { DiaRolando(), quase }, Agora));
    }

    [Fact]
    public void Torneio_onde_nada_rolou_hoje_nao_dispara_atraso_nenhum()
    {
        // O organizador ainda não colocou o dia no ar. Sem esta trava, um torneio que
        // começa às 9h com o portão fechado pushparia "atrasado" pra todos os inscritos.
        var a = Nova(previsto: Agora.AddMinutes(-30));
        var b = Nova(previsto: Agora.AddMinutes(-50));

        Assert.Empty(QuadraAtrasada.Atrasadas(new[] { a, b }, Agora));
    }

    [Fact]
    public void Final_de_ontem_nao_conta_como_atividade_de_hoje()
    {
        // Torneio de dois dias: a final de sábado não pode fazer o domingo "já ter
        // começado" antes de o primeiro jogo do dia rolar.
        var ontem = Nova(status: "Finalizada", previsto: Agora.AddDays(-1),
            fimReal: Agora.AddDays(-1).AddHours(1));
        var atrasadaDeHoje = Nova(previsto: Agora.AddMinutes(-30));

        Assert.Empty(QuadraAtrasada.Atrasadas(new[] { ontem, atrasadaDeHoje }, Agora));
        Assert.False(QuadraAtrasada.TorneioEmAtividade(new[] { ontem }, Agora));
    }

    [Fact]
    public void Jogo_finalizado_hoje_conta_como_atividade()
    {
        var deHoje = Nova(status: "Finalizada", previsto: Agora.AddHours(-2),
            fimReal: Agora.AddHours(-1));

        Assert.True(QuadraAtrasada.TorneioEmAtividade(new[] { deHoje }, Agora));
    }

    [Fact]
    public void Quem_ja_ouviu_seu_jogo_e_o_proximo_nao_recebe_aviso_de_atraso()
    {
        // Os dois avisos juntos se contradizem: "fique por perto, você é o próximo" e
        // "sua quadra está atrasada" na mesma partida confundem em vez de informar.
        var jaAvisada = Nova(previsto: Agora.AddMinutes(-25), avisoProximo: Agora.AddMinutes(-5));

        Assert.Empty(QuadraAtrasada.Atrasadas(new[] { DiaRolando(), jaAvisada }, Agora));
    }

    [Fact]
    public void O_aviso_sai_uma_vez_so_por_partida()
    {
        var jaRecebeu = Nova(previsto: Agora.AddMinutes(-40), avisoAtraso: Agora.AddMinutes(-20));

        Assert.Empty(QuadraAtrasada.Atrasadas(new[] { DiaRolando(), jaRecebeu }, Agora));
    }

    [Fact]
    public void Atraso_alem_do_teto_nao_gera_aviso()
    {
        // Com horas de atraso não é "fique por perto", é torneio com problema — e um jogo
        // de ONTEM nunca lançado, num torneio de dois dias, cairia exatamente aqui.
        var antiga = Nova(previsto: Agora.AddHours(-4));

        Assert.Empty(QuadraAtrasada.Atrasadas(new[] { DiaRolando(), antiga }, Agora));
    }

    [Fact]
    public void Sem_horario_previsto_nao_ha_o_que_atrasar()
    {
        var semHora = Nova(previsto: null);

        Assert.Empty(QuadraAtrasada.Atrasadas(new[] { DiaRolando(), semHora }, Agora));
    }

    [Fact]
    public void Jogo_ao_vivo_ou_finalizado_nao_recebe_aviso_de_atraso()
    {
        var aoVivo = Nova(status: "AoVivo", previsto: Agora.AddMinutes(-30));
        var acabou = Nova(status: "Finalizada", previsto: Agora.AddMinutes(-30), fimReal: Agora);

        Assert.Empty(QuadraAtrasada.Atrasadas(new[] { aoVivo, acabou }, Agora));
    }

    [Fact]
    public void Varias_atrasadas_saem_em_ordem_de_horario()
    {
        var depois = Nova(previsto: Agora.AddMinutes(-20));
        var antes = Nova(previsto: Agora.AddMinutes(-40));

        var quem = QuadraAtrasada.Atrasadas(new[] { DiaRolando(), depois, antes }, Agora);

        Assert.Equal(new[] { antes.Id, depois.Id }, quem.Select(p => p.Id).ToArray());
    }

    [Fact]
    public void A_mensagem_diz_horario_atraso_e_que_a_vez_nao_foi_perdida()
    {
        var comQuadra = Nova(previsto: Agora.AddMinutes(-25), quadra: "Quadra 2");

        var corpo = QuadraAtrasada.Corpo(comQuadra, Agora);

        Assert.Contains("14:35", corpo);
        Assert.Contains("Quadra 2", corpo);
        Assert.Contains("25 min", corpo);
        Assert.Contains("não perdeu a vez", corpo);

        // Torneio pequeno não nomeia quadra — a frase precisa continuar fazendo sentido.
        var semQuadra = Nova(previsto: Agora.AddMinutes(-25));
        Assert.DoesNotContain(" na ", QuadraAtrasada.Corpo(semQuadra, Agora).Split('.')[0]);
    }
}
