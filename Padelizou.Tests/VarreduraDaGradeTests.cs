using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;
using Xunit.Abstractions;
namespace Padelizou.Tests;
// VARREDURA DA GRADE (11/11/2026). 🗣️ Felipe, depois do Chakra: *"faça uma boa verificação em
// toda essa parte de torneio, para evitarmos problemas futuros"* — e, antes: *"preciso que isso
// não aconteça mais em nenhuma hipótese"*.
//
// Em vez de um cenário escrito à mão, 200 torneios GERADOS (dias, faixas por dia, quadras com e
// sem janela, 1 a 3 categorias, impedimentos e "Pôr os 2 jogos em" sorteados), cada um passando
// pelo fluxo de verdade: sortear, (metade) mudar o horário de um dia e aplicar, aprovar. Em cada um
// valem as regras que NUNCA podem quebrar:
//   • nenhum jogo (real ou previsto do mata-mata) antes de abrir o dia, depois da hora limite do
//     dia, antes da abertura da grade ou depois do último dia;
//   • nenhum jogo numa quadra fora da janela "disponível de / até" dela;
//   • mata-mata sempre depois do último jogo de grupo da PRÓPRIA categoria;
//   • se o Planejamento diz "Cabe", nenhum jogo fica sem horário.
// Conflito de pessoa e quadra ocupada NÃO entram: são o último recurso deliberado do encaixe
// (GradeDeJogosTests.Conflito_inevitavel_nao_trava_a_grade, GradeComHorarioQuebradoTests).
//
// A semente fixa as CONFIGURAÇÕES; o sorteio dos grupos continua aleatório, então cada rodada da
// suíte testa chaves diferentes — de propósito. Rodando 500 sementes, achou em 11/11/2026 quatro
// defeitos, todos com teste próprio em GradeNuncaPassaDoFimDoTorneioTests.
public class VarreduraDaGradeTests
{
    private readonly ITestOutputHelper _o;
    public VarreduraDaGradeTests(ITestOutputHelper o) => _o = o;

    [Fact]
    public async Task Duzentos_torneios_gerados_respeitam_as_regras_da_grade()
    {
        var problemas = new Dictionary<string, int>();
        var exemplos = new Dictionary<string, string>();
        void P(string tipo, string cfg) { problemas[tipo] = problemas.GetValueOrDefault(tipo) + 1; exemplos.TryAdd(tipo, cfg); }

        for (int seed = 1; seed <= 200; seed++)
        {
            var r = new Random(seed);
            var ctx = TestInfra.NovoContexto();
            int duplasCat1 = r.Next(4, 13);
            var (t, _, org) = TestInfra.MontarTorneio(ctx, duplasCat1);
            var quinta = new DateTime(2026, 11, 12);
            int diaInicio = r.Next(0, 2); // qui ou sex
            int dias = r.Next(2, 4);
            t.DataInicio = quinta.AddDays(diaInicio);
            t.DataFim = t.DataInicio.Value.AddDays(dias - 1 + r.Next(0, 2));
            t.HoraInicioDoDia = new TimeSpan(18, 0, 0);
            t.HoraInicioDiasSeguintes = new TimeSpan(r.Next(8, 11), 0, 0);
            t.HoraFimDoDia = new TimeSpan(r.Next(20, 23), 10, 0);
            t.TempoPrevistoPartidaMinutos = 50;
            int nq = r.Next(1, 4);
            t.QuantidadeQuadras = nq;
            var faixas = new List<string>();
            for (var d = t.DataInicio.Value; d <= t.DataFim.Value; d = d.AddDays(1))
                if (r.Next(3) == 0) { var a = r.Next(8, 15); var f = a + r.Next(3, 9); faixas.Add($"{d:yyyy-MM-dd}={a:00}:00-{Math.Min(f, 23):00}:00"); }
            t.HorariosPorDia = faixas.Count > 0 ? string.Join(';', faixas) : null;
            for (int q = 0; q < nq; q++)
            {
                var quadra = new Quadra { TorneioId = t.Id, Nome = $"Quadra {(char)('A' + q)}" };
                if (r.Next(4) == 0) { quadra.DisponivelDe = t.DataInicio.Value.AddHours(18); quadra.DisponivelAte = t.DataFim.Value.AddHours(r.Next(14, 21)); }
                ctx.Quadras.Add(quadra);
            }
            int n = 1000;
            for (int c = 0; c < r.Next(0, 3); c++)
            {
                var cat = new Categoria { Nome = $"Cat{c}", Codigo = $"C{c}", Torneio = t };
                ctx.Categorias.Add(cat);
                for (int i = 0; i < r.Next(4, 11); i++)
                {
                    var j1 = TestInfra.NovoJogador(n++); var j2 = TestInfra.NovoJogador(n++);
                    ctx.Jogadores.AddRange(j1, j2);
                    ctx.Duplas.Add(new Dupla { Categoria = cat, Jogador1 = j1, Jogador2 = j2 });
                }
            }
            await ctx.SaveChangesAsync();
            foreach (var d in ctx.Duplas)
            {
                int x = r.Next(10);
                if (x == 0) d.ImpedimentoSextaNoite = true;
                if (x == 1) d.ImpedimentoSabadoManha = true;
                if (x == 2) d.ImpedimentoSabadoTarde = true;
                if (x == 3) d.ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha;
                if (x == 4) d.ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
            }
            await ctx.SaveChangesAsync();
            var cfg = $"seed {seed}: {t.DataInicio:ddd dd}–{t.DataFim:ddd dd} quadras {nq} faixas [{t.HorariosPorDia}] fim {t.HoraFimDoDia} demais {t.HoraInicioDiasSeguintes} duplas {ctx.Duplas.Count()}";

            try { await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(t.Id, sortearMesmoSemCaber: true); }
            catch (Exception e) { P("exceção no sorteio: " + e.GetType().Name, cfg + " " + e.Message); continue; }
            var vm = (Padelizou.ViewModels.PlanejamentoDeQuadrasVM)((Microsoft.AspNetCore.Mvc.ViewResult)
                await TestInfra.NovoTorneiosController(ctx, org.Id).Planejamento(t.Id)).Model!;
            bool cabe = vm.Plano.Faltam == 0 && vm.JogosSemHorario == 0;
            if (seed % 2 == 0)
            {
                // Muda o horário de um dia e aplica (recalcula a grade, como o Felipe fez).
                var dia = t.DataInicio!.Value.AddDays(r.Next(0, dias));
                try { await TestInfra.NovoTorneiosController(ctx, org.Id).AplicarPlanejamento(t.Id,
                    limites: $"{dia:yyyy-MM-dd}={r.Next(16, 23)}:00", aberturas: $"{dia:yyyy-MM-dd}={r.Next(8, 12)}:00"); }
                catch (Exception e) { P("exceção ao aplicar: " + e.GetType().Name, cfg + " " + e.Message); continue; }
                ctx.ChangeTracker.Clear();
                cfg += " +aplicou " + (await ctx.Torneios.AsNoTracking().SingleAsync(x => x.Id == t.Id)).HorariosPorDia;
                vm = (Padelizou.ViewModels.PlanejamentoDeQuadrasVM)((Microsoft.AspNetCore.Mvc.ViewResult)
                    await TestInfra.NovoTorneiosController(ctx, org.Id).Planejamento(t.Id)).Model!;
                cabe = vm.Plano.Faltam == 0 && vm.JogosSemHorario == 0;
            }
            try { await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(t.Id, false); }
            catch (Exception e) { P("exceção ao aprovar: " + e.GetType().Name, cfg + " " + e.Message); continue; }

            ctx.ChangeTracker.Clear();
            t = await ctx.Torneios.SingleAsync(x => x.Id == t.Id);
            var cal = HorarioDoDia.Do(t);
            var jogos = await ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == t.Id).ToListAsync();
            var reservas = await ctx.ReservasDeHorario.AsNoTracking().Where(x => x.Categoria.TorneioId == t.Id).ToListAsync();
            var duplas = await ctx.Duplas.AsNoTracking().Where(d => d.Categoria.TorneioId == t.Id).ToDictionaryAsync(d => d.Id);
            var quadras = await ctx.Quadras.AsNoTracking().Where(q => q.TorneioId == t.Id).ToListAsync();

            void ConfereHora(DateTime h, string oque)
            {
                if (cal.UltimoInicio is DateTime u && h > u) P("depois do último dia", cfg + $" {oque} {h:ddd dd HH:mm}");
                if (h < t.AberturaDaGrade) P("antes da abertura", cfg + $" {oque} {h:ddd dd HH:mm}");
                if (h.TimeOfDay < cal.Abre(h.Date)) P("antes de abrir o dia", cfg + $" {oque} {h:ddd dd HH:mm} abre {cal.Abre(h.Date)}");
                if (h.TimeOfDay > cal.Limite(h.Date)) P("depois do limite do dia", cfg + $" {oque} {h:ddd dd HH:mm} limite {cal.Limite(h.Date)}");
            }
            foreach (var j in jogos.Where(j => j.HorarioPrevisto != null)) ConfereHora(j.HorarioPrevisto!.Value, "jogo " + j.Fase);
            foreach (var x in reservas) ConfereHora(x.Horario, "prévia " + x.Fase);

            // quadra fora da janela
            foreach (var j in jogos.Where(j => j.HorarioPrevisto != null && j.NomeQuadra != null))
            { var q = quadras.FirstOrDefault(q => q.Nome == j.NomeQuadra); if (q != null && ((q.DisponivelDe != null && j.HorarioPrevisto < q.DisponivelDe) || (q.DisponivelAte != null && j.HorarioPrevisto > q.DisponivelAte))) P("quadra fora da janela", cfg + $" {q.Nome} {j.HorarioPrevisto:ddd HH:mm}"); }
            // jogo sem quadra (havendo quadra)
            // mata-mata antes do fim do grupo da categoria
            foreach (var x in reservas)
            {
                var fimGrupo = jogos.Where(j => j.CategoriaId == x.CategoriaId && j.HorarioPrevisto != null && FasesTorneio.EhFaseDeGrupos(j.Fase)).Select(j => j.HorarioPrevisto!.Value).DefaultIfEmpty().Max();
                if (fimGrupo != default && x.Horario <= fimGrupo) P("mata-mata antes do fim do grupo", cfg + $" {x.Fase} {x.Horario:ddd HH:mm} grupo até {fimGrupo:ddd HH:mm}");
            }
            // cabe mas ficou sem hora
            int marcados = jogos.Count(j => j.HorarioPrevisto != null) + reservas.Count;
            int semHora = jogos.Count(j => j.HorarioPrevisto == null);
            if (cabe && (semHora > 0 || marcados < vm.JogosDoTorneio))
                P("planejamento diz Cabe mas sobrou jogo sem hora", cfg + $" total {vm.JogosDoTorneio} marcados {marcados} semHora {semHora}");
        }
        foreach (var (k, v) in problemas.OrderByDescending(x => x.Value)) _o.WriteLine($"{v,4}  {k}\n      ex: {exemplos[k]}");
        Assert.True(problemas.Count == 0, string.Join("\n", problemas.Select(p => $"{p.Value}× {p.Key} — ex.: {exemplos[p.Key]}")));
    }
}
