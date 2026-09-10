using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A ORDEM DAS FASES É DO TORNEIO, NÃO DA CATEGORIA.
//
// 🗣️ Felipe, 09/09/2026, num print da grade do Er em `dev`: *"como que aqui tem jogo de chave e
// nas outras categorias tem final? o torneio tem q seguir uma ordem, primeiro todas as chaves,
// depois todas as primeiras eliminatorias (decimas > oitavas > quartas > semi > final) a ideia e
// fazer as finais de cada categorias ser os ultimos jogos do torneio"*.
//
// O print mostrava a Final da 3ª Feminina e da 6ª Feminina às 22:10 de 12/09, e jogos de GRUPO
// da 6ª Masculina em 15/09 — três dias DEPOIS das finais.
//
// ⚠️ A REGRA CEDE NUMA COISA SÓ, e foi ele quem disse: *"a menos que fique horario vazio, mas a
// ordem é colocar todos jogos de chave antes"*. Por isso as afirmações daqui são `>=` e não `>`
// no limite entre dois postos: o primeiro jogo de um posto pode dividir o HORÁRIO com o último
// do posto anterior, ocupando a quadra que sobraria vazia naquele minuto. O que não pode é vir
// ANTES dele.
public class OrdemDasFasesNoTorneioTests
{
    [Fact]
    public async Task Nenhuma_eliminatoria_comeca_antes_do_ultimo_jogo_de_grupo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarTorneioDeTresTamanhos(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);
        await JogarOTorneioInteiroAsync(ctx, controller, torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();

        var ultimoDeGrupo = jogos
            .Where(j => FasesTorneio.EhFaseDeGrupos(j.Fase))
            .Max(j => j.HorarioPrevisto)!.Value;

        var eliminatorias = jogos.Where(j => !FasesTorneio.EhFaseDeGrupos(j.Fase)).ToList();
        Assert.NotEmpty(eliminatorias);

        var cedoDemais = eliminatorias
            .Where(j => j.HorarioPrevisto < ultimoDeGrupo)
            .Select(j => $"{j.Fase} (cat {j.CategoriaId}) às {j.HorarioPrevisto:dd/MM HH:mm}")
            .ToList();

        Assert.True(cedoDemais.Count == 0,
            $"a fase de grupos só acaba {ultimoDeGrupo:dd/MM HH:mm}, e estas eliminatórias "
            + $"começam antes disso: {string.Join(", ", cedoDemais)}");
    }

    [Fact]
    public async Task A_final_de_cada_categoria_e_dos_ultimos_jogos_do_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarTorneioDeTresTamanhos(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);
        await JogarOTorneioInteiroAsync(ctx, controller, torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();

        var finais = jogos.Where(j => j.Fase == "Final").ToList();
        // Três categorias, três finais — se o torneio não chegou lá, o teste não testou nada.
        Assert.Equal(3, finais.Count);

        var ultimoQueNaoEFinal = jogos
            .Where(j => j.Fase != "Final")
            .Max(j => j.HorarioPrevisto)!.Value;

        var finalCedoDemais = finais
            .Where(j => j.HorarioPrevisto < ultimoQueNaoEFinal)
            .Select(j => $"Final da cat {j.CategoriaId} às {j.HorarioPrevisto:dd/MM HH:mm}")
            .ToList();

        Assert.True(finalCedoDemais.Count == 0,
            $"o último jogo que não é final é {ultimoQueNaoEFinal:dd/MM HH:mm}, e estas finais "
            + $"acontecem antes dele: {string.Join(", ", finalCedoDemais)}");
    }

    [Fact]
    public async Task Cada_posto_de_fase_so_comeca_quando_o_anterior_acabou()
    {
        // A régua completa do pedido, e não só as duas pontas: décimas > oitavas > quartas >
        // semi > final, valendo entre TODAS as categorias ao mesmo tempo.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarTorneioDeTresTamanhos(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);
        await JogarOTorneioInteiroAsync(ctx, controller, torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();

        var porPosto = jogos
            .Where(j => j.HorarioPrevisto != null)
            .GroupBy(j => OrdemDasFases.Posto(j.Fase))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Posto = g.Key,
                Fases = string.Join("/", g.Select(j => j.Fase).Distinct().Order()),
                Comeca = g.Min(j => j.HorarioPrevisto)!.Value,
                Acaba = g.Max(j => j.HorarioPrevisto)!.Value,
            })
            .ToList();

        // Um torneio com três tamanhos de categoria passa por oitavas, quartas, semi e final.
        Assert.True(porPosto.Count >= 4, $"o torneio só teve os postos {string.Join(", ", porPosto.Select(p => p.Posto))}");

        for (int i = 1; i < porPosto.Count; i++)
        {
            var anterior = porPosto[i - 1];
            var atual = porPosto[i];

            Assert.True(atual.Comeca >= anterior.Acaba,
                $"o posto {atual.Posto} ({atual.Fases}) começa {atual.Comeca:dd/MM HH:mm}, antes de "
                + $"o posto {anterior.Posto} ({anterior.Fases}) acabar {anterior.Acaba:dd/MM HH:mm}");
        }
    }

    [Fact]
    public async Task A_rodada_2_dos_grupos_nao_e_atravessada_por_eliminatoria()
    {
        // ⚠️ ALERTA DO PRÓPRIO FELIPE: *"cuidado por que os grupos podem ter rodada 2 tambem
        // cuide com essa logica"*. Um grupo de 3 duplas são TRÊS rodadas, e a régua de ordem não
        // pode enxergar só a primeira delas — a última rodada de grupo é tão fase de grupos
        // quanto a primeira, e é ela que segura as eliminatórias.
        //
        // Aqui a checagem é por DUPLA: cada dupla joga todos os jogos de grupo dela ANTES de
        // qualquer eliminatória do torneio. É o que "primeiro todas as chaves" quer dizer pra
        // quem está jogando.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org) = MontarTorneioDeTresTamanhos(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);
        await JogarOTorneioInteiroAsync(ctx, controller, torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();

        // Prova que o cenário tem mesmo grupo com mais de uma rodada — senão o teste é decorativo.
        var rodadasPorDupla = jogos
            .Where(j => FasesTorneio.EhFaseDeGrupos(j.Fase))
            .SelectMany(j => new[] { j.Dupla1Id, j.Dupla2Id })
            .GroupBy(d => d)
            .Max(g => g.Count());
        Assert.True(rodadasPorDupla >= 2, $"nenhuma dupla joga 2 jogos de grupo (máx {rodadasPorDupla})");

        var primeiraEliminatoria = jogos
            .Where(j => !FasesTorneio.EhFaseDeGrupos(j.Fase) && j.HorarioPrevisto != null)
            .Min(j => j.HorarioPrevisto)!.Value;

        var gruposAtrasados = jogos
            .Where(j => FasesTorneio.EhFaseDeGrupos(j.Fase) && j.HorarioPrevisto > primeiraEliminatoria)
            .Select(j => $"{j.Fase} (cat {j.CategoriaId}) às {j.HorarioPrevisto:dd/MM HH:mm}")
            .ToList();

        Assert.True(gruposAtrasados.Count == 0,
            $"a primeira eliminatória do torneio é {primeiraEliminatoria:dd/MM HH:mm} e estes jogos "
            + $"de GRUPO acontecem depois dela: {string.Join(", ", gruposAtrasados)}");
    }

    [Fact]
    public async Task A_chave_direta_espera_todos_os_grupos_em_vez_de_abrir_o_torneio()
    {
        // ⚠️ ESTE TESTE INVERTE UMA DECISÃO DE 05/08/2026 — de propósito, e por escrito.
        //
        // A chave direta passou a ABRIR o torneio naquela data porque ela não espera resultado
        // de ninguém (as duplas já estão definidas) e é a que tem mais rodadas pela frente: 24
        // duplas são cinco rodadas até a final, e jogá-la pro fim dos grupos empurrou as cinco
        // junto — no Interno a final da chave geral foi parar às 23h18.
        //
        // 🗣️ Perguntado sobre isso em 09/09/2026, o Felipe escolheu o outro lado: *"a menos que
        // fique horario vazio, mas a ordem é colocar todos jogos de chave antes"*. A final tardia
        // continua sendo o custo — só que agora ela cai junto com as OUTRAS finais, que é o que
        // ele pediu, e quem avisa que não cabe é o aviso do sorteio.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, chave) = MontarTorneioComChaveDireta(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();

        var ultimoDeGrupo = jogos
            .Where(j => FasesTorneio.EhFaseDeGrupos(j.Fase))
            .Max(j => j.HorarioPrevisto)!.Value;

        var daChave = jogos.Where(j => j.CategoriaId == chave.Id).ToList();
        Assert.NotEmpty(daChave);

        var cedoDemais = daChave
            .Where(j => j.HorarioPrevisto < ultimoDeGrupo)
            .Select(j => $"{j.Fase} às {j.HorarioPrevisto:dd/MM HH:mm}")
            .ToList();

        Assert.True(cedoDemais.Count == 0,
            $"a fase de grupos só acaba {ultimoDeGrupo:dd/MM HH:mm} e a chave direta já joga "
            + $"antes disso: {string.Join(", ", cedoDemais)}");
    }

    // Uma categoria comum de 12 duplas e uma chave direta de 24, montada com os MESMOS
    // jogadores remontados em outros pares — o cenário real do Interno.
    private static (Torneio torneio, Jogador organizador, Categoria chaveDireta)
        MontarTorneioComChaveDireta(DbPadelContext ctx)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Interno com Chave Direta",
            Codigo = "INTCD",
            Status = "Chaves em Sorteio",
            DataInicio = new DateTime(2026, 9, 12, 8, 0, 0),
            HoraInicioDoDia = new TimeSpan(8, 0, 0),
            HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            QuantidadeQuadras = 5,
            TempoPrevistoPartidaMinutos = 12,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "6a Masculina", Codigo = "C6M", Torneio = torneio };
        var chave = new Categoria { Nome = "Mata-Mata Geral", Codigo = "MM", Torneio = torneio, ChaveDireta = true };
        ctx.Categorias.AddRange(categoria, chave);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        var gente = Enumerable.Range(1, 48).Select(TestInfra.NovoJogador).ToList();
        ctx.Jogadores.AddRange(gente);
        ctx.SaveChanges();

        for (int i = 0; i < 12; i++)
            ctx.Duplas.Add(new Dupla { Categoria = categoria, Jogador1 = gente[i * 2], Jogador2 = gente[i * 2 + 1] });

        // A chave remonta os mesmos 48: o jogador n joga com o n+24, então ninguém repete o
        // parceiro da categoria dele.
        for (int i = 0; i < 24; i++)
            ctx.Duplas.Add(new Dupla { Categoria = chave, Jogador1 = gente[i], Jogador2 = gente[i + 24] });

        ctx.SaveChanges();

        return (torneio, organizador, chave);
    }

    // ── O cenário ────────────────────────────────────────────────────────────────────────
    //
    // Três categorias de tamanhos diferentes, que é o que faz a ordem GLOBAL importar: cada uma
    // estreia no mata-mata numa fase diferente e, pela régua antiga, terminaria numa hora
    // diferente. Com 16, 8 e 4 duplas o torneio passa por Oitavas, Quartas, Semifinal e Final.
    private static (Torneio torneio, Jogador organizador) MontarTorneioDeTresTamanhos(DbPadelContext ctx)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Torneio de Tres Tamanhos",
            Codigo = "TRES3",
            Status = "Chaves em Sorteio",
            DataInicio = new DateTime(2026, 9, 12, 8, 0, 0),
            HoraInicioDoDia = new TimeSpan(8, 0, 0),
            HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            QuantidadeQuadras = 3,
            TempoPrevistoPartidaMinutos = 30,
        };
        ctx.Torneios.Add(torneio);

        var grande = new Categoria { Nome = "6a Masculina", Codigo = "C6M", Torneio = torneio };
        var media = new Categoria { Nome = "4a Masculina", Codigo = "C4M", Torneio = torneio };
        var pequena = new Categoria { Nome = "3a Feminina", Codigo = "C3F", Torneio = torneio };
        ctx.Categorias.AddRange(grande, media, pequena);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        int proximo = 1;
        void Inscrever(Categoria cat, int quantasDuplas)
        {
            for (int i = 0; i < quantasDuplas; i++)
            {
                var j1 = TestInfra.NovoJogador(proximo++);
                var j2 = TestInfra.NovoJogador(proximo++);
                ctx.Jogadores.AddRange(j1, j2);
                ctx.Duplas.Add(new Dupla { Categoria = cat, Jogador1 = j1, Jogador2 = j2 });
            }
        }

        Inscrever(grande, 16);
        Inscrever(media, 8);
        Inscrever(pequena, 4);
        ctx.SaveChanges();

        return (torneio, organizador);
    }

    // Joga o torneio até não sobrar jogo agendado, SEMPRE na ordem do relógio — que é como a
    // vida acontece e a única ordem em que o robô das próximas fases vê a grade como ela é.
    private static async Task JogarOTorneioInteiroAsync(DbPadelContext ctx,
        Padelizou.Controllers.TorneiosController controller, int torneioId)
    {
        // Trava de segurança: um torneio destes fecha em muito menos que isto, e um dado torto
        // não pode virar laço infinito no CI.
        for (int volta = 0; volta < 200; volta++)
        {
            var proximo = await ctx.Partidas
                .Where(p => p.TorneioId == torneioId && p.Status == "Agendada")
                .OrderBy(p => p.HorarioPrevisto ?? DateTime.MaxValue).ThenBy(p => p.Id)
                .FirstOrDefaultAsync();

            if (proximo == null) return;

            await TestInfra.FinalizarComPlacarAsync(ctx, controller, proximo, 9, 3);
        }

        Assert.Fail("o torneio não terminou em 200 jogos");
    }
}
