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

    // 🗣️ Felipe, 10/09/2026, na tela de trocar horário do Er: *"quando eu trocar aqui, tem q
    // cuidar para nao trocar o clube, por que o clube é pelo horario"*. No "por ordem" a quadra
    // some e o que fica é o CLUBE carimbado (Partida.ClubeId) — ele é do slot, não do jogo: o
    // 12:10 de sábado é no Radar seja quem for que jogue ali.
    [Fact]
    public void A_troca_leva_o_clube_junto_com_o_horario()
    {
        var a = Jogo(1, horaEm: 10);
        a.ClubeId = 2;                        // Radar, sábado de manhã
        var b = Jogo(2, horaEm: 20);
        b.ClubeId = 1;                        // Er Padel, sexta à noite

        TrocaDeHorario.Trocar(a, b);

        Assert.Equal(20, a.HorarioPrevisto!.Value.Hour);
        Assert.Equal(1, a.ClubeId);
        Assert.Equal(10, b.HorarioPrevisto!.Value.Hour);
        Assert.Equal(2, b.ClubeId);
    }

    // E a categoria que fica em casa (a 3ª, a 4ª) não pode ser mandada pro slot do local
    // alugado por uma troca na mão — a grade automática não faria isso, e a troca também não.
    private static SedesDoTorneio SedesDoEr(params Categoria[] categorias) =>
        SedesDoTorneio.Montar(1, 0,
            new[]
            {
                new Quadra { Nome = "Arena 1", ClubeId = 1 },
                new Quadra { Nome = "Radar 1", ClubeId = 2 },
            },
            categorias,
            new Dictionary<int, string> { [1] = "Er Padel", [2] = "Radar" });

    [Fact]
    public void Categoria_presa_em_casa_nao_troca_pra_vaga_do_local_externo()
    {
        var terceira = new Categoria { Id = 3, Nome = "3ª Masculina", Codigo = "3M", PodeJogarNaSedeExtra = false };
        var daTerceira = Jogo(1, horaEm: 20);
        daTerceira.CategoriaId = 3;
        daTerceira.Categoria = terceira;
        daTerceira.ClubeId = 1;
        var noRadar = Jogo(2, horaEm: 10);
        noRadar.CategoriaId = 6;
        noRadar.ClubeId = 2;

        var motivo = TrocaDeHorario.MotivoParaNaoTrocar(daTerceira, noRadar, Torneio, SedesDoEr(terceira));

        Assert.NotNull(motivo);
        Assert.Contains("Radar", motivo);
        Assert.Contains("3ª Masculina", motivo);
    }

    [Fact]
    public void Categoria_que_pode_ir_pro_externo_troca_normalmente()
    {
        var sexta = new Categoria { Id = 6, Nome = "6ª Masculina", Codigo = "6M" };
        var daSexta = Jogo(1, horaEm: 20);
        daSexta.CategoriaId = 6;
        daSexta.ClubeId = 1;
        var noRadar = Jogo(2, horaEm: 10);
        noRadar.CategoriaId = 5;
        noRadar.ClubeId = 2;

        Assert.Null(TrocaDeHorario.MotivoParaNaoTrocar(daSexta, noRadar, Torneio, SedesDoEr(sexta)));
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
