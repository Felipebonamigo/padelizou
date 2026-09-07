using Xunit;

namespace Padelizou.Tests;

// 07/09/2026 — O BOTÃO DE CURTIR NO PERFIL.
//
// A REGRA (um por pessoa, autor não curte o próprio, cascade ao apagar o comentário) tem
// trava de comportamento de verdade em CurtirComentarioTests. O que só existe na VIEW — o
// botão, o `asp-action` trocando entre Curtir/Descurtir, o número — não tem como um teste de
// comportamento alcançar: a suíte não renderiza Razor.
public class CurtirComentarioNaTelaTests
{
    [Fact]
    public void O_botao_de_curtir_existe_e_alterna_a_acao()
    {
        var fonte = Fonte();

        Assert.Contains("asp-action=\"@(euCurti ? \"DescurtirComentario\" : \"CurtirComentario\")\"", fonte);
        Assert.Contains("asp-route-comentarioId=\"@c.Id\"", fonte);
    }

    // O autor não curte o próprio comentário — mesma régua do elogio.
    [Fact]
    public void O_botao_nao_aparece_pro_autor_do_proprio_comentario()
    {
        var fonte = Fonte();

        Assert.Contains("if (meuId != null && meuId != c.AutorId)", fonte);
    }

    [Fact]
    public void Mostra_quantas_pessoas_curtiram()
    {
        var fonte = Fonte();

        Assert.Contains("c.Curtidas.Count", fonte);
        Assert.Contains("c.Curtidas.Any(k => k.JogadorId == meuId)", fonte);
    }

    private static string Fonte()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "Views", "Jogadores", "Perfil.cshtml");
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException("Perfil.cshtml não encontrado a partir do bin.");
    }
}
