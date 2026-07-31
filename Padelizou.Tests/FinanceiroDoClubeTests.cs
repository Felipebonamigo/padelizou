using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Contas a pagar e a receber do clube. São MANUAIS de propósito: o que já passou pelo
// Padelizou (comanda fechada, reserva paga) aparece em coluna separada e nunca somado —
// somar contaria a mesma receita duas vezes.
public class FinanceiroDoClubeTests
{
    private static DateTime Hoje => new(2026, 7, 31);

    // ---------- Situação ----------

    [Fact]
    public void A_situacao_e_calculada_na_hora_nunca_gravada()
    {
        // "Vencida" muda sozinha com a passagem do tempo. Gravada no banco, obrigaria alguém
        // a varrer a tabela toda meia-noite só pra manter a verdade em dia.
        var vencida = new LancamentoFinanceiro { Vencimento = Hoje.AddDays(-1) };
        var hoje = new LancamentoFinanceiro { Vencimento = Hoje };
        var futura = new LancamentoFinanceiro { Vencimento = Hoje.AddDays(1) };
        var paga = new LancamentoFinanceiro { Vencimento = Hoje.AddDays(-30), QuitadoEm = Hoje.AddDays(-30) };

        Assert.Equal(FinanceiroDoClube.Vencido, FinanceiroDoClube.Situacao(vencida, Hoje));
        Assert.Equal(FinanceiroDoClube.VenceHoje, FinanceiroDoClube.Situacao(hoje, Hoje));
        Assert.Equal(FinanceiroDoClube.AVencer, FinanceiroDoClube.Situacao(futura, Hoje));
        // Conta paga não fica "vencida" pra sempre só porque a data passou.
        Assert.Equal(FinanceiroDoClube.Quitado, FinanceiroDoClube.Situacao(paga, Hoje));
    }

    [Fact]
    public void A_hora_do_dia_nao_muda_a_situacao()
    {
        // Vencimento é DIA, não instante: uma conta que vence hoje não pode virar "vencida"
        // porque o relógio passou das 14h.
        var conta = new LancamentoFinanceiro { Vencimento = new DateTime(2026, 7, 31) };
        Assert.Equal(FinanceiroDoClube.VenceHoje,
            FinanceiroDoClube.Situacao(conta, new DateTime(2026, 7, 31, 23, 59, 0)));
    }

    // ---------- Validação ----------

    [Theory]
    [InlineData(null, 100, "Pagar", 1)]
    [InlineData("   ", 100, "Pagar", 1)]
    [InlineData("Luz", 0, "Pagar", 1)]        // conta de R$ 0 não é conta, é lembrete
    [InlineData("Luz", -50, "Pagar", 1)]      // o sinal quem dá é o Tipo
    [InlineData("Luz", 100, "Sei lá", 1)]
    [InlineData("Luz", 100, "Pagar", 0)]
    [InlineData("Luz", 100, "Pagar", 61)]     // 5 anos é o teto
    public void Lancamento_torto_e_recusado(string? descricao, decimal valor, string tipo, int meses)
        => Assert.NotNull(FinanceiroDoClube.ProblemaCom(descricao, valor, tipo, meses));

    [Fact]
    public void Lancamento_certo_passa()
    {
        Assert.Null(FinanceiroDoClube.ProblemaCom("Cerveja — pedido de junho", 1289.90m, "Pagar", 1));
        Assert.Null(FinanceiroDoClube.ProblemaCom("Aluguel", 3500m, "Pagar", 12));
        Assert.Null(FinanceiroDoClube.ProblemaCom("Patrocínio", 500m, "Receber", 6));
    }

    // ---------- Recorrência ----------

    [Fact]
    public void Repetir_gera_uma_parcela_por_mes_no_mesmo_dia()
    {
        var datas = FinanceiroDoClube.Vencimentos(new DateTime(2026, 1, 10), 4);

        Assert.Equal(4, datas.Count);
        Assert.Equal(new DateTime(2026, 1, 10), datas[0]);
        Assert.Equal(new DateTime(2026, 2, 10), datas[1]);
        Assert.Equal(new DateTime(2026, 3, 10), datas[2]);
        Assert.Equal(new DateTime(2026, 4, 10), datas[3]);
    }

    [Fact]
    public void Dia_31_cai_no_ultimo_dia_do_mes_que_nao_tem_31()
    {
        // É como todo boleto se comporta. Sem isso, vencimento dia 31 estouraria em fevereiro.
        var datas = FinanceiroDoClube.Vencimentos(new DateTime(2026, 1, 31), 4);

        Assert.Equal(new DateTime(2026, 1, 31), datas[0]);
        Assert.Equal(new DateTime(2026, 2, 28), datas[1]);
        Assert.Equal(new DateTime(2026, 3, 31), datas[2]);   // volta pro 31, não fica preso no 28
        Assert.Equal(new DateTime(2026, 4, 30), datas[3]);
    }

    [Fact]
    public void Fevereiro_de_ano_bissexto_tem_29()
    {
        var datas = FinanceiroDoClube.Vencimentos(new DateTime(2028, 1, 31), 2);
        Assert.Equal(new DateTime(2028, 2, 29), datas[1]);
    }

    // ---------- Resumo ----------

    [Fact]
    public void O_resumo_separa_o_que_ja_venceu_do_que_vai_vencer()
    {
        var lancamentos = new[]
        {
            new LancamentoFinanceiro { Tipo = "Pagar", Valor = 1000m, Vencimento = Hoje.AddDays(-5) },
            new LancamentoFinanceiro { Tipo = "Pagar", Valor = 500.50m, Vencimento = Hoje.AddDays(5) },
            new LancamentoFinanceiro { Tipo = "Receber", Valor = 2000m, Vencimento = Hoje.AddDays(-2) },
            new LancamentoFinanceiro { Tipo = "Receber", Valor = 300m, Vencimento = Hoje.AddDays(10) },
            // Quitada não entra em nada: já aconteceu.
            new LancamentoFinanceiro { Tipo = "Pagar", Valor = 9999m, Vencimento = Hoje.AddDays(-9), QuitadoEm = Hoje },
        };

        var r = FinanceiroDoClube.Resumir(lancamentos, Hoje);

        Assert.Equal(1000m, r.APagarVencido);
        Assert.Equal(500.50m, r.APagarAVencer);
        Assert.Equal(1500.50m, r.TotalAPagar);
        Assert.Equal(2000m, r.AReceberVencido);
        Assert.Equal(2300m, r.TotalAReceber);
        Assert.Equal(799.50m, r.Previsto);
    }

    [Fact]
    public void Sem_conta_nenhuma_o_resumo_e_zero_e_nao_estoura()
    {
        var r = FinanceiroDoClube.Resumir(Array.Empty<LancamentoFinanceiro>(), Hoje);
        Assert.Equal(0m, r.TotalAPagar);
        Assert.Equal(0m, r.TotalAReceber);
        Assert.Equal(0m, r.Previsto);
    }

    // ---------- De ponta a ponta ----------

    private static ContasController Controller(DbPadelContext ctx, int usuarioId, bool habilitado)
    {
        var c = new ContasController(ctx,
            new ModuloDoBar(ctx, Options.Create(new BarSettings { Habilitado = habilitado })));

        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "Teste")),
            },
        };
        c.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            c.HttpContext, NSubstitute.Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        return c;
    }

    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Dono do Clube", Cpf = "1" });
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Felipe", Cpf = "2", IsAdminRaiz = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Arena Teste", DonoId = 1 });
        ctx.ClubeAdministradores.Add(new ClubeAdministrador { ClubeId = 1, JogadorId = 2, AdicionadoEm = DateTime.Now });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task Em_construcao_nem_o_dono_do_clube_ve_as_contas()
    {
        var ctx = Cenario();
        Assert.IsType<ForbidResult>(await Controller(ctx, 1, habilitado: false).Index(1, null, null, null));
        Assert.IsType<ViewResult>(await Controller(ctx, 2, habilitado: false).Index(1, null, null, null));
    }

    [Fact]
    public async Task Conta_que_repete_nasce_com_todas_as_parcelas_gravadas()
    {
        // Gravadas, e não uma regra "repete todo dia 10": conta que se repete muda — o
        // aluguel reajusta, o contrato acaba — e com as linhas gravadas mexer numa parcela é
        // mexer numa linha.
        var ctx = Cenario();

        await Controller(ctx, 2, habilitado: false)
            .Salvar(1, "Pagar", "Aluguel do galpão", 3500m, new DateTime(2026, 8, 5),
                    "Imobiliária", "Estrutura", null, repetirMeses: 12);

        var contas = ctx.LancamentosFinanceiros.OrderBy(l => l.Vencimento).ToList();
        Assert.Equal(12, contas.Count);
        Assert.Equal(new DateTime(2026, 8, 5), contas.First().Vencimento);
        Assert.Equal(new DateTime(2027, 7, 5), contas.Last().Vencimento);
        // Todas no mesmo grupo, pra dar pra apagar a série inteira depois.
        Assert.Single(contas.Select(l => l.GrupoRecorrencia).Distinct());
        Assert.All(contas, l => Assert.Equal(3500m, l.Valor));
    }

    [Fact]
    public async Task Quitar_e_um_interruptor()
    {
        var ctx = Cenario();
        var c = Controller(ctx, 2, habilitado: false);
        await c.Salvar(1, "Pagar", "Luz", 890.45m, Hoje, null, null, null);

        var id = ctx.LancamentosFinanceiros.Single().Id;

        await c.AlternarQuitado(id);
        Assert.NotNull(ctx.LancamentosFinanceiros.Single().QuitadoEm);

        // Marcou errado? Desmarca no mesmo botão, sem apagar e relançar.
        await c.AlternarQuitado(id);
        Assert.Null(ctx.LancamentosFinanceiros.Single().QuitadoEm);
    }

    [Fact]
    public async Task Apagar_a_serie_poupa_as_parcelas_ja_pagas()
    {
        // Apagar parcela paga apagaria o registro de um dinheiro que de fato saiu.
        var ctx = Cenario();
        var c = Controller(ctx, 2, habilitado: false);
        await c.Salvar(1, "Pagar", "Aluguel", 3500m, new DateTime(2026, 8, 5), null, null, null, repetirMeses: 6);

        var primeira = ctx.LancamentosFinanceiros.OrderBy(l => l.Vencimento).First();
        await c.AlternarQuitado(primeira.Id);

        var outra = ctx.LancamentosFinanceiros.OrderBy(l => l.Vencimento).Skip(1).First();
        await c.Apagar(outra.Id, apagarSerie: true);

        var restantes = ctx.LancamentosFinanceiros.ToList();
        Assert.Single(restantes);
        Assert.Equal(primeira.Id, restantes[0].Id);
        Assert.NotNull(restantes[0].QuitadoEm);
    }

    [Fact]
    public async Task Valor_com_centavos_atravessa_inteiro()
    {
        var ctx = Cenario();
        await Controller(ctx, 2, habilitado: false)
            .Salvar(1, "Pagar", "Cerveja — pedido de junho", 1289.90m, Hoje, "Ambev", "Bebidas", null);

        Assert.Equal(1289.90m, ctx.LancamentosFinanceiros.Single().Valor);
    }
}
