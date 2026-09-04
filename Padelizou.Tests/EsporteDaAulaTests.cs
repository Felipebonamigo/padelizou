using Microsoft.EntityFrameworkCore;
using padelizou.Controllers;   // AulasController ficou no namespace legado, em minúsculo
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// Pedido do Felipe (23/08/2026, sobre o João dar tênis e beach tênis também): a AULA sabe o
// esporte que é, nasce em Padel (era o único que existia até aqui), e a Minha Agenda ganha um
// filtro que só aparece pra quem já lançou mais de um.
public class EsporteDaAulaTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000001", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Batata Padel", PrecoPadrao = 110, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    // ---- A tabela de valores válidos ----

    [Fact]
    public void O_padrao_e_padel()
    {
        Assert.Equal(EsporteDaAula.Padel, EsporteDaAula.Padrao);
    }

    [Fact]
    public void Todos_lista_os_tres()
    {
        Assert.Equal(
            new[] { EsporteDaAula.Padel, EsporteDaAula.Tenis, EsporteDaAula.BeachTenis },
            EsporteDaAula.Todos);
    }

    // ---- Lançar aula (AdicionarManual) ----

    [Fact]
    public async Task Aula_lancada_sem_esporte_nasce_em_padel()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await TestInfra.NovoAulasController(ctx, professor.Id).AdicionarManual(
            localId: local.Id, nomeAluno: "Medina", telefoneAluno: null,
            data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(2).AddHours(7)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(2).AddHours(7)), preco: null,
            recorrente: false, semanasRecorrencia: 0);

        Assert.Equal(EsporteDaAula.Padel, (await ctx.Aulas.SingleAsync()).Esporte);
    }

    [Fact]
    public async Task Aula_lancada_com_esporte_escolhido_guarda_o_esporte()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await TestInfra.NovoAulasController(ctx, professor.Id).AdicionarManual(
            localId: local.Id, nomeAluno: "Medina", telefoneAluno: null,
            data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(2).AddHours(7)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(2).AddHours(7)), preco: null,
            recorrente: false, semanasRecorrencia: 0, esporte: EsporteDaAula.Tenis);

        Assert.Equal(EsporteDaAula.Tenis, (await ctx.Aulas.SingleAsync()).Esporte);
    }

    [Fact]
    public async Task Esporte_fora_da_lista_cai_no_padrao()
    {
        // Formulário adulterado (ou aba velha mandando lixo) não pode gravar um esporte que
        // a tela nunca ofereceu.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await TestInfra.NovoAulasController(ctx, professor.Id).AdicionarManual(
            localId: local.Id, nomeAluno: "Medina", telefoneAluno: null,
            data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(2).AddHours(7)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(2).AddHours(7)), preco: null,
            recorrente: false, semanasRecorrencia: 0, esporte: "Vôlei");

        Assert.Equal(EsporteDaAula.Padel, (await ctx.Aulas.SingleAsync()).Esporte);
    }

    // ---- Editar ----

    private static Aula Marcada(DbPadelContext ctx, Jogador professor, LocalAula local, DateTime quando,
        string esporte = EsporteDaAula.Padel, Guid? turmaId = null)
    {
        var aula = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id,
            DataHora = quando, Preco = 110, Status = PoliticaAula.Confirmada,
            NomeAlunoAvulso = "Medina", Esporte = esporte, TurmaId = turmaId,
        };
        ctx.Aulas.Add(aula);
        ctx.SaveChanges();
        return aula;
    }

    [Fact]
    public async Task Editar_muda_so_o_esporte_e_salva()
    {
        // Regressão: MudancaDaAula só conhece horário/local/preço/duração. Sem entrar na
        // conta de "mudou algo", trocar só o esporte caía no early-return de "nada mudou" e a
        // troca nunca ia pro banco.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, local, DateTime.Today.AddDays(4).AddHours(8));

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(aula.DataHora), hora: DataEHoraDoFormulario.ParaCampoDeHora(aula.DataHora), aula.Preco, esporte: EsporteDaAula.BeachTenis);

        Assert.Equal(EsporteDaAula.BeachTenis, (await ctx.Aulas.SingleAsync()).Esporte);
    }

    [Fact]
    public async Task Editar_sem_mudar_nada_nem_esporte_nao_mexe_no_banco()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, local, DateTime.Today.AddDays(4).AddHours(8));

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(aula.DataHora), hora: DataEHoraDoFormulario.ParaCampoDeHora(aula.DataHora), aula.Preco, esporte: EsporteDaAula.Padel);

        Assert.Equal("Nada mudou nessa aula.", controller.TempData["Sucesso"]);
        Assert.Equal(EsporteDaAula.Padel, (await ctx.Aulas.SingleAsync()).Esporte);
    }

    [Fact]
    public async Task Editar_o_esporte_de_uma_turma_muda_a_turma_inteira()
    {
        // Os colegas de turma jogam junto, na mesma quadra: não faz sentido um estar de
        // padel e o outro de beach tênis na mesma sessão.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var turmaId = Guid.NewGuid();
        var quando = DateTime.Today.AddDays(4).AddHours(8);
        var titular = Marcada(ctx, professor, local, quando, turmaId: turmaId);
        var colega = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id, DataHora = quando, Preco = 75,
            Status = PoliticaAula.Confirmada, NomeAlunoAvulso = "Ana", TurmaId = turmaId,
            Esporte = EsporteDaAula.Padel,
        };
        ctx.Aulas.Add(colega);
        ctx.SaveChanges();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(titular.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(quando), hora: DataEHoraDoFormulario.ParaCampoDeHora(quando), titular.Preco, esporte: EsporteDaAula.Tenis);

        var todas = await ctx.Aulas.ToListAsync();
        Assert.All(todas, a => Assert.Equal(EsporteDaAula.Tenis, a.Esporte));
    }

    [Fact]
    public async Task Editar_com_esporte_invalido_mantem_o_que_a_aula_ja_tinha()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, local, DateTime.Today.AddDays(4).AddHours(8), esporte: EsporteDaAula.Tenis);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(aula.DataHora), hora: DataEHoraDoFormulario.ParaCampoDeHora(aula.DataHora), aula.Preco, esporte: "Vôlei");

        Assert.Equal(EsporteDaAula.Tenis, (await ctx.Aulas.SingleAsync()).Esporte);
    }

    // ---- Filtro na Minha Agenda ----

    // ⚠️ AS DATAS DA AGENDA SAEM DA JANELA DE VERDADE, e não de `DateTime.Today.AddDays(n)`.
    //
    // Foi assim que estes quatro testes nasceram (26/08/2026) — e eles quebraram sozinhos nove
    // dias depois, sem uma linha de produção ter mudado. A semana da agenda vai de DOMINGO a
    // sábado (`PeriodoAgenda.Janela`): escritos numa QUARTA, `Today.AddDays(2)` caía dentro da
    // janela; rodando numa SEXTA, o mesmo `+2` cai no domingo SEGUINTE, que já é a outra semana.
    // O resultado dependia do dia em que a suíte rodava.
    //
    // Ancorar no início da janela é o que torna o teste determinístico: `DentroDaSemana(1)` é
    // sempre o segundo dia da semana que a tela está mostrando, seja lá quando a suíte rodar.
    private static DateTime DentroDaSemana(int diaDaJanela)
    {
        var (inicio, _) = PeriodoAgenda.Janela("semana", DateTime.Today);
        return inicio.AddDays(diaDaJanela).AddHours(8);
    }

    // A prova de que a âncora acima resolve de verdade, e não só "passa hoje": rodando com
    // CADA um dos sete dias possíveis como referência, os dias 1 e 2 da janela caem dentro
    // dela — que é o que o `Today.AddDays(n)` antigo não garantia.
    //
    // Sem este teste, a correção seria indistinguível de ter esperado o calendário virar.
    [Theory]
    [InlineData("2026-08-30")] // domingo — primeiro dia da janela
    [InlineData("2026-08-31")] // segunda
    [InlineData("2026-09-01")] // terça
    [InlineData("2026-09-02")] // quarta — o dia em que estes testes nasceram passando
    [InlineData("2026-09-03")] // quinta
    [InlineData("2026-09-04")] // sexta — o dia em que eles quebraram sozinhos
    [InlineData("2026-09-05")] // sábado — último dia da janela
    public void A_ancora_da_semana_cai_dentro_da_janela_em_qualquer_dia(string hoje)
    {
        var referencia = DateTime.Parse(hoje);
        var (inicio, fim) = PeriodoAgenda.Janela("semana", referencia);

        foreach (var dia in new[] { 1, 2 })
        {
            var quando = inicio.AddDays(dia).AddHours(8);
            Assert.True(quando >= inicio && quando < fim,
                $"Com hoje={hoje}, o dia {dia} da janela ({quando:dd/MM}) caiu fora de "
                + $"[{inicio:dd/MM}, {fim:dd/MM}) — a âncora voltou a depender do calendário.");
        }
    }

    [Fact]
    public async Task Professor_de_um_esporte_so_nao_ve_opcao_de_filtro()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Marcada(ctx, professor, local, DentroDaSemana(1));
        Marcada(ctx, professor, local, DentroDaSemana(2));

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id).MinhaAgenda(null, "semana", DateTime.Today);
        var vm = Assert.IsType<AgendaProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        Assert.Single(vm.EsportesDoProfessor);
    }

    [Fact]
    public async Task Professor_de_dois_esportes_ve_os_dois_na_lista_de_filtro()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Marcada(ctx, professor, local, DentroDaSemana(1), esporte: EsporteDaAula.Padel);
        Marcada(ctx, professor, local, DentroDaSemana(2), esporte: EsporteDaAula.Tenis);

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id).MinhaAgenda(null, "semana", DateTime.Today);
        var vm = Assert.IsType<AgendaProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        Assert.Equal(2, vm.EsportesDoProfessor.Count);
        Assert.Contains(EsporteDaAula.Padel, vm.EsportesDoProfessor);
        Assert.Contains(EsporteDaAula.Tenis, vm.EsportesDoProfessor);
    }

    [Fact]
    public async Task Filtrar_por_esporte_so_traz_as_aulas_daquele_esporte()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Marcada(ctx, professor, local, DentroDaSemana(1), esporte: EsporteDaAula.Padel);
        Marcada(ctx, professor, local, DentroDaSemana(2), esporte: EsporteDaAula.Tenis);

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id)
            .MinhaAgenda(null, "semana", DateTime.Today, esporte: EsporteDaAula.Tenis);
        var vm = Assert.IsType<AgendaProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        var noPeriodo = Assert.Single(vm.NoPeriodo);
        Assert.Equal(EsporteDaAula.Tenis, noPeriodo.Esporte);
        Assert.Equal(EsporteDaAula.Tenis, vm.EsporteFiltro);
    }

    [Fact]
    public async Task Filtro_invalido_na_url_e_ignorado()
    {
        // "esporte=Vôlei" na URL não pode fazer a lista sumir por não bater com nada — cai
        // pra "todos", que é o comportamento sem filtro nenhum.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Marcada(ctx, professor, local, DentroDaSemana(1), esporte: EsporteDaAula.Padel);
        Marcada(ctx, professor, local, DentroDaSemana(2), esporte: EsporteDaAula.Tenis);

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id)
            .MinhaAgenda(null, "semana", DateTime.Today, esporte: "Vôlei");
        var vm = Assert.IsType<AgendaProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        Assert.Equal(2, vm.NoPeriodo.Count);
        Assert.Null(vm.EsporteFiltro);
    }
}
