using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// DEFINIR O HORÁRIO DE UM JOGO NA MÃO (10/09/2026).
//
// 🗣️ Felipe: *"permita tambem, trocar o horario na mão, na lista de jogos, para nós
// organizadores"*. A troca ⇄ exige OUTRO jogo pra trocar de lugar; na véspera do Er ele quer
// digitar a hora que quiser num jogo só. O que a tela grava é só este jogo: hora nova, e o
// CLUBE acompanha a hora (Services/HorarioNaMao) — se o clube atual não tem quadra aberta no
// horário novo (o Radar só abre sábado de manhã), o jogo volta pro clube principal. A grade
// não é recalculada; quem aponta gente em dois jogos no mesmo horário é o Conferir grade,
// como na troca.
public class DefinirHorarioNaMaoTests
{
    private static readonly DateTime Sexta = new(2026, 9, 11, 18, 0, 0);
    private static readonly DateTime Sabado = new(2026, 9, 12);
    private static DateTime As(string hora) => DateTime.Parse($"2026-09-12 {hora}");

    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Jogador Organizador, Categoria Categoria,
        Clube Er, Clube Radar, Partida NoEr, Partida NoRadar);

    private static Cenario Montar(bool porOrdem = true)
    {
        var ctx = TestInfra.NovoContexto();
        var er = new Clube { Nome = "Er Padel" };
        var radar = new Clube { Nome = "Radar" };
        ctx.Clubes.AddRange(er, radar);
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "2ª Etapa ER PADEL TOUR", Codigo = "EPT2", Status = "Fase de Grupos", ClubeId = er.Id,
            DataInicio = Sexta, DataFim = Sabado.AddDays(1),
            HoraInicioDoDia = new TimeSpan(18, 0, 0), HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0), QuantidadeQuadras = 2, TempoPrevistoPartidaMinutos = 50,
            SemHorarioPrevisto = porOrdem,
        };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });
        ctx.Quadras.AddRange(
            new Quadra { TorneioId = torneio.Id, Nome = "Arena 1", ClubeId = er.Id },
            new Quadra
            {
                TorneioId = torneio.Id, Nome = "Radar 1", ClubeId = radar.Id,
                DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12).AddMinutes(10),
            });
        var categoria = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", Torneio = torneio, PodeJogarNaSedeExtra = true };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        int proximo = 1;
        Dupla Dupla()
        {
            var j1 = TestInfra.NovoJogador(proximo++);
            var j2 = TestInfra.NovoJogador(proximo++);
            ctx.Jogadores.AddRange(j1, j2);
            var dupla = new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 };
            ctx.Duplas.Add(dupla);
            return dupla;
        }
        Partida Jogo(string codigo, DateTime hora, int clubeId, string? quadra = null) => new()
        {
            TorneioId = torneio.Id, Categoria = categoria, Dupla1 = Dupla(), Dupla2 = Dupla(),
            Codigo = codigo, Status = "Agendada", Fase = "Grupo A",
            HorarioPrevisto = hora, ClubeId = clubeId, NomeQuadra = quadra,
        };
        var noEr = Jogo("NOER", As("20:00"), er.Id, porOrdem ? null : "Arena 1");
        var noRadar = Jogo("RADAR", As("08:00"), radar.Id, porOrdem ? null : "Radar 1");
        ctx.Partidas.AddRange(noEr, noRadar);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return new Cenario(ctx, torneio, organizador, categoria, er, radar, noEr, noRadar);
    }

    private static Padelizou.Controllers.TorneiosController Controller(Cenario c, int? quem = null)
    {
        c.Ctx.ChangeTracker.Clear();
        return TestInfra.NovoTorneiosController(c.Ctx, quem ?? c.Organizador.Id);
    }

    private static Task<Partida> Recarregar(Cenario c, Partida jogo) =>
        c.Ctx.Partidas.AsNoTracking().SingleAsync(p => p.Id == jogo.Id);

    [Fact]
    public async Task Define_a_hora_de_um_jogo_agendado_e_mantem_o_clube_quando_ele_esta_aberto()
    {
        var c = Montar();
        var controller = Controller(c);

        await controller.DefinirHorario(c.Torneio.Id, c.NoEr.Id.ToString(), As("21:40"));

        Assert.Null(controller.TempData["Erro"]);
        var depois = await Recarregar(c, c.NoEr);
        Assert.Equal(As("21:40"), depois.HorarioPrevisto);
        Assert.Equal(c.Er.Id, depois.ClubeId);
        Assert.Null(depois.NomeQuadra);   // por ordem: a quadra continua sendo do balcão
        Assert.Contains("21:40", (string?)controller.TempData["Sucesso"] ?? "");
    }

    [Fact]
    public async Task Jogo_do_Radar_levado_pra_hora_em_que_o_Radar_esta_fechado_vai_pro_clube_principal()
    {
        var c = Montar();
        await Controller(c).DefinirHorario(c.Torneio.Id, c.NoRadar.Id.ToString(), As("20:00"));

        var depois = await Recarregar(c, c.NoRadar);
        Assert.Equal(As("20:00"), depois.HorarioPrevisto);
        Assert.Equal(c.Er.Id, depois.ClubeId);   // o Radar só abre de manhã: o clube é do horário
    }

    [Fact]
    public async Task Jogo_do_Radar_dentro_da_janela_continua_no_Radar()
    {
        var c = Montar();
        await Controller(c).DefinirHorario(c.Torneio.Id, c.NoRadar.Id.ToString(), As("09:40"));

        var depois = await Recarregar(c, c.NoRadar);
        Assert.Equal(As("09:40"), depois.HorarioPrevisto);
        Assert.Equal(c.Radar.Id, depois.ClubeId);
    }

    [Fact]
    public async Task Jogo_finalizado_nao_muda_de_hora()
    {
        var c = Montar();
        var jogo = await c.Ctx.Partidas.SingleAsync(p => p.Id == c.NoEr.Id);
        jogo.Status = "Finalizada";
        await c.Ctx.SaveChangesAsync();

        var controller = Controller(c);
        await controller.DefinirHorario(c.Torneio.Id, c.NoEr.Id.ToString(), As("21:40"));

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal(As("20:00"), (await Recarregar(c, c.NoEr)).HorarioPrevisto);
    }

    [Fact]
    public async Task Jogo_de_outro_torneio_e_recusado()
    {
        var c = Montar();
        var (outro, categoriaDoOutro, _) = TestInfra.MontarTorneio(c.Ctx, qtdDuplas: 2, status: "Fase de Grupos");
        var duplas = await c.Ctx.Duplas.Where(d => d.CategoriaId == categoriaDoOutro.Id).ToListAsync();
        var jogoDoOutro = new Partida
        {
            TorneioId = outro.Id, CategoriaId = categoriaDoOutro.Id, Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id,
            Codigo = "OUTRO", Status = "Agendada", Fase = "Grupo A", HorarioPrevisto = As("10:00"),
        };
        c.Ctx.Partidas.Add(jogoDoOutro);
        await c.Ctx.SaveChangesAsync();

        var controller = Controller(c);
        await controller.DefinirHorario(c.Torneio.Id, jogoDoOutro.Id.ToString(), As("21:40"));

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal(As("10:00"), (await Recarregar(c, jogoDoOutro)).HorarioPrevisto);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_define()
    {
        var c = Montar();
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000098" };
        c.Ctx.Jogadores.Add(intruso);
        await c.Ctx.SaveChangesAsync();

        var resultado = await Controller(c, intruso.Id).DefinirHorario(c.Torneio.Id, c.NoEr.Id.ToString(), As("21:40"));

        Assert.IsType<ForbidResult>(resultado);
        Assert.Equal(As("20:00"), (await Recarregar(c, c.NoEr)).HorarioPrevisto);
    }

    // Torneio com quadra marcada: duas quadras iguais no mesmo horário é a única coisa que a
    // grade não pode produzir. Aqui não se troca com o dono (é uma hora digitada, não um slot):
    // recusa e diz o caminho.
    [Fact]
    public async Task Quadra_ja_ocupada_no_horario_novo_e_recusada()
    {
        var c = Montar(porOrdem: false);
        var terceiro = new Partida
        {
            TorneioId = c.Torneio.Id, CategoriaId = c.Categoria.Id, Dupla1Id = c.NoEr.Dupla1Id, Dupla2Id = c.NoRadar.Dupla1Id,
            Codigo = "TERC", Status = "Agendada", Fase = "Grupo A", HorarioPrevisto = As("21:00"), NomeQuadra = "Arena 1", ClubeId = c.Er.Id,
        };
        c.Ctx.Partidas.Add(terceiro);
        await c.Ctx.SaveChangesAsync();

        var controller = Controller(c);
        await controller.DefinirHorario(c.Torneio.Id, terceiro.Id.ToString(), As("20:00"));   // NOER já está na Arena 1 às 20:00

        var erro = (string?)controller.TempData["Erro"];
        Assert.NotNull(erro);
        Assert.Contains("Arena 1", erro);
        Assert.Equal(As("21:00"), (await Recarregar(c, terceiro)).HorarioPrevisto);
    }

    // ── A tela ──
    [Fact]
    public void A_lista_oferece_o_botao_de_definir_horario_na_mao_pro_jogo_real_e_pra_previa()
    {
        var pasta = Path.Combine(PastaDoProjeto(), "Views", "Torneios");
        Assert.Contains("data-bs-target=\"#modalDefinirHorario\"", File.ReadAllText(Path.Combine(pasta, "_JogoEmLinha.cshtml")));
        Assert.Contains("data-bs-target=\"#modalDefinirHorario\"", File.ReadAllText(Path.Combine(pasta, "_JogoQueVem.cshtml")));

        var lista = File.ReadAllText(Path.Combine(pasta, "_JogosDoTorneio.cshtml"));
        Assert.Contains("asp-action=\"DefinirHorario\"", lista);
        Assert.Contains("type=\"datetime-local\"", lista);
        Assert.Contains("name=\"horario\"", lista);
    }

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Padelizou.csproj não encontrado a partir do bin.");
    }
}
