using System;
using System.IO;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — AS FERRAMENTAS DO DIA SOBEM PRO TOPO DEPOIS QUE A CHAVE É PUBLICADA.
//
// 🗣️ Felipe, com o torneio do Er no ar: *"acho que a ferramentas do organizador tem q ser a
// primeira coisa da tela, depois que as chaves foram publicadas"*.
//
// Elas eram o ÚLTIMO bloco do Painel de Controle, atrás do status, do sorteio, dos
// organizadores, dos marcadores e do formulário inteiro de editar o torneio — e o Painel só
// abre depois de um clique na aba "Gerenciar Torneio". No dia do jogo isso é o caminho mais
// longo até as três telas que só servem NAQUELE dia: check-in, financeiro e relatório.
//
// A régua é a mesma que já decide as abas de chave (AprovacaoDeChaves.ChavePublicada): antes
// de publicar, o trabalho é montar o torneio e as ferramentas ficam onde sempre estiveram.
//
// ⚠️ O card MUDA DE LUGAR, não ganha uma cópia: dois cards divergiriam na primeira mudança.
// Daí o parcial — e daí o teste de que o texto dele não mora mais no Details.
//
// ⚠️ E ele sai de dentro do `<fieldset disabled="@gestaoSoLeitura">` do Painel, então o novo
// lugar precisa repetir a casca: sem ela o assistente do sistema ganharia o "Avisar todo
// mundo" ligado — o servidor recusaria (Comunicar exige EhOrganizadorAsync), mas só depois
// de a pessoa escrever a mensagem e clicar.
//
// Teste de FONTE: a suíte não renderiza Razor.
public class FerramentasDoOrganizadorNoTopoTests
{
    private const string Parcial = "<partial name=\"_FerramentasDoOrganizador\"";

    // ── A RÉGUA ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Fase de Grupos", true)]
    [InlineData("Mata-Mata", true)]
    [InlineData("Finalizado", true)]
    [InlineData(AprovacaoDeChaves.Pendente, false)]   // sorteado, mas ainda não publicado
    [InlineData("Inscrições Abertas", false)]
    [InlineData("Chaves em Sorteio", false)]          // = inscrições fechadas, sem sorteio
    [InlineData("Cancelado", false)]
    public void A_chave_esta_publicada_responde_pelo_status(string status, bool esperado) =>
        Assert.Equal(esperado, AprovacaoDeChaves.ChavePublicada(new Torneio { Status = status }));

    // ── ONDE O CARD FICA ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Com_a_chave_publicada_as_ferramentas_vem_antes_das_abas()
    {
        var fonte = Details();
        int topo = fonte.IndexOf(Parcial, StringComparison.Ordinal);
        int abas = fonte.IndexOf("id=\"torneioTabs\"", StringComparison.Ordinal);

        Assert.True(topo >= 0, "O parcial das ferramentas não é renderizado no Details.");
        Assert.True(abas >= 0, "Não achei a barra de abas no Details.");
        Assert.True(topo < abas, "As ferramentas precisam vir ANTES das abas — é o topo da tela.");

        // "Antes das abas" não basta: entre o cabeçalho e elas moram o cartaz, o convite, o
        // grupo do WhatsApp, as fotos, os cards de campeão, o pódio, o MVP, o mural e os
        // palpiteiros. Encostado nas abas, o card ainda seria uma rolagem no dia do jogo.
        int cartaz = fonte.IndexOf("var podeDivulgar =", StringComparison.Ordinal);
        Assert.True(cartaz >= 0, "Não achei o bloco do cartaz de divulgação no Details.");
        Assert.True(topo < cartaz, "As ferramentas têm que vir antes do cartaz e do resto da página.");

        // Mas DEPOIS do cabeçalho: um card antes do nome do torneio deixa a tela sem dizer
        // de que torneio ela fala.
        int cabecalho = fonte.IndexOf("<!-- Cabeçalho do Torneio -->", StringComparison.Ordinal);
        Assert.True(cabecalho >= 0, "Não achei o cabeçalho do torneio no Details.");
        Assert.True(topo > cabecalho, "O card não pode vir antes do nome do torneio.");
    }

    [Fact]
    public void O_bloco_do_topo_e_de_quem_gerencia_com_a_chave_publicada_e_respeita_o_modo_so_leitura()
    {
        var fonte = Details();
        int topo = fonte.IndexOf(Parcial, StringComparison.Ordinal);
        Assert.True(topo >= 0, "O parcial das ferramentas não é renderizado no Details.");

        var antes = fonte[Math.Max(0, topo - 700)..topo];
        Assert.Contains("ViewBag.PodeGerenciar == true", antes);
        Assert.Contains("AprovacaoDeChaves.ChavePublicada(Model)", antes);
        Assert.Contains("fieldset disabled=", antes);
    }

    [Fact]
    public void Antes_de_publicar_elas_continuam_no_painel_de_controle()
    {
        var fonte = Details();
        int painel = fonte.IndexOf("id=\"admin\"", StringComparison.Ordinal);
        Assert.True(painel >= 0, "Não achei o painel do organizador no Details.");

        int dentro = fonte.IndexOf(Parcial, painel, StringComparison.Ordinal);
        Assert.True(dentro > painel, "O painel perdeu as ferramentas — quem ainda não publicou ficaria sem elas.");

        var antes = fonte[Math.Max(painel, dentro - 400)..dentro];
        Assert.Contains("!Padelizou.Services.AprovacaoDeChaves.ChavePublicada(Model)", antes);
    }

    [Fact]
    public void O_card_mora_num_arquivo_so_e_leva_o_comunicado_junto()
    {
        var parcial = Ler("Torneios/_FerramentasDoOrganizador.cshtml");

        Assert.Contains("Ferramentas do organizador", parcial);
        Assert.Contains("asp-action=\"CheckIn\"", parcial);
        Assert.Contains("asp-action=\"Financeiro\"", parcial);
        Assert.Contains("asp-action=\"Relatorio\"", parcial);
        Assert.Contains("asp-action=\"Comunicar\"", parcial);

        // Uma cópia deixada pra trás viraria dois cards divergindo.
        Assert.DoesNotContain("Ferramentas do organizador", Details());
        Assert.DoesNotContain("asp-action=\"Comunicar\"", Details());
    }

    // ── A ABA DE CHAVES USA A MESMA RÉGUA ────────────────────────────────────────────────

    [Fact]
    public void A_aba_de_chaves_pergunta_pela_mesma_regua()
    {
        // Duas listas de status pra "a chave já é pública?" divergiriam na primeira mudança.
        var fonte = Details();
        Assert.DoesNotContain("Model.Status == \"Mata-Mata\"", fonte);

        int aba = fonte.IndexOf("id=\"grupos-tab\"", StringComparison.Ordinal);
        Assert.True(aba >= 0, "Não achei a aba Chaves e Grupos no Details.");
        Assert.Contains("AprovacaoDeChaves.ChavePublicada(Model)", fonte[Math.Max(0, aba - 700)..aba]);
    }

    private static string Details() => Ler("Torneios/Details.cshtml");

    private static string Ler(string view) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", view));

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Padelizou.csproj não encontrado a partir do bin.");
    }
}
