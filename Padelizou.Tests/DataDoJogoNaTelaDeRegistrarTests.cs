using System.IO;
using Xunit;

namespace Padelizou.Tests;

// 20/08/2026 — print de usuário sobre a tela de registrar jogo: "aqui está confuso, para
// registrar o jogo, lá em cima tem uma data e abaixo outra".
//
// A tela tinha TRÊS lugares falando de data: a linha de resumo ("26 ago. · Paladino"), o aviso
// amarelo ("o jogo marcado pra 26/08, que ainda não chegou") e o campo. Os dois primeiros saíam
// prontos do servidor a partir de `DataSugerida` e ficavam congelados — então quem obedecia o
// aviso e corrigia o campo pra 19/08 (o dia em que de fato jogou) terminava olhando pra uma tela
// que se contradizia, sem pista de qual data seria gravada.
//
// ⚠️ E não é confusão inofensiva: o ranking DA SEMANA é fatiado por data. Diante da contradição
// a leitura natural é "vale o de cima" — a pessoa desfaz a correção e grava o jogo na semana
// errada, que é o estrago exato que o aviso existe pra evitar.
//
// Estes testes seguram a regra que resolveu isso: O CAMPO É A DATA. Nenhum texto de data é
// escrito pelo servidor, e o resumo só existe com o painel fechado.
public class DataDoJogoNaTelaDeRegistrarTests
{
    private static string Tela() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Grupos", "RegistrarJogo.cshtml"));

    private static string Script() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "registrar-jogo-da-panelinha.js"));

    // O coração: se voltar a existir uma data escrita pelo servidor fora do campo, volta a
    // existir uma segunda data — e a segunda é sempre a que fica pra trás.
    [Fact]
    public void O_servidor_so_escreve_a_data_dentro_do_campo()
    {
        var tela = Tela();

        Assert.Contains("value=\"@dataSugerida.ToString(\"yyyy-MM-dd\")\"", tela);
        Assert.DoesNotContain("dataSugerida.ToString(\"dd MMM\")", tela);
        Assert.DoesNotContain("dataSugerida.ToString(\"dd/MM\")", tela);

        // O texto do aviso mora no JS, junto do recálculo. Uma cópia aqui nasceria certa e
        // envelheceria no primeiro toque no campo — foi assim que ele virou o print.
        Assert.DoesNotContain("ainda não chegou", tela);
    }

    // Mesma história do lado do local: o nome do clube vinha do servidor e ficava congelado
    // enquanto o seletor logo abaixo já mostrava outro.
    [Fact]
    public void O_nome_do_clube_tambem_sai_do_seletor_nao_do_servidor()
    {
        Assert.DoesNotContain("NomeDoClubeDoGrupo", Tela());
        Assert.DoesNotContain(
            "NomeDoClubeDoGrupo",
            File.ReadAllText(Path.Combine(PastaDoProjeto(), "Controllers", "GruposController.cs")));
    }

    [Fact]
    public void A_tela_entrega_o_desenho_da_data_pro_JS()
    {
        var tela = Tela();

        Assert.Contains("pdzMontarDataDoJogo();", tela);
        Assert.Contains("id=\"pdzDataJogo\"", tela);
        Assert.Contains("id=\"pdzResumoDataLocal\"", tela);
        Assert.Contains("id=\"pdzAvisoDataFutura\"", tela);

        // Com o painel aberto o resumo some, e com ele o botão "Alterar": sem esta saída a
        // pessoa fica sem como fechar o que abriu.
        Assert.Contains("id=\"pdzFecharOndeEQuando\"", tela);

        // O toggle inline de antes deixava o resumo E o painel na tela ao mesmo tempo — que é
        // literalmente o "uma data em cima e outra embaixo" do relato.
        Assert.DoesNotContain("onclick=\"document.getElementById('pdzOndeEQuando')", tela);
    }

    [Fact]
    public void O_resumo_e_a_versao_fechada_do_painel_e_o_aviso_se_recalcula()
    {
        var js = Script();

        Assert.Contains("function pdzMontarDataDoJogo", js);

        // Um esconde o outro: os dois visíveis juntos são as duas datas do print.
        Assert.Contains("resumo.classList.toggle('d-none', editando", js);

        // O aviso segue o campo, não o que o servidor achava quando a página abriu.
        Assert.Contains("campo.onchange = desenhar", js);
        Assert.Contains("aviso.classList.toggle('d-none', !noFuturo())", js);
    }

    // ⚠️ `new Date('2026-08-26')` é lido como UTC: no fuso do Brasil vira dia 25 às 21h. O aviso
    // de "data futura" apareceria (ou sumiria) um dia fora do lugar — e um dia fora do lugar é
    // exatamente a semana errada no ranking.
    [Fact]
    public void A_comparacao_com_hoje_nao_passa_por_UTC()
    {
        var js = Script();

        Assert.DoesNotContain("new Date(campo.value)", js);
        Assert.Contains("campo.value > hoje()", js);
    }

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
            $"Não achei a pasta do projeto subindo a partir de {AppContext.BaseDirectory}");
    }
}
