using Xunit;

namespace Padelizou.Tests;

// O VALOR TEM QUE ESTAR NO CARD DO PIX, E ANTES DA CHAVE.
//
// A regra de quanto mostrar mora em `PixDoOrganizador` e tem teste próprio. O que este arquivo
// prende é o outro lado: a regra pode estar certa e o número não aparecer na tela — que é
// exatamente o estado anterior, em que o preço existia só no cabeçalho da página.
//
// ⚠️ É TESTE DE FONTE, e isso é escolha consciente: não há suíte de Razor neste projeto, e a
// alternativa era não travar nada.
public class ValorNoPixNaTelaTests
{
    private static string Fonte()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "Views", "Torneios", "Details.cshtml");
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException("Details.cshtml não encontrado a partir do bin.");
    }

    [Fact]
    public void O_valor_aparece_ANTES_da_chave_no_card_do_Pix()
    {
        // "Perto da chave" não basta: a ordem de quem paga é decidir o valor, copiar a chave,
        // mandar. Número depois do campo é número que já não muda o que a pessoa digitou.
        var fonte = Fonte();

        int ondeEstaOValor = fonte.IndexOf("PixDoOrganizador.ValorDaInscricao(Model)");
        int ondeEstaAChave = fonte.IndexOf("campoPixTorneio");

        Assert.True(ondeEstaOValor > 0, "o valor da inscrição não aparece no card do Pix");
        Assert.True(ondeEstaOValor < ondeEstaAChave,
            "o valor precisa vir antes do campo da chave Pix");
    }

    [Fact]
    public void A_tela_nao_recalcula_o_preco_por_conta_propria()
    {
        // O risco real de manutenção: alguém escrever `Model.PrecoInscricao * 2` aqui dentro.
        // Aí passam a existir duas contas do mesmo dinheiro, e a página anuncia dois valores
        // diferentes no dia em que uma das duas mudar.
        var fonte = Fonte();

        Assert.Contains("PixDoOrganizador.ValorPorPessoa(Model)", fonte);
        Assert.DoesNotContain("Model.PrecoInscricao * 2", fonte);
        Assert.DoesNotContain("Model.PrecoInscricao * Model.PessoasPorInscricao", fonte);
    }

    [Fact]
    public void O_torneio_com_preco_variavel_avisa_em_vez_de_prometer()
    {
        // Impedimento soma e segunda categoria pode custar outro preço. O card não sabe o que
        // esta pessoa vai marcar — e um total errado colado numa chave Pix é dinheiro pago a
        // menos, num fluxo que o Padelizou não vê e não conserta.
        var fonte = Fonte();

        Assert.Contains("PixDoOrganizador.OTotalPodeVariar(Model)", fonte);
        Assert.Contains("Esse é o valor base", fonte);
    }
}
