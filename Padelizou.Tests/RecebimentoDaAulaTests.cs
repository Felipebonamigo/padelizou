using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// "Concluída" deixou de querer dizer "paga" (pedido do Felipe em 25/08/2026: "tem alunos que
// pagam depois ou por mês"). A régua pura de quem deve o quê mora aqui, e não no controller,
// porque a mesma pergunta é feita em quatro lugares que não podem discordar: a folha da
// agenda, o card "a cobrar" do Financeiro, a lista de devedores e a conta do mês.
public class RecebimentoDaAulaTests
{
    private static Aula Aula(string status, decimal preco = 110, bool cobrarMesmoFaltando = false,
        DateTime? pagaEm = null, int? repoe = null) => new()
    {
        Status = status,
        Preco = preco,
        CobrarMesmoFaltando = cobrarMesmoFaltando,
        PagaEm = pagaEm,
        RecuperaAulaId = repoe,
        DataHora = new DateTime(2026, 8, 18, 9, 0, 0),
    };

    [Fact]
    public void Aula_dada_e_nao_paga_esta_a_receber()
    {
        Assert.True(RecebimentoDaAula.EstaAReceber(Aula(PoliticaAula.Realizada)));
    }

    [Fact]
    public void Aula_dada_e_paga_sai_do_a_receber()
    {
        var aula = Aula(PoliticaAula.Realizada, pagaEm: new DateTime(2026, 8, 18, 10, 0, 0));

        Assert.True(RecebimentoDaAula.EstaPaga(aula));
        Assert.False(RecebimentoDaAula.EstaAReceber(aula));
    }

    // A linha mais comum do mensalista: não veio, foi cobrada assim mesmo, e o dinheiro ainda
    // não entrou. É exatamente o caso que um status "Paga" não conseguiria representar.
    [Fact]
    public void Falta_cobravel_nao_paga_esta_a_receber()
    {
        Assert.True(RecebimentoDaAula.EstaAReceber(
            Aula(PoliticaAula.Faltou, cobrarMesmoFaltando: true)));
    }

    [Fact]
    public void Falta_cobravel_ja_paga_sai_do_a_receber()
    {
        Assert.False(RecebimentoDaAula.EstaAReceber(
            Aula(PoliticaAula.Faltou, cobrarMesmoFaltando: true, pagaEm: DateTime.Now)));
    }

    [Fact]
    public void Falta_que_o_professor_nao_cobrou_nao_esta_a_receber()
    {
        Assert.False(RecebimentoDaAula.EstaAReceber(Aula(PoliticaAula.Faltou)));
    }

    // "Vai recuperar" marca CobrarMesmoFaltando — quem paga o mês não desconta a falta.
    [Fact]
    public void A_recuperar_cobravel_esta_a_receber()
    {
        Assert.True(RecebimentoDaAula.EstaAReceber(
            Aula(PoliticaAula.ARecuperar, cobrarMesmoFaltando: true)));
    }

    // ⚠️ A reposição ACONTECEU, mas o dinheiro dela entrou no mês da aula original — foi por
    // isso que ela nasceu sem preço. Cobrá-la aqui cobraria a mesma aula duas vezes.
    [Fact]
    public void Reposicao_nunca_esta_a_receber()
    {
        Assert.False(RecebimentoDaAula.EstaAReceber(
            Aula(PoliticaAula.Realizada, preco: 0, repoe: 42)));
    }

    // Mesmo com preço (professor que editou o valor da reposição na mão): continua fora.
    [Fact]
    public void Reposicao_com_preco_continua_fora_do_a_receber()
    {
        Assert.False(RecebimentoDaAula.EstaAReceber(
            Aula(PoliticaAula.Realizada, preco: 110, repoe: 42)));
    }

    [Fact]
    public void Aula_de_graca_nao_esta_a_receber()
    {
        Assert.False(RecebimentoDaAula.EstaAReceber(Aula(PoliticaAula.Realizada, preco: 0)));
    }

    [Fact]
    public void Aula_confirmada_ainda_nao_gerou_cobranca()
    {
        Assert.False(RecebimentoDaAula.EstaAReceber(Aula(PoliticaAula.Confirmada)));
        Assert.False(RecebimentoDaAula.PodeMarcar(Aula(PoliticaAula.Confirmada)));
    }

    [Fact]
    public void Aula_cancelada_nao_esta_a_receber()
    {
        Assert.False(RecebimentoDaAula.EstaAReceber(Aula(PoliticaAula.Cancelada)));
    }

    [Fact]
    public void Pode_marcar_recebida_a_aula_dada_e_a_falta_cobravel()
    {
        Assert.True(RecebimentoDaAula.PodeMarcar(Aula(PoliticaAula.Realizada)));
        Assert.True(RecebimentoDaAula.PodeMarcar(Aula(PoliticaAula.Faltou, cobrarMesmoFaltando: true)));
    }

    [Fact]
    public void Nao_pode_marcar_recebida_a_reposicao_nem_a_aula_de_graca()
    {
        Assert.False(RecebimentoDaAula.PodeMarcar(Aula(PoliticaAula.Realizada, preco: 0, repoe: 42)));
        Assert.False(RecebimentoDaAula.PodeMarcar(Aula(PoliticaAula.Realizada, preco: 0)));
    }

    // ⚠️ A invariante que segura o Financeiro: toda aula que gerou cobrança está de UM dos dois
    // lados, nunca nos dois e nunca em nenhum. Sem ela, "Recebido" + "A cobrar" deixa de somar
    // o faturamento do período e o professor vê dinheiro sumir sem nome.
    [Fact]
    public void Recebida_e_a_receber_partem_em_duas_o_que_gerou_cobranca()
    {
        var todas = new[]
        {
            Aula(PoliticaAula.Realizada),
            Aula(PoliticaAula.Realizada, pagaEm: DateTime.Now),
            Aula(PoliticaAula.Faltou, cobrarMesmoFaltando: true),
            Aula(PoliticaAula.Faltou, cobrarMesmoFaltando: true, pagaEm: DateTime.Now),
            Aula(PoliticaAula.Faltou),
            Aula(PoliticaAula.Confirmada),
            Aula(PoliticaAula.Cancelada),
            Aula(PoliticaAula.Realizada, preco: 0, repoe: 42),
            Aula(PoliticaAula.Realizada, preco: 0),
        };

        foreach (var aula in todas)
        {
            var recebida = RecebimentoDaAula.FoiRecebida(aula);
            var aReceber = RecebimentoDaAula.EstaAReceber(aula);

            Assert.False(recebida && aReceber);
            Assert.Equal(RecebimentoDaAula.GerouCobranca(aula), recebida || aReceber);
        }
    }

    // O motivo é escrito pro professor ler na tela — e cada caso tem o SEU, senão a mensagem
    // genérica manda ele procurar o problema no lugar errado.
    [Fact]
    public void O_motivo_de_nao_dar_pra_marcar_nomeia_o_caso()
    {
        Assert.Contains("repõe", RecebimentoDaAula.MotivoParaNaoMarcar(
            Aula(PoliticaAula.Realizada, preco: 0, repoe: 42)), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("R$ 0", RecebimentoDaAula.MotivoParaNaoMarcar(
            Aula(PoliticaAula.Realizada, preco: 0)));
        Assert.Contains("cobrável", RecebimentoDaAula.MotivoParaNaoMarcar(
            Aula(PoliticaAula.Confirmada)), StringComparison.OrdinalIgnoreCase);
    }
}
