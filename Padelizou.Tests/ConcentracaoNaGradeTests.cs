using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// AS DUAS RESTRIÇÕES NOVAS DE 08/09/2026 DENTRO DO ENCAIXE, e o que as separa: A FASE.
//
//  • CONCENTRAÇÃO ("os 2 jogos na sexta") vale só na FASE DE GRUPOS. É por dupla.
//  • "SEM ELIMINATÓRIA NO SÁBADO À NOITE" vale só FORA da fase de grupos. É por categoria.
//
// As duas foram decisão do Felipe, e o recorte por fase é o coração das duas: uma
// concentração que valesse na eliminatória pediria o impossível (a eliminatória sai DEPOIS
// dos grupos), e um bloqueio de noite de sábado que pegasse os grupos empurraria jogo que ele
// não pediu pra empurrar.
//
// O impedimento comum (`janelasProibidasPorDupla`) continua valendo em TODA fase, e está
// travado em GradeDeJogosTests — nada aqui mexe nele.
public class ConcentracaoNaGradeTests
{
    private const int DuracaoDaGrade = 50;

    private static readonly DateTime Sabado = new(2026, 8, 15);

    private static Partida JogoEntre(int dupla1, int dupla2, string fase, int categoriaId = 1) => new()
    {
        Codigo = $"{dupla1}x{dupla2}",
        Status = "Agendada",
        Fase = fase,
        CategoriaId = categoriaId,
        Dupla1Id = dupla1,
        Dupla2Id = dupla2,
    };

    // A manhã inteira do sábado proibida — a forma que a concentração "só na sexta" produz.
    private static Dictionary<int, (DateTime Inicio, DateTime Fim)[]> Manha(int duplaId) => new()
    {
        [duplaId] = new[] { (Sabado, Sabado.AddHours(12)) },
    };

    private static List<DateTime> DuasVagas() => new()
    {
        Sabado.AddHours(9),     // dentro da janela proibida
        Sabado.AddHours(13),    // fora
    };

    [Fact]
    public void Concentracao_empurra_o_jogo_de_grupo_pra_fora_da_janela()
    {
        var jogo = JogoEntre(1, 2, "Grupo A");
        var horarios = DuasVagas();

        GradeDeJogos.Encaixar(new List<Partida> { jogo }, horarios, DuracaoDaGrade,
            janelasSoNosGruposPorDupla: Manha(1));

        Assert.Equal(horarios[1], jogo.HorarioPrevisto);
    }

    // ⚠️ A TRAVA QUE DEFINE O ESCOPO. A eliminatória acontece depois dos grupos por definição —
    // se a concentração a alcançasse, a dupla que passasse ficaria sem horário possível.
    [Fact]
    public void Concentracao_nao_alcanca_a_eliminatoria()
    {
        var jogo = JogoEntre(1, 2, "Semifinal");
        var horarios = DuasVagas();

        GradeDeJogos.Encaixar(new List<Partida> { jogo }, horarios, DuracaoDaGrade,
            janelasSoNosGruposPorDupla: Manha(1));

        Assert.Equal(horarios[0], jogo.HorarioPrevisto);
    }

    [Fact]
    public void Concentracao_vale_pro_lado_2_da_dupla_tambem()
    {
        var jogo = JogoEntre(1, 2, "Grupo A");
        var horarios = DuasVagas();

        GradeDeJogos.Encaixar(new List<Partida> { jogo }, horarios, DuracaoDaGrade,
            janelasSoNosGruposPorDupla: Manha(2));

        Assert.Equal(horarios[1], jogo.HorarioPrevisto);
    }

    // Mesma troca que já vale pro impedimento: jogo sem horário nenhum é pior que jogo
    // marcado em cima da restrição.
    [Fact]
    public void Sem_vaga_fora_da_janela_a_concentracao_cede()
    {
        var jogo = JogoEntre(1, 2, "Grupo A");
        var horarios = new List<DateTime> { Sabado.AddHours(9) };

        GradeDeJogos.Encaixar(new List<Partida> { jogo }, horarios, DuracaoDaGrade,
            janelasSoNosGruposPorDupla: Manha(1));

        Assert.Equal(horarios[0], jogo.HorarioPrevisto);
    }

    // ── A régua POR CATEGORIA: sem eliminatória no sábado à noite ─────────────────────────

    private static Dictionary<int, (DateTime Inicio, DateTime Fim)[]> NoiteDeSabado(int categoriaId) => new()
    {
        [categoriaId] = new[] { (Sabado.AddHours(18), Sabado.AddDays(1)) },
    };

    private static List<DateTime> NoiteEDepois() => new()
    {
        Sabado.AddHours(21),                // sábado à noite — proibido pra eliminatória
        Sabado.AddDays(1).AddHours(8),      // domingo de manhã — pra onde ela passa
    };

    [Fact]
    public void Eliminatoria_da_categoria_bloqueada_passa_pro_domingo_de_manha()
    {
        var jogo = JogoEntre(1, 2, "Semifinal", categoriaId: 5);
        var horarios = NoiteEDepois();

        GradeDeJogos.Encaixar(new List<Partida> { jogo }, horarios, DuracaoDaGrade,
            janelasSoNaEliminatoriaPorCategoria: NoiteDeSabado(5));

        Assert.Equal(horarios[1], jogo.HorarioPrevisto);
    }

    // ⚠️ O OUTRO LADO DO ESCOPO: o Felipe pediu "jogos de eliminatórias", e jogo de grupo
    // continua entrando no sábado à noite normalmente.
    [Fact]
    public void Jogo_de_grupo_da_mesma_categoria_continua_entrando_no_sabado_a_noite()
    {
        var jogo = JogoEntre(1, 2, "Grupo A", categoriaId: 5);
        var horarios = NoiteEDepois();

        GradeDeJogos.Encaixar(new List<Partida> { jogo }, horarios, DuracaoDaGrade,
            janelasSoNaEliminatoriaPorCategoria: NoiteDeSabado(5));

        Assert.Equal(horarios[0], jogo.HorarioPrevisto);
    }

    [Fact]
    public void Categoria_de_fora_do_mapa_nao_e_afetada()
    {
        var jogo = JogoEntre(1, 2, "Semifinal", categoriaId: 9);
        var horarios = NoiteEDepois();

        GradeDeJogos.Encaixar(new List<Partida> { jogo }, horarios, DuracaoDaGrade,
            janelasSoNaEliminatoriaPorCategoria: NoiteDeSabado(5));

        Assert.Equal(horarios[0], jogo.HorarioPrevisto);
    }

    // As duas restrições convivem no mesmo torneio sem se atrapalhar: cada uma pega a fase que
    // é dela, no mesmo `Encaixar`.
    [Fact]
    public void As_duas_reguas_convivem_na_mesma_grade()
    {
        var doGrupo = JogoEntre(1, 2, "Grupo A", categoriaId: 5);
        var daChave = JogoEntre(3, 4, "Semifinal", categoriaId: 5);
        var horarios = new List<DateTime>
        {
            Sabado.AddHours(9),                 // manhã: proibida pro GRUPO da dupla 1
            Sabado.AddHours(21),                // noite: proibida pra ELIMINATÓRIA da categoria 5
            Sabado.AddDays(1).AddHours(8),      // domingo de manhã
        };

        GradeDeJogos.Encaixar(new List<Partida> { doGrupo, daChave }, horarios, DuracaoDaGrade,
            janelasSoNosGruposPorDupla: Manha(1),
            janelasSoNaEliminatoriaPorCategoria: NoiteDeSabado(5));

        // Cada um caiu na ÚNICA vaga que a régua do outro deixou livre pra ele: o jogo de grupo
        // não podia de manhã, a eliminatória não podia à noite, e o guloso varre a fila inteira
        // por vaga (a manhã sobrou pro segundo da fila porque o primeiro estava proibido nela).
        Assert.Equal(horarios[1], doGrupo.HorarioPrevisto);
        Assert.Equal(horarios[0], daChave.HorarioPrevisto);
    }
}
