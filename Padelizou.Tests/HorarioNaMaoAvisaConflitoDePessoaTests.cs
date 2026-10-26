using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// 26/10/2026 — A HORA DIGITADA NA MÃO TEM QUE AVISAR QUANDO PÕE ALGUÉM EM DOIS JOGOS.
//
// 🗣️ Um organizador, depois de um torneio de outro sistema: *"regra pra evitar 2 jogos de um
// mesmo atleta em 2 categorias diferentes em horarios iguais, nas fases de mata-mata? [...]
// atrasou uma cacetada de jogos ontem por conta disso"*. A grade automática já compara por
// PESSOA entre categorias (GradeDeJogos), e a troca ⇄ e as setas já medem o Conferir grade antes
// e depois (ImpactoDaTroca). A brecha era a hora DIGITADA (HorarioNaMao): gravava calada, e o
// conflito só aparecia se alguém lembrasse de apertar Conferir grade. 🗣️ Felipe: *"tem que avisar
// do conflito"*.
//
// AVISA, não bloqueia: quem organiza pode estar resolvendo outra coisa no mesmo movimento (a
// outra partida vai mudar logo depois). Mas o aviso diz QUEM e ONDE — "Mesma pessoa em dois jogos
// (+1)" sozinho mandaria o organizador procurar na lista.
public class HorarioNaMaoAvisaConflitoDePessoaTests
{
    private static readonly DateTime Sexta = new(2026, 9, 11, 18, 0, 0);
    private static DateTime NaSexta(int hora) => Sexta.Date.AddHours(hora);
    private static DateTime NoSabado(int hora) => Sexta.Date.AddDays(1).AddHours(hora);

    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Jogador Organizador,
        Partida NaQuinta, Partida NaMista);

    // Rafael joga a 5ª Masculina E a Mista B — duas duplas diferentes, a mesma pessoa.
    private static Cenario Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var clube = new Clube { Nome = "Chakra" };
        ctx.Clubes.Add(clube);
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000098" };
        var rafael = new Jogador { Nome = "Rafael Paim", Cpf = "99900000097" };
        ctx.Jogadores.AddRange(organizador, rafael);
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "The Last Dance", Codigo = "TLD", Status = "Fase de Grupos", ClubeId = clube.Id,
            DataInicio = Sexta, DataFim = Sexta.Date.AddDays(2),
            HoraInicioDoDia = new TimeSpan(18, 0, 0), HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0), QuantidadeQuadras = 2, TempoPrevistoPartidaMinutos = 50,
            // "Por ordem": sem quadra, então a recusa de quadra ocupada não entra no caminho.
            SemHorarioPrevisto = true,
        };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        var quinta = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "C5M", Torneio = torneio };
        var mista = new Categoria { Nome = "Categoria Mista B", Codigo = "MISTA-B", Torneio = torneio };
        ctx.Categorias.AddRange(quinta, mista);
        ctx.SaveChanges();

        int proximo = 1;
        Dupla Dupla(Categoria categoria, Jogador? com = null)
        {
            var j1 = com ?? TestInfra.NovoJogador(proximo++);
            var j2 = TestInfra.NovoJogador(proximo++);
            if (com == null) ctx.Jogadores.Add(j1);
            ctx.Jogadores.Add(j2);
            var dupla = new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 };
            ctx.Duplas.Add(dupla);
            return dupla;
        }
        Partida Jogo(Categoria categoria, string codigo, DateTime hora, Dupla d1) => new()
        {
            TorneioId = torneio.Id, Categoria = categoria, Dupla1 = d1, Dupla2 = Dupla(categoria),
            Codigo = codigo, Status = "Agendada", Fase = "Semifinal",
            HorarioPrevisto = hora, ClubeId = clube.Id,
        };

        var naQuinta = Jogo(quinta, "Q5", NaSexta(20), Dupla(quinta, rafael));
        var naMista = Jogo(mista, "MB", NoSabado(10), Dupla(mista, rafael));
        ctx.Partidas.AddRange(naQuinta, naMista);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return new Cenario(ctx, torneio, organizador, naQuinta, naMista);
    }

    private static Padelizou.Controllers.TorneiosController Controller(Cenario c)
    {
        c.Ctx.ChangeTracker.Clear();
        return TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id);
    }

    [Fact]
    public async Task Por_alguem_em_dois_jogos_no_mesmo_horario_grava_e_avisa_quem_e()
    {
        var c = Montar();
        var controller = Controller(c);

        // A Mista do Rafael vai pra sexta 20h — a mesma hora da semifinal dele na 5ª.
        await controller.DefinirHorario(c.Torneio.Id, c.NaMista.Id.ToString(), NaSexta(20));

        // Grava: é aviso, não trava.
        c.Ctx.ChangeTracker.Clear();
        var mista = await c.Ctx.Partidas.SingleAsync(p => p.Id == c.NaMista.Id);
        Assert.Equal(NaSexta(20), mista.HorarioPrevisto);

        var mensagem = controller.TempData["Sucesso"]?.ToString() ?? "";
        Assert.Contains("⚠️", mensagem);
        Assert.Contains(Padelizou.Services.AuditoriaDaGrade.PessoaEmDoisJogos, mensagem);
        // E diz QUEM: sem o nome, o organizador teria que achar o conflito na lista.
        Assert.Contains("Rafael", mensagem);
    }

    [Fact]
    public async Task Hora_livre_nao_avisa_nada()
    {
        var c = Montar();
        var controller = Controller(c);

        // Sábado 14h: longe da sexta 20h, sem conflito nem jogos seguidos.
        await controller.DefinirHorario(c.Torneio.Id, c.NaMista.Id.ToString(), NoSabado(14));

        var mensagem = controller.TempData["Sucesso"]?.ToString() ?? "";
        Assert.StartsWith("Agora", mensagem);
        Assert.DoesNotContain("⚠️", mensagem);
        Assert.DoesNotContain(Padelizou.Services.AuditoriaDaGrade.PessoaEmDoisJogos, mensagem);
    }
}
