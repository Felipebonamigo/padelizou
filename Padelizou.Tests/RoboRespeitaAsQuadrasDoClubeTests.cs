using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O ROBÔ RESPEITA AS QUADRAS DO CLUBE NO "POR ORDEM DE LIBERAÇÃO" (10/09/2026, ensaio C, achado C6).
//
// No "por ordem" (Torneio.SemHorarioPrevisto) todo jogo marcado tem HORA e CLUBE carimbado
// (Partida.ClubeId), e a QUADRA fica nula — quem a dá é o balcão do check-in, conforme vaga. O
// encaixe (GradeDeJogos.Encaixar) contava a ocupação das quadras por NOME, então um jogo já marcado
// sem quadra não bloqueava quadra nenhuma: quando o robô criava a rodada seguinte, achava as cinco
// Arenas livres num horário em que já havia cinco jogos "Er Padel" — e carimbava a sexta e a sétima
// no mesmo clube, com o Radar aberto e VAZIO ao lado. No ensaio de um dia inteiro jogado pela app:
// 11:20 de sábado com 7 jogos Er Padel / 0 Radar, pra 5 Arenas; e 13:00 com 7 Er Padel, com o Radar
// já fechado — 7 jogos pra 5 quadras.
//
// A régua: num horário, o número de jogos carimbados num clube nunca passa do número de quadras
// ABERTAS daquele clube — contando os já marcados sem quadra pelo clube carimbado. O que não cabe
// vai pro outro clube aberto (se a categoria pode) ou pro horário seguinte — que é o que o encaixe
// já faz assim que enxerga a ocupação.
//
// Chama `AgendarNaGradeAsync` direto, com os jogos já marcados montados à mão: é o único jeito de
// pôr exatamente cinco jogos sem quadra num horário e ver o que a rodada nova faz com ele. O
// caminho real (GerarChaves + finalizar grupos) distribui os grupos pela grade e não deixa escolher
// o horário cheio.
public class RoboRespeitaAsQuadrasDoClubeTests
{
    private static readonly DateTime Sexta = new(2026, 9, 11, 18, 0, 0);
    private static readonly DateTime Sabado = new(2026, 9, 12);

    private static DateTime As(string hora) => DateTime.Parse($"2026-09-12 {hora}");

    private sealed class Cenario
    {
        public required DbPadelContext Ctx { get; init; }
        public required Torneio Torneio { get; init; }
        public required Clube Er { get; init; }
        public required Clube Radar { get; init; }
        public required Categoria Presa { get; init; }   // 3ª: não vai pro Radar
        public required Categoria Livre { get; init; }   // 5ª: pode ir pro Radar
        public int ProximoJogador;
    }

    // O Er, no que importa aqui: 5 Arenas o fim de semana todo, 2 quadras no Radar só no sábado
    // das 08:00 às 12:10, uma categoria presa em casa e uma que pode transbordar. Por ordem.
    private static Cenario MontarOEr()
    {
        var ctx = TestInfra.NovoContexto();

        var er = new Clube { Nome = "Er Padel" };
        var radar = new Clube { Nome = "Radar" };
        ctx.Clubes.AddRange(er, radar);
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "2ª Etapa ER PADEL TOUR", Codigo = "EPT2",
            Status = "Fase de Grupos",
            ClubeId = er.Id,
            DataInicio = Sexta, DataFim = Sabado.AddDays(1),
            HoraInicioDoDia = new TimeSpan(18, 0, 0),
            HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            QuantidadeQuadras = 7,
            TempoPrevistoPartidaMinutos = 50,
            SemHorarioPrevisto = true,
        };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();

        for (int i = 1; i <= 5; i++)
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = $"Arena {i}", ClubeId = er.Id });
        for (int i = 1; i <= 2; i++)
            ctx.Quadras.Add(new Quadra
            {
                TorneioId = torneio.Id, Nome = $"Radar {i}", ClubeId = radar.Id,
                DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12).AddMinutes(10),
            });

        var presa = new Categoria { Nome = "3ª Masculina", Codigo = "C3M", Torneio = torneio, PodeJogarNaSedeExtra = false };
        var livre = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", Torneio = torneio, PodeJogarNaSedeExtra = true };
        ctx.Categorias.AddRange(presa, livre);
        ctx.SaveChanges();

        return new Cenario { Ctx = ctx, Torneio = torneio, Er = er, Radar = radar, Presa = presa, Livre = livre };
    }

    private static Dupla NovaDupla(Cenario c, Categoria categoria)
    {
        var j1 = TestInfra.NovoJogador(++c.ProximoJogador);
        var j2 = TestInfra.NovoJogador(++c.ProximoJogador);
        c.Ctx.Jogadores.AddRange(j1, j2);
        var dupla = new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 };
        c.Ctx.Duplas.Add(dupla);
        c.Ctx.SaveChanges();
        return dupla;
    }

    private static Partida Jogo(Cenario c, Categoria categoria, string fase) => new()
    {
        TorneioId = c.Torneio.Id,
        CategoriaId = categoria.Id,
        Dupla1Id = NovaDupla(c, categoria).Id,
        Dupla2Id = NovaDupla(c, categoria).Id,
        Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
        Status = "Agendada",
        Fase = fase,
    };

    // `quantos` jogos JÁ MARCADOS neste horário, do jeito que o "por ordem" os grava: com hora, com o
    // clube carimbado e SEM quadra.
    private static void JaMarcados(Cenario c, Categoria categoria, string fase, string hora, int quantos,
        string status = "Agendada")
    {
        for (int i = 0; i < quantos; i++)
        {
            var jogo = Jogo(c, categoria, fase);
            jogo.Status = status;
            jogo.HorarioPrevisto = As(hora);
            jogo.NomeQuadra = null;
            jogo.ClubeId = c.Er.Id;
            c.Ctx.Partidas.Add(jogo);
        }
        c.Ctx.SaveChanges();
    }

    // A rodada que o robô está criando: sem hora, sem quadra, sem clube — ainda fora do banco,
    // exatamente como AvancarFaseAsync a entrega ao AgendarNaGradeAsync.
    private static List<Partida> RodadaNova(Cenario c, Categoria categoria, string fase, int quantos) =>
        Enumerable.Range(0, quantos).Select(_ => Jogo(c, categoria, fase)).ToList();

    private static async Task AgendarAsync(Cenario c, List<Partida> novos)
    {
        c.Ctx.ChangeTracker.Clear();
        await new RoboDoChaveamento(c.Ctx).AgendarNaGradeAsync(novos, c.Torneio.Id);
        c.Ctx.Partidas.AddRange(novos);
        await c.Ctx.SaveChangesAsync();
    }

    private static string Descrever(Cenario c, IEnumerable<Partida> jogos) =>
        string.Join(", ", jogos.Select(j =>
            $"{j.Fase} {j.HorarioPrevisto:dd HH:mm} · " +
            (j.ClubeId == c.Er.Id ? "Er Padel" : j.ClubeId == c.Radar.Id ? "Radar" : $"clube {j.ClubeId?.ToString() ?? "?"}")));

    private static async Task<List<Partida>> NoHorarioAsync(Cenario c, string hora) =>
        await c.Ctx.Partidas
            .Where(p => p.TorneioId == c.Torneio.Id && p.HorarioPrevisto == As(hora))
            .ToListAsync();

    // A QUADRA QUE O BALCÃO DEU É DECISÃO DO BALCÃO (10/09/2026, revisão adversarial do ensaio).
    //
    // No "por ordem" o jogo "Agendada" que JÁ TEM quadra foi chamado pelo balcão: as duplas estão
    // caminhando pra quadra. O reencaixe dos "fora de ordem" (uma rodada de posto menor nascendo
    // depois) zerava hora e quadra desse jogo e o empurrava pra depois — a Final da 7ª, chamada
    // pra Arena 3 às 11:20, sumia da quadra no minuto em que a Semifinal da 3ª nascia. Jogo com
    // quadra no por ordem fica onde está, como o jogo em quadra.
    [Fact]
    public async Task No_por_ordem_o_jogo_agendado_que_ja_tem_quadra_do_balcao_nao_e_reencaixado()
    {
        var c = MontarOEr();

        JaMarcados(c, c.Presa, "Grupo A", "10:30", 2, status: "Finalizada");
        JaMarcados(c, c.Livre, "Grupo A", "10:30", 3, status: "Finalizada");
        JaMarcados(c, c.Livre, "Semifinal", "10:30", 2, status: "Finalizada");

        // A Final da categoria livre (posto maior), chamada pelo balcão pra Arena 3 às 11:20.
        var final = Jogo(c, c.Livre, "Final");
        final.HorarioPrevisto = As("11:20");
        final.NomeQuadra = "Arena 3";
        final.ClubeId = c.Er.Id;
        c.Ctx.Partidas.Add(final);
        await c.Ctx.SaveChangesAsync();

        // Nasce a Semifinal da presa (posto menor): a Final acima está "fora de ordem".
        var novos = RodadaNova(c, c.Presa, "Semifinal", 2);
        await AgendarAsync(c, novos);

        var depois = await c.Ctx.Partidas.SingleAsync(p => p.Id == final.Id);
        Assert.Equal(As("11:20"), depois.HorarioPrevisto);
        Assert.Equal("Arena 3", depois.NomeQuadra);
    }

    // O caso do ensaio: sábado 11:20, cinco jogos "Er Padel" já marcados sem quadra, Radar aberto e
    // vazio. A rodada nova de uma categoria que PODE ir pro Radar tem que ir pra lá — e não virar a
    // sexta e a sétima "Arena".
    [Fact]
    public async Task A_rodada_nova_nao_carimba_mais_jogos_no_Er_do_que_Arenas_e_usa_o_Radar_aberto()
    {
        var c = MontarOEr();

        // Os grupos das duas categorias acabam 10:30: a barreira de posto é 10:30 e o piso da 5ª
        // (uma rodada depois do último jogo de grupo dela) é 11:20 — a rodada nova abre lá.
        JaMarcados(c, c.Presa, "Grupo A", "10:30", 2, status: "Finalizada");
        JaMarcados(c, c.Livre, "Grupo A", "10:30", 3, status: "Finalizada");
        // Às 11:20, cinco jogos já marcados no Er Padel — todos sem quadra, como manda o por ordem.
        JaMarcados(c, c.Presa, "Quartas de Final", "11:20", 5);

        var novos = RodadaNova(c, c.Livre, "Quartas de Final", 3);
        await AgendarAsync(c, novos);

        var as1120 = await NoHorarioAsync(c, "11:20");
        int noEr = as1120.Count(p => p.ClubeId == c.Er.Id);
        Assert.True(noEr <= 5,
            $"{noEr} jogos carimbados Er Padel às 11:20, e o Er tem 5 Arenas. A rodada nova saiu: {Descrever(c, novos)}");

        // O que não coube no Er foi pro Radar, que estava aberto e vazio — duas quadras, dois jogos.
        Assert.Equal(2, as1120.Count(p => p.ClubeId == c.Radar.Id));
        // E o terceiro, pro horário seguinte, com clube e sem quadra, como todo jogo por ordem.
        var terceiro = Assert.Single(novos, j => j.HorarioPrevisto != As("11:20"));
        Assert.Equal(As("12:10"), terceiro.HorarioPrevisto);
        Assert.All(novos, j =>
        {
            Assert.NotNull(j.HorarioPrevisto);
            Assert.Null(j.NomeQuadra);
            Assert.NotNull(j.ClubeId);
        });
    }

    // A categoria PRESA em casa não pode ir pro Radar: com as cinco Arenas tomadas às 11:20, a rodada
    // dela espera o horário seguinte em vez de virar a sexta Arena.
    [Fact]
    public async Task A_categoria_presa_em_casa_espera_o_horario_seguinte_em_vez_de_virar_a_sexta_Arena()
    {
        var c = MontarOEr();

        JaMarcados(c, c.Presa, "Grupo A", "10:30", 2, status: "Finalizada");
        JaMarcados(c, c.Livre, "Grupo A", "10:30", 3, status: "Finalizada");
        JaMarcados(c, c.Livre, "Quartas de Final", "11:20", 5);

        var novos = RodadaNova(c, c.Presa, "Quartas de Final", 3);
        await AgendarAsync(c, novos);

        var as1120 = await NoHorarioAsync(c, "11:20");
        int noEr = as1120.Count(p => p.ClubeId == c.Er.Id);
        Assert.True(noEr <= 5,
            $"{noEr} jogos carimbados Er Padel às 11:20, e o Er tem 5 Arenas. A rodada nova saiu: {Descrever(c, novos)}");

        Assert.All(novos, j =>
        {
            Assert.Equal(As("12:10"), j.HorarioPrevisto);
            Assert.Equal(c.Er.Id, j.ClubeId);   // presa em casa: nunca no Radar
            Assert.Null(j.NomeQuadra);
        });
    }

    // Com o Radar FECHADO (13:00 de sábado), só existem cinco quadras — e três jogos já marcados sem
    // quadra são três Arenas tomadas. A rodada de três não cabe inteira: dois entram e o terceiro vai
    // pras 13:50. No ensaio saíram 7 jogos "Er Padel" às 13:00.
    [Fact]
    public async Task Com_o_Radar_fechado_o_Er_nunca_passa_de_cinco_jogos_por_horario()
    {
        var c = MontarOEr();

        JaMarcados(c, c.Presa, "Grupo A", "12:10", 2, status: "Finalizada");
        JaMarcados(c, c.Livre, "Grupo A", "12:10", 3, status: "Finalizada");
        JaMarcados(c, c.Presa, "Quartas de Final", "13:00", 3);

        var novos = RodadaNova(c, c.Livre, "Quartas de Final", 3);
        await AgendarAsync(c, novos);

        var as1300 = await NoHorarioAsync(c, "13:00");
        int noEr = as1300.Count(p => p.ClubeId == c.Er.Id);
        Assert.True(noEr <= 5,
            $"{noEr} jogos carimbados Er Padel às 13:00, e o Er tem 5 Arenas (Radar fechado). A rodada nova saiu: {Descrever(c, novos)}");

        Assert.DoesNotContain(novos, j => j.ClubeId == c.Radar.Id);   // fechado é fechado
        Assert.Equal(2, novos.Count(j => j.HorarioPrevisto == As("13:00")));
        var terceiro = Assert.Single(novos, j => j.HorarioPrevisto != As("13:00"));
        Assert.Equal(As("13:50"), terceiro.HorarioPrevisto);
        Assert.All(novos, j => Assert.Equal(c.Er.Id, j.ClubeId));
    }
}
