using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using padelizou.Controllers;

namespace Padelizou.Tests;

// 10/09/2026 — O .ICS DO JOGADOR MANDAVA PRO PRÉDIO ERRADO NO "POR ORDEM".
//
// Achado pela revisão adversarial e confirmado com reprodução: sem quadra (é o modo do Er),
// `LocalDaPartida` caía em `LocalTorneio` = nome do clube PRINCIPAL — e todo jogo carimbado no
// Radar (Partida.ClubeId) ia pro Google/Apple Calendar com LOCATION "Er Padel". O carimbo
// existe desde o build-875 justamente pra dizer o clube quando não há quadra; o calendário
// era o leitor que não o lia.
public class IcsDizOClubeDoCarimboTests
{
    [Fact]
    public async Task No_por_ordem_o_jogo_carimbado_no_Radar_vai_pro_calendario_como_Radar()
    {
        using var ctx = TestInfra.NovoContexto();

        var token = Guid.NewGuid();
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Er Padel" });
        ctx.Clubes.Add(new Clube { Id = 2, Nome = "Radar" });
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Ana", Cpf = "1", AgendaFeedToken = token });
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Bia", Cpf = "2" });
        ctx.Jogadores.Add(new Jogador { Id = 3, Nome = "Carla", Cpf = "3" });
        ctx.Jogadores.Add(new Jogador { Id = 4, Nome = "Dani", Cpf = "4" });
        ctx.Torneios.Add(new Torneio
        {
            Id = 1, Nome = "2ª Etapa ER PADEL TOUR", Codigo = "ER2", ClubeId = 1, LocalTorneio = "Er Padel",
            SemHorarioPrevisto = true, UsaCheckIn = true, Status = "Fase de Grupos",
            DataInicio = new DateTime(2026, 9, 11), QuantidadeQuadras = 7
        });
        ctx.Quadras.Add(new Quadra { Id = 1, TorneioId = 1, Nome = "Arena 1", ClubeId = null });
        ctx.Quadras.Add(new Quadra { Id = 2, TorneioId = 1, Nome = "Radar 1", ClubeId = 2 });
        ctx.Categorias.Add(new Categoria { Id = 1, TorneioId = 1, Nome = "5ª Masculina", Codigo = "C5M", PodeJogarNaSedeExtra = true });
        ctx.Duplas.Add(new Dupla { Id = 1, CategoriaId = 1, Jogador1Id = 1, Jogador2Id = 2 });
        ctx.Duplas.Add(new Dupla { Id = 2, CategoriaId = 1, Jogador1Id = 3, Jogador2Id = 4 });
        ctx.Partidas.Add(new Partida
        {
            Id = 1, Codigo = "P1", CategoriaId = 1, Dupla1Id = 1, Dupla2Id = 2, Status = "Agendada", Fase = "Fase de Grupos",
            HorarioPrevisto = new DateTime(2026, 9, 12, 8, 0, 0), NomeQuadra = null, ClubeId = 2
        });
        ctx.SaveChanges();

        var controller = new AgendaController(ctx);
        var resultado = await controller.Feed(1, token);

        var conteudo = Assert.IsType<ContentResult>(resultado);
        var ics = conteudo.Content!;
        var location = ics.Split('\n').Select(l => l.TrimEnd('\r'))
            .Where(l => l.StartsWith("LOCATION:")).ToList();
        var daPartida = ics.Split("BEGIN:VEVENT").First(b => b.Contains("padelizou-partida-1@"));
        var locationDaPartida = daPartida.Split('\n').Select(l => l.TrimEnd('\r')).First(l => l.StartsWith("LOCATION:"));

        Assert.True(locationDaPartida.Contains("Radar"), $"LOCATION da partida saiu: [{locationDaPartida}]");
    }
}
