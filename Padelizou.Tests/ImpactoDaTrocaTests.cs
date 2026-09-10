using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O QUE ESTA TROCA VAI FAZER COM O CONFERIR GRADE — antes de apertar Trocar.
//
// 🗣️ Felipe, 10/09/2026, no modal de trocar horário do Er: *"veja para avisar se o jogo q eu trocar
// altera algo do 'conferir grade', por exemplo, se vai atrapalhar o impedimento, restrição ou jogos
// seguidos"*.
//
// A régua é a MESMA do Conferir grade e a MESMA do reparo (AuditoriaDaGrade + os pesos de
// ReparoDaGrade): três telas dizendo coisas diferentes sobre a mesma troca seria pior que não
// dizer nada. Aqui só se faz a conta duas vezes — com e sem a troca — e se compara.
public class ImpactoDaTrocaTests
{
    private static readonly DateTime Sexta = new(2026, 10, 9);
    private static readonly DateTime Sabado = new(2026, 10, 10);

    private static Torneio Torneio() => new()
    {
        Id = 1, Nome = "T", Codigo = "T1",
        DataInicio = Sexta.AddHours(18), DataFim = Sabado.AddDays(1),
        QuantidadeQuadras = 2, TempoPrevistoPartidaMinutos = 50,
        HoraInicioDoDia = new TimeSpan(18, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
    };

    private static readonly Categoria Cat = new() { Id = 1, Nome = "3ª Categoria Masculina", Codigo = "C3" };

    private static Dupla Dupla(int id, int j1, int j2) =>
        new() { Id = id, Jogador1Id = j1, Jogador2Id = j2, Categoria = Cat };

    private static Partida Jogo(int id, int d1, int d2, DateTime quando) =>
        new()
        {
            Id = id, TorneioId = 1, Codigo = $"J{id}", Status = "Agendada",
            Fase = "Grupo A", CategoriaId = 1, Categoria = Cat,
            Dupla1Id = d1, Dupla2Id = d2, HorarioPrevisto = quando,
        };

    [Fact]
    public void Avisa_quando_a_troca_joga_alguem_dentro_do_impedimento()
    {
        var impedida = Dupla(1, 10, 11);
        impedida.ImpedimentoSextaNoite = true;
        var duplas = new[] { impedida, Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };

        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sabado.AddHours(9)),          // a impedida, no lugar certo
            Jogo(2, 3, 4, Sexta.AddHours(20)),          // sexta à noite, livre
        };

        var impacto = ImpactoDaTroca.Avaliar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma,
            jogos[0], jogos[1]);

        Assert.Equal(ImpactoDaTroca.Nivel.Perigo, impacto.Grau);
        Assert.Contains("Impedimento", impacto.Texto);
    }

    [Fact]
    public void Diz_quando_a_troca_melhora_a_grade()
    {
        var impedida = Dupla(1, 10, 11);
        impedida.ImpedimentoSextaNoite = true;
        var duplas = new[] { impedida, Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };

        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sexta.AddHours(20)),          // errado: dentro do impedimento
            Jogo(2, 3, 4, Sabado.AddHours(9)),
        };

        var impacto = ImpactoDaTroca.Avaliar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma,
            jogos[0], jogos[1]);

        Assert.Equal(ImpactoDaTroca.Nivel.Melhora, impacto.Grau);
        Assert.Contains("Impedimento", impacto.Texto);
    }

    [Fact]
    public void Diz_quando_nada_muda()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };
        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sabado.AddHours(9)),
            Jogo(2, 3, 4, Sabado.AddHours(14)),
        };

        var impacto = ImpactoDaTroca.Avaliar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma,
            jogos[0], jogos[1]);

        Assert.Equal(ImpactoDaTroca.Nivel.Igual, impacto.Grau);
    }

    // Jogos seguidos é MOLE: avisa, mas não é o alarme vermelho do impedimento.
    //
    // ⚠️ O 09:50 é de duplas que não repetem em lugar nenhum, de propósito. Na primeira versão
    // deste teste ele era da dupla 2 (que já jogava 09:00) — e aí a troca TIRAVA um par colado e
    // PUNHA outro: a contagem não mudava e o impacto era "Igual", corretamente. A conta é de
    // quantos achados existem, não de quem os causou.
    [Fact]
    public void Jogo_seguido_novo_e_atencao_e_nao_perigo()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 30, 31),
                             Dupla(4, 40, 41), Dupla(5, 50, 51) };
        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sabado.AddHours(9)),
            Jogo(2, 1, 3, Sabado.AddHours(15)),                       // a dupla 1 de novo, longe
            Jogo(3, 4, 5, Sabado.AddHours(9).AddMinutes(50)),         // ninguém repetido
        };

        // Trocar o das 15h com o das 09:50 põe a dupla 1 em dois jogos colados — e não desfaz
        // nenhum, porque o 09:50 não tinha ninguém repetido.
        var impacto = ImpactoDaTroca.Avaliar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma,
            jogos[1], jogos[2]);

        Assert.Equal(ImpactoDaTroca.Nivel.Atencao, impacto.Grau);
        Assert.Contains("Jogos seguidos", impacto.Texto);
    }

    // A troca que o próprio sistema recusa (categoria presa em casa) não vira "impacto": o motivo
    // vem de TrocaDeHorario, que é quem manda. Aqui só se diz que não dá.
    [Fact]
    public void Troca_recusada_pela_regra_do_clube_e_impossivel()
    {
        var terceira = new Categoria { Id = 3, Nome = "3ª Masculina", Codigo = "3M", PodeJogarNaSedeExtra = false };
        var sedes = SedesDoTorneio.Montar(1, 0,
            new[]
            {
                new Quadra { Nome = "Casa", ClubeId = 1 },
                new Quadra { Nome = "Alugada", ClubeId = 2 },
            },
            new[] { terceira },
            new Dictionary<int, string> { [1] = "Er Padel", [2] = "Radar" });

        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };
        var daTerceira = Jogo(1, 1, 2, Sabado.AddHours(9));
        daTerceira.CategoriaId = 3;
        daTerceira.Categoria = terceira;
        daTerceira.NomeQuadra = "Casa";
        var noRadar = Jogo(2, 3, 4, Sabado.AddHours(14));
        noRadar.NomeQuadra = "Alugada";

        var impacto = ImpactoDaTroca.Avaliar(Torneio(), new List<Partida> { daTerceira, noRadar },
            duplas, sedes, daTerceira, noRadar);

        Assert.Equal(ImpactoDaTroca.Nivel.Impossivel, impacto.Grau);
        Assert.Contains("Radar", impacto.Texto);
    }

    // ⚠️ A conta é feita EM MEMÓRIA e desfeita: prever não pode mexer na grade.
    [Fact]
    public void Prever_nao_mexe_na_grade()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };
        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sabado.AddHours(9)),
            Jogo(2, 3, 4, Sabado.AddHours(14)),
        };

        ImpactoDaTroca.Avaliar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma, jogos[0], jogos[1]);

        Assert.Equal(Sabado.AddHours(9), jogos[0].HorarioPrevisto);
        Assert.Equal(Sabado.AddHours(14), jogos[1].HorarioPrevisto);
    }

    // ── A FIAÇÃO ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_nao_organiza_nao_pergunta_o_impacto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000088" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .ImpactoDaTroca(torneio.Id, "1", "2");

        Assert.IsType<ForbidResult>(resultado);
    }

    // A prévia responde honestamente que não dá pra saber — e não "nada muda".
    [Fact]
    public async Task Jogo_previsto_diz_que_so_da_pra_conferir_depois()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        var umJogo = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);

        var previsto = ReferenciaDoJogo.Prevista(categoria.Id, "Final", 1).ToString();
        var resultado = await controller.ImpactoDaTroca(torneio.Id, umJogo.Id.ToString(), previsto);

        var json = Assert.IsType<JsonResult>(resultado);
        Assert.Contains("previa", System.Text.Json.JsonSerializer.Serialize(json.Value));
    }

    // A tela pergunta pela rota certa e tem onde pôr a resposta.
    [Fact]
    public void O_modal_pergunta_o_impacto_e_tem_onde_mostrar()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_JogosDoTorneio.cshtml"));

        Assert.Contains("data-impacto-url", fonte);
        Assert.Contains("id=\"trocaImpacto\"", fonte);
        // A lista enxuta: dia no optgroup, nome e categoria curtos.
        Assert.Contains("<optgroup", fonte);
        Assert.Contains("NomeDaDupla.CompactoNa", fonte);
        Assert.Contains("CategoriaNaTela.Curto", fonte);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
