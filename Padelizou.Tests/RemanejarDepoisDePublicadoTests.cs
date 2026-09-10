using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// REMANEJAR DEPOIS DE A CHAVE ESTAR PUBLICADA — a pergunta do Felipe (10/09/2026):
// 🗣️ *"eu consigo trocar horarios depois de publicado e aprovado? se não, permita q tenha um
// botão la, altera tambem, pq terá alguns jogadores q vao querer trocar e as vezes tem trocas
// depois das chaves publicadas"*.
//
// A resposta é SIM, e este arquivo é o que a mantém sim. Nenhuma das ações de horário olha
// `Torneio.Status` — a autorização é `PodeOperarODiaDeJogoAsync` (organizador ou marcador) e o
// resto é regra POR JOGO: só jogo "Agendada" muda de hora, porque jogo em quadra ou jogado tem
// horário de história, não de agenda.
//
// ⚠️ ISTO É UM TESTE DE CARACTERIZAÇÃO, e por isso ele nasceu VERDE. Pra saber que ele testa o
// que promete, foi falsificado antes de entrar (10/09/2026) — escrevendo o defeito que ele existe
// pra pegar, que é alguém "proteger" a grade publicada e achar que não quebrou nada:
//
//   1. `if (torneio.Status != AprovacaoDeChaves.Pendente) return Forbid();` no `TrocarHorario` e
//      no `DefinirHorario` (TorneiosController.Chaves.cs);
//   2. o portão da tela fechando ao contrário, `Status != AprovacaoDeChaves.Pendente` e sem a
//      exceção do organizador (CarregarViewBagJogosAsync).
//
// Com os dois, `Failed: 4, Passed: 0` — cada um pelo motivo dele: a troca não aconteceu, a hora
// não mudou, a prévia sumiu da tela, e `ChaveAindaNaoPublicada` veio `true` pro organizador de um
// torneio publicado. Os gates foram removidos em seguida.
public class RemanejarDepoisDePublicadoTests
{
    // O torneio como ele fica DEPOIS do Aprovar chaves: status "Fase de Grupos", jogadores já
    // avisados, horários já no calendário de todo mundo.
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Jogador Org, Padelizou.Controllers.TorneiosController Controller)>
        PublicadoAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);

        var publicado = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal("Fase de Grupos", publicado!.Status);
        Assert.NotEqual(AprovacaoDeChaves.Pendente, publicado.Status);

        return (ctx, publicado, org, controller);
    }

    private static Task<List<Partida>> AgendadosAsync(DbPadelContext ctx, int torneioId) =>
        ctx.Partidas
            .Where(p => p.TorneioId == torneioId && p.Status == "Agendada" && p.HorarioPrevisto != null)
            .OrderBy(p => p.HorarioPrevisto).ThenBy(p => p.Id)
            .ToListAsync();

    [Fact]
    public async Task Com_a_chave_publicada_o_organizador_ainda_troca_o_horario_de_dois_jogos()
    {
        var (ctx, torneio, org, _) = await PublicadoAsync();
        var jogos = await AgendadosAsync(ctx, torneio.Id);
        var primeiro = jogos[0];
        var ultimo = jogos[^1];
        var (horaDoPrimeiro, horaDoUltimo) = (primeiro.HorarioPrevisto, ultimo.HorarioPrevisto);
        Assert.NotEqual(horaDoPrimeiro, horaDoUltimo);

        ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.TrocarHorario(torneio.Id, primeiro.Id.ToString(), ultimo.Id.ToString());

        Assert.Null(controller.TempData["Erro"]);
        ctx.ChangeTracker.Clear();
        Assert.Equal(horaDoUltimo, (await ctx.Partidas.FindAsync(primeiro.Id))!.HorarioPrevisto);
        Assert.Equal(horaDoPrimeiro, (await ctx.Partidas.FindAsync(ultimo.Id))!.HorarioPrevisto);
    }

    [Fact]
    public async Task Com_a_chave_publicada_o_organizador_ainda_define_a_hora_na_mao()
    {
        var (ctx, torneio, org, _) = await PublicadoAsync();
        var jogo = (await AgendadosAsync(ctx, torneio.Id))[0];
        var novaHora = jogo.HorarioPrevisto!.Value.AddDays(1);

        ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.DefinirHorario(torneio.Id, jogo.Id.ToString(), novaHora);

        Assert.Null(controller.TempData["Erro"]);
        ctx.ChangeTracker.Clear();
        Assert.Equal(novaHora, (await ctx.Partidas.FindAsync(jogo.Id))!.HorarioPrevisto);
    }

    [Fact]
    public async Task Com_a_chave_publicada_a_eliminatoria_PREVISTA_ainda_troca_de_horario()
    {
        // A prévia é justamente o que o organizador remaneja depois de publicar: a Semifinal e a
        // Final não existem no banco até a fase anterior fechar (Models/ReservaDeHorario).
        var (ctx, torneio, org, _) = await PublicadoAsync();

        ctx.ChangeTracker.Clear();
        var daTela = TestInfra.NovoTorneiosController(ctx, org.Id);
        Assert.IsType<ViewResult>(await daTela.Jogos(torneio.Id, null, null));
        var previas = (List<JogoQueVem>)daTela.ViewBag.JogosQueVem;
        Assert.NotEmpty(previas);

        var previa = previas.First(j => j.Horario != null && j.CategoriaId != null);

        // ⚠️ MAIS TARDE, e não pra qualquer hora: a régua que recusa é a da própria categoria —
        // a semifinal não acontece antes de a fase de grupos dela terminar (ReservasDeHorario.Vale),
        // e isso vale publicado ou não. Atrasar é o que o organizador faz no dia de jogo.
        var maisTarde = previa.Horario!.Value.AddHours(2);

        ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.DefinirHorario(torneio.Id,
            ReferenciaDoJogo.Prevista(previa.CategoriaId!.Value, previa.Fase, previa.Numero).ToString(),
            maisTarde);

        Assert.Null(controller.TempData["Erro"]);
        var reserva = Assert.Single(await ctx.ReservasDeHorario.ToListAsync());
        Assert.Equal(maisTarde, reserva.Horario);
        Assert.Equal(previa.Fase, reserva.Fase);
    }

    [Fact]
    public async Task Com_a_chave_publicada_a_tela_do_organizador_ainda_traz_o_que_o_modal_precisa()
    {
        // O botão só serve se a tela tiver o que oferecer: os jogos pra trocar e os horários da
        // grade pra escolher. Ambos são do ViewBag — e o portão da chave não aprovada, que
        // esvazia tudo pra visitante, não pode alcançar o organizador de um torneio publicado.
        var (ctx, torneio, org, _) = await PublicadoAsync();

        ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        Assert.IsType<ViewResult>(await controller.Jogos(torneio.Id, null, null));

        Assert.True(controller.ViewBag.EhOrganizador);
        Assert.False(controller.ViewBag.ChaveAindaNaoPublicada);
        Assert.NotEmpty((List<Partida>)controller.ViewBag.Agendadas);
        Assert.NotEmpty((List<HorariosDaGrade.Slot>)controller.ViewBag.SlotsDaGrade);
    }
}
