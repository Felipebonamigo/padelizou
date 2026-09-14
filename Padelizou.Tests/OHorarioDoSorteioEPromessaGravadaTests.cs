using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 14/09/2026 — O HORÁRIO DO SORTEIO VIRA COMPROMISSO GRAVADO.
//
// 🗣️ Felipe: *"é muito importante que o chaveamento pré definido seja seguido, por que o pessoal
// se baseia nisso para se programar, o chaveamento fixo, os horarios fixos"* · *"ele é
// obrigatoriamente obrigado a respeitar os horarios das quadras dos sorteios, pq o pessoal se
// programa para jogar por esses horarios mesmo com o checkin"*.
//
// 🕳️ A PROJEÇÃO NÃO É ESTÁVEL — medido em 13/09 e fixado em OHorarioDoSorteioEPromessaTests: com
// o torneio andando NO HORÁRIO, ela promete 14:40 enquanto a Semifinal é só promessa e 15:30
// depois que as Quartas viram resultado. Por isso o desenho simples ("o robô pergunta à prévia
// onde prometeu") não serve: não existe UMA resposta, depende de quando se pergunta.
//
// ✅ A promessa é GRAVADA no instante em que a chave é aprovada — o mesmo instante em que o
// cruzamento congela (CongelarOCruzamentoPrevisto). A partir daí ela não é mais recalculada: é
// lida de uma linha.
//
// ⚠️ REUSA O `ReservaDeHorario`, que já existe e já tem três consumidores obedecendo: a prévia, o
// robô ao criar a rodada, e o reencaixe quando outra categoria avança. A PK composta
// `(categoria, fase, número)` é o que torna re-gravar um UPDATE em vez de duplicata.
public class OHorarioDoSorteioEPromessaGravadaTests
{
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria, int OrgId)>
        SorteadoAsync(int qtdDuplas = 12)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        return (ctx, torneio, categoria, org.Id);
    }

    private static Task<List<ReservaDeHorario>> ReservasAsync(DbPadelContext ctx, int categoriaId) =>
        ctx.ReservasDeHorario.Where(r => r.CategoriaId == categoriaId).ToListAsync();

    [Fact]
    public async Task Aprovar_a_chave_grava_o_horario_de_cada_eliminatoria_prevista()
    {
        var (ctx, torneio, categoria, orgId) = await SorteadoAsync();
        using var _ctx = ctx;

        Assert.Empty(await ReservasAsync(ctx, categoria.Id));

        await TestInfra.NovoTorneiosController(ctx, orgId).AprovarChaves(torneio.Id);

        // 12 duplas → 4 grupos de 3 → quadro de 8: Quartas (4), Semifinal (2) e Final (1).
        var reservas = await ReservasAsync(ctx, categoria.Id);
        Assert.NotEmpty(reservas);
        Assert.All(reservas, r => Assert.True(r.Horario != default, "reserva sem horário"));

        Assert.Contains(reservas, r => r.Fase == "Final" && r.Numero == 1);
        Assert.Equal(2, reservas.Count(r => r.Fase == "Semifinal"));
    }

    [Fact]
    public async Task O_horario_gravado_e_exatamente_o_que_a_previa_mostrava()
    {
        var (ctx, torneio, categoria, orgId) = await SorteadoAsync();
        using var _ctx = ctx;

        // O que o jogador lê na página ANTES de aprovar.
        var todas = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        var previa = (await new RoboDoChaveamento(ctx, TestInfra.EstatisticasFalsas())
                .ProjetarProximasFasesAsync(torneio.Id, todas)).Jogos
            .Where(j => j.CategoriaId == categoria.Id)
            .ToDictionary(j => (j.Fase, j.Numero), j => j.Horario);

        await TestInfra.NovoTorneiosController(ctx, orgId).AprovarChaves(torneio.Id);

        var reservas = await ReservasAsync(ctx, categoria.Id);

        // ⚠️ SEM ISTO O TESTE PASSA VAZIO: `foreach` sobre lista vazia não afirma nada, e ele
        // ficaria verde com a gravação inexistente.
        Assert.NotEmpty(reservas);

        foreach (var r in reservas)
        {
            Assert.True(previa.ContainsKey((r.Fase, r.Numero)),
                $"gravou {r.Fase} {r.Numero}, que a prévia não prometia");
            Assert.Equal(previa[(r.Fase, r.Numero)], r.Horario);
        }
    }

    [Fact]
    public async Task Recalcular_os_horarios_RE_GRAVA_a_promessa()
    {
        var (ctx, torneio, categoria, orgId) = await SorteadoAsync();
        using var _ctx = ctx;

        await TestInfra.NovoTorneiosController(ctx, orgId).AprovarChaves(torneio.Id);
        var antes = (await ReservasAsync(ctx, categoria.Id))
            .ToDictionary(r => (r.Fase, r.Numero), r => r.Horario);
        Assert.NotEmpty(antes);

        ctx.ChangeTracker.Clear();
        await TestInfra.NovoTorneiosController(ctx, orgId).RefazerGrade(torneio.Id);

        // ⚠️ ESCOLHA DO FELIPE: o botão apaga as reservas (o aviso dele diz isso), refaz a grade
        // e o resultado vira o NOVO compromisso. Sem a re-gravação, depois de recalcular os
        // horários voltariam a poder mudar sozinhos no nascimento — um buraco silencioso, aberto
        // justamente pelo botão que existe pra arrumar a grade.
        var depois = await ReservasAsync(ctx, categoria.Id);
        Assert.NotEmpty(depois);
        Assert.Equal(antes.Count, depois.Count);
        Assert.All(depois, r => Assert.True(r.Horario != default, "reserva re-gravada sem horário"));
    }

    [Fact]
    public async Task Aprovar_duas_vezes_nao_duplica_nem_muda_o_que_ja_foi_prometido()
    {
        var (ctx, torneio, categoria, orgId) = await SorteadoAsync();
        using var _ctx = ctx;

        await TestInfra.NovoTorneiosController(ctx, orgId).AprovarChaves(torneio.Id);
        var primeira = (await ReservasAsync(ctx, categoria.Id))
            .ToDictionary(r => (r.Fase, r.Numero), r => r.Horario);
        Assert.NotEmpty(primeira);   // mesmo motivo: sem promessa gravada, o resto não afirma nada

        // O segundo clique é recusado (o status já saiu de "Pendente"), mas o que importa é que
        // a promessa não se mexeu: a PK composta faz re-gravar ser UPDATE, nunca linha nova.
        await TestInfra.NovoTorneiosController(ctx, orgId).AprovarChaves(torneio.Id);

        var depois = await ReservasAsync(ctx, categoria.Id);
        Assert.Equal(primeira.Count, depois.Count);
        Assert.All(depois, r => Assert.Equal(primeira[(r.Fase, r.Numero)], r.Horario));
    }
}
