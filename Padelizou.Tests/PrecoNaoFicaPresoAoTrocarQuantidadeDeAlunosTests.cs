namespace Padelizou.Tests;

// O CAMPO DE PREÇO FICAVA PRESO NO VALOR DIGITADO PRA UM TAMANHO ANTIGO — pedido do Felipe
// (08/09/2026): "quando marco 4 atletas as vezes ele buga e da uns valores doidos". O
// professor digitava um valor com "1" selecionado, trocava pra "Quarteto", e o número antigo
// continuava no campo — só a SUGESTÃO (o placeholder) mudava junto. Submetido, o servidor lê
// esse número como o TOTAL da turma (AulasController.Agenda.AdicionarManual) e racha entre os
// 4: um valor pensado pra UMA pessoa vira a fatia de cada uma das quatro.
//
// Mesma régua que este arquivo já usa pros blocos de nome por aluno (ver
// renderizarBlocosDeTurma): "preencheu 3 e voltou pra 2 ... some tudo e o professor preenche
// de novo, que é 2 toques, não um dilema."
public class PrecoNaoFicaPresoAoTrocarQuantidadeDeAlunosTests
{
    private static string Fonte()
    {
        var pasta = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "Views", "Aulas", "AdicionarManual.cshtml");
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException("Views/Aulas/AdicionarManual.cshtml não encontrado a partir do bin.");
    }

    [Fact]
    public void Trocar_a_quantidade_de_alunos_limpa_o_preco_digitado()
    {
        var fonte = Fonte();

        // ⚠️ Ancorado no `.on('change'`, não só em `input[name="quantidadeAlunos"]` — esse
        // seletor aparece ANTES dentro de `atualizarSugestao()` (o `:checked` que lê o valor
        // atual), e um IndexOf sem esse recorte pegaria aquele trecho em vez do listener.
        var inicio = fonte.IndexOf("input[name=\"quantidadeAlunos\"]').on('change'", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o listener de troca de quantidade de alunos.");

        var fimDoHandler = fonte.IndexOf("});", inicio, StringComparison.Ordinal);
        Assert.True(fimDoHandler > inicio, "Não achei o fim do handler de troca de quantidade de alunos.");

        var handler = fonte[inicio..fimDoHandler];
        Assert.Contains("inputPreco.val(", handler);
    }
}
