namespace Padelizou.Tests;

// O RÓTULO DO CAMPO "VALOR DA AULA" NA EDIÇÃO DE TURMA — pedido do Felipe (08/09/2026):
// "quando marco 4 atletas as vezes ele buga e da uns valores doidos". O campo sempre foi a
// FATIA da linha clicada, sem dizer isso em lugar nenhum — o professor comparava com o TOTAL
// que via no card da agenda (Services/AgendaDeTurma.Colapsar) e "corrigia" achando que estava
// errado, e só aquela linha mudava. Agora o campo fala do TOTAL (ver
// AulasController.Agenda.Editar) e o rótulo avisa disso.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. A regra de verdade (o GET manda o
// total, o POST racha de novo) tem trava de comportamento em AcoesEmGrupoNaTurmaTests; isto
// aqui trava só o aviso na tela, que nenhum teste de comportamento alcança.
public class RotuloDoValorNaEdicaoDeTurmaTests
{
    private static string Fonte()
    {
        var pasta = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "Views", "Aulas", "Editar.cshtml");
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException("Views/Aulas/Editar.cshtml não encontrado a partir do bin.");
    }

    [Fact]
    public void O_rotulo_avisa_que_o_campo_e_o_total_da_turma_quando_ha_mais_de_um_aluno()
    {
        var fonte = Fonte();

        var inicio = fonte.IndexOf("Valor da aula", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o rótulo \"Valor da aula\".");

        var fimDoLabel = fonte.IndexOf("</label>", inicio, StringComparison.Ordinal);
        Assert.True(fimDoLabel > inicio, "Não achei o fim do <label> do valor.");

        var label = fonte[inicio..fimDoLabel];
        Assert.Contains("QuantidadeAlunos", label);
        Assert.Contains("total da turma", label);
    }
}
