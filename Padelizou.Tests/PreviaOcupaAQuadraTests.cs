using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 13/09/2026 — O "DEFINIR HORÁRIO" DIZIA "2 QUADRAS LIVRES" COM AS DUAS OCUPADAS.
//
// 🗣️ Felipe, com o seletor aberto na Final da 5ª Feminina: *"as quadras nao estao livres, esta
// agendado para outros jogos, so nao estao definidos ainda quais serao, mas o horario e jogo ja
// existe"*.
//
// 🕳️ `HorariosDaGrade.Montar` contava a ocupação SÓ dos jogos reais:
//
//     foreach (var jogo in jogos)                    // ← Partida no banco
//         ocupadas[quando] = ocupadas.GetValueOrDefault(quando) + 1;
//
//     var usados = ocupadas.Keys.Concat(tambem…)     // ← a prévia entrava SÓ aqui
//     …
//     new Slot(horario, ocupadas.GetValueOrDefault(horario), …)
//
// O `tambem` (os horários das eliminatórias PREVISTAS) servia só pra o horário APARECER na
// lista. Na hora de contar quantas quadras sobram, a prévia sumia — e o seletor oferecia como
// vaga um horário que a chave já tinha prometido a outro jogo.
//
// ⚠️ É A MESMA RAIZ do estrago do 2ª Etapa ER PADEL TOUR, vista de outra tela: a prévia não era
// tratada como COMPROMISSO. 🗣️ *"o chaveamento fixo, os horarios fixos"* — um jogo previsto
// ocupa a quadra dele tanto quanto um jogo que já existe no banco.
public class PreviaOcupaAQuadraTests
{
    private static Torneio ComDuasQuadras() => new()
    {
        Id = 1,
        Nome = "Torneio de Teste",
        Codigo = "TST123",
        Status = "Em Andamento",
        DataInicio = new DateTime(2026, 9, 13, 8, 0, 0),
        DataFim = new DateTime(2026, 9, 13, 23, 0, 0),
        QuantidadeQuadras = 2,
        TempoPrevistoPartidaMinutos = 50,
    };

    private static Partida JogoReal(DateTime quando) => new()
    {
        CategoriaId = 1, Fase = "Quartas de Final", Status = "Agendada",
        Dupla1Id = 1, Dupla2Id = 2, Codigo = "AAA111", HorarioPrevisto = quando,
    };

    private static HorariosDaGrade.Slot Em(List<HorariosDaGrade.Slot> slots, DateTime quando) =>
        slots.Single(s => s.Horario == quando);

    [Fact]
    public void Duas_previas_no_mesmo_horario_lotam_as_duas_quadras()
    {
        var dezoito = new DateTime(2026, 9, 13, 18, 0, 0);

        var slots = HorariosDaGrade.Montar(ComDuasQuadras(), new List<Partida>(),
            sedes: null, tambem: new DateTime?[] { dezoito, dezoito });

        // O flagrante do print: as duas finais previstas às 18:00, e o seletor dizendo
        // "2 quadras livres".
        Assert.Equal(2, Em(slots, dezoito).Ocupadas);
        Assert.Equal(0, Em(slots, dezoito).Livres);
        Assert.True(Em(slots, dezoito).Lotado);
    }

    [Fact]
    public void Uma_previa_ao_lado_de_um_jogo_real_tambem_lota()
    {
        var dezoito = new DateTime(2026, 9, 13, 18, 0, 0);

        var slots = HorariosDaGrade.Montar(ComDuasQuadras(), new[] { JogoReal(dezoito) },
            sedes: null, tambem: new DateTime?[] { dezoito });

        // Jogo no banco e jogo prometido pesam igual: a quadra está comprometida nos dois casos.
        Assert.Equal(2, Em(slots, dezoito).Ocupadas);
        Assert.True(Em(slots, dezoito).Lotado);
    }

    [Fact]
    public void Horario_com_uma_previa_so_ainda_tem_uma_quadra_livre()
    {
        var dezenove = new DateTime(2026, 9, 13, 18, 50, 0);

        var slots = HorariosDaGrade.Montar(ComDuasQuadras(), new List<Partida>(),
            sedes: null, tambem: new DateTime?[] { dezenove });

        // ⚠️ O GUARDA-CORPO: a correção não pode fechar horário que tem vaga de verdade — o
        // organizador precisa conseguir remarcar, e uma lista toda "sem quadra livre" seria
        // tão inútil quanto uma que mente pro outro lado.
        Assert.Equal(1, Em(slots, dezenove).Ocupadas);
        Assert.Equal(1, Em(slots, dezenove).Livres);
        Assert.False(Em(slots, dezenove).Lotado);
    }

    [Fact]
    public void Horario_vazio_continua_oferecendo_as_duas_quadras()
    {
        var slots = HorariosDaGrade.Montar(ComDuasQuadras(), new List<Partida>(),
            sedes: null, tambem: new DateTime?[] { new DateTime(2026, 9, 13, 18, 0, 0) });

        // Um horário QUALQUER que não é o da prévia — escolhido da lista devolvida, e não
        // adivinhado: a abertura da grade não é o DataInicio, e chutar o slot faria o teste
        // falhar por fixture em vez de por comportamento.
        var vazio = slots.First(s => s.Horario != new DateTime(2026, 9, 13, 18, 0, 0));
        Assert.Equal(0, vazio.Ocupadas);
        Assert.Equal(2, vazio.Livres);
    }
}
