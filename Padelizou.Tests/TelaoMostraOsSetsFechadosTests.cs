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

    // ALINHADOS (22/11/2026). 🗣️ Vini, com a foto da TV: *"deu certo mas ficou deslocado"* — a
    // bolinha do saque só existe na linha de quem saca, e o set vinha logo depois dela: o 6 de cima
    // colava na bolinha e o 2 de baixo ia pro meio. Sets e placar moram num bloco só, à direita,
    // com largura fixa por set — as colunas das duas linhas caem uma embaixo da outra.
    [Fact]
    public void Sets_e_placar_ficam_num_bloco_so_a_direita_nas_duas_linhas()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) dir = dir.Parent;
        var raiz = dir?.FullName ?? throw new DirectoryNotFoundException();
        var conteudo = TestInfra.SemComentarios(File.ReadAllText(Path.Combine(raiz, "Padelizou", "Views", "Torneios", "_TelaoConteudo.cshtml")));
        var pagina = File.ReadAllText(Path.Combine(raiz, "Padelizou", "Views", "Torneios", "Telao.cshtml"));

        foreach (var lado in new[] { "1", "2" })
        {
            var bloco = conteudo.IndexOf("<span class=\"tl-numeros\">", conteudo.IndexOf($"set.Vencedor == {lado}", StringComparison.Ordinal) - 400, StringComparison.Ordinal);
            var set = conteudo.IndexOf($"set.Games{lado}", StringComparison.Ordinal);
            var placar = conteudo.IndexOf($"p.GamesDupla{lado} ?? 0", StringComparison.Ordinal);
            Assert.True(bloco >= 0 && bloco < set && set < placar, $"linha {lado}: sets e placar fora do bloco tl-numeros");
        }
        Assert.Contains(".tl-numeros {", pagina);
    }
}
