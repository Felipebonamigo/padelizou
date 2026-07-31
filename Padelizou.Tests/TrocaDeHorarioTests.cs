using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A troca de horário entre dois jogos, pedida pelo Felipe em 30/07/2026: depois do sorteio
// o organizador pode trocar o jogo A pelo jogo B — o slot (hora + quadra) muda de dono e a
// grade segue íntegra.
public class TrocaDeHorarioTests
{
    private const int Torneio = 7;

    private static Partida Jogo(int id, string status = "Agendada", int? horaEm = 10, string? quadra = null, int torneio = Torneio)
        => new()
        {
            Id = id,
            TorneioId = torneio,
            Codigo = $"J{id}",
            Status = status,
            HorarioPrevisto = horaEm == null ? null : new DateTime(2026, 8, 1, horaEm.Value, 0, 0),
            NomeQuadra = quadra,
        };

    [Fact]
    public void Dois_jogos_agendados_do_mesmo_torneio_podem_trocar()
    {
        Assert.Null(TrocaDeHorario.MotivoParaNaoTrocar(Jogo(1), Jogo(2, horaEm: 14), Torneio));
    }

    [Fact]
    public void A_troca_leva_horario_E_quadra_juntos()
    {
        // O slot físico é o par (hora, quadra): trocar só a hora deixaria dois jogos na
        // mesma quadra na mesma hora.
        var a = Jogo(1, horaEm: 10, quadra: "Quadra A");
        var b = Jogo(2, horaEm: 14, quadra: "Quadra B");

        TrocaDeHorario.Trocar(a, b);

        Assert.Equal(14, a.HorarioPrevisto!.Value.Hour);
        Assert.Equal("Quadra B", a.NomeQuadra);
        Assert.Equal(10, b.HorarioPrevisto!.Value.Hour);
        Assert.Equal("Quadra A", b.NomeQuadra);
    }

    [Fact]
    public void Jogo_em_andamento_ou_finalizado_nao_troca()
    {
        // O horário de um jogo que já aconteceu é história, não agenda.
        Assert.NotNull(TrocaDeHorario.MotivoParaNaoTrocar(Jogo(1, status: "AoVivo"), Jogo(2), Torneio));
        Assert.NotNull(TrocaDeHorario.MotivoParaNaoTrocar(Jogo(1), Jogo(2, status: "Finalizada"), Torneio));
    }

    [Fact]
    public void Jogo_de_outro_torneio_nao_entra_na_troca()
    {
        // Segura o POST montado à mão: sem isto, um organizador mexeria na grade alheia.
        Assert.NotNull(TrocaDeHorario.MotivoParaNaoTrocar(Jogo(1), Jogo(2, torneio: 99), Torneio));
    }

    [Fact]
    public void O_mesmo_jogo_duas_vezes_nao_e_troca()
    {
        Assert.NotNull(TrocaDeHorario.MotivoParaNaoTrocar(Jogo(1), Jogo(1), Torneio));
    }

    [Fact]
    public void Jogo_sem_horario_nao_tem_o_que_trocar()
    {
        Assert.NotNull(TrocaDeHorario.MotivoParaNaoTrocar(Jogo(1, horaEm: null), Jogo(2), Torneio));
    }

    [Fact]
    public void Jogo_inexistente_recusa_em_vez_de_estourar()
    {
        Assert.NotNull(TrocaDeHorario.MotivoParaNaoTrocar(null, Jogo(2), Torneio));
        Assert.NotNull(TrocaDeHorario.MotivoParaNaoTrocar(Jogo(1), null, Torneio));
    }
}
