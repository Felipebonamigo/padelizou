using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Montar a grade do clube de uma vez (pedido do Felipe, 30/07): período de funcionamento +
// dias + o botão da duração (30min/1h/1h30) geram uma regra por quadra × dia. Regra por
// regra eram 21 formulários iguais num clube de 3 quadras — onde o dono desiste no meio.
public class GradeDoClubeTests
{
    private static TimeSpan H(int hora) => new(hora, 0, 0);

    private static HorarioMarcacaoDisponivel Regra(TimeSpan inicio, TimeSpan fim, int duracao, bool ativo = true) =>
        new() { HoraInicio = inicio, HoraFim = fim, DuracaoMinutos = duracao, Ativo = ativo };

    // ---------- A decisão por slot ----------

    [Fact]
    public void Sem_nada_no_dia_cria()
    {
        var (decisao, _) = HorarioDoClube.DecidirSlot(Array.Empty<HorarioMarcacaoDisponivel>(), H(7), H(23), 60);

        Assert.Equal(HorarioDoClube.DecisaoDaGrade.Criar, decisao);
    }

    [Fact]
    public void Regra_identica_ativa_e_pulada_e_pausada_e_religada()
    {
        var ativa = new[] { Regra(H(7), H(23), 60) };
        var pausada = new[] { Regra(H(7), H(23), 60, ativo: false) };

        Assert.Equal(HorarioDoClube.DecisaoDaGrade.Pular,
            HorarioDoClube.DecidirSlot(ativa, H(7), H(23), 60).Decisao);

        // Recadastrar a grade depois das férias faz o que a pessoa quis, sem duplicar —
        // a mesma regra dos horários do professor.
        var (decisao, alvo) = HorarioDoClube.DecidirSlot(pausada, H(7), H(23), 60);
        Assert.Equal(HorarioDoClube.DecisaoDaGrade.Religar, decisao);
        Assert.Same(pausada[0], alvo);
    }

    [Fact]
    public void Sobreposicao_com_ativa_e_pulada_para_nao_vender_o_mesmo_horario_duas_vezes()
    {
        var existente = new[] { Regra(H(18), H(21), 60) };

        Assert.Equal(HorarioDoClube.DecisaoDaGrade.Pular,
            HorarioDoClube.DecidirSlot(existente, H(7), H(23), 90).Decisao);
        // Janelas que só se encostam (7–18 contra 18–21) não se sobrepõem: cria.
        Assert.Equal(HorarioDoClube.DecisaoDaGrade.Criar,
            HorarioDoClube.DecidirSlot(existente, H(7), H(18), 60).Decisao);
    }

    [Fact]
    public void Sobreposicao_so_com_pausada_nao_impede()
    {
        var pausada = new[] { Regra(H(18), H(21), 60, ativo: false) };

        Assert.Equal(HorarioDoClube.DecisaoDaGrade.Criar,
            HorarioDoClube.DecidirSlot(pausada, H(7), H(23), 90).Decisao);
    }

    // ---------- O gerador de ponta a ponta ----------

    private static HorarioMarcacaoController Controller(DbPadelContext ctx, int usuarioId)
    {
        var c = new HorarioMarcacaoController(ctx, Substitute.For<IHorarioMarcacaoService>());
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "Teste")),
            },
        };
        c.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            c.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        return c;
    }

    private static DbPadelContext CenarioComDuasQuadras()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Dono", Cpf = "1" });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Arena", DonoId = 1 });
        ctx.QuadrasClube.AddRange(
            new QuadraClube { Id = 1, ClubeId = 1, Nome = "Quadra 1", Ativa = true },
            new QuadraClube { Id = 2, ClubeId = 1, Nome = "Quadra 2", Ativa = true });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task Todas_as_quadras_e_todos_os_dias_num_clique()
    {
        using var ctx = CenarioComDuasQuadras();

        await Controller(ctx, 1).GerarGrade(1, null, new[] { 0, 1, 2, 3, 4, 5, 6 },
            H(7), H(23), 60, 80m);

        // 2 quadras × 7 dias = 14 regras, todas iguais.
        Assert.Equal(14, ctx.HorariosMarcacaoDisponivel.Count());
        Assert.All(ctx.HorariosMarcacaoDisponivel, h =>
        {
            Assert.Equal(60, h.DuracaoMinutos);
            Assert.Equal(80m, h.Preco);
            Assert.True(h.Ativo);
        });
    }

    [Fact]
    public async Task So_uma_quadra_e_so_os_dias_marcados()
    {
        using var ctx = CenarioComDuasQuadras();

        await Controller(ctx, 1).GerarGrade(1, 2, new[] { 1, 3, 5 }, H(18), H(22), 90, null);

        Assert.Equal(3, ctx.HorariosMarcacaoDisponivel.Count());
        Assert.All(ctx.HorariosMarcacaoDisponivel, h => Assert.Equal(2, h.QuadraClubeId));
        Assert.Equal(new[] { 1, 3, 5 }, ctx.HorariosMarcacaoDisponivel.Select(h => h.DiaSemana).OrderBy(d => d));
    }

    [Fact]
    public async Task Rodar_de_novo_nao_duplica_nada()
    {
        using var ctx = CenarioComDuasQuadras();
        var dias = new[] { 0, 1, 2, 3, 4, 5, 6 };

        await Controller(ctx, 1).GerarGrade(1, null, dias, H(7), H(23), 60, 80m);
        await Controller(ctx, 1).GerarGrade(1, null, dias, H(7), H(23), 60, 80m);

        Assert.Equal(14, ctx.HorariosMarcacaoDisponivel.Count());
    }

    [Fact]
    public async Task O_que_ja_existe_na_faixa_e_preservado_e_o_resto_e_criado()
    {
        using var ctx = CenarioComDuasQuadras();
        // Segunda à noite na quadra 1 já tem regra própria, mais cara.
        ctx.HorariosMarcacaoDisponivel.Add(new HorarioMarcacaoDisponivel
        {
            ClubeId = 1, QuadraClubeId = 1, DiaSemana = 1,
            HoraInicio = H(18), HoraFim = H(23), DuracaoMinutos = 60, Preco = 120m, Ativo = true,
        });
        ctx.SaveChanges();

        await Controller(ctx, 1).GerarGrade(1, null, new[] { 0, 1, 2, 3, 4, 5, 6 },
            H(7), H(23), 60, 80m);

        // 14 slots do gerador, mas o (quadra 1, segunda) foi pulado: 13 novos + o antigo.
        Assert.Equal(14, ctx.HorariosMarcacaoDisponivel.Count());
        var daSegundaQ1 = ctx.HorariosMarcacaoDisponivel.Single(h => h.QuadraClubeId == 1 && h.DiaSemana == 1);
        Assert.Equal(120m, daSegundaQ1.Preco);   // o preço nobre não foi atropelado
    }

    [Fact]
    public async Task Sem_dia_marcado_ou_com_janela_que_nao_comporta_recusa_sem_criar()
    {
        using var ctx = CenarioComDuasQuadras();
        var c = Controller(ctx, 1);

        await c.GerarGrade(1, null, Array.Empty<int>(), H(7), H(23), 60, null);
        await c.GerarGrade(1, null, new[] { 1 }, H(19), H(20), 90, null);   // 1h30 não cabe
        await c.GerarGrade(1, null, new[] { 1 }, H(23), H(7), 60, null);    // fim antes do início

        Assert.Empty(ctx.HorariosMarcacaoDisponivel);
    }

    [Fact]
    public async Task Quem_nao_gerencia_o_clube_nao_gera_grade()
    {
        using var ctx = CenarioComDuasQuadras();
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Intruso", Cpf = "9" });
        ctx.SaveChanges();

        var resultado = await Controller(ctx, 9).GerarGrade(1, null, new[] { 1 }, H(7), H(23), 60, null);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Empty(ctx.HorariosMarcacaoDisponivel);
    }

    // ---------- Excluir todos ----------

    [Fact]
    public async Task Excluir_todos_limpa_a_grade_pra_recomecar()
    {
        using var ctx = CenarioComDuasQuadras();
        await Controller(ctx, 1).GerarGrade(1, null, new[] { 0, 1, 2 }, H(7), H(23), 30, null);
        Assert.Equal(6, ctx.HorariosMarcacaoDisponivel.Count());

        // Montou de 30 min mas queria 1h: apaga tudo e gera de novo.
        await Controller(ctx, 1).ExcluirTodos(1, null);
        Assert.Empty(ctx.HorariosMarcacaoDisponivel);

        await Controller(ctx, 1).GerarGrade(1, null, new[] { 0, 1, 2 }, H(7), H(23), 60, 80m);
        Assert.Equal(6, ctx.HorariosMarcacaoDisponivel.Count());
        Assert.All(ctx.HorariosMarcacaoDisponivel, h => Assert.Equal(60, h.DuracaoMinutos));
    }

    [Fact]
    public async Task Excluir_de_uma_quadra_nao_toca_na_outra()
    {
        using var ctx = CenarioComDuasQuadras();
        await Controller(ctx, 1).GerarGrade(1, null, new[] { 1, 2 }, H(7), H(23), 60, null);

        await Controller(ctx, 1).ExcluirTodos(1, quadraClubeId: 1);

        Assert.Equal(2, ctx.HorariosMarcacaoDisponivel.Count());
        Assert.All(ctx.HorariosMarcacaoDisponivel, h => Assert.Equal(2, h.QuadraClubeId));
    }

    [Fact]
    public async Task Excluir_exige_gerenciar_o_clube()
    {
        using var ctx = CenarioComDuasQuadras();
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Intruso", Cpf = "9" });
        ctx.SaveChanges();
        await Controller(ctx, 1).GerarGrade(1, null, new[] { 1 }, H(7), H(23), 60, null);

        Assert.IsType<ForbidResult>(await Controller(ctx, 9).ExcluirTodos(1, null));
        Assert.Equal(2, ctx.HorariosMarcacaoDisponivel.Count());
    }
}
