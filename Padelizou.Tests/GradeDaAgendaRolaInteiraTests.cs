using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// A GRADE DA AGENDA ROLA INTEIRA, CABEÇALHO JUNTO COM O CORPO.
//
// 🐛 O DEFEITO QUE ESTE ARQUIVO PRENDE (visto em produção no celular do Felipe, 25/08/2026):
// o `overflow-x: auto` estava SÓ no `.pdz-grade-corpo`. As duas faixas são `display: flex`
// com as mesmas sete colunas de largura mínima — o corpo rolava por dentro do card, e o
// CABEÇALHO, sem rolagem, TRANSBORDAVA o card inteiro. Na tela: "QUI 27" e "SEX 28" flutuando
// fora da borda, sobre o fundo da página, e os dias desalinhados das colunas de baixo.
//
// ⚠️ É TESTE DE FONTE, e isso é escolha consciente: não há suíte de CSS neste projeto, e a
// alternativa era não travar nada. O que ele prende é a INVARIANTE, não a aparência —
// cabeçalho e corpo rolam no MESMO elemento. Quem devolver o `overflow-x` pro corpo (ou
// tirar o wrapper) fica vermelho aqui, que é exatamente o caminho de volta do defeito.
public class GradeDaAgendaRolaInteiraTests
{
    private static string Fonte()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "Views", "Aulas", "MinhaAgenda.cshtml");
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException("MinhaAgenda.cshtml não encontrado a partir do bin.");
    }

    [Fact]
    public void O_cabecalho_e_o_corpo_rolam_no_MESMO_elemento()
    {
        var fonte = Fonte();

        // Um wrapper só, e é ele que rola.
        Assert.Matches(new Regex(@"\.pdz-grade-rolagem\s*\{[^}]*overflow-x:\s*auto"), fonte);

        // E o corpo NÃO rola por conta própria — foi assim que o cabeçalho ficou pra trás.
        var corpo = Regex.Match(fonte, @"\.pdz-grade-corpo\s*\{([^}]*)\}");
        Assert.True(corpo.Success, "regra .pdz-grade-corpo não encontrada");
        Assert.DoesNotContain("overflow-x", corpo.Groups[1].Value);
    }

    [Fact]
    public void As_duas_faixas_ficam_DENTRO_do_wrapper_que_rola()
    {
        var fonte = Fonte();

        var wrapper = fonte.IndexOf("\"pdz-grade-rolagem\"", StringComparison.Ordinal);
        var cabecalho = fonte.IndexOf("\"pdz-grade-cabecalho\"", StringComparison.Ordinal);
        var corpo = fonte.IndexOf("\"pdz-grade-corpo\"", StringComparison.Ordinal);

        Assert.True(wrapper > 0, "o wrapper de rolagem não existe no HTML");
        Assert.True(wrapper < cabecalho, "o cabeçalho precisa estar dentro do wrapper que rola");
        Assert.True(wrapper < corpo, "o corpo precisa estar dentro do wrapper que rola");
    }

    // ⚠️ E A OUTRA METADE DA CORREÇÃO: as duas faixas precisam ter a MESMA largura, senão
    // rolam juntas e continuam desalinhadas. `width: max-content` com `min-width: 100%` dá as
    // duas coisas — em tela larga preenche o card (as colunas `flex:1` esticam), em tela
    // estreita cresce até o conteúdo e o wrapper rola.
    [Fact]
    public void As_duas_faixas_tem_a_mesma_largura_em_qualquer_tela()
    {
        var fonte = Fonte();

        var regra = Regex.Match(fonte, @"\.pdz-grade-cabecalho,\s*\.pdz-grade-corpo\s*\{([^}]*)\}");
        Assert.True(regra.Success, "a regra compartilhada das duas faixas não foi encontrada");

        var corpoDaRegra = regra.Groups[1].Value;
        Assert.Contains("width: max-content", corpoDaRegra);
        Assert.Contains("min-width: 100%", corpoDaRegra);
    }
}
