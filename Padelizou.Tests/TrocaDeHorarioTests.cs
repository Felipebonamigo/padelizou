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

    // ═══ A REFERÊNCIA DO JOGO NO FORMULÁRIO (10/09/2026) ═══
    //
    // O modal passou a oferecer também as eliminatórias PREVISTAS (as que ainda não existem no
    // banco), então "qual jogo" deixou de ser um Id: é o Id do jogo real, ou a tripla
    // (categoria, fase, número) do jogo previsto. O texto sai e volta pelo mesmo tipo
    // (Services/ReferenciaDoJogo), pelo mesmo motivo da AncoraDoJogo: dois formatos separados
    // viravam clique que não chega a lugar nenhum no dia em que um deles mudasse.

    [Fact]
    public void Referencia_de_jogo_real_e_o_id()
    {
        var referencia = ReferenciaDoJogo.Ler("42");

        Assert.NotNull(referencia);
        Assert.False(referencia!.EhPrevia);
        Assert.Equal(42, referencia.PartidaId);
    }

    [Fact]
    public void Referencia_de_previa_carrega_categoria_fase_e_numero()
    {
        var referencia = ReferenciaDoJogo.Ler("previa:7:Quartas de Final:2");

        Assert.NotNull(referencia);
        Assert.True(referencia!.EhPrevia);
        Assert.Equal(7, referencia.CategoriaId);
        Assert.Equal("Quartas de Final", referencia.Fase);
        Assert.Equal(2, referencia.Numero);
    }

    [Fact]
    public void A_referencia_da_previa_e_a_mesma_nos_dois_lados()
    {
        // O botão da linha escreve; o POST lê. Tem que ser o mesmo texto.
        var escrita = ReferenciaDoJogo.Prevista(7, "Quartas de Final", 2);

        Assert.Equal(escrita, ReferenciaDoJogo.Ler(escrita.ToString()));
        Assert.Equal(ReferenciaDoJogo.Real(42), ReferenciaDoJogo.Ler(ReferenciaDoJogo.Real(42).ToString()));
    }

    [Fact]
    public void Texto_que_nao_e_referencia_nao_vira_jogo()
    {
        // Segura o POST montado à mão: nada disto pode virar exceção nem jogo inventado.
        Assert.Null(ReferenciaDoJogo.Ler(null));
        Assert.Null(ReferenciaDoJogo.Ler(""));
        Assert.Null(ReferenciaDoJogo.Ler("abc"));
        Assert.Null(ReferenciaDoJogo.Ler("previa:x:Final:1"));
        Assert.Null(ReferenciaDoJogo.Ler("previa:7:Final"));
        Assert.Null(ReferenciaDoJogo.Ler("previa:7:Final:zero"));
        Assert.Null(ReferenciaDoJogo.Ler("previa:7::1"));
    }

    // A MESMA régua do clube vale quando um dos lados é a eliminatória PREVISTA (mesclagem com o
    // PR #120, 10/09/2026): a Final prevista da 3ª não pode receber, por troca, o horário de um
    // jogo real que é no Radar.
    [Fact]
    public void Previa_de_categoria_presa_em_casa_nao_troca_pra_vaga_do_local_externo()
    {
        var terceira = new Categoria { Id = 3, Nome = "3ª Masculina", Codigo = "3M", PodeJogarNaSedeExtra = false };
        var finalPrevista = new TrocaDeHorario.Lado(
            ReferenciaDoJogo.Prevista(3, "Final", 1), null,
            new ProximasFasesDaChave.JogoQueVem("3ª Masculina", "Final", 1, new DateTime(2026, 8, 1, 20, 0, 0),
                new ProximasFasesDaChave.Lado("Vencedor Semifinal 1"), new ProximasFasesDaChave.Lado("Vencedor Semifinal 2"),
                "Arena 1", 3));

        var noRadar = Jogo(2, horaEm: 10);
        noRadar.CategoriaId = 6;
        noRadar.ClubeId = 2;
        var real = new TrocaDeHorario.Lado(ReferenciaDoJogo.Real(2), noRadar, null);

        var motivo = TrocaDeHorario.MotivoParaNaoTrocar(finalPrevista, real, Torneio, SedesDoEr(terceira));

        Assert.NotNull(motivo);
        Assert.Contains("Radar", motivo);
        Assert.Contains("3ª Masculina", motivo);
    }
}
