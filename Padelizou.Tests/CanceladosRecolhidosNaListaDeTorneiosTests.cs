using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 23/10/2026 — CANCELADOS FICAM RECOLHIDOS, NO FIM DA LISTA.
//
// 🗣️ Felipe, olhando /Torneios com dois torneios cancelados no meio da página: *"os cancelados
// tem q ficar minimizado também, e no final da pagina"*. Até aqui eles apareciam ABERTOS, entre
// "Inscrições Abertas" e "Finalizados", empurrando o botão de Finalizados pra baixo — por uma
// escolha antiga (cancelamento costuma ter dinheiro pendente, então não escondia). Quem organiza
// continua achando o torneio: é um clique, e o botão mostra QUANTOS há.
//
// O comportamento é HTML + um script dentro do Razor; aqui se guarda o que o Razor precisa
// entregar, no mesmo estilo de AbaQueFicaOndeEstaTests.
public class CanceladosRecolhidosNaListaDeTorneiosTests
{
    private static string Fonte() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Padelizou", "Views", "Torneios", "Index.cshtml"));

    [Fact]
    public void Cancelados_tem_um_botao_que_recolhe_e_mostra_a_quantidade()
    {
        var fonte = Fonte();

        Assert.Matches(@"data-bs-target=""#secaoCancelados""", fonte);
        Assert.Matches(@"Cancelados \(@cancelados\.Count\)", fonte);
    }

    [Fact]
    public void A_secao_de_cancelados_nasce_recolhida()
    {
        // `collapse` SEM `show`: aberta ela voltaria a empurrar Finalizados pra baixo.
        var div = Regex.Match(Fonte(), @"<div[^>]*id=""secaoCancelados""[^>]*>");

        Assert.True(div.Success, "Não achei a seção #secaoCancelados.");
        Assert.Contains("collapse", div.Value);
        Assert.DoesNotContain("show", div.Value);
    }

    [Fact]
    public void Cancelados_vem_depois_de_Finalizados()
    {
        var fonte = Fonte();
        var finalizados = fonte.IndexOf("id=\"secaoFinalizados\"", StringComparison.Ordinal);
        var cancelados = fonte.IndexOf("id=\"secaoCancelados\"", StringComparison.Ordinal);

        Assert.True(finalizados > 0, "Não achei #secaoFinalizados.");
        Assert.True(cancelados > finalizados, "Cancelados precisa ficar DEPOIS de Finalizados, no fim da lista.");
    }

    [Fact]
    public void A_busca_abre_a_secao_recolhida_onde_achou_o_torneio()
    {
        // Antes só Finalizados abria sozinha. Quem busca "Americano dos Loberos" e ele está em
        // Cancelados não pode ver "nada encontrado" com o resultado escondido.
        var fonte = Fonte();

        Assert.Contains("secao.closest('.collapse')", fonte);
        Assert.DoesNotContain("getElementById('secaoFinalizados')", fonte);
    }
}
