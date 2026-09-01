using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// AS TRÊS SAÍDAS DO TORNEIO PRECISAM EXISTIR NA TELA.
//
// A regra de quem pode sair mora em Services/DesistenciaDeInscricao e tem teste próprio. O que
// este arquivo prende é o outro lado: a regra pode estar perfeita e o botão não existir. Foi
// exatamente o que aconteceu com o Americano — o servidor sabia recusar e permitir, e o card do
// inscrito simplesmente não tinha formulário nenhum, então quem jogava Americano continuava
// dependendo de mandar mensagem pro organizador.
//
// ⚠️ É TESTE DE FONTE, e isso é escolha consciente: não há suíte de Razor neste projeto, e a
// alternativa era não travar nada. Ele prende a INVARIANTE — as três saídas estão ligadas a uma
// ação do servidor —, não a aparência dos botões.
public class SairDoTorneioNaTelaTests
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
    public void A_dupla_completa_recebe_as_DUAS_saidas()
    {
        // Antes o sistema escolhia "sai só eu" por você. A dupla que desistia junto precisava
        // clicar em duas contas, e a segunda quase nunca clicava — meia inscrição segurava a
        // vaga até o encerramento, com a lista de espera parada atrás dela.
        var fonte = Fonte();

        Assert.Contains("name=\"escolha\" value=\"@Padelizou.Services.EscolhaDeQuemSai.SoEu\"", fonte);
        Assert.Contains("name=\"escolha\" value=\"@Padelizou.Services.EscolhaDeQuemSai.ADuplaInteira\"", fonte);
    }

    [Fact]
    public void O_inscrito_do_americano_tem_como_sair()
    {
        // O buraco original: o botão de desistir nasceu dentro do laço de DUPLAS, e a lista do
        // Americano é outra (InscricaoAmericana, um jogador por linha).
        var fonte = Fonte();

        Assert.Contains("asp-action=\"DesistirDoAmericano\"", fonte);
        Assert.Contains("name=\"inscricaoId\"", fonte);
    }

    [Fact]
    public void Quem_ja_pagou_e_avisado_de_que_a_devolucao_nao_e_automatica()
    {
        // Estornar é botão do organizador, por desenho (ESTORNO.md). Sem este aviso a pessoa
        // cancela achando que o dinheiro volta junto.
        var fonte = Fonte();

        // Uma vez pra inscrição em dupla, uma vez pra inscrição de Americano.
        int quantos = Regex.Matches(fonte, "a devolução é feita pelo organizador, não é automática").Count;

        Assert.Equal(2, quantos);
    }

    [Fact]
    public void O_botao_do_americano_so_aparece_no_card_de_quem_e_dono()
    {
        // Sem a checagem de dono na tela, "sair do torneio" vira "tirar gente do torneio". A
        // trava de verdade é do servidor (DesistenciaDeInscricao), mas mostrar o botão no card
        // alheio convida ao clique e devolve um erro — quando o certo é não oferecer.
        //
        // ⚠️ Aqui vai regex, e não `Assert.Contains`, porque `inscricao.JogadorId ==
        // meuJogadorId` JÁ aparecia nesta tela por outro motivo (o prazo de pagamento do dono):
        // procurar o texto solto daria verde mesmo com o botão exposto no card de todo mundo.
        // A janela curta é o que amarra a guarda ao formulário — hoje são 570 caracteres entre
        // as duas linhas.
        //
        // O bloco das duplas não entra aqui: a guarda dele é anterior a este trabalho e passava
        // antes da mudança. Teste que fica verde dos dois lados não prende nada.
        var fonte = Fonte();

        Assert.Matches(new Regex(
            @"inscricao\.JogadorId == meuJogadorId\)[\s\S]{0,900}?asp-action=""DesistirDoAmericano"""),
            fonte);
    }
}
