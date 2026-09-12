using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — A PRESENÇA PASSA A SER DO JOGO, E NÃO DO TORNEIO.
//
// 🗣️ Felipe, horas depois de a chamada por pessoa entrar no ar: *"e o checkin, ele herda dos
// outros jogos pra mesma pessoa? pq se sim, nao deveria, tem q ser separado jogo a jogo"*.
//
// 🕳️ HERDAVA, E ESTAVA ESCRITO NO MODELO COMO DECISÃO: a chave era `(TorneioId, JogadorId)` —
// *"quem chegou ao clube chegou pro torneio INTEIRO"*. Quem joga 5ª Masculina às 15:30 e Mista às
// 19:00 aparecia verde nas duas assim que marcasse a primeira, e o organizador das 19:00 lia
// "todos presentes" para gente que tinha ido embora depois do primeiro jogo.
//
// ✅ A chave virou **`(PartidaId, JogadorId)`**. Um check responde uma pergunta só: *"esta pessoa
// está aqui pra ESTE jogo?"*.
//
// ⚠️ ISTO É O QUE SEGURA A ORDEM POR PRESENÇA. Com a herança, um jogo das 19:00 apareceria como
// "pronto pra começar" porque os quatro marcaram nos jogos DA TARDE — e subiria no topo do
// horário na frente de quem realmente está na quadra. Os dois pedidos são um bloco só por isso.
public class PresencaSeparadaPorJogoTests
{
    private static readonly DateTime Tarde = new(2026, 9, 12, 15, 30, 0);
    private static readonly DateTime Noite = new(2026, 9, 12, 19, 0, 0);

    private static (Torneio torneio, Categoria categoria, Jogador organizador, List<Dupla> duplas) Montar(
        DbPadelContext ctx, int qtdDuplas = 4)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas, status: "Fase de Grupos");
        torneio.UsaCheckIn = true;
        ctx.SaveChanges();

        var duplas = ctx.Duplas
            .Include(d => d.Jogador1).Include(d => d.Jogador2)
            .Where(d => d.CategoriaId == categoria.Id).ToList();

        return (torneio, categoria, organizador, duplas);
    }

    private static Partida Jogo(DbPadelContext ctx, Torneio t, Categoria c, Dupla d1, Dupla d2,
        DateTime hora, string status = "Agendada")
    {
        var p = new Partida
        {
            TorneioId = t.Id, CategoriaId = c.Id, Dupla1Id = d1.Id, Dupla2Id = d2.Id,
            Fase = "Fase de Grupos", Status = status, HorarioPrevisto = hora,
            Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
        };
        ctx.Partidas.Add(p);
        ctx.SaveChanges();
        return p;
    }

    // ── O CORAÇÃO DO PEDIDO ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Marcar_num_jogo_nao_marca_no_outro_jogo_da_mesma_pessoa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx);

        // A MESMA dupla joga duas vezes no dia: 15:30 e 19:00.
        var daTarde = Jogo(ctx, torneio, categoria, duplas[0], duplas[1], Tarde);
        var daNoite = Jogo(ctx, torneio, categoria, duplas[0], duplas[2], Noite);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(duplas[0].Jogador1Id, daTarde.Id, presente: true);

        var linhas = ctx.Presencas.Select(p => new { p.PartidaId, p.JogadorId }).ToList();

        Assert.Single(linhas);
        Assert.Equal(daTarde.Id, linhas[0].PartidaId);
        Assert.DoesNotContain(linhas, l => l.PartidaId == daNoite.Id);
    }

    [Fact]
    public async Task Desfazer_num_jogo_nao_desfaz_no_outro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx);
        var daTarde = Jogo(ctx, torneio, categoria, duplas[0], duplas[1], Tarde);
        var daNoite = Jogo(ctx, torneio, categoria, duplas[0], duplas[2], Noite);

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.MarcarCheckIn(duplas[0].Jogador1Id, daTarde.Id, presente: true);
        await controller.MarcarCheckIn(duplas[0].Jogador1Id, daNoite.Id, presente: true);
        await controller.MarcarCheckIn(duplas[0].Jogador1Id, daTarde.Id, presente: false);

        var restaram = ctx.Presencas.Select(p => p.PartidaId).ToList();

        Assert.Single(restaram);
        Assert.Equal(daNoite.Id, restaram[0]);
    }

    // ── REGRA 0: a checagem de dono ficou MAIS ESTREITA ──────────────────────────────────

    [Fact]
    public async Task Jogador_que_nao_joga_NAQUELE_jogo_nao_ganha_linha()
    {
        // Antes bastava jogar no TORNEIO. Agora o `partidaId` chega por campo de formulário junto
        // com o `jogadorId`, e o par tem que fazer sentido: carimbar a Carla no jogo das outras é
        // lixo no banco com cara de dado bom — e faria aquele jogo subir no topo do horário.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, duplas) = Montar(ctx);
        var jogoDosOutros = Jogo(ctx, torneio, categoria, duplas[2], duplas[3], Tarde);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(duplas[0].Jogador1Id, jogoDosOutros.Id, presente: true);

        Assert.IsType<NotFoundResult>(resultado);
        Assert.Empty(ctx.Presencas);
    }

    [Fact]
    public async Task Quem_nao_opera_o_dia_daquele_torneio_nao_grava()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, duplas) = Montar(ctx);
        var jogo = Jogo(ctx, torneio, categoria, duplas[0], duplas[1], Tarde);
        var estranho = TestInfra.NovoJogador(999);
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoTorneiosController(ctx, estranho.Id)
            .MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: true);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Empty(ctx.Presencas);
    }

    // ── A RÉGUA DERIVADA ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Dupla_completa_le_o_JOGO_e_nao_so_a_pessoa()
    {
        var dupla = new Dupla { Id = 5, Jogador1Id = 10, Jogador2Id = 11 };

        // Os dois chegaram — pro jogo 100.
        var chegadas = new Dictionary<(int PartidaId, int JogadorId), DateTime>
        {
            [(100, 10)] = Tarde,
            [(100, 11)] = Tarde,
        };

        Assert.True(PresencaNoDia.DuplaCompleta(100, dupla, chegadas));
        Assert.False(PresencaNoDia.DuplaCompleta(200, dupla, chegadas));
    }

    [Fact]
    public void ChegouEm_tambem_e_por_jogo()
    {
        var chegadas = new Dictionary<(int PartidaId, int JogadorId), DateTime> { [(100, 10)] = Tarde };

        Assert.Equal(Tarde, PresencaNoDia.ChegouEm(100, 10, chegadas));
        Assert.Null(PresencaNoDia.ChegouEm(200, 10, chegadas));
    }

    // ── E A ORDEM POR PRESENÇA LÊ O JOGO CERTO ───────────────────────────────────────────

    [Fact]
    public void Um_jogo_nao_sobe_no_horario_por_check_feito_em_OUTRO_jogo()
    {
        // ⚠️ É ESTE O DEFEITO QUE O BLOCO INTEIRO EXISTE PRA IMPEDIR: com a herança, os quatro
        // marcados no jogo da tarde faziam o jogo da noite parecer pronto pra começar.
        var daNoite = new Partida
        {
            Id = 2, Codigo = "N", Status = "Agendada", Fase = "Grupo", CategoriaId = 1,
            HorarioPrevisto = Noite,
            Dupla1 = new Dupla { Id = 1, Jogador1Id = 10, Jogador2Id = 11 },
            Dupla2 = new Dupla { Id = 2, Jogador1Id = 12, Jogador2Id = 13 },
        };
        var outroDaNoite = new Partida
        {
            Id = 3, Codigo = "N2", Status = "Agendada", Fase = "Grupo", CategoriaId = 1,
            HorarioPrevisto = Noite,
            Dupla1 = new Dupla { Id = 3, Jogador1Id = 20, Jogador2Id = 21 },
            Dupla2 = new Dupla { Id = 4, Jogador1Id = 22, Jogador2Id = 23 },
        };

        // Os quatro do jogo 2 marcaram — mas no jogo 1 (o da tarde), não no 2.
        var chegadas = new[] { 10, 11, 12, 13 }
            .ToDictionary(j => (PartidaId: 1, JogadorId: j), _ => Tarde);

        var linhas = OrdemNoHorario.Ordenar(new[] { daNoite, outroDaNoite },
            Array.Empty<ProximasFasesDaChave.JogoQueVem>(), chegadas);

        // Nada subiu: o jogo 2 continua onde estava, pela régua de sempre (o Id).
        Assert.Equal(new[] { 2, 3 }, linhas.Select(l => l.Jogo!.Id));
    }
}
