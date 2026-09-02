using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// AS DUAS TELAS DO IMPEDIMENTO.
//
// A regra de quem pode trocar e do que acontece com o valor mora em
// Services/AlteracaoDeImpedimento e tem teste próprio. O que este arquivo prende é o outro
// lado: a regra pode estar perfeita e a tela não oferecer nada — foi exatamente o que
// aconteceu com o botão de desistir do Americano, que existia no servidor e não na página.
//
// ⚠️ É TESTE DE FONTE, e isso é escolha consciente: não há suíte de Razor neste projeto.
public class ImpedimentoNaTelaTests
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
    public void A_lista_de_impedimentos_vem_ANTES_do_botao_de_sortear()
    {
        // 🗣️ "para a hora de gerar as chaves, colocar uma lista com todos os impedimentos".
        // Depois do botão não serve pra nada: a decisão de apertar já foi tomada.
        var fonte = Fonte();

        int ondeEstaALista = fonte.IndexOf("comImpedimento.Any()");
        int ondeEstaOBotao = fonte.IndexOf("asp-action=\"GerarChaves\"");

        Assert.True(ondeEstaALista > 0, "a lista de impedimentos não aparece na tela");
        Assert.True(ondeEstaALista < ondeEstaOBotao,
            "a lista precisa vir antes do botão de gerar as chaves");
    }

    [Fact]
    public void A_lista_diz_a_categoria_e_o_turno_de_cada_dupla()
    {
        // "por dupla" quer dizer com nome e com turno — uma contagem sozinha não ajuda a
        // decidir nada.
        var fonte = Fonte();

        Assert.Contains("AlteracaoDeImpedimento.Rotulo(turno)", fonte);
        Assert.Contains("x.Categoria.Nome", fonte);
    }

    [Fact]
    public void O_inscrito_tem_como_TROCAR_o_impedimento()
    {
        var fonte = Fonte();

        Assert.Contains("asp-action=\"AlterarImpedimento\"", fonte);
        Assert.Contains("name=\"turno\"", fonte);
    }

    [Fact]
    public void A_tela_avisa_o_valor_ANTES_de_marcar_o_primeiro_impedimento()
    {
        // 🗣️ A régua do Felipe: "se não [tem outro], avisa que é cobrado e o valor que é
        // adicionado". Cobrar sem avisar é como a pessoa descobre a taxa no extrato.
        var fonte = Fonte();

        Assert.Matches(new Regex(
            @"turnoAtual == Padelizou\.Services\.TurnoDoImpedimento\.Nenhum[\s\S]{0,400}?Marcar um impedimento soma"),
            fonte);
    }

    [Fact]
    public void O_seletor_so_aparece_na_inscricao_de_quem_e_dono()
    {
        // Sem isso, "trocar meu impedimento" vira "trocar o impedimento dos outros". A trava
        // de verdade é do servidor (AlteracaoDeImpedimento), mas oferecer o campo no card
        // alheio convida ao clique e devolve erro — quando o certo é não oferecer.
        //
        // ⚠️ A prova aqui é de ORDEM entre três âncoras, e não uma janela de N caracteres: o
        // bloco do dono é grande (o formulário de trocar parceiro mora dentro dele), então
        // qualquer distância que eu escolhesse seria arbitrária — grande o bastante pra passar
        // e, por isso mesmo, grande o bastante pra não provar nada. Estar DEPOIS da checagem de
        // dono e ANTES do formulário de desistir, que é o último item do mesmo bloco, é o que
        // amarra o campo lá dentro.
        var fonte = Fonte();

        Assert.Matches(new Regex(
            @"dupla\.Jogador1Id == meuIdNaPagina[\s\S]*?asp-action=""AlterarImpedimento""[\s\S]*?asp-action=""Desistir"""),
            fonte);

        // E existe UM formulário de troca, não uma segunda cópia solta em outro canto da tela.
        Assert.Equal(1, Regex.Matches(fonte, @"asp-action=""AlterarImpedimento""").Count);
    }
}
