using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 13/09/2026 — O JOGO NASCIA COM PLACAR 0 x 0.
//
// 🗣️ Felipe, com o print de um jogo da chave que ainda não tinha sido chamado: *"Aqui esta
// aparecendo placar que ainda não comecou"*.
//
// 🕳️ `DbPadelContext` declarava `HasDefaultValue(0)` nas quatro colunas de placar. Elas são
// NULÁVEIS (`int?`) e o robô nunca escreve placar ao criar a partida
// (`RoboDoChaveamento.cs:168`) — então o DEFAULT do banco preenchia `0`, e o jogo nascia com
// placar.
//
// ⚠️ O RESTO DO SISTEMA JÁ TRATAVA `null` COMO "SEM PLACAR", e aquela linha era a única que
// discordava:
//   • `DesfazerDoJogo.VoltarParaAgendado` zera pra `null` — *"placar de um jogo que não
//     aconteceu é o que enche a Mesa de número que ninguém sabe de onde veio"*
//   • `QuemVenceu.MotivoParaNaoFinalizar` recusa finalizar quando os dois são `null` — e esse
//     guarda estava MORTO nos jogos novos, porque `0 != null`
//   • `PadelimetroService` exige `!= null`
//   • as duas views da chave já escreviam `?? "–"`: a regra certa estava lá, o dado é que
//     nunca a satisfazia
//
// ⚠️ POR QUE O GATE OLHA O METADADO DO MODELO, E NÃO UMA PARTIDA SALVA: **o EF InMemory NÃO
// aplica `HasDefaultValue`**. Uma partida criada na suíte nasce com `null` MESMO COM O DEFEITO
// PRESENTE — conferido antes de escrever isto. Um teste que salvasse e olhasse o placar
// passaria verde defendendo nada. O que atravessa os dois provedores é a anotação
// `Relational:DefaultValue` no modelo, e é ela que o Npgsql lê pra montar o DEFAULT da coluna.
public class PlacarNaoNasceZeradoTests
{
    [Theory]
    [InlineData(nameof(Partida.GamesDupla1))]
    [InlineData(nameof(Partida.GamesDupla2))]
    [InlineData(nameof(Partida.SetsDupla1))]
    [InlineData(nameof(Partida.SetsDupla2))]
    public void Nenhuma_coluna_de_placar_tem_valor_padrao_no_banco(string coluna)
    {
        using var ctx = TestInfra.NovoContexto();

        var propriedade = ctx.Model.FindEntityType(typeof(Partida))!.FindProperty(coluna)!;

        // `Relational:DefaultValue` é o que o Npgsql traduz em `DEFAULT 0` na coluna. Com ela
        // presente, todo jogo criado sem placar nasce 0 x 0 no Postgres — e nenhum teste desta
        // suíte enxergaria isso, porque o InMemory ignora a anotação.
        Assert.Null(propriedade.FindAnnotation("Relational:DefaultValue")?.Value);
    }

    // ── A RÉGUA DA CHAVE, CASO A CASO ──────────────────────────────────────────────────
    //
    // ⚠️ O caso "Agendada com 0 gravado" é o que faz esta régua valer a pena: a migration
    // limpa as linhas que já estão no banco, mas a tela não pode DEPENDER disso. Se uma linha
    // escapar — banco restaurado de backup antigo, jogo criado por caminho que não passou
    // pela limpeza —, a chave continua dizendo a verdade.

    [Theory]
    [InlineData("Agendada", null)]
    [InlineData("Agendada", 0)]
    public void Jogo_que_nao_comecou_nao_mostra_placar(string status, int? games)
    {
        Assert.Equal("–", PlacarNaTela.DaChave(status, games));
    }

    [Fact]
    public void Jogo_em_quadra_sem_game_feito_mostra_zero()
    {
        // 🗣️ Escolha do Felipe, 13/09/2026. O card grande do AO VIVO já mostra `0` (`?? 0`);
        // a chave mostrando "–" no mesmo jogo faria as duas telas discordarem, do lado do
        // selo ● AO VIVO.
        Assert.Equal("0", PlacarNaTela.DaChave("AoVivo", null));
    }

    [Theory]
    [InlineData("AoVivo", 3, "3")]
    [InlineData("Finalizada", 9, "9")]
    [InlineData("Finalizada", 0, "0")]
    public void Jogo_com_placar_mostra_o_placar(string status, int? games, string esperado)
    {
        Assert.Equal(esperado, PlacarNaTela.DaChave(status, games));
    }

    [Fact]
    public void Jogo_finalizado_sem_placar_nao_inventa_zero()
    {
        // Não deveria existir (`QuemVenceu.MotivoParaNaoFinalizar` barra), mas se existir a
        // tela não vai afirmar um 0 que ninguém marcou.
        Assert.Equal("–", PlacarNaTela.DaChave("Finalizada", null));
    }

    [Theory]
    [InlineData("_ChaveVaga.cshtml")]
    [InlineData("_ChaveDoMataMata.cshtml")]
    public void As_duas_views_da_chave_usam_a_regua(string caminho)
    {
        var fonte = TestInfra.SemComentarios(File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", caminho)));

        // Sem isto a régua fica escrita e sem uso, e a view volta a decidir por conta própria.
        Assert.Contains("PlacarNaTela.DaChave", fonte);
        Assert.DoesNotContain("GamesDupla1?.ToString()", fonte);
        Assert.DoesNotContain("GamesDupla2?.ToString()", fonte);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
