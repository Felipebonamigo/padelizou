using Xunit;

namespace Padelizou.Tests;

// 06/09/2026 — O BOTÃO "TROCAR CATEGORIA" NA PÁGINA DO TORNEIO.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. `TrocarCategoriaDuplaTests` trava a
// REGRA (o `TrocarCategoriaDupla` em si — autorização, janela do sorteio, sexo, vaga/lista de
// espera, tipo de categoria) com testes de comportamento de verdade. O que só existe na VIEW
// — o botão, o select de categorias, o gate — não tem como um teste de comportamento
// alcançar, e é isso que este arquivo trava.
public class TrocarCategoriaNaPaginaDoTorneioTests
{
    [Fact]
    public void O_botao_e_o_formulario_existem()
    {
        var bloco = BlocoDoFormulario();

        Assert.Contains("data-bs-target=\"#trocarCategoria-@dupla.Id\"", bloco);
        Assert.Contains("asp-action=\"TrocarCategoriaDupla\"", bloco);
        Assert.Contains("name=\"novaCategoriaId\"", bloco);
    }

    // O método é POST — GET nesta rota moveria uma dupla de categoria com um clique num link
    // salvo ou seguido por engano (Regra 0 do CLAUDE.md: gravação de dado é sempre POST).
    [Fact]
    public void O_formulario_manda_por_post()
    {
        var bloco = BlocoDoFormulario();

        Assert.Contains("method=\"post\"", bloco);
    }

    // A dupla não escolhe se mover pra própria categoria atual — sem isto o select listaria a
    // categoria onde ela já está, e "mover" pra onde já se está é a mesma pergunta confusa que
    // a ação recusa no servidor (`novaCategoria.Id == dupla.CategoriaId`).
    [Fact]
    public void O_select_nao_lista_a_categoria_atual_da_dupla()
    {
        var bloco = BlocoDoFormulario();

        Assert.Contains("Where(c => c.Id != dupla.CategoriaId)", bloco);
    }

    // Só aparece quando há PRA ONDE mover — um torneio de categoria única não tem sentido
    // nenhum de oferecer o botão.
    [Fact]
    public void So_aparece_com_mais_de_uma_categoria_no_torneio()
    {
        var bloco = BlocoDoFormulario();

        Assert.Contains("Model.Categorias.Count > 1", bloco);
    }

    private static string BlocoDoFormulario()
    {
        var fonte = Details();

        var inicio = fonte.IndexOf("Trocar categoria: pra quem inscreveu na errada", StringComparison.Ordinal);
        Assert.True(inicio >= 0,
            "Não achei o bloco do botão \"Trocar categoria\" na página do torneio. Ele foi "
            + "renomeado ou removido, e esta trava parou de olhar pra ele.");

        var fim = fonte.IndexOf("bi-check-lg\"></i> Mover", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei o fim do bloco (o botão \"Mover\" vem depois dele).");

        return fonte[inicio..fim];
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
