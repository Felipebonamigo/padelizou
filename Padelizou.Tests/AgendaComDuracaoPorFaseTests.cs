using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using padelizou.Controllers;

namespace Padelizou.Tests;

// O evento do calendário do jogador dura o que a FASE dura (19/11/2026, Services/DuracaoDaPartida):
// a final de 80 min não pode terminar no Google Calendar aos 50.
public class AgendaComDuracaoPorFaseTests
{
    [Fact]
    public async Task A_final_longa_vai_pro_calendario_com_a_duracao_dela()
    {
        using var ctx = TestInfra.NovoContexto();
        var token = Guid.NewGuid();
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Wallau" });
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Ana", Cpf = "1", AgendaFeedToken = token });
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Bia", Cpf = "2" });
        ctx.Jogadores.Add(new Jogador { Id = 3, Nome = "Carla", Cpf = "3" });
        ctx.Jogadores.Add(new Jogador { Id = 4, Nome = "Dani", Cpf = "4" });
        ctx.Torneios.Add(new Torneio
        {
            Id = 1, Nome = "Wallau", Codigo = "W3", ClubeId = 1, LocalTorneio = "Wallau", Status = "Fase de Grupos",
            DataInicio = new DateTime(2026, 11, 21), QuantidadeQuadras = 2,
            TempoPrevistoPartidaMinutos = 50, TempoFinalMinutos = 80,
        });
        ctx.Categorias.Add(new Categoria { Id = 1, TorneioId = 1, Nome = "3ª", Codigo = "C3" });
        ctx.Duplas.Add(new Dupla { Id = 1, CategoriaId = 1, Jogador1Id = 1, Jogador2Id = 2 });
        ctx.Duplas.Add(new Dupla { Id = 2, CategoriaId = 1, Jogador1Id = 3, Jogador2Id = 4 });
        ctx.Partidas.Add(new Partida
        {
            Id = 1, Codigo = "F1", CategoriaId = 1, TorneioId = 1, Dupla1Id = 1, Dupla2Id = 2, Status = "Agendada", Fase = "Final",
            HorarioPrevisto = new DateTime(2026, 11, 21, 18, 0, 0), NomeQuadra = "Quadra 1",
        });
        ctx.SaveChanges();

        var ics = Assert.IsType<ContentResult>(await new AgendaController(ctx).Feed(1, token)).Content!;
        var evento = ics.Split("BEGIN:VEVENT").First(b => b.Contains("padelizou-partida-1@"));

        DateTime Ler(string campo) => DateTime.ParseExact(
            evento.Split('\n').Select(l => l.TrimEnd('\r')).First(l => l.StartsWith(campo + ":"))[(campo.Length + 1)..],
            "yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(TimeSpan.FromMinutes(80), Ler("DTEND") - Ler("DTSTART"));
    }
}
