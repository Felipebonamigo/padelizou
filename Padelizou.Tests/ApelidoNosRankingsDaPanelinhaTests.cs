using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// 26/08/2026 — SÓ O APELIDO NOS RANKINGS DA PANELINHA, pedido do Felipe logo depois de a lista
// de jogos ganhar o mesmo tratamento: "no ranking do grupo, mostra só o apelido também".
//
// ⚠️ VALE NOS QUATRO RANKINGS DE PANELINHA, e não só no que foi citado. "Ranking do grupo" tem
// o "Ranking da semana" COLADO NELE na mesma tela, e o "Ranking Geral" do Detalhes é o MESMO
// dado (PontuacaoInterna) noutra tela, com o "Ranking do Mês" ao lado. Atender só o citado
// deixaria dois cards vizinhos escrevendo o mesmo jogador de dois jeitos — que é bug visual, não
// fidelidade ao pedido.
//
// ⚠️ E ISTO NÃO REVOGA A DECISÃO DE 06/08/2026 (o apelido sozinho saiu das telas porque não
// identifica ninguém de fora da turma). Mesma razão da lista de jogos: ranking de panelinha é
// lido DE DENTRO do grupo. `ComoChamar` continua intacto valendo em torneio, chave e no ranking
// PÚBLICO de jogadores, que é onde a razão de 06/08 continua de pé.
public class ApelidoNosRankingsDaPanelinhaTests
{
    [Fact]
    public void Jogador_com_apelido_aparece_so_pelo_apelido()
    {
        var marcao = new Jogador { Nome = "Márcio Azeredo", Apelido = "Marcião", Cpf = "33300000001" };

        Assert.Equal("Marcião", marcao.SoOApelido);
        // A forma completa continua existindo, intocada, pras telas de fora da turma.
        Assert.Equal("Márcio Azeredo (Marcião)", marcao.ComoChamar);
    }

    [Theory]
    // Sem apelido cai no nome CURTO — o mesmo que o ranking já mostrava. Sumir não era opção.
    [InlineData("Matias Leidemer", null, "Matias Leidemer")]
    [InlineData("Matias Leidemer", "", "Matias Leidemer")]
    [InlineData("Matias Leidemer", "   ", "Matias Leidemer")]
    // Nome longo encurta igual ao resto do app: primeiro e último.
    [InlineData("Frederico Siqueira de Paula Vargas", null, "Frederico Vargas")]
    // Apelido de duas palavras continua inteiro.
    [InlineData("Leonardo Turatti", "Leo Turatti", "Leo Turatti")]
    // Caixa torta se arruma nos dois campos.
    [InlineData("charls gustavio polese", "CHARLINHO", "Charlinho")]
    public void Sem_apelido_vale_o_nome_curto(string nome, string? apelido, string esperado)
    {
        var jogador = new Jogador { Nome = nome, Apelido = apelido, Cpf = "33300000002" };

        Assert.Equal(esperado, jogador.SoOApelido);
    }
}

// A LIGAÇÃO COM AS TELAS — teste de FONTE, pelo mesmo motivo de sempre neste projeto: trocar
// `SoOApelido` de volta por `ComoChamar` numa das views não quebra teste nenhum de
// comportamento (a suíte não renderiza Razor), e o estrago só aparece com alguém olhando.
//
// Cada âncora é o TÍTULO do card, que é único no arquivo — e o recorte para no fim da lista.
// Ancorar em algo genérico (`ComoChamar`, `list-group-numbered`) pegaria o card errado: foi
// exatamente esse o erro que custou uma rodada na trava dos `.Include()` mais cedo hoje.
public class RankingsDaPanelinhaUsamOApelidoTests
{
    [Theory]
    [InlineData("Grupos/Semana.cshtml", "Ranking da semana")]
    [InlineData("Grupos/Semana.cshtml", "Ranking do grupo")]
    [InlineData("Grupos/Detalhes.cshtml", "Ranking Geral")]
    [InlineData("Grupos/Detalhes.cshtml", "Ranking do Mês")]
    public void O_card_de_ranking_escreve_o_apelido(string view, string titulo)
    {
        var bloco = BlocoDoCard(view, titulo);

        Assert.Contains("SoOApelido", bloco);
        Assert.DoesNotContain("ComoChamar", bloco);
    }

    // Do título do card até o fim da `<ol>` dele.
    private static string BlocoDoCard(string view, string titulo)
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", view.Replace('/', Path.DirectorySeparatorChar)));

        var inicio = fonte.IndexOf(titulo, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Não achei o card \"{titulo}\" em {view} — o título mudou e esta trava parou de olhar pra ele.");

        var fim = fonte.IndexOf("</ol>", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, $"Não achei o fim da lista do card \"{titulo}\" em {view}.");

        return fonte[inicio..fim];
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
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
