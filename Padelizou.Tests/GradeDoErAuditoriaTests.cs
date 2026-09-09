using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// AUDITORIA NA ESCALA DO TORNEIO DO ER (09/09/2026).
//
// 🗣️ Felipe: "criei o teste em dev, verifique se cumpriu bem os impedimentos e questões de
// horários, se ele respeitou isso".
//
// ⚠️ ESTA SESSÃO NÃO ALCANÇA O `dev` (o proxy recusa o CONNECT com 403), então não dá pra
// auditar as linhas do banco dele. O que dá — e vale mais que um print — é rodar o MOTOR DE
// VERDADE na forma exata daquele torneio e medir. Os números saem da tela dele:
//
//   63 duplas em 24 grupos = 54 jogos de grupo + 33 de mata-mata = 87 jogos, em 2 QUADRAS,
//   modo "por ordem", e 33 duplas com impedimento de horário.
//
// ⚠️ O QUE ESTÁ EM JOGO É JUSTAMENTE O APERTO: `GradeDeJogos.Encaixar` deixa o impedimento
// CEDER quando as vagas acabam ("um jogo sem horário nenhum é pior"). Com 87 jogos em 2
// quadras e MAIS DA METADE das duplas com restrição, esse caminho deixa de ser exceção — e é
// exatamente isso que este arquivo mede em vez de supor.
public class GradeDoErAuditoriaTests
{
    // Sexta 09/10/2026 às 18h — fim de semana no padrão que a grade assume.
    private static readonly DateTime AberturaSexta = new(2026, 10, 9, 18, 0, 0);

    // ⚠️ VARRE A QUANTIDADE DE QUADRAS, e não só as 2 do Er. Medindo, o furo apareceu com
    // QUATRO quadras — ou seja, com MAIS capacidade, não com menos. É o contrário do que a
    // intuição diz, e por isso o teste não podia ficar só no número dele: com 4 quadras a grade
    // termina em menos RODADAS, e o impedimento bloqueia DIAS INTEIROS — sobram menos horários
    // distintos pra dupla escapar da janela dela, e o último recurso do encaixe entra.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public async Task Na_escala_do_Er_nenhum_impedimento_e_furado(int quadras)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarComoOEr(ctx, quadras);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var furos = (await AchadosAsync(ctx, torneio))
            .Where(a => a.Regra == AuditoriaDaGrade.Impedimento).ToList();

        Assert.True(furos.Count == 0,
            $"{furos.Count} jogo(s) marcados dentro do impedimento da dupla:\n"
            + string.Join("\n", furos.Take(15).Select(a => a.Descricao)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public async Task Na_escala_do_Er_ninguem_e_chamado_pra_dois_jogos_no_mesmo_horario(int quadras)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarComoOEr(ctx, quadras);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var conflitos = (await AchadosAsync(ctx, torneio))
            .Where(a => a.Regra == AuditoriaDaGrade.PessoaEmDoisJogos).ToList();

        Assert.True(conflitos.Count == 0,
            $"{conflitos.Count} conflito(s):\n"
            + string.Join("\n", conflitos.Take(15).Select(a => a.Descricao)));
    }

    // "Questões de horário": todo jogo tem hora, e nenhum cai fora do expediente do torneio.
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Na_escala_do_Er_todo_jogo_tem_hora_dentro_do_expediente(int quadras)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarComoOEr(ctx, quadras);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();

        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));

        var foraDoExpediente = jogos
            .Where(j => j.HorarioPrevisto!.Value.TimeOfDay > torneio.HoraFimDoDia
                     || j.HorarioPrevisto!.Value.TimeOfDay < torneio.HoraInicioDiasSeguintes)
            .Select(j => $"{j.Fase}: {j.HorarioPrevisto:dd/MM HH:mm}")
            .ToList();

        Assert.True(foraDoExpediente.Count == 0,
            $"{foraDoExpediente.Count} jogo(s) fora do expediente:\n"
            + string.Join("\n", foraDoExpediente.Take(15)));
    }

    // ⚠️ CHAMA O MESMO SERVIÇO QUE A TELA (Services/AuditoriaDaGrade). Este arquivo tinha a
    // auditoria escrita à mão; quando ela virou botão (09/09/2026), a cópia daqui foi apagada
    // em vez de mantida em paralelo — duas auditorias divergem, e a que fica errada é sempre a
    // que ninguém está olhando.
    private static async Task<List<AuditoriaDaGrade.Achado>> AchadosAsync(DbPadelContext ctx, Torneio torneio)
    {
        var cheio = await ctx.Torneios
            .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
            .FirstAsync(t => t.Id == torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        var duplas = cheio.Categorias.SelectMany(c => c.Duplas).ToList();

        return AuditoriaDaGrade.Conferir(cheio, jogos, duplas,
            await SedesDoTorneio.CarregarAsync(ctx, torneio.Id));
    }

    // 63 duplas em 24 grupos = 15 grupos de 3 (45 duplas, 45 jogos) + 9 de 2 (18 duplas, 9
    // jogos) = 54 jogos de grupo. É a conta que a tela dele mostra.
    private static (Torneio torneio, Jogador organizador) MontarComoOEr(DbPadelContext ctx, int quadras = 2)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "2ª Etapa ER PADEL TOUR",
            Codigo = "ERT234",
            Status = "Chaves em Sorteio",
            DataInicio = AberturaSexta,
            QuantidadeQuadras = quadras,
            TempoPrevistoPartidaMinutos = 50,
            HoraInicioDoDia = new TimeSpan(18, 0, 0),
            HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            SemHorarioPrevisto = true,
            PermiteImpedimentos = true,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "Geral", Codigo = "GER", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });
        for (int q = 1; q <= quadras; q++)
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = $"Quadra {q}" });

        var jogadores = Enumerable.Range(1, 126).Select(TestInfra.NovoJogador).ToList();
        ctx.Jogadores.AddRange(jogadores);
        ctx.SaveChanges();

        // 33 das 63 com impedimento, na mesma proporção da lista dele (sexta à noite é o mais
        // marcado, depois sábado à tarde e sábado de manhã).
        var turnos = new[]
        {
            TurnoDoImpedimento.SextaNoite, TurnoDoImpedimento.SabadoTarde,
            TurnoDoImpedimento.SextaNoite, TurnoDoImpedimento.SabadoManha,
        };

        for (int i = 0; i < 63; i++)
        {
            var dupla = new Dupla
            {
                Categoria = categoria,
                Jogador1 = jogadores[i * 2],
                Jogador2 = jogadores[i * 2 + 1],
            };

            if (i < 33)
            {
                var turno = turnos[i % turnos.Length];
                dupla.ImpedimentoSextaNoite = turno == TurnoDoImpedimento.SextaNoite;
                dupla.ImpedimentoSabadoManha = turno == TurnoDoImpedimento.SabadoManha;
                dupla.ImpedimentoSabadoTarde = turno == TurnoDoImpedimento.SabadoTarde;
            }

            ctx.Duplas.Add(dupla);
        }

        ctx.SaveChanges();
        return (torneio, organizador);
    }
}
