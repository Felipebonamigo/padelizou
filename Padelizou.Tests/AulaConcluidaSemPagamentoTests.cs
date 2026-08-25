using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// 🗣️ Pedido do Felipe, 25/08/2026: "permita o professor colocar como aula concluída mas ainda
// não paga, tem alunos que pagam depois ou por mês".
//
// Até aqui "Concluir" gravava, na mesma tacada, "a aula aconteceu" E "o dinheiro entrou" — o
// Financeiro somava toda aula Realizada como recebida, e a lista de devedores só conhecia
// falta cobrável. Aula dada e não paga era INVISÍVEL: somada em "Recebido", fora do "a
// cobrar", fora de "quem está devendo".
public class AulaConcluidaSemPagamentoTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000021", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Batata Padel", PrecoPadrao = 110, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Aula Linha(Jogador professor, LocalAula local, DateTime quando,
        string nome = "Medina", decimal preco = 110, Guid? turma = null) => new()
    {
        ProfessorId = professor.Id,
        LocalAulaId = local.Id,
        DataHora = quando,
        DuracaoMinutos = 60,
        Preco = preco,
        Status = PoliticaAula.Confirmada,
        QuantidadeAlunos = turma == null ? 1 : 3,
        TurmaId = turma,
        NomeAlunoAvulso = nome,
    };

    // ─── A folha da agenda: dois botões, e eles gravam coisas diferentes ───────────────

    [Fact]
    public async Task Concluir_e_recebi_grava_a_data_do_recebimento()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Linha(professor, local, DateTime.Today.AddDays(-1).AddHours(9));
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .AtualizarStatus(aula.Id, PoliticaAula.Realizada, recebido: true);

        var salva = await ctx.Aulas.FindAsync(aula.Id);
        Assert.Equal(PoliticaAula.Realizada, salva!.Status);
        Assert.NotNull(salva.PagaEm);
    }

    [Fact]
    public async Task Concluir_receber_depois_conclui_sem_gravar_recebimento()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Linha(professor, local, DateTime.Today.AddDays(-1).AddHours(9));
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .AtualizarStatus(aula.Id, PoliticaAula.Realizada, recebido: false);

        var salva = await ctx.Aulas.FindAsync(aula.Id);
        Assert.Equal(PoliticaAula.Realizada, salva!.Status);
        Assert.Null(salva.PagaEm);
    }

    // ⚠️ Cancelar não pode carimbar recebimento nem por acidente — o parâmetro é o mesmo.
    [Fact]
    public async Task Cancelar_nunca_grava_recebimento()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Linha(professor, local, DateTime.Today.AddDays(-1).AddHours(9));
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .AtualizarStatus(aula.Id, PoliticaAula.Cancelada, recebido: true);

        var salva = await ctx.Aulas.FindAsync(aula.Id);
        Assert.Equal(PoliticaAula.Cancelada, salva!.Status);
        Assert.Null(salva.PagaEm);
    }

    [Fact]
    public async Task Concluir_e_recebi_na_turma_vale_pras_tres_linhas()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var turma = Guid.NewGuid();
        var quando = DateTime.Today.AddDays(-1).AddHours(9);
        ctx.Aulas.AddRange(
            Linha(professor, local, quando, "Medina", 60, turma),
            Linha(professor, local, quando, "Coello", 60, turma),
            Linha(professor, local, quando, "Lima", 60, turma));
        await ctx.SaveChangesAsync();

        var uma = await ctx.Aulas.FirstAsync(a => a.TurmaId == turma);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .AtualizarStatus(uma.Id, PoliticaAula.Realizada, recebido: true);

        var todas = await ctx.Aulas.Where(a => a.TurmaId == turma).ToListAsync();
        Assert.Equal(3, todas.Count);
        Assert.All(todas, a => Assert.NotNull(a.PagaEm));
    }

    // ─── Registrar o Pix que chegou depois ────────────────────────────────────────────

    [Fact]
    public async Task Marcar_recebida_depois_carimba_a_aula_ja_concluida()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Linha(professor, local, DateTime.Today.AddDays(-3).AddHours(9));
        aula.Status = PoliticaAula.Realizada;
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarRecebida(aula.Id, true);

        var salva = await ctx.Aulas.FindAsync(aula.Id);
        Assert.NotNull(salva!.PagaEm);
    }

    [Fact]
    public async Task Desmarcar_devolve_a_aula_pro_a_receber()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Linha(professor, local, DateTime.Today.AddDays(-3).AddHours(9));
        aula.Status = PoliticaAula.Realizada;
        aula.PagaEm = DateTime.Now;
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarRecebida(aula.Id, false);

        var salva = await ctx.Aulas.FindAsync(aula.Id);
        Assert.Null(salva!.PagaEm);
    }

    // ⚠️ Regra 0: o ProfessorId no filtro É a autorização. Sem ele, qualquer professor logado
    // dá baixa na aula de qualquer outro só mandando o id.
    [Fact]
    public async Task Professor_de_fora_nao_da_baixa_na_aula_alheia()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var intruso = new Jogador { Nome = "Outro", Login = "outro", Cpf = "99900000022", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var aula = Linha(professor, local, DateTime.Today.AddDays(-3).AddHours(9));
        aula.Status = PoliticaAula.Realizada;
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, intruso.Id).MarcarRecebida(aula.Id, true);

        var salva = await ctx.Aulas.FindAsync(aula.Id);
        Assert.Null(salva!.PagaEm);
    }

    [Fact]
    public async Task Reposicao_nao_pode_ser_marcada_como_recebida()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var original = Linha(professor, local, DateTime.Today.AddDays(-10).AddHours(9));
        original.Status = PoliticaAula.ARecuperar;
        original.CobrarMesmoFaltando = true;
        ctx.Aulas.Add(original);
        await ctx.SaveChangesAsync();

        var reposicao = Linha(professor, local, DateTime.Today.AddDays(-2).AddHours(9), preco: 0);
        reposicao.Status = PoliticaAula.Realizada;
        reposicao.RecuperaAulaId = original.Id;
        ctx.Aulas.Add(reposicao);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarRecebida(reposicao.Id, true);

        var salva = await ctx.Aulas.FindAsync(reposicao.Id);
        Assert.Null(salva!.PagaEm);
    }

    // ─── O Financeiro deixa de contar como recebido o que não entrou ──────────────────

    [Fact]
    public async Task Recebido_conta_so_a_aula_paga_e_a_nao_paga_vira_a_cobrar()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var hoje = DateTime.Today;
        var paga = Linha(professor, local, hoje.AddHours(9), "Medina");
        paga.Status = PoliticaAula.Realizada;
        paga.PagaEm = DateTime.Now;

        var naoPaga = Linha(professor, local, hoje.AddHours(11), "Coello");
        naoPaga.Status = PoliticaAula.Realizada;

        ctx.Aulas.AddRange(paga, naoPaga);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoAulasController(ctx, professor.Id).Financeiro("mes");
        var vm = Assert.IsType<FinanceiroProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado).Model);

        Assert.Equal(110m, vm.Recebido);
        Assert.Equal(110m, vm.AReceber);
        // As duas continuam sendo aula dada: o CONTADOR de aulas não muda com o pagamento.
        Assert.Equal(2, vm.AulasRealizadas);
    }

    [Fact]
    public async Task Quem_esta_devendo_passa_a_incluir_a_aula_dada_e_nao_paga()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Linha(professor, local, DateTime.Today.AddHours(9), "Coello");
        aula.Status = PoliticaAula.Realizada;
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoAulasController(ctx, professor.Id).Financeiro("mes");
        var vm = Assert.IsType<FinanceiroProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado).Model);

        var devedor = Assert.Single(vm.Devedores);
        Assert.Equal("Coello", devedor.Nome);
        Assert.Equal(110m, devedor.Valor);
        Assert.Equal(1, devedor.AulasEmAberto);
    }

    // ⚠️ Contraprova: sem ela, um Financeiro que jogasse TUDO em "a cobrar" passaria nos dois
    // testes acima sem provar nada.
    [Fact]
    public async Task Aula_paga_nao_aparece_na_lista_de_devedores()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Linha(professor, local, DateTime.Today.AddHours(9), "Coello");
        aula.Status = PoliticaAula.Realizada;
        aula.PagaEm = DateTime.Now;
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoAulasController(ctx, professor.Id).Financeiro("mes");
        var vm = Assert.IsType<FinanceiroProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado).Model);

        Assert.Empty(vm.Devedores);
        Assert.Equal(0m, vm.AReceber);
    }

    // O gráfico de tendência é FATURAMENTO por competência, não caixa: um Pix atrasado não
    // pode mudar a forma do mês que já passou.
    [Fact]
    public async Task O_grafico_dos_ultimos_meses_continua_contando_aula_dada_e_nao_pagamento()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Linha(professor, local, DateTime.Today.AddHours(9));
        aula.Status = PoliticaAula.Realizada;   // dada e NÃO paga
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoAulasController(ctx, professor.Id).Financeiro("mes");
        var vm = Assert.IsType<FinanceiroProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado).Model);

        var mesCorrente = vm.UltimosMeses.Last();
        Assert.Equal(110m, mesCorrente.Valor);
    }

    // ─── "Recebido" quer dizer a MESMA coisa nas quatro telas ─────────────────────────

    // Duas telas com dois números pro mesmo mês é como o professor conclui que o sistema
    // perdeu dinheiro dele. O Relatório, o Painel do Professor e a tabela por local diziam
    // "recebido" somando aula DADA — o mesmo defeito do Financeiro, em outras portas.
    [Fact]
    public async Task O_relatorio_conta_como_recebido_so_o_que_entrou()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var paga = Linha(professor, local, DateTime.Today.AddHours(9), "Medina");
        paga.Status = PoliticaAula.Realizada;
        paga.PagaEm = DateTime.Now;

        var naoPaga = Linha(professor, local, DateTime.Today.AddHours(11), "Coello");
        naoPaga.Status = PoliticaAula.Realizada;

        ctx.Aulas.AddRange(paga, naoPaga);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoAulasController(ctx, professor.Id)
            .Relatorio(DateTime.Today, DateTime.Today);
        var vm = Assert.IsType<RelatorioAulasViewModel>(
            Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado).Model);

        Assert.Equal(110m, vm.TotalRecebido);
        // O CONTADOR de aulas dadas não muda: as duas aconteceram.
        Assert.Equal(2, vm.TotalAulas);
        Assert.Equal(110m, Assert.Single(vm.PorLocal).Recebido);
        Assert.Equal(2, Assert.Single(vm.PorLocal).QuantidadeAulas);
    }

    [Fact]
    public async Task A_tabela_por_local_do_financeiro_tambem_conta_so_o_que_entrou()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var paga = Linha(professor, local, DateTime.Today.AddHours(9), "Medina");
        paga.Status = PoliticaAula.Realizada;
        paga.PagaEm = DateTime.Now;

        var naoPaga = Linha(professor, local, DateTime.Today.AddHours(11), "Coello");
        naoPaga.Status = PoliticaAula.Realizada;

        ctx.Aulas.AddRange(paga, naoPaga);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoAulasController(ctx, professor.Id).Financeiro("mes");
        var vm = Assert.IsType<FinanceiroProfessorVM>(
            Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado).Model);

        var porLocal = Assert.Single(vm.PorLocal);
        Assert.Equal(110m, porLocal.Recebido);
        Assert.Equal(2, porLocal.Aulas);
        // ⚠️ E o total da tabela tem que bater com o card do topo, senão a tela se contradiz
        // sozinha: dois números "recebido" na mesma página, diferentes.
        Assert.Equal(vm.Recebido, vm.PorLocal.Sum(l => l.Recebido));
    }

    // ─── O card da turma na agenda ────────────────────────────────────────────────────

    // ⚠️ O card mostra o preço SOMADO da sessão. Dizer "paga" porque a representante está paga
    // afirmaria que a soma inteira entrou — com dois de três tendo pago.
    [Fact]
    public void O_card_da_turma_so_sai_pago_quando_todos_pagaram()
    {
        var turma = Guid.NewGuid();
        var agora = new DateTime(2026, 9, 1, 20, 0, 0);

        var todosPagos = AgendaDeTurma.Colapsar(new[]
        {
            LinhaDeTurma(1, turma, "Medina", agora),
            LinhaDeTurma(2, turma, "Coello", agora),
        });
        Assert.NotNull(Assert.Single(todosPagos).PagaEm);

        var umFaltando = AgendaDeTurma.Colapsar(new[]
        {
            LinhaDeTurma(1, turma, "Medina", agora),
            LinhaDeTurma(2, turma, "Coello", null),
        });
        Assert.Null(Assert.Single(umFaltando).PagaEm);
    }

    [Fact]
    public void Aula_sozinha_leva_o_proprio_recebimento_pro_card()
    {
        var quando = new DateTime(2026, 9, 1, 20, 0, 0);
        var colapsadas = AgendaDeTurma.Colapsar(new[] { LinhaDeTurma(1, null, "Medina", quando) });

        Assert.Equal(quando, Assert.Single(colapsadas).PagaEm);
    }

    private static Aula LinhaDeTurma(int id, Guid? turma, string nome, DateTime? pagaEm) => new()
    {
        Id = id,
        ProfessorId = 1,
        LocalAulaId = 1,
        LocalAula = new LocalAula { Nome = "Batata Padel" },
        DataHora = new DateTime(2026, 9, 1, 9, 0, 0),
        DuracaoMinutos = 90,
        Preco = 60,
        Status = PoliticaAula.Realizada,
        QuantidadeAlunos = turma == null ? 1 : 2,
        TurmaId = turma,
        NomeAlunoAvulso = nome,
        PagaEm = pagaEm,
    };
}
