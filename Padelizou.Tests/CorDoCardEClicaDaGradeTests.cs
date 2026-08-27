using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// DOIS RELATOS DO MESMO PROFESSOR (28/08/2026, repassados pelo Felipe):
//
//   1. "clico no nome da aula pra concluir e abre a janela de MARCAR aula" — clicar no card
//      de uma aula na grade de Dia/Semana navegava pro AdicionarManual em vez de abrir o
//      modal da aula.
//   2. "se conseguir deixar em amarelo o a receber na agenda fica filé" — a aula dada e não
//      paga era verde igual à paga; a informação só existia no Financeiro e no modal.
//
// ⚠️ TESTES DE FONTE, escolha consciente (o padrão de GradeDaAgendaRolaInteiraTests): a suíte
// não renderiza Razor nem roda JS, e a alternativa era não travar nada. Cada um prende a
// INVARIANTE, não a aparência.
public class CorDoCardEClicaDaGradeTests
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

    // 🐛 O BUG DO CLIQUE. O card da aula (`[data-aula]`) mora DENTRO da coluna clicável
    // (`.pdz-grade-coluna`), e clique borbulha: sem uma guarda, o mesmo toque abre o modal E
    // navega pro "Adicionar Aula" — na prática a navegação vence, e o professor vê a tela de
    // marcar aula ao clicar numa aula que já existe. O comentário antigo do código dizia que
    // "o clique nunca chega a borbulhar até a coluna": dizia errado, nada impedia.
    //
    // A invariante: o handler da coluna DESISTE quando o clique veio de dentro de um card.
    [Fact]
    public void O_clique_na_aula_nao_vira_clique_no_espaco_vazio_da_coluna()
    {
        var fonte = Fonte();

        // Recorta o handler da coluna — do querySelectorAll até o window.location que ele monta.
        var handler = Regex.Match(fonte,
            @"querySelectorAll\('\.pdz-grade-coluna'\)(.*?)window\.location\.href",
            RegexOptions.Singleline);

        Assert.True(handler.Success, "o handler de clique da .pdz-grade-coluna sumiu da view");
        Assert.Contains("closest('[data-aula]')", handler.Groups[1].Value);
    }

    // 💛 A COR DO "A RECEBER". A régua é a MESMA do Financeiro e do selo do modal —
    // `RecebimentoDaAula.EstaAReceber` — porque telas que refazem a conta por conta própria é
    // como duas telas passam a discordar sobre dinheiro. E a cor é a MESMA do selo do modal
    // (#c78a1e): dois amarelos diferentes pra mesma coisa viraria uma terceira pergunta.
    [Fact]
    public void O_card_da_agenda_pinta_a_receber_pela_regua_do_financeiro()
    {
        var fonte = Fonte();

        // A função de cor POR AULA consulta a régua de recebimento…
        var corDa = Regex.Match(fonte, @"CorDa\(Aula[^)]*\)\s*=>?(.*?);", RegexOptions.Singleline);
        Assert.True(corDa.Success, "a view não tem uma função de cor que receba a Aula (CorDa)");
        Assert.Contains("RecebimentoDaAula.EstaAReceber", corDa.Groups[1].Value);
        Assert.Contains("#c78a1e", corDa.Groups[1].Value);

        // …e é ELA que pinta o card da grade e o chip do mês — não o switch cru por status.
        Assert.Matches(new Regex(@"pdz-evento[^>]*\n[^\n]*background:@CorDa\(aula\)"), fonte);
        Assert.Matches(new Regex(@"pdz-chip[^§]{0,200}?background:@CorDa\(aula\)"), fonte);

        // A legenda explica a cor nova — cor sem legenda é adivinhação.
        Assert.Contains("A receber", fonte);
    }
}
