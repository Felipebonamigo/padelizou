using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — COMENTÁRIO RAZOR DENTRO DE UMA TAG DERRUBA OS ATRIBUTOS QUE VÊM DEPOIS DELE.
//
// Achado no ensaio do torneio do Er numa app de verdade: o "Sortear" não pedia confirmação
// nenhuma, e o "Recalcular horários" do PR #123 — o aviso que o Felipe pediu depois de uma noite
// trocando horário na mão (🗣️ *"crie um aviso, quando clicar no recalcular horarios…"*) —
// renderizava `<form method="post" class="mb-3" action="/Torneios/RefazerGrade">`, sem
// `data-confirmar` algum. Em produção o clique recalculava na hora e apagava as trocas.
//
// 🕳️ Nos dois, um `@* … *@` tinha sido escrito ENTRE os atributos da tag `<form …>`. O Razor
// aceita o comentário ali, mas o que vem depois dele dentro da tag não chega ao HTML — e o
// `confirmar.js` só intercepta `form[data-confirmar]`. Nada quebra, nada avisa: a tela só perde
// o diálogo. Por isso é teste de FONTE, sobre todas as views: a suíte não renderiza Razor, e o
// defeito é invisível pra teste de controller.
//
// A regra: comentário Razor fica FORA da tag — na linha de cima, ou dentro do corpo do
// elemento. Nunca entre `<tag` e `>`.
public class ComentarioRazorDentroDeTagTests
{
    [Fact]
    public void Nenhuma_view_tem_comentario_Razor_entre_os_atributos_de_uma_tag()
    {
        var pastaDasViews = Path.Combine(PastaDoProjeto(), "Views");
        var ofensas = new List<string>();

        foreach (var arquivo in Directory.EnumerateFiles(pastaDasViews, "*.cshtml", SearchOption.AllDirectories))
        {
            var fonte = File.ReadAllText(arquivo);
            foreach (Match comentario in Regex.Matches(fonte, @"@\*"))
            {
                // Olha pra trás: o último delimitador de tag antes do comentário. Se for um `<`
                // de abertura de tag (seguido de letra), sem `>` no meio, o comentário está
                // DENTRO da tag.
                int abre = fonte.LastIndexOf('<', comentario.Index);
                int fecha = fonte.LastIndexOf('>', comentario.Index);
                bool dentroDeTag = abre >= 0 && abre > fecha
                    && abre + 1 < fonte.Length && char.IsLetter(fonte[abre + 1]);
                if (!dentroDeTag) continue;

                int linha = fonte.Take(comentario.Index).Count(c => c == '\n') + 1;
                int fimDaTag = fonte.IndexOf(' ', abre);
                string tag = fimDaTag > abre ? fonte[abre..fimDaTag] : fonte[abre..Math.Min(abre + 20, fonte.Length)];
                ofensas.Add($"{Path.GetRelativePath(pastaDasViews, arquivo)}:{linha} — comentário Razor dentro de `{tag}`");
            }
        }

        Assert.True(ofensas.Count == 0,
            "Comentário Razor entre os atributos de uma tag apaga os atributos seguintes do HTML:\n  "
            + string.Join("\n  ", ofensas));
    }

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
