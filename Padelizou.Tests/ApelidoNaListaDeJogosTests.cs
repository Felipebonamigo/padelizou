using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 26/08/2026 — SÓ O APELIDO NA LISTA DE JOGOS DA PANELINHA, pedido do Felipe num print da tela
// "Jogos recentes": "pode ser só o apelido, e ao clicar abrir o perfil".
//
// ⚠️ ISTO NÃO DESFAZ A DECISÃO DE 06/08/2026, e é importante que a próxima sessão não leia
// assim. Naquele dia o apelido SOZINHO saiu de todas as telas, com o motivo escrito em
// NomeBonito.ComApelido: "apelido não identifica ninguém fora da turma — 'Zeca' pode ser três
// pessoas no mesmo torneio, e quem lê a chave de fora não sabe de quem se trata".
//
// O caso aqui é o OPOSTO desse: a lista de jogos de uma panelinha é lida DE DENTRO da turma,
// por gente que se conhece pelo apelido, e a linha tem quatro nomes competindo com data, placar
// e dois botões. É por isso que a mudança é escopada nesta lista (ConvidadoNoJogo.ApelidoNaTela)
// em vez de mexer em ComApelido — que continua valendo para torneio, chave e ranking, onde a
// razão de 06/08 continua de pé.
public class ApelidoNaListaDeJogosTests
{
    // ── A régua pura ──────────────────────────────────────────────────────────────────────

    [Theory]
    // O caso do pedido: quem tem apelido aparece SÓ por ele.
    [InlineData("Márcio Azeredo", "Marcião", "Marcião")]
    [InlineData("Jeferson Vier", "Jef", "Jef")]
    // Apelido de duas palavras continua inteiro — "Leo Turatti" é como a turma chama.
    [InlineData("Leonardo Turatti", "Leo Turatti", "Leo Turatti")]
    // ⚠️ SEM APELIDO NÃO PODE SUMIR: cai no nome curto, que é o que a tela já mostrava.
    [InlineData("Matias Leidemer", null, "Matias Leidemer")]
    [InlineData("Matias Leidemer", "", "Matias Leidemer")]
    [InlineData("Matias Leidemer", "   ", "Matias Leidemer")]
    // O nome curto é o mesmo do resto do app: primeiro e último, sem os do meio.
    [InlineData("Frederico Siqueira de Paula Vargas", null, "Frederico Vargas")]
    // Caixa torta se arruma igual em todo lugar.
    [InlineData("charls gustavio polese", "CHARLINHO", "Charlinho")]
    // Nome de uma palavra só (cadastro antigo) não quebra.
    [InlineData("Arthur", null, "Arthur")]
    public void O_apelido_manda_e_sem_ele_vale_o_nome_curto(string nome, string? apelido, string esperado)
    {
        Assert.Equal(esperado, NomeBonito.ApelidoOuCurto(nome, apelido));
    }

    [Fact]
    public void Nome_vazio_com_apelido_ainda_mostra_o_apelido()
    {
        Assert.Equal("Marcião", NomeBonito.ApelidoOuCurto("", "Marcião"));
        Assert.Equal("Marcião", NomeBonito.ApelidoOuCurto(null, "Marcião"));
    }

    // ── A vaga do convidado e a trava do Include ──────────────────────────────────────────

    [Fact]
    public void Vaga_sem_id_continua_sendo_Convidado()
    {
        // A vaga do convidado sem nome (id nulo) não vira apelido de ninguém.
        Assert.Equal(ConvidadoNoJogo.Rotulo, ConvidadoNoJogo.ApelidoNaTela(null, null));
    }

    [Fact]
    public void Id_preenchido_com_navegacao_nula_continua_estourando()
    {
        // A MESMA guarda de NomeNaTela, e ela não pode se perder na versão de apelido: navegação
        // nula com id preenchido é `.Include()` esquecido, não convidado. Engolir isso como
        // "Convidado" imprimiria a palavra por cima do nome de um membro real, sem erro e sem log.
        var estouro = Assert.Throws<InvalidOperationException>(
            () => ConvidadoNoJogo.ApelidoNaTela(42, null));

        Assert.Contains("Include", estouro.Message);
    }

    [Fact]
    public void Jogador_com_apelido_aparece_so_pelo_apelido()
    {
        var marcao = new Jogador { Id = 7, Nome = "Márcio Azeredo", Apelido = "Marcião", Cpf = "44400000001" };

        Assert.Equal("Marcião", ConvidadoNoJogo.ApelidoNaTela(marcao.Id, marcao));
        // E a versão completa continua existindo pras telas que precisam identificar de fora.
        Assert.Equal("Márcio Azeredo (Marcião)", ConvidadoNoJogo.NomeNaTela(marcao.Id, marcao));
    }

    [Fact]
    public void Jogador_sem_apelido_aparece_pelo_nome_curto()
    {
        var matias = new Jogador { Id = 8, Nome = "Matias Leidemer", Apelido = null, Cpf = "44400000002" };

        Assert.Equal("Matias Leidemer", ConvidadoNoJogo.ApelidoNaTela(matias.Id, matias));
    }

    // ── O que a tela consome ──────────────────────────────────────────────────────────────

    [Fact]
    public void O_ViewModel_da_lista_entrega_o_apelido()
    {
        // É este `Texto` que o partial _NomeNoJogoDaPanelinha imprime — as duas telas de
        // panelinha (Jogos recentes e Jogos da semana) passam pelo mesmo caminho, de propósito:
        // são a mesma listagem, e divergir entre elas seria o próximo "por que aqui está
        // diferente?".
        var marcao = new Jogador { Id = 9, Nome = "Márcio Azeredo", Apelido = "Marcião", Cpf = "44400000003" };

        Assert.Equal("Marcião", new Padelizou.ViewModels.NomeNoJogoDaPanelinha(marcao.Id, marcao).Texto);
        Assert.Equal(ConvidadoNoJogo.Rotulo, new Padelizou.ViewModels.NomeNoJogoDaPanelinha(null, null).Texto);
    }
}
