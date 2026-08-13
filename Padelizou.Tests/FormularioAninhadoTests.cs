using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 13/08/2026 — `Views/Torneios/Create.cshtml` tinha um <form> DENTRO do formulário de criação.
//
// HTML não permite aninhar <form>, e o estrago não é visual: o navegador "conserta" a marcação
// sozinho FECHANDO o de fora naquele ponto. O formulário de criar torneio terminava com 10
// campos — `Nome`, `ClubeId`, `PrecoInscricao`, `ChavePixOrganizador` e as categorias caíam
// todos FORA dele. Quem via aquele aviso (quem ainda não é organizador aprovado) preenchia a
// tela inteira e o POST levava só o formato.
//
// Nada disso gera erro em lugar nenhum: não quebra o build, não quebra o Razor, e o servidor
// recebe um pedido que parece apenas mal preenchido. É exatamente o tipo de defeito que só um
// teste como este encontra — ninguém "vê" um <form> aninhado olhando a tela.
public class FormularioAninhadoTests
{
    private static string PastaDeViews()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
            dir = dir.Parent;

        Assert.True(dir != null, "Não achei a pasta Views subindo a partir de " + AppContext.BaseDirectory);
        return Path.Combine(dir!.FullName, "Padelizou", "Views");
    }

    // ⚠️ SEM ISTO O TESTE NASCE VERMELHO EM 4 ARQUIVOS QUE ESTÃO CERTOS. Este projeto comenta
    // muito, e vários comentários FALAM sobre <form> — inclusive um que documenta "aqui não
    // existe um <form>, e há teste garantindo". Contar a palavra dentro do comentário faria o
    // teste acusar justamente as telas mais bem explicadas. (Mesma lição do parser do
    // PwaArquivosTests, que se enganava com vírgula dentro de comentário.)
    //
    // Os comentários viram linhas em branco em vez de sumirem: assim o número da linha que o
    // erro aponta continua sendo o do arquivo de verdade.
    private static string SemComentarios(string conteudo)
    {
        conteudo = Regex.Replace(conteudo, @"@\*.*?\*@", SoAsQuebras, RegexOptions.Singleline);
        conteudo = Regex.Replace(conteudo, @"<!--.*?-->", SoAsQuebras, RegexOptions.Singleline);

        // Comentário de JavaScript só quando a linha INTEIRA é comentário. Cortar todo `//`
        // apagaria `https://` no meio de um href e junto o resto da linha.
        return Regex.Replace(conteudo, @"(?m)^\s*//.*$", "");
    }

    private static string SoAsQuebras(Match m) => new('\n', m.Value.Count(c => c == '\n'));

    [Fact]
    public void Nenhuma_view_abre_um_form_dentro_de_outro()
    {
        var pasta = PastaDeViews();
        var erros = new List<string>();

        foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.cshtml", SearchOption.AllDirectories))
        {
            var linhas = SemComentarios(File.ReadAllText(arquivo)).Split('\n');
            var abertos = 0;

            for (var i = 0; i < linhas.Length; i++)
            {
                foreach (Match tag in Regex.Matches(linhas[i], @"<form\b|</form\s*>"))
                {
                    if (tag.Value.StartsWith("</")) abertos--;
                    else if (++abertos > 1)
                        erros.Add($"{Path.GetRelativePath(pasta, arquivo)}:{i + 1}");
                }
            }
        }

        Assert.True(erros.Count == 0,
            "<form> dentro de outro <form> — o navegador fecha o de fora ali e os campos "
            + "seguintes deixam de ser enviados, sem erro nenhum: " + string.Join(", ", erros));
    }

    // O conserto do Create.cshtml deixou o botão longe do formulário dele, ligados só pelo
    // atributo `form`. Renomear um dos dois não quebra nada visível: o botão continua na tela,
    // bonito, e não faz absolutamente nada ao ser clicado. Este teste é o fio entre os dois.
    [Fact]
    public void O_botao_de_pedir_perfil_aponta_pra_um_formulario_que_existe()
    {
        var create = File.ReadAllText(Path.Combine(PastaDeViews(), "Torneios", "Create.cshtml"));

        var idNoBotao = Regex.Match(create, @"<button[^>]*\bform=""(?<id>[^""]+)""", RegexOptions.Singleline);
        Assert.True(idNoBotao.Success, "O botão de solicitar permissão perdeu o atributo form=\"…\".");

        Assert.Matches($@"<form[^>]*\bid=""{Regex.Escape(idNoBotao.Groups["id"].Value)}""", create);

        // E o formulário do pedido tem que vir ANTES do de criação — se voltar pra dentro dele,
        // o teste de cima pega, mas aqui fica escrito por quê.
        Assert.True(
            create.IndexOf($@"id=""{idNoBotao.Groups["id"].Value}""", StringComparison.Ordinal)
            < create.IndexOf(@"id=""pdzFormCriacao""", StringComparison.Ordinal),
            "O formulário do pedido de perfil precisa ser declarado antes do pdzFormCriacao.");
    }
}
