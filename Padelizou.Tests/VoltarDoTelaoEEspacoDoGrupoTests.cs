namespace Padelizou.Tests;

// Dois retoques de tela vistos no celular do Felipe no dia da Wallau (21/11/2026).
public class VoltarDoTelaoEEspacoDoGrupoTests
{
    private static string Pasta() =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Padelizou", "Views", "Torneios");

    // 🗣️ *"A abrir telão não tem botão de voltar"* — a página não usa o layout do site, então não
    // herda a seta do topo; aberta no celular, era beco sem saída.
    [Fact]
    public void O_telao_tem_botao_de_voltar_pro_torneio()
    {
        var telao = File.ReadAllText(Path.Combine(Pasta(), "Telao.cshtml"));

        Assert.Contains("class=\"tl-voltar\"", telao);
        Assert.Contains("asp-action=\"Details\"", telao);
    }

    // 🗣️ *"o botão de entrar no grupo do torneio está quase em cima"* do card das ferramentas.
    [Fact]
    public void O_botao_do_grupo_tem_espaco_do_card_de_cima()
    {
        var details = File.ReadAllText(Path.Combine(Pasta(), "Details.cshtml"));

        Assert.Contains("<div class=\"d-flex flex-wrap align-items-center gap-2 mt-3 mb-3\">", details);
    }
}
