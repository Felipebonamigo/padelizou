using Microsoft.AspNetCore.Mvc;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// 🗣️ Pedido do Felipe, 01/09/2026, num print do card "Últimas 6 semanas" do Financeiro:
// *"aonde diz (Ultimas 6 semanas), permita tambem escolher ali o mês, separando as semanas,
// como padrão vem o mês atual"*.
//
// O card era uma janela ROLANTE de 6 semanas: ela atravessava a virada do mês (a última barra
// do print era "31/08–06/09"), então nenhuma soma de barras batia com mês nenhum. Agora as
// barras são as semanas DE UM MÊS, e é o professor quem escolhe qual.
//
// ⚠️ A semana continua sendo de segunda a domingo — ela só é RECORTADA no mês. É isso que faz
// a soma das barras ser exatamente o faturamento daquele mês, que é o número que o card
// "Últimos 6 meses", logo abaixo na mesma tela, mostra pra ele.
public class SemanasDoMesNoFinanceiroTests
{
    // ─── A régua pura: como o mês é fatiado ───────────────────────────────────────────

    // Agosto/2026 é o mês que prova as duas pontas: começa num SÁBADO (a primeira semana
    // entra pela metade) e termina numa SEGUNDA (a última fatia tem um dia só).
    [Fact]
    public void A_primeira_e_a_ultima_fatia_sao_recortadas_no_mes()
    {
        var fatias = SemanasDoMes.Fatiar(new DateTime(2026, 8, 1));

        Assert.Equal(6, fatias.Count);
        Assert.Equal(new DateTime(2026, 8, 1), fatias[0].Inicio);   // sábado
        Assert.Equal(new DateTime(2026, 8, 2), fatias[0].Fim);      // domingo
        Assert.Equal(new DateTime(2026, 8, 31), fatias[5].Inicio);  // segunda
        Assert.Equal(new DateTime(2026, 8, 31), fatias[5].Fim);     // e acaba o mês
    }

    // Junho/2026 começa numa segunda: aí a primeira fatia é uma semana inteira, sem recorte.
    [Fact]
    public void Mes_que_comeca_na_segunda_nao_recorta_a_primeira_fatia()
    {
        var fatias = SemanasDoMes.Fatiar(new DateTime(2026, 6, 1));

        Assert.Equal(new DateTime(2026, 6, 1), fatias[0].Inicio);
        Assert.Equal(new DateTime(2026, 6, 7), fatias[0].Fim);
    }

    [Fact]
    public void As_fatias_do_meio_vao_de_segunda_a_domingo()
    {
        var fatias = SemanasDoMes.Fatiar(new DateTime(2026, 8, 1));

        foreach (var fatia in fatias.Skip(1).SkipLast(1))
        {
            Assert.Equal(DayOfWeek.Monday, fatia.Inicio.DayOfWeek);
            Assert.Equal(DayOfWeek.Sunday, fatia.Fim.DayOfWeek);
        }
    }

    // ⚠️ A invariante que faz a soma das barras ser o mês: as fatias cobrem o mês INTEIRO,
    // sem buraco e sem sobreposição. Um dia fora de todas as fatias é dinheiro que some da
    // tela; um dia em duas é dinheiro contado duas vezes.
    [Theory]
    [InlineData(2026, 9)]   // começa terça, acaba quarta
    [InlineData(2026, 8)]   // começa sábado, acaba segunda
    [InlineData(2026, 6)]   // começa segunda
    [InlineData(2026, 2)]   // fevereiro, começa domingo e acaba sábado
    [InlineData(2024, 2)]   // fevereiro bissexto
    public void As_fatias_cobrem_o_mes_inteiro_sem_buraco_e_sem_sobreposicao(int ano, int mes)
    {
        var primeiro = new DateTime(ano, mes, 1);
        var fatias = SemanasDoMes.Fatiar(primeiro);

        var dias = new List<DateTime>();
        foreach (var fatia in fatias)
            for (var d = fatia.Inicio; d <= fatia.Fim; d = d.AddDays(1))
                dias.Add(d);

        Assert.Equal(DateTime.DaysInMonth(ano, mes), dias.Count);
        Assert.Equal(dias.Count, dias.Distinct().Count());
        Assert.Equal(primeiro, dias.First());
        Assert.Equal(primeiro.AddMonths(1).AddDays(-1), dias.Last());
    }

    // ─── A régua pura: qual mês está na tela ──────────────────────────────────────────

    // ⚠️ InvariantCulture na leitura, e não a cultura da thread: "2026-08" lido em pt-BR é a
    // armadilha que já mordeu este projeto em DatasDaAulaFixa.
    [Fact]
    public void O_mes_vem_do_parametro_quando_ele_e_valido()
    {
        Assert.Equal(new DateTime(2026, 8, 1), SemanasDoMes.Escolhido("2026-08", new DateTime(2026, 9, 15)));
    }

    // Parâmetro perdido, vazio ou impossível cai no mês atual — a tela abre certa em vez de
    // dar erro por causa de uma URL editada na mão.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("banana")]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("08/2026")]
    public void Parametro_invalido_cai_no_mes_atual(string? texto)
    {
        Assert.Equal(new DateTime(2026, 9, 1), SemanasDoMes.Escolhido(texto, new DateTime(2026, 9, 15)));
    }

    [Fact]
    public void O_rotulo_do_mes_e_escrito_em_portugues()
    {
        // Nomes à mão (PeriodoAgenda): o servidor não tem cultura pt-BR garantida, e
        // "September" no meio do Financeiro seria descoberto pelo professor, não por nós.
        Assert.Equal("setembro de 2026", SemanasDoMes.Rotulo(new DateTime(2026, 9, 1)));
    }

    // ─── A tela ───────────────────────────────────────────────────────────────────────

    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000051", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Batata Padel", PrecoPadrao = 110, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Aula Realizada(Jogador professor, LocalAula local, DateTime quando, decimal preco = 110m) => new()
    {
        ProfessorId = professor.Id,
        LocalAulaId = local.Id,
        DataHora = quando,
        DuracaoMinutos = 60,
        Preco = preco,
        Status = PoliticaAula.Realizada,
        QuantidadeAlunos = 1,
        NomeAlunoAvulso = "Medina",
        PagaEm = quando,
    };

    private static async Task<FinanceiroProfessorVM> Abrir(DbPadelContext ctx, int professorId, string? semanas = null)
    {
        var resultado = await TestInfra.NovoAulasController(ctx, professorId).Financeiro("mes", semanas);
        return Assert.IsType<FinanceiroProfessorVM>(Assert.IsType<ViewResult>(resultado).Model);
    }

    [Fact]
    public async Task Sem_parametro_o_card_abre_no_mes_atual()
    {
        var (ctx, professor, _) = Montar();
        using var _c = ctx;

        var vm = await Abrir(ctx, professor.Id);

        var hoje = DateTime.Today;
        Assert.Equal(new DateTime(hoje.Year, hoje.Month, 1), vm.MesDasSemanas);
    }

    [Fact]
    public async Task O_mes_escolhido_manda_nas_barras()
    {
        var (ctx, professor, _) = Montar();
        using var _c = ctx;

        var vm = await Abrir(ctx, professor.Id, "2026-08");

        Assert.Equal(new DateTime(2026, 8, 1), vm.MesDasSemanas);
        Assert.Equal(6, vm.Semanas.Count);
        Assert.Equal("01/08–02/08", vm.Semanas[0].Rotulo);
        Assert.Equal("31/08–31/08", vm.Semanas[5].Rotulo);
    }

    // ⚠️ A invariante da tela: as barras somam o mês. É o mesmo número que o card "Últimos 6
    // meses" mostra pra agosto, logo abaixo — dois valores diferentes pro mesmo mês na mesma
    // página é como o professor conclui que o sistema perdeu dinheiro dele.
    [Fact]
    public async Task A_soma_das_barras_e_o_faturamento_do_mes()
    {
        var (ctx, professor, local) = Montar();
        using var _c = ctx;

        ctx.Aulas.AddRange(
            Realizada(professor, local, new DateTime(2026, 8, 1, 9, 0, 0), 100m),    // sábado da ponta
            Realizada(professor, local, new DateTime(2026, 8, 12, 9, 0, 0), 120m),   // meio do mês
            Realizada(professor, local, new DateTime(2026, 8, 31, 9, 0, 0), 130m));  // segunda da outra ponta
        await ctx.SaveChangesAsync();

        var vm = await Abrir(ctx, professor.Id, "2026-08");

        Assert.Equal(350m, vm.Semanas.Sum(s => s.Valor));
        Assert.Equal(100m, vm.Semanas[0].Valor);
        Assert.Equal(130m, vm.Semanas[5].Valor);
    }

    // O recorte é o ponto: 31/08 é segunda, e a semana dela atravessa pro setembro. A aula de
    // 02/09 é da MESMA semana de calendário e não pode entrar na barra de agosto.
    [Fact]
    public async Task Aula_do_mes_vizinho_nao_entra_mesmo_na_semana_que_atravessa_a_virada()
    {
        var (ctx, professor, local) = Montar();
        using var _c = ctx;

        ctx.Aulas.AddRange(
            Realizada(professor, local, new DateTime(2026, 8, 31, 9, 0, 0), 130m),
            Realizada(professor, local, new DateTime(2026, 9, 2, 9, 0, 0), 500m));
        await ctx.SaveChangesAsync();

        var agosto = await Abrir(ctx, professor.Id, "2026-08");
        Assert.Equal(130m, agosto.Semanas.Sum(s => s.Valor));

        // E a de setembro aparece em setembro, na primeira barra dele.
        var setembro = await Abrir(ctx, professor.Id, "2026-09");
        Assert.Equal(500m, setembro.Semanas.Sum(s => s.Valor));
    }

    // Não existe faturamento no futuro: a seta pra frente para no mês atual, senão o professor
    // caminha por meses vazios achando que o sistema apagou as aulas dele.
    [Fact]
    public async Task A_seta_pra_frente_para_no_mes_atual()
    {
        var (ctx, professor, _) = Montar();
        using var _c = ctx;

        Assert.False((await Abrir(ctx, professor.Id)).PodeAvancarSemanas);
        Assert.True((await Abrir(ctx, professor.Id, "2026-08")).PodeAvancarSemanas);
    }

    // ⚠️ A armadilha que a navegação por mês cria, e que não existia na janela rolante: a tela
    // corta TUDO abaixo dos cartões quando o PERÍODO do topo não teve movimento ("Nenhum
    // movimento neste mês"). Com o card ganhando mês próprio, o professor que clica na seta
    // pra ver agosto — que teve movimento — cairia na tela vazia do mês corrente, e sumiria
    // justamente o card que o link dele pediu. Quem tem o que mostrar em QUALQUER um dos dois
    // recortes não vê a tela vazia.
    [Fact]
    public async Task O_mes_escolhido_no_card_segura_a_pagina_de_pe()
    {
        var (ctx, professor, local) = Montar();
        using var _c = ctx;

        // Só aula velha e paga: o período corrente do topo não tem nada (nem recebido, nem a
        // receber, nem previsto).
        ctx.Aulas.Add(Realizada(professor, local, new DateTime(2026, 8, 12, 9, 0, 0), 120m));
        await ctx.SaveChangesAsync();

        Assert.False((await Abrir(ctx, professor.Id)).TemMovimento);
        Assert.True((await Abrir(ctx, professor.Id, "2026-08")).TemMovimento);
    }

    // E pra trás ela para na primeira aula: andar por meses anteriores ao cadastro é a mesma
    // caminhada vazia, do outro lado.
    [Fact]
    public async Task A_seta_pra_tras_para_antes_da_primeira_aula()
    {
        var (ctx, professor, local) = Montar();
        using var _c = ctx;

        Assert.False((await Abrir(ctx, professor.Id, "2026-08")).PodeVoltarSemanas);

        ctx.Aulas.Add(Realizada(professor, local, new DateTime(2026, 7, 20, 9, 0, 0)));
        await ctx.SaveChangesAsync();

        Assert.True((await Abrir(ctx, professor.Id, "2026-08")).PodeVoltarSemanas);
    }
}
