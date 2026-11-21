using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O TELÃO DO TORNEIO — a tela pra TV do clube (18/11/2026).
//
// 🗣️ Felipe, pelo organizador da Wallau: *"quer por em uma tv, os jogos aovivo, agendado e
// finalizado para exibir"*. Decisões dele: os três blocos, o palpitômetro nos jogos ao vivo, a
// faixa de patrocinadores (os da plataforma — não existe patrocinador por torneio) e o filtro de
// clube pelo endereço (`?clube=`), pra cada TV mostrar o próprio clube.
//
// Só leitura e pública: os mesmos dados que a página Jogos já mostra pra qualquer pessoa, e as
// MESMAS portas — torneio oculto e chave esperando aprovação não aparecem.
public class TelaoDoTorneioTests
{
    private static readonly DateTime Dia = new(2026, 11, 21);
    private static DateTime As(string hora) => DateTime.Parse($"2026-11-21 {hora}");

    private int _id = 1;

    private Partida Jogo(string status, string? hora = null, string? quadra = null, string? fimReal = null,
        int? clubeId = null) => new()
    {
        Id = _id++, Codigo = $"J{_id}", Fase = "Grupo A", Status = status,
        HorarioPrevisto = hora == null ? null : As(hora),
        HorarioFimReal = fimReal == null ? null : As(fimReal),
        NomeQuadra = quadra, ClubeId = clubeId,
    };

    [Fact]
    public void Separa_ao_vivo_proximos_e_resultados()
    {
        var aoVivo = Jogo("AoVivo", "10:00", "Quadra A");
        var agendado = Jogo("Agendada", "11:00", "Quadra A");
        var finalizado = Jogo("Finalizada", "09:00", "Quadra A", fimReal: "09:50");

        var quadro = TelaoDoTorneio.Montar(new[] { aoVivo, agendado, finalizado }, sedes: null, clubeId: null);

        Assert.Equal(new[] { aoVivo }, quadro.AoVivo);
        Assert.Equal(new[] { agendado }, quadro.Proximos);
        Assert.Equal(new[] { finalizado }, quadro.Resultados);
    }

    [Fact]
    public void Proximos_vem_por_horario_e_param_no_limite_sem_os_sem_hora()
    {
        var jogos = new[] { "13:00", "11:00", "12:00", "14:00" }.Select(h => Jogo("Agendada", h)).ToList();
        var semHora = Jogo("Agendada");
        jogos.Add(semHora);

        var quadro = TelaoDoTorneio.Montar(jogos, null, null, maxProximos: 3);

        Assert.Equal(new[] { As("11:00"), As("12:00"), As("13:00") },
            quadro.Proximos.Select(p => p.HorarioPrevisto!.Value));
    }

    [Fact]
    public void Resultados_vem_do_que_acabou_por_ultimo_e_param_no_limite()
    {
        var antigo = Jogo("Finalizada", "09:00", fimReal: "09:40");
        var recente = Jogo("Finalizada", "10:00", fimReal: "10:55");
        var meio = Jogo("Finalizada", "09:50", fimReal: "10:30");

        var quadro = TelaoDoTorneio.Montar(new[] { antigo, recente, meio }, null, null, maxResultados: 2);

        Assert.Equal(new[] { recente, meio }, quadro.Resultados);
    }

    [Fact]
    public void Ao_vivo_vem_na_ordem_das_quadras()
    {
        var b = Jogo("AoVivo", "10:00", "Quadra B");
        var a = Jogo("AoVivo", "10:00", "Quadra A");

        Assert.Equal(new[] { a, b }, TelaoDoTorneio.Montar(new[] { b, a }, null, null).AoVivo);
    }

    [Fact]
    public void Com_clube_no_endereco_mostra_so_os_jogos_daquele_clube()
    {
        // Duas sedes: a quadra diz o clube — a mesma régua da página Jogos (FiltroDeJogos).
        var sedes = SedesDoTorneio.Montar(7, 0,
            new[]
            {
                new Quadra { Nome = "Wallau 1", ClubeId = 7 },
                new Quadra { Nome = "Paralelo 1", ClubeId = 8 },
            },
            Array.Empty<Categoria>(),
            new Dictionary<int, string> { [7] = "Wallau", [8] = "Paralelo" });
        var doClube = Jogo("AoVivo", "10:00", "Wallau 1");
        var deOutro = Jogo("AoVivo", "10:00", "Paralelo 1");

        var quadro = TelaoDoTorneio.Montar(new[] { doClube, deOutro }, sedes, clubeId: 7);

        Assert.Equal(new[] { doClube }, quadro.AoVivo);
        Assert.Equal(2, TelaoDoTorneio.Montar(new[] { doClube, deOutro }, sedes, clubeId: null).AoVivo.Count);
    }

    // ── A porta: as mesmas da página Jogos ─────────────────────────────────────────

    [Fact]
    public async Task Abre_sem_login_e_traz_o_quadro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, 0);
        var view = Assert.IsType<ViewResult>(await controller.Telao(torneio.Id, null));

        Assert.IsType<TelaoDoTorneio.Quadro>(view.Model);
    }

    [Fact]
    public async Task Pedido_parcial_devolve_so_o_miolo_pra_atualizar_sem_recarregar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, 0).Telao(torneio.Id, null, parcial: true);

        Assert.IsType<PartialViewResult>(resultado);
    }

    [Fact]
    public async Task Torneio_oculto_nao_abre_pra_quem_nao_esta_logado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        torneio.Oculto = true;
        await ctx.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await TestInfra.NovoTorneiosController(ctx, 0).Telao(torneio.Id, null));
    }

    [Fact]
    public async Task Chave_esperando_aprovacao_nao_vai_pra_tv()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: AprovacaoDeChaves.Pendente);
        await ctx.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await TestInfra.NovoTorneiosController(ctx, 0).Telao(torneio.Id, null));
    }
}

// 🗣️ Felipe, com a foto da TV da Wallau (21/11/2026): *"aqui não tá aparecendo o sacador"*.
public class SaqueNoTelaoTests
{
    [Fact]
    public void So_a_dupla_que_saca_ganha_a_bolinha()
    {
        var jogo = new Padelizou.Models.Partida { Codigo = "J", Dupla1Id = 1, Dupla2Id = 2, DuplaSacandoId = 2 };

        Assert.False(Padelizou.Services.TelaoDoTorneio.Saca(jogo, 1));
        Assert.True(Padelizou.Services.TelaoDoTorneio.Saca(jogo, 2));
        Assert.False(Padelizou.Services.TelaoDoTorneio.Saca(new Padelizou.Models.Partida { Codigo = "J", Dupla1Id = 1, Dupla2Id = 2 }, 1));
    }

    [Fact]
    public void O_card_ao_vivo_do_telao_desenha_a_bolinha_nas_duas_linhas()
    {
        var pasta = AppContext.BaseDirectory;
        while (pasta != null && !File.Exists(Path.Combine(pasta, "Padelizou", "Padelizou.csproj")))
            pasta = Directory.GetParent(pasta)?.FullName;
        var miolo = File.ReadAllText(Path.Combine(pasta!, "Padelizou", "Views", "Torneios", "_TelaoConteudo.cshtml"));

        Assert.Contains("TelaoDoTorneio.Saca(p, p.Dupla1Id)", miolo);
        Assert.Contains("TelaoDoTorneio.Saca(p, p.Dupla2Id)", miolo);
    }
}
