using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// O BOTÃO DE COMPARTILHAR TEM QUE PEDIR A ARTE **DA TELA QUE ESTÁ ABERTA**.
//
// ⚠️ É O DEFEITO CALADO DESTE TRABALHO, e é por isso que a URL é montada em C# e não no Razor:
// um botão que esquece um filtro não quebra, não loga e não fica feio — ele desenha, com
// capricho, o ranking do Brasil todo debaixo de uma tela que diz "Porto Alegre". Quem posta
// acha que está postando a própria posição. São 17 botões na tela do Ranking; montada à mão no
// `.cshtml`, seriam 17 chances de esquecer um parâmetro e nenhuma de perceber.
public class BotaoDeCompartilharRankingTests
{
    private static RankingHubVM Hub() => new();

    [Fact]
    public void A_url_leva_o_estado_e_as_cidades_que_a_tela_esta_mostrando()
    {
        var hub = Hub();
        hub.Estado = "RS";
        hub.Cidades.AddRange(new[] { "Porto Alegre", "Gravataí" });

        var url = new BotaoDeCompartilharRanking(hub, AbaDoRanking.Times).Url;

        Assert.Contains("estado=RS", url);
        // ⚠️ `cidade` REPETIDO, e não separado por vírgula: é o formato que a própria página já
        // usa e o que o binder do ASP.NET lê como `string[]`. Uma segunda convenção aqui faria
        // o card de duas cidades sair com o ranking de nenhuma.
        Assert.Contains("cidade=Porto%20Alegre", url);
        Assert.Contains("cidade=Gravata%C3%AD", url);
    }

    [Fact]
    public void A_url_leva_o_torneio_e_o_periodo_escolhidos()
    {
        var hub = Hub();
        hub.TorneioSelecionadoId = 26;
        hub.Periodo = "mes";

        var url = new BotaoDeCompartilharRanking(hub, AbaDoRanking.Trofeus, "3ª Masculina").Url;

        Assert.Contains("torneioId=26", url);
        Assert.Contains("periodo=mes", url);
        Assert.Contains("categoria=3%C2%AA%20Masculina", url);
    }

    [Fact]
    public void Sem_filtro_nenhum_a_url_leva_so_a_aba()
    {
        // "sempre" é o padrão do servidor: mandá-lo escrito só deixaria a URL mais longa, e
        // uma URL com parâmetro à toa é uma que alguém vai copiar e editar errado.
        var url = new BotaoDeCompartilharRanking(Hub(), AbaDoRanking.Palpiteiros).Url;

        Assert.Equal(BotaoDeCompartilharRanking.Acao + "?aba=Palpiteiros", url);
    }

    [Fact]
    public void Cada_aba_tem_um_nome_de_arquivo_proprio()
    {
        // A pessoa compartilha mais de uma: "ranking.png (1)" não diz qual é qual na galeria.
        Assert.Equal("ranking-times.png", new BotaoDeCompartilharRanking(Hub(), AbaDoRanking.Times).Arquivo);
        Assert.Equal("ranking-padelimetro.png",
            new BotaoDeCompartilharRanking(Hub(), AbaDoRanking.Padelimetro).Arquivo);
    }

    [Fact]
    public void Aba_sem_ninguem_nao_ganha_botao()
    {
        // ⚠️ A GUARDA MORA NA RÉGUA DE QUEM DESENHA, e não no `if` da tabela vizinha: são 17
        // botões na tela, cada um ao lado de uma tabela com a sua própria guarda de vazio, e a
        // primeira a discordar entregaria um botão que só sabe abrir 404.
        Assert.False(new BotaoDeCompartilharRanking(Hub(), AbaDoRanking.Times).TemArte);

        var comTime = Hub();
        comTime.Times.Add(new RankingTimeVM { TimeId = 1, Time = "Los Corneteiros", Pontos = 1301 });

        Assert.True(new BotaoDeCompartilharRanking(comTime, AbaDoRanking.Times).TemArte);
    }

    // ⚠️ O GATE QUE SEGURA A PRÓXIMA ABA. O `RankingParaCard.Escolher` é um `switch` sobre o
    // enum: aba nova sem entrada nele cai no `default`, e o `default` é `throw` de propósito —
    // devolver lista vazia faria o botão sumir da tela sem ninguém saber por quê, que é a
    // família de defeito que este repositório mais persegue. Este teste é o que transforma esse
    // esquecimento num erro de suíte em vez de um botão morto em produção.
    [Theory]
    [MemberData(nameof(TodasAsAbas))]
    public void Toda_aba_do_enum_sabe_virar_arte(AbaDoRanking aba)
    {
        // Hub VAZIO de propósito: o que se quer provar é que o `switch` tem o caso — e não que
        // existe dado. Sem o caso, isto estoura com `ArgumentOutOfRangeException` em vez de
        // devolver nulo.
        Assert.Null(RankingParaCard.Montar(Hub(), aba));
    }

    public static TheoryData<AbaDoRanking> TodasAsAbas()
    {
        var dados = new TheoryData<AbaDoRanking>();
        foreach (var aba in Enum.GetValues<AbaDoRanking>()) dados.Add(aba);
        return dados;
    }
}
