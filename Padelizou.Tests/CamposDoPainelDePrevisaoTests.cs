using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// O painel "Vai caber?" quebrou em 09/08/2026 e NENHUM teste sentiu.
//
// A aritmética estava certa (PrevisaoDoTorneioTests), a porta estava certa
// (EndpointDePrevisaoTests) — o que quebrou foi a LIGAÇÃO entre a tela e a porta: o campo da
// data ganhou um `id="dataInicio"` (pro turno de quinta) que atropelou o `id="DataInicio"` que
// o asp-for escrevia, e o `pdzValor` procurava só por id. A data ia VAZIA pro servidor, o
// servidor respondia TemConta=false, e a tela pedia "Preencha a data de início" com a data
// preenchida na frente do organizador.
//
// ⚠️ O pior era o disfarce: os eventos continuavam disparando (aquele laço já caía no `name`),
// então a tela reagia a cada tecla — parecia viva, só que sempre com a mesma frase. E o painel
// do RITMO ao lado seguia certo, porque os campos dele têm id casando. Meia tela funcionando é
// o que faz o defeito passar por "é assim mesmo".
//
// Estes testes leem a VIEW porque é o único lugar onde esse defeito mora.
public class CamposDoPainelDePrevisaoTests
{
    private static string Tela()
    {
        var caminho = Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Create.cshtml");

        // Comentário Razor não chega ao navegador — e o comentário que explica este defeito
        // cita os próprios nomes de campo. Sem o corte, o teste acusaria a explicação.
        return Regex.Replace(File.ReadAllText(caminho), @"@\*.*?\*@", "", RegexOptions.Singleline);
    }

    private static List<string> CamposQueEntramNaConta(string tela) =>
        Regex.Matches(tela, @"pdzValor\('([^']+)'\)")
             .Select(m => m.Groups[1].Value)
             .Distinct()
             .ToList();

    [Fact]
    public void Todo_campo_que_entra_na_conta_existe_na_tela_com_esse_nome()
    {
        var tela = Tela();
        var campos = CamposQueEntramNaConta(tela);

        // Sanidade: se o painel for reescrito e ninguém mais chamar pdzValor, o laço abaixo
        // passaria vazio e este arquivo viraria enfeite.
        Assert.True(campos.Count >= 8, $"Só achei {campos.Count} campos — o painel mudou de forma?");

        foreach (var campo in campos)
        {
            // Duas formas de o navegador achar o campo, e é de propósito que sejam duas:
            // `id="x"` explícito, ou `asp-for="X"` — que sempre escreve `name="X"`, mesmo
            // quando um id explícito rouba o lugar do id gerado. Foi exatamente esse o caso
            // da data.
            var achavel = tela.Contains($"id=\"{campo}\"")
                       || tela.Contains($"asp-for=\"{campo}\"")
                       || tela.Contains($"name=\"{campo}\"");

            Assert.True(achavel,
                $"O painel 'Vai caber?' lê '{campo}', mas nenhum campo da tela responde por " +
                $"esse nome (nem id, nem asp-for, nem name). O painel vai calcular com ele VAZIO " +
                $"— e sem erro nenhum, só a conta errada ou a frase de 'preencha os campos'.");
        }
    }

    [Fact]
    public void A_busca_do_valor_cai_no_name_quando_o_id_foi_atropelado()
    {
        // Este é o teste que guarda o conserto de 09/08/2026, e ele existe porque o teste de
        // cima sozinho MENTE: ele aceita `asp-for="DataInicio"` como prova de que o campo é
        // achável, e isso só é verdade enquanto o pdzValor procurar também pelo `name`.
        // Voltando a ser só getElementById, aquele teste segue verde e a tela volta a quebrar.
        var tela = Tela();

        var corpo = Regex.Match(tela, @"function pdzValor\(id\)\s*\{(.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(corpo.Success, "Não achei o pdzValor — se ele mudou de forma, revise esta trava.");
        Assert.Contains("[name=\"' + id + '\"]", corpo.Groups[1].Value);
    }

    [Fact]
    public void Todo_campo_da_conta_tambem_dispara_o_recalculo()
    {
        // A outra metade da ligação: campo que entra na conta e não avisa quando muda deixa a
        // tela mostrando a resposta de um formulário que já não existe — que é pior que não
        // mostrar nada, porque parece atual.
        var tela = Tela();

        var lista = Regex.Match(tela, @"\[('(?:[^']+)'(?:,\s*'[^']+')*)\]\s*\n\s*\.forEach", RegexOptions.Singleline);
        Assert.True(lista.Success, "Não achei a lista de campos que ligam os eventos do painel.");

        var ouvidos = Regex.Matches(lista.Groups[1].Value, @"'([^']+)'").Select(m => m.Groups[1].Value).ToList();

        foreach (var campo in CamposQueEntramNaConta(tela))
            Assert.True(ouvidos.Contains(campo),
                $"'{campo}' entra na conta do 'Vai caber?' mas não dispara o recálculo: " +
                $"mexer nele deixa o painel respondendo o valor antigo, sem sinal nenhum.");
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
