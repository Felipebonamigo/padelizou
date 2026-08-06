using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A FINAL DA CATEGORIA DE TIMES DO INTERNO SAIU COM UM TIME QUE NÃO VENCEU SEMIFINAL.
//
// O que estava gravado em produção (torneio 18, categoria "Times"):
//   Semifinal: CredHub 6 × 0 Kayser        → CredHub
//   Semifinal: ST Led  6 × 4 Target.it     → ST Led
//   Final:     CredHub × Valandro Gestão   ← a Valandro tinha caído na fase de grupos
// O ST Led, que ganhou a própria semifinal, sumiu do torneio.
//
// Duas causas, encaixadas uma na outra:
//
//  1. O grupo A terminou com TRÊS times empatados em 1 vitória e −2 de saldo, e a régua de
//     classificação parava no saldo. O 2º colocado saía então da ordem em que as duplas
//     chegavam na consulta — e cada chamador monta a consulta do seu jeito. A mesma tabela
//     dava respostas diferentes conforme quem perguntasse.
//
//  2. Quem "classificou e não tem jogo de mata-mata" é tratado como BYE (regra da chave
//     direta, onde duplas de verdade pulam a primeira rodada). Com a classificação instável,
//     o 2º do grupo A recalculado na hora do avanço era OUTRO time — que passou a parecer um
//     bye. Os que avançavam viraram três, e o pareamento (primeiro × último) descartou o
//     vencedor do meio.
//
// Os dois testes abaixo trancam as duas causas em separado, porque cada uma sozinha já
// estraga uma chave.
public class FinalDosTimesTests
{
    // Os números REAIS do grupo A dos times, no Interno de 05/08/2026.
    // CredHub vence tudo; Target.it, Valandro e Argentus fecham 1 vitória e −2 cada um.
    private static (DbPadelContext ctx, Categoria categoria, Dictionary<string, int> ids) MontarGrupoEmpatado()
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio { Nome = "Interno", Codigo = "INT18", Status = "Fase de Grupos" };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria
        {
            Nome = "Times", Codigo = "TIMES", Torneio = torneio,
            DeTimes = true, QuantidadeGrupos = 2, ClassificadosPorGrupo = 2,
        };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        var dono = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(dono);
        ctx.SaveChanges();

        // Time é uma Dupla com NomeTime; o Jogador1 é o organizador em todos eles.
        Dupla Time(string nome, string grupo)
        {
            var d = new Dupla { CategoriaId = categoria.Id, Jogador1Id = dono.Id, NomeTime = nome, Grupo = grupo };
            ctx.Duplas.Add(d);
            return d;
        }

        var target = Time("Target.it", "A");
        var valandro = Time("Valandro Gestão", "A");
        var credhub = Time("CredHub", "A");
        var argentus = Time("Argentus XP", "A");
        var protech = Time("Pro Tech", "B");
        var stled = Time("ST Led", "B");
        var malaga = Time("Málaga Seguros", "B");
        var kayser = Time("Kayser Imóveis", "B");
        ctx.SaveChanges();

        void Jogo(Dupla a, int ga, int gb, Dupla b, string grupo) =>
            ctx.Partidas.Add(new Partida
            {
                TorneioId = torneio.Id, CategoriaId = categoria.Id, Fase = $"Grupo {grupo}",
                Status = "Finalizada", Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
                Dupla1Id = a.Id, Dupla2Id = b.Id, GamesDupla1 = ga, GamesDupla2 = gb,
                VencedorId = ga > gb ? a.Id : b.Id,
            });

        Jogo(target, 4, 1, valandro, "A");
        Jogo(target, 2, 4, credhub, "A");
        Jogo(target, 1, 4, argentus, "A");
        Jogo(valandro, 1, 4, credhub, "A");
        Jogo(valandro, 4, 0, argentus, "A");
        Jogo(credhub, 4, 3, argentus, "A");

        Jogo(protech, 3, 4, stled, "B");
        Jogo(protech, 4, 2, malaga, "B");
        Jogo(protech, 3, 4, kayser, "B");
        Jogo(stled, 4, 3, malaga, "B");
        Jogo(stled, 4, 1, kayser, "B");
        Jogo(malaga, 3, 4, kayser, "B");
        ctx.SaveChanges();

        var ids = new Dictionary<string, int>
        {
            ["Target.it"] = target.Id, ["Valandro"] = valandro.Id, ["CredHub"] = credhub.Id,
            ["Argentus"] = argentus.Id, ["ProTech"] = protech.Id, ["ST Led"] = stled.Id,
            ["Málaga"] = malaga.Id, ["Kayser"] = kayser.Id,
        };
        return (ctx, categoria, ids);
    }

    // CAUSA 1. Empate triplo em vitórias E saldo: a régua tem que responder a MESMA coisa,
    // não importa em que ordem as duplas chegaram.
    [Fact]
    public void Empate_triplo_classifica_igual_em_qualquer_ordem_de_entrada()
    {
        var (ctx, _, ids) = MontarGrupoEmpatado();
        using var _fecha = ctx;
        var duplas = ctx.Duplas.ToList();
        var jogos = ctx.Partidas.ToList();

        var normal = ClassificacaoDeGrupos.Calcular(duplas, jogos, 2);
        var aoContrario = ClassificacaoDeGrupos.Calcular(Enumerable.Reverse(duplas).ToList(), jogos, 2);
        var porNome = ClassificacaoDeGrupos.Calcular(
            duplas.OrderBy(d => d.NomeTime, StringComparer.Ordinal).ToList(), jogos, 2);

        string Resumo(IEnumerable<ChaveamentoMataMata.Classificado> c) =>
            string.Join(", ", c.Select(x => $"{x.Grupo}{x.Posicao}={x.DuplaId}"));

        Assert.Equal(Resumo(normal), Resumo(aoContrario));
        Assert.Equal(Resumo(normal), Resumo(porNome));

        // E o desempate é o que a régua promete: games PRÓ desempata o saldo empatado.
        // Target.it fez 7 games, Argentus 7 e Valandro 6 — a Valandro é a que fica de fora.
        var doGrupoA = normal.Where(c => c.Grupo == "A").Select(c => c.DuplaId).ToList();
        Assert.Equal(new[] { ids["CredHub"], ids["Target.it"] }, doGrupoA);
        Assert.DoesNotContain(ids["Valandro"], doGrupoA);
    }

    // CAUSA 2. Quadro CHEIO não tem bye — nem que a classificação diga o contrário.
    //
    // Este teste planta a divergência de propósito: quem joga a semifinal NÃO é quem a
    // classificação aponta como 2º do grupo A. Antes, o time "classificado sem jogo" entrava
    // como bye e virava um terceiro semifinalista; agora a aritmética do quadro barra.
    [Fact]
    public async Task Quadro_cheio_nao_inventa_bye_mesmo_com_a_classificacao_divergindo()
    {
        var (ctx, categoria, ids) = MontarGrupoEmpatado();
        using var _ = ctx;

        // 4 classificados → 2 semifinais → 4 vagas. Quadro cheio.
        // De propósito, a Valandro entra na semifinal no lugar do 2º colocado de verdade.
        void Semifinal(int a, int b, int vencedor) =>
            ctx.Partidas.Add(new Partida
            {
                TorneioId = categoria.TorneioId, CategoriaId = categoria.Id, Fase = "Semifinal",
                Status = "Finalizada", Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
                Dupla1Id = a, Dupla2Id = b, GamesDupla1 = a == vencedor ? 6 : 0,
                GamesDupla2 = b == vencedor ? 6 : 0, VencedorId = vencedor,
            });

        Semifinal(ids["CredHub"], ids["Kayser"], ids["CredHub"]);
        Semifinal(ids["ST Led"], ids["Valandro"], ids["ST Led"]);
        await ctx.SaveChangesAsync();

        var byes = await AvancoDaChave.ByesDaCategoriaAsync(ctx, categoria.Id);
        Assert.Empty(byes);

        // E, por consequência, a final é entre os DOIS vencedores de semifinal — nada de um
        // terceiro nome aparecendo do nada.
        var avancam = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, "Semifinal");
        Assert.Equal(new[] { ids["CredHub"], ids["ST Led"] }.OrderBy(x => x),
                     avancam.OrderBy(x => x));
    }

    // A chave direta continua tendo bye de verdade: 24 duplas num quadro de 32 são 8 jogos
    // (16 vagas) e 8 que descansam. A trava nova é aritmética, então não pode cortar isto.
    [Fact]
    public async Task Chave_direta_com_vaga_sobrando_continua_tendo_bye()
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio { Nome = "Interno", Codigo = "INT18" };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria
        {
            Nome = "Mata-Mata Geral", Codigo = "MMG", Torneio = torneio, ChaveDireta = true,
        };
        ctx.Categorias.Add(categoria);
        var dono = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(dono);
        ctx.SaveChanges();

        var duplas = new List<Dupla>();
        for (int i = 0; i < 24; i++)
        {
            var j1 = TestInfra.NovoJogador(i * 2 + 1);
            var j2 = TestInfra.NovoJogador(i * 2 + 2);
            ctx.Jogadores.AddRange(j1, j2);
            var d = new Dupla { CategoriaId = categoria.Id, Jogador1 = j1, Jogador2 = j2 };
            duplas.Add(d);
            ctx.Duplas.Add(d);
        }
        ctx.SaveChanges();

        // Primeira rodada: 8 jogos com as 16 primeiras duplas. As outras 8 descansam.
        for (int i = 0; i < 8; i++)
            ctx.Partidas.Add(new Partida
            {
                TorneioId = torneio.Id, CategoriaId = categoria.Id,
                Fase = ChaveamentoMataMata.PrimeiraRodada, Status = "Finalizada",
                Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
                Dupla1Id = duplas[i * 2].Id, Dupla2Id = duplas[i * 2 + 1].Id,
                GamesDupla1 = 4, GamesDupla2 = 1, VencedorId = duplas[i * 2].Id,
            });
        await ctx.SaveChangesAsync();

        var byes = await AvancoDaChave.ByesDaCategoriaAsync(ctx, categoria.Id);
        Assert.Equal(8, byes.Count);

        // 8 vencedores + 8 byes = as 16 duplas das oitavas.
        var avancam = await AvancoDaChave.QuemAvancaAsync(ctx, categoria.Id, ChaveamentoMataMata.PrimeiraRodada);
        Assert.Equal(16, avancam.Count);
        Assert.Equal(16, avancam.Distinct().Count());
    }
}
