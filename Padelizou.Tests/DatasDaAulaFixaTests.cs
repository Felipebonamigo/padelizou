using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A aula fixa deixou de ser "repetir por N semanas" em 09/08/2026. O professor tinha que
// adivinhar o N e descobrir o estrago depois: mês com 5 sextas, feriado no meio, a semana em
// que ele viaja — nada disso cabe num número, e o conserto era apagar aula por aula.
public class DatasDaAulaFixaTests
{
    private static readonly DateTime Agora = new(2026, 8, 9, 10, 0, 0);

    [Fact]
    public void Le_as_datas_que_a_tela_mandou()
    {
        var lidas = DatasDaAulaFixa.Ler(new[] { "2026-08-14T08:00", "2026-08-21T09:00" }, Agora);

        Assert.Equal(2, lidas.Count);
        Assert.Equal(new DateTime(2026, 8, 14, 8, 0, 0), lidas[0]);
        // A hora de cada data é dela: a semana em que a quadra só tem 9h não obriga a
        // remarcar a série inteira.
        Assert.Equal(new DateTime(2026, 8, 21, 9, 0, 0), lidas[1]);
    }

    [Fact]
    public void A_data_e_lida_como_ano_mes_dia_mesmo_com_o_app_em_pt_BR()
    {
        // "2026-08-10" lido na cultura pt-BR viraria dia 2026 do mês 8 — e cairia fora em
        // silêncio, sem aula nenhuma criada.
        var lidas = DatasDaAulaFixa.Ler(new[] { "2026-08-10T07:00" }, Agora);

        Assert.Equal(new DateTime(2026, 8, 10, 7, 0, 0), Assert.Single(lidas));
    }

    [Fact]
    public void Data_no_passado_nao_entra()
    {
        // Aula fixa é combinado pra frente; criar aula de ontem só suja o financeiro.
        var lidas = DatasDaAulaFixa.Ler(new[] { "2026-08-01T08:00", "2026-08-14T08:00" }, Agora);

        Assert.Equal(new DateTime(2026, 8, 14, 8, 0, 0), Assert.Single(lidas));
    }

    [Fact]
    public void Hoje_ainda_vale()
    {
        // A aula pode ser daqui a duas horas — o corte é o DIA, não o relógio.
        var lidas = DatasDaAulaFixa.Ler(new[] { "2026-08-09T20:00" }, Agora);

        Assert.Single(lidas);
    }

    [Fact]
    public void Data_repetida_vira_uma_so()
    {
        // Duas iguais criariam duas aulas no mesmo horário: a trava de conflito do controller
        // só olha o que JÁ ESTÁ no banco, não o lote que está entrando.
        var lidas = DatasDaAulaFixa.Ler(new[] { "2026-08-14T08:00", "2026-08-14T08:00" }, Agora);

        Assert.Single(lidas);
    }

    [Fact]
    public void Lixo_no_formulario_e_ignorado_sem_derrubar_a_marcacao()
    {
        var lidas = DatasDaAulaFixa.Ler(new[] { "", "banana", "2026-08-14T08:00" }, Agora);

        Assert.Single(lidas);
    }

    [Fact]
    public void Tem_teto_de_datas()
    {
        // Sem limite, um formulário adulterado criaria mil aulas de uma vez.
        var muitas = Enumerable.Range(1, 60).Select(i => Agora.AddDays(i).ToString("yyyy-MM-ddTHH:mm"));

        Assert.Equal(DatasDaAulaFixa.MaxDatas, DatasDaAulaFixa.Ler(muitas, Agora).Count);
    }

    [Fact]
    public void A_sugestao_semanal_anda_de_sete_em_sete()
    {
        var datas = DatasDaAulaFixa.Semanais(new DateTime(2026, 8, 28, 8, 0, 0), 3);

        Assert.Equal(new DateTime(2026, 8, 28, 8, 0, 0), datas[0]);
        // Atravessa a virada de mês sozinho.
        Assert.Equal(new DateTime(2026, 9, 4, 8, 0, 0), datas[1]);
        Assert.Equal(new DateTime(2026, 9, 11, 8, 0, 0), datas[2]);
    }

    // ---- No controller ----

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

    [Fact]
    public async Task Cria_exatamente_as_datas_marcadas_e_nao_as_desmarcadas()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var primeira = DateTime.Today.AddDays(5).AddHours(8);
        var terceira = primeira.AddDays(14);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);

        // O professor desmarcou a segunda semana (feriado). Sem a lista, "repetir por 3
        // semanas" criaria a aula no feriado e ele apagaria depois.
        await controller.AdicionarManual(local.Id, "Robson", null, primeira, null,
            recorrente: true, semanasRecorrencia: 3,
            datas: new List<string>
            {
                primeira.ToString("yyyy-MM-ddTHH:mm"),
                terceira.ToString("yyyy-MM-ddTHH:mm"),
            });

        var criadas = await ctx.Aulas.OrderBy(a => a.DataHora).ToListAsync();
        Assert.Equal(2, criadas.Count);
        Assert.Equal(primeira, criadas[0].DataHora);
        Assert.Equal(terceira, criadas[1].DataHora);
        // Continuam sendo a mesma série: é ela que permite aceitar/recusar tudo de uma vez.
        Assert.NotNull(criadas[0].RecorrenciaId);
        Assert.Equal(criadas[0].RecorrenciaId, criadas[1].RecorrenciaId);
    }

    [Fact]
    public async Task Cada_data_pode_ter_a_sua_hora()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var primeira = DateTime.Today.AddDays(5).AddHours(8);
        var outraHora = primeira.AddDays(7).AddHours(1);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);

        await controller.AdicionarManual(local.Id, "Robson", null, primeira, null,
            recorrente: true, semanasRecorrencia: 2,
            datas: new List<string>
            {
                primeira.ToString("yyyy-MM-ddTHH:mm"),
                outraHora.ToString("yyyy-MM-ddTHH:mm"),
            });

        var criadas = await ctx.Aulas.OrderBy(a => a.DataHora).ToListAsync();
        Assert.Equal(9, criadas[1].DataHora.Hour);
    }

    [Fact]
    public async Task Sem_a_lista_a_contagem_por_semanas_continua_valendo()
    {
        // Plano B do navegador sem JS — e de quem já tinha o formulário aberto.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var primeira = DateTime.Today.AddDays(5).AddHours(8);
        var controller = TestInfra.NovoAulasController(ctx, professor.Id);

        await controller.AdicionarManual(local.Id, "Robson", null, primeira, null,
            recorrente: true, semanasRecorrencia: 4);

        Assert.Equal(4, await ctx.Aulas.CountAsync());
    }

    [Fact]
    public async Task Aula_avulsa_segue_sendo_uma_so()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);

        await controller.AdicionarManual(local.Id, "Robson", null,
            DateTime.Today.AddDays(5).AddHours(8), null,
            recorrente: false, semanasRecorrencia: 4);

        var criada = Assert.Single(await ctx.Aulas.ToListAsync());
        Assert.Null(criada.RecorrenciaId);
    }
}
