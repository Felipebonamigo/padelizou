using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O PLACAR DO CARD DO JOGO FINALIZADO EM JOGO DE MAIS DE UM SET (22/11/2026, Wallau).
// 🗣️ Felipe, com o print da final "3-6 2-6": *"tá errado esse visual do placar, como eram dois sets,
// pense na melhor forma quando for dois sets"*. O número grande era o placar de games do ÚLTIMO set
// (2 × 6) — lido como se o jogo tivesse sido 2 × 6 — e quem ganhou saía desses mesmos games (num
// jogo decidido no super tie-break, 0 × 0: ninguém em destaque). Agora, como num placar de padel: o
// número grande são os SETS vencidos, e ao lado os games de cada set, o vencedor do set aceso.
public class PlacarNoCardTests
{
    private static Partida Jogo(string? sets, int? s1, int? s2, int g1, int g2, int? vencedor = null) => new()
    {
        Codigo = "J", Status = "Finalizada", Dupla1Id = 10, Dupla2Id = 20,
        PlacarDosSets = sets, SetsDupla1 = s1, SetsDupla2 = s2, GamesDupla1 = g1, GamesDupla2 = g2, VencedorId = vencedor,
    };

    [Fact]
    public void Dois_sets_o_grande_sao_os_sets_e_ao_lado_os_games_de_cada_set()
    {
        var p = PlacarNoCard.De(Jogo("3-6 2-6", 0, 2, 2, 6, vencedor: 20));

        Assert.True(p.PorSets);
        Assert.Equal((0, 2), (p.Dupla1.Grande, p.Dupla2.Grande));
        Assert.Equal(new[] { (3, false), (2, false) }, p.Dupla1.Sets);
        Assert.Equal(new[] { (6, true), (6, true) }, p.Dupla2.Sets);
        Assert.Equal(2, p.Vencedor);
    }

    [Fact]
    public void Decidido_no_super_tie_break_o_vencedor_sai_dos_sets_e_nao_dos_games_zerados()
    {
        var p = PlacarNoCard.De(Jogo("6-3 4-6 [10-8]", 2, 1, 0, 0));

        Assert.Equal((2, 1), (p.Dupla1.Grande, p.Dupla2.Grande));
        Assert.Equal(new[] { (6, true), (4, false), (10, true) }, p.Dupla1.Sets);
        Assert.Equal(1, p.Vencedor);
    }

    [Fact]
    public void Jogo_de_um_set_continua_como_sempre_os_games()
    {
        var p = PlacarNoCard.De(Jogo(null, null, null, 9, 7));

        Assert.False(p.PorSets);
        Assert.Equal((9, 7), (p.Dupla1.Grande, p.Dupla2.Grande));
        Assert.Empty(p.Dupla1.Sets);
        Assert.Equal(1, p.Vencedor);
    }

    [Fact]
    public void Vencedor_gravado_vale_mais_que_a_conta_um_WO_por_exemplo()
    {
        Assert.Equal(2, PlacarNoCard.De(Jogo(null, null, null, 0, 0, vencedor: 20)).Vencedor);
    }

    [Fact]
    public void O_card_usa_o_placar_por_sets()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) dir = dir.Parent;
        var view = TestInfra.SemComentarios(File.ReadAllText(Path.Combine(
            dir?.FullName ?? throw new DirectoryNotFoundException(), "Padelizou", "Views", "Torneios", "_JogoEmLinha.cshtml")));

        Assert.Contains("PlacarNoCard.De(jogo)", view);
    }
}
