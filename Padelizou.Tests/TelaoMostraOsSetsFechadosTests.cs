using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O TELÃO MOSTRA O SET QUE JÁ FECHOU (22/11/2026, Wallau).
// 🗣️ Vini Monaco, com a foto da TV: *"Telão não mostra que a dupla ganhou o primeiro set"*. O card
// só tinha os games do set em jogo (0 × 0 depois de um 6-3 parecia jogo zerado). Agora cada linha
// leva os sets fechados (`Partida.PlacarDosSets`, "6-3 4-6 [10-8]") antes do número grande.
public class TelaoMostraOsSetsFechadosTests
{
    private static Partida Jogo(string? sets) => new() { Codigo = "J", Status = "AoVivo", PlacarDosSets = sets };

    [Fact]
    public void Le_cada_set_fechado_com_quem_venceu()
    {
        var sets = TelaoDoTorneio.SetsFechados(Jogo("6-3 4-6 [10-8]"));

        Assert.Equal(new[]
        {
            new TelaoDoTorneio.SetFechado(6, 3, false),
            new TelaoDoTorneio.SetFechado(4, 6, false),
            new TelaoDoTorneio.SetFechado(10, 8, true),
        }, sets);
        Assert.Equal(new[] { 1, 2, 1 }, sets.Select(s => s.Vencedor));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("lixo")]
    public void Sem_set_fechado_ou_texto_torto_nao_mostra_nada(string? texto) =>
        Assert.Empty(TelaoDoTorneio.SetsFechados(Jogo(texto)));

    [Fact]
    public void O_card_da_quadra_desenha_os_sets_fechados()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) dir = dir.Parent;
        var view = TestInfra.SemComentarios(File.ReadAllText(Path.Combine(
            dir?.FullName ?? throw new DirectoryNotFoundException(), "Padelizou", "Views", "Torneios", "_TelaoConteudo.cshtml")));

        Assert.Contains("TelaoDoTorneio.SetsFechados(p)", view);
    }
}
