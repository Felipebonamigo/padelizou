using Xunit;

namespace Padelizou.Tests;

// A PÁGINA DO TORNEIO MAIS ENXUTA ATÉ AS ABAS (27/11/2026).
// 🗣️ Felipe, com o print da 2ª Etapa EPT: *"me parece meio poluído demais essa tela, com muita informação
// até chegar no 'torneio' (inscritos, jogos, etc)"* — aprovou o desenho e pediu: *"essa sessão 'sua
// inscrição' pode vir minimizada com a opção de expandir"*.
//   - o recado do organizador numa linha, com "ver mais";
//   - cartaz e convidar num botão só, "Compartilhar";
//   - camiseta, pagar agora, Pix, grupo do WhatsApp e avisos dentro de "Sua inscrição", FECHADA
//     (abre sozinha só com pagamento pelo site pendente — pendência não se esconde).
public class TelaDoTorneioEnxutaTests
{
    private static string Details()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) dir = dir.Parent;
        return TestInfra.SemComentarios(File.ReadAllText(Path.Combine(
            dir?.FullName ?? throw new DirectoryNotFoundException(), "Padelizou", "Views", "Torneios", "Details.cshtml")));
    }

    private static string Bloco(string view, string abertura, string fim)
    {
        int i = view.IndexOf(abertura, StringComparison.Ordinal);
        Assert.True(i >= 0, $"não achei {abertura}");
        int j = view.IndexOf(fim, i, StringComparison.Ordinal);
        Assert.True(j > i, $"não achei o fim de {abertura}");
        return view.Substring(i, j - i);
    }

    [Fact]
    public void Sua_inscricao_junta_camiseta_pagamento_pix_grupo_e_avisos_num_quadro_que_abre_no_toque()
    {
        var bloco = Bloco(Details(), "<details class=\"pdz-sua-inscricao", "data-fim-sua-inscricao");

        Assert.Contains("EscolherCamiseta", bloco);
        Assert.Contains("minhasNaoPagas.Count > 0", bloco);
        Assert.Contains("id=\"campoPixTorneio\"", bloco);
        Assert.Contains("Entrar no grupo do torneio", bloco);
        Assert.Contains("SeguirTorneio", bloco);
        Assert.Contains("<summary", bloco);
    }

    [Fact]
    public void Sua_inscricao_vem_fechada_e_so_abre_sozinha_com_pagamento_pendente()
    {
        var view = Details();
        int i = view.IndexOf("<details class=\"pdz-sua-inscricao", StringComparison.Ordinal);
        Assert.True(i >= 0);
        var abertura = view.Substring(i, view.IndexOf('\n', i) - i);

        Assert.Contains("open=\"@(minhasNaoPagas.Count > 0)\"", abertura);
    }

    [Fact]
    public void O_recado_e_o_compartilhar_tambem_recolhem()
    {
        var view = Details();
        Assert.Contains("Model.RecadoAosInscritos", Bloco(view, "<details class=\"pdz-recado", "</details>"));
        var compartilhar = Bloco(view, "<details class=\"pdz-compartilhar", "</details>");
        Assert.Contains("Cartaz pra divulgar", compartilhar);
        Assert.Contains("#modalConvidarPraOTorneio", compartilhar);
    }
}
