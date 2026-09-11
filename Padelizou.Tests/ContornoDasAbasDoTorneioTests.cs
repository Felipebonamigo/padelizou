using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — CONTORNO EM TODAS AS ABAS DE TOPO DA PÁGINA DO TORNEIO.
//
// 🗣️ Felipe, num print da página do 2ª Etapa ER Padel Tour: *"Acho q temos q deixar pelo menos
// desenhado o contorno das abinhas do jogos inscritos e as demais, pra ficar mais facil pro
// usuario ver q é uma aba"*.
//
// É a MESMA queixa de 08/08/2026 que criou o contorno das `.pdz-pills` ("sem borda, as abas
// inativas eram texto solto boiando na barra") — as pills de dentro (Ao Vivo/Agendadas/
// Finalizadas) ganharam o remédio, e a barra de CIMA ficou como o Bootstrap a desenha:
// `.nav-tabs .nav-link { border: 1px solid transparent }`, com cor de borda só na ativa. Por
// isso, no print, "Jogos" é a única que parece um botão e Inscritos, Chaves e Grupos, Times e
// Palpiteiros parecem texto solto.
//
// ⚠️ AQUI A SELEÇÃO É ANEL, E NÃO PREENCHIMENTO LIMA como nas pills, e isso é escolha, não
// esquecimento: esta barra tem abas que carregam cor PRÓPRIA — "Gerenciar Torneio" é
// `text-danger` (vermelho) e "Inscreva-se" é verde sobre navy, ambas com `!important`/inline
// vencendo qualquer cor que o CSS da aba ativa escrevesse. Vermelho sobre lima não se lê.
public class ContornoDasAbasDoTorneioTests
{
    [Fact]
    public void Toda_aba_de_topo_tem_contorno()
    {
        var regra = RegraDoCss(@"\.pdz-abas\s+\.nav-link");

        // ⚠️ O VALOR, e não só a propriedade: `border: 1px solid transparent` é EXATAMENTE o que
        // o Bootstrap já faz, e é o defeito que o Felipe viu. A borda precisa ter cor.
        Assert.Matches(new Regex(@"border:[^;]*var\(--pdz-border\)"), regra);

        // Caixa inteira: o Bootstrap arredonda só os dois cantos de cima
        // (`border-top-left-radius`/`border-top-right-radius`), desenho de aba COLADA numa linha.
        // Com a linha fora (ver o teste abaixo), meia-caixa vira um degrau na base.
        Assert.Matches(new Regex(@"border-radius:\s*[^;]+;"), regra);
    }

    [Fact]
    public void A_aba_ativa_continua_obvia()
    {
        // Contornar TODAS tira justamente o que distinguia a ativa: no `.nav-tabs` do Bootstrap
        // ela é a única com cor de borda. Sem sinal novo, a barra fica com cinco caixas iguais e
        // nenhuma pista de onde a pessoa está — trocar um problema de leitura por outro.
        var regra = RegraDoCss(@"\.pdz-abas\s+\.nav-link\.active");

        Assert.Matches(new Regex(@"border-color:\s*var\(--pdz-lime\)"), regra);
        Assert.Matches(new Regex(@"background-color:\s*var\(--pdz-surface-alt\)"), regra);
    }

    [Fact]
    public void A_linha_da_nav_tabs_sai_junto()
    {
        // O `.nav-tabs` desenha uma linha embaixo da barra e puxa cada aba 1px pra baixo
        // (`margin-bottom: calc(-1 * ...)`) pra que a ATIVA se funda nela. Esse desenho só
        // funciona com aba de meia-caixa: com as cinco viradas caixa fechada, a linha passa
        // CORTANDO todas e a borda de baixo de cada uma fica 1px fora do lugar.
        Assert.Matches(new Regex(@"border-bottom:\s*0"), RegraDoCss(@"\.pdz-abas"));
        Assert.Matches(new Regex(@"margin-bottom:\s*0"), RegraDoCss(@"\.pdz-abas\s+\.nav-link"));
    }

    [Fact]
    public void As_abas_nao_se_encostam()
    {
        // Cinco caixas coladas viram uma grade só — o contorno de uma é o da vizinha, e volta o
        // "onde termina esta e começa a outra". O `gap` também é o que o cálculo de duas por
        // linha até 430px (`flex: 1 1 calc(50% - .25rem)`, logo abaixo) já supunha existir.
        Assert.Matches(new Regex(@"gap:\s*\.25rem"), RegraDoCss(@"\.pdz-abas"));
    }

    // O corpo da regra CSS cujo seletor casa com `seletor` — o texto entre `{` e `}`.
    private static string RegraDoCss(string seletor)
    {
        var css = File.ReadAllText(CaminhoDoCss());
        var m = Regex.Match(css, seletor + @"\s*\{([^}]*)\}", RegexOptions.Singleline);
        Assert.True(m.Success, $"Não achei a regra `{seletor}` no site.css.");
        return m.Groups[1].Value;
    }

    private static string CaminhoDoCss() =>
        Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css");

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
