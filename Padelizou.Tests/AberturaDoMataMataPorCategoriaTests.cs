using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A CATEGORIA QUE JÁ FECHOU OS GRUPOS NÃO ESPERA A QUE NÃO FECHOU (21/08/2026).
//
// ⚠️ O QUE ESTAVA ERRADO: `EncaixarNasLevas` agenda em duas levas — primeiro tudo que não
// espera resultado (grupos e chave direta), depois tudo que sai dos grupos. A abertura da
// segunda leva era UMA SÓ pro torneio inteiro: o último jogo de grupo de QUALQUER categoria.
//
// No meio do torneio isso vira quadra parada. A 4ª masculina terminou os grupos às 15h e tem
// semifinal pra jogar; a 2ª, muito maior, ainda tem grupo até as 21h. A semifinal da 4ª ia pro
// fim da fila — atrás de TODA a fase de grupos da 2ª — mesmo com quadra livre às 15h30. E a
// prioridade declarada pro torneio é justamente "nenhuma quadra sem jogo até o final".
//
// ⚠️ E DUAS COISAS ANDAM JUNTAS COM ISSO, OU NADA ANDA:
//
//  1. A segunda leva passou a enxergar o que a primeira acabou de marcar. Antes ela só recebia
//     os jogos INTOCADOS. Não dava problema por acidente: como ela começava depois do último
//     grupo do torneio inteiro, não havia o que atropelar. Com a âncora solta, sem isso, a
//     semifinal da 4ª cairia na mesma quadra e no mesmo horário de um grupo da 2ª.
//
//  2. Toda leva ganhou um PISO: nenhuma começa antes da abertura da grade. A âncora de uma leva
//     vem do fim dos grupos da categoria, que pode ser uma hora do passado. É guarda de canto,
//     não conserto de bug observado — e o teste abaixo mede o RESULTADO ("nada foi remarcado
//     pro passado"), não a linha: tirar o piso hoje não faz nenhum teste falhar.
//
// Os testes deste arquivo são essas três coisas, mais a garantia que nenhuma delas pode ser
// comprada: ninguém em duas quadras ao mesmo tempo.
public class AberturaDoMataMataPorCategoriaTests
{
    private const int Duracao = 50;

    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria pronta, Categoria curta, Categoria atrasada)>
        TorneioNoMeioDoCaminhoAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Torneio de Duas Velocidades",
            Codigo = "DUAS1",
            Status = "Fase de Grupos",
            DataInicio = DateTime.Today.AddDays(-1).AddHours(9),
            QuantidadeQuadras = 4,
            TempoPrevistoPartidaMinutos = Duracao,
            TamanhoGrupo = 4,
            ClassificadosPorGrupo = 2,
        };
        ctx.Torneios.Add(torneio);

        var pronta = new Categoria { Nome = "4ª Masculina", Codigo = "CAT4M", Torneio = torneio };
        var curta = new Categoria { Nome = "3ª Masculina", Codigo = "CAT3M", Torneio = torneio };
        var atrasada = new Categoria { Nome = "2ª Masculina", Codigo = "CAT2M", Torneio = torneio };
        ctx.Categorias.AddRange(pronta, curta, atrasada);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        foreach (var nome in new[] { "Quadra 1", "Quadra 2", "Quadra 3", "Quadra 4" })
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = nome });

        int cpf = 1;
        List<int> Duplas(Categoria categoria, int quantas)
        {
            var criadas = new List<Dupla>();
            for (int i = 0; i < quantas; i++)
            {
                var j1 = TestInfra.NovoJogador(cpf++);
                var j2 = TestInfra.NovoJogador(cpf++);
                ctx.Jogadores.AddRange(j1, j2);
                var dupla = new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 };
                ctx.Duplas.Add(dupla);
                criadas.Add(dupla);
            }
            ctx.SaveChanges();
            return criadas.Select(d => d.Id).ToList();
        }

        var daPronta = Duplas(pronta, 8);
        var daCurta = Duplas(curta, 8);
        var daAtrasada = Duplas(atrasada, 24);

        var ontem = DateTime.Today.AddDays(-1).AddHours(9);

        void Jogo(Categoria categoria, int d1, int d2, string fase, string status, DateTime? quando)
        {
            ctx.Partidas.Add(new Partida
            {
                TorneioId = torneio.Id,
                CategoriaId = categoria.Id,
                Dupla1Id = d1,
                Dupla2Id = d2,
                Fase = fase,
                Status = status,
                HorarioPrevisto = quando,
                NomeQuadra = quando == null ? null : "Quadra 1",
                Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
            });
        }

        // A 4ª FECHOU: os grupos dela já foram jogados, ontem.
        for (int i = 0; i + 1 < daPronta.Count; i += 2)
            Jogo(pronta, daPronta[i], daPronta[i + 1], "Grupo A", "Finalizada", ontem.AddMinutes(i * Duracao));

        // E tem semifinal e final esperando vaga.
        Jogo(pronta, daPronta[0], daPronta[2], "Semifinal", "Agendada", null);
        Jogo(pronta, daPronta[4], daPronta[6], "Semifinal", "Agendada", null);
        Jogo(pronta, daPronta[0], daPronta[4], "Final", "Agendada", null);

        // ⚠️ A 3ª É A REDE DO PAR OBRIGATÓRIO: poucos jogos de grupo, todos por marcar. Os
        // grupos dela fecham logo no começo do dia, e a semifinal dela é ancorada logo depois —
        // NO MEIO da fase de grupos da 2ª, que continua ocupando as quadras. É exatamente aí
        // que a segunda leva precisa enxergar o que a primeira marcou; sem isso, a semifinal da
        // 3ª nasce em cima de um jogo de grupo da 2ª, mesma quadra e mesma hora.
        //
        // Ela vem ANTES da 2ª na fila (os jogos são criados nesta ordem), que é o que faz os
        // grupos dela caberem nas primeiras vagas.
        for (int i = 0; i < 4; i++)
            Jogo(curta, daCurta[i * 2], daCurta[i * 2 + 1], "Grupo C", "Agendada", null);

        Jogo(curta, daCurta[0], daCurta[2], "Semifinal", "Agendada", null);
        Jogo(curta, daCurta[4], daCurta[6], "Semifinal", "Agendada", null);
        Jogo(curta, daCurta[0], daCurta[4], "Final", "Agendada", null);

        // A 2ª NÃO FECHOU: um monte de jogo de grupo ainda por marcar, o que em 4 quadras
        // atravessa o dia — é o tempo que a 4ª esperava à toa.
        for (int i = 0; i < 36; i++)
            Jogo(atrasada, daAtrasada[i % 12 * 2], daAtrasada[i % 12 * 2 + 1], "Grupo B", "Agendada", null);

        Jogo(atrasada, daAtrasada[0], daAtrasada[2], "Semifinal", "Agendada", null);
        Jogo(atrasada, daAtrasada[4], daAtrasada[6], "Semifinal", "Agendada", null);
        Jogo(atrasada, daAtrasada[0], daAtrasada[4], "Final", "Agendada", null);

        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).RefazerGrade(torneio.Id);

        return (ctx, torneio, pronta, curta, atrasada);
    }

    private static async Task<List<Partida>> JogosAsync(DbPadelContext ctx, int torneioId) =>
        await ctx.Partidas.Where(p => p.TorneioId == torneioId).ToListAsync();

    // ⚠️ O TESTE QUE PROVA O FURO. Com a âncora única, a semifinal da 4ª nasce DEPOIS do último
    // jogo de grupo da 2ª — quadra parada a tarde inteira com jogo pronto pra entrar.
    [Fact]
    public async Task Categoria_que_ja_fechou_os_grupos_joga_junto_com_os_grupos_da_outra()
    {
        var (ctx, torneio, pronta, _, atrasada) = await TorneioNoMeioDoCaminhoAsync();
        using var _ = ctx;

        var jogos = await JogosAsync(ctx, torneio.Id);

        var primeiroDaPronta = jogos
            .Where(j => j.CategoriaId == pronta.Id && !FasesTorneio.EhFaseDeGrupos(j.Fase))
            .Min(j => j.HorarioPrevisto);

        var ultimoGrupoDaAtrasada = jogos
            .Where(j => j.CategoriaId == atrasada.Id && FasesTorneio.EhFaseDeGrupos(j.Fase))
            .Max(j => j.HorarioPrevisto);

        Assert.NotNull(primeiroDaPronta);
        Assert.NotNull(ultimoGrupoDaAtrasada);

        Assert.True(primeiroDaPronta < ultimoGrupoDaAtrasada,
            $"A semifinal da categoria que já fechou os grupos foi marcada pra {primeiroDaPronta}, "
            + $"e os grupos da outra só terminam às {ultimoGrupoDaAtrasada}. Ela não tem o que "
            + "esperar — e enquanto espera, tem quadra vazia.");
    }

    // Nada pode ser remarcado pra trás. Os grupos da 4ª foram jogados ONTEM, e a âncora de uma
    // leva sai do fim dos grupos da categoria — é daí que uma hora do passado pode entrar.
    //
    // ⚠️ ESTE TESTE MEDE O RESULTADO, NÃO O PISO. Hoje o piso de `Agendar` não é alcançado por
    // nenhum caminho que eu tenha conseguido montar, e tirá-lo não faz este teste falhar. Ele
    // está aqui porque a garantia ("jogo nenhum nasce no passado") tem que valer sempre, venha
    // de onde vier — não pra provar aquela linha.
    [Fact]
    public async Task Nada_e_remarcado_pra_um_horario_que_ja_passou()
    {
        var (ctx, torneio, _, _, _) = await TorneioNoMeioDoCaminhoAsync();
        using var __ = ctx;

        var remarcados = (await JogosAsync(ctx, torneio.Id))
            .Where(j => j.Status == "Agendada" && j.HorarioPrevisto != null)
            .ToList();

        Assert.NotEmpty(remarcados);
        Assert.All(remarcados, j => Assert.True(j.HorarioPrevisto >= DateTime.Today,
            $"Jogo {j.Codigo} ({j.Fase}) foi remarcado pra {j.HorarioPrevisto} — no passado."));
    }

    // ⚠️ A REDE DO PAR OBRIGATÓRIO. Soltar a âncora sem passar os jogos da primeira leva como
    // já-marcados põe a semifinal da 4ª na mesma quadra e no mesmo horário de um grupo da 2ª.
    [Fact]
    public async Task Nenhuma_quadra_recebe_dois_jogos_ao_mesmo_tempo()
    {
        var (ctx, torneio, _, _, _) = await TorneioNoMeioDoCaminhoAsync();
        using var __ = ctx;

        var dobradas = (await JogosAsync(ctx, torneio.Id))
            .Where(j => j.HorarioPrevisto != null && j.NomeQuadra != null && j.Status == "Agendada")
            .GroupBy(j => new { j.HorarioPrevisto, j.NomeQuadra })
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.NomeQuadra} às {g.Key.HorarioPrevisto:dd/MM HH:mm} com {g.Count()} jogos")
            .ToList();

        Assert.True(dobradas.Count == 0,
            "Quadra com dois jogos ao mesmo tempo: " + string.Join("; ", dobradas));
    }

    // A garantia que nunca pode ser trocada por aproveitamento de quadra: quem joga duas
    // categorias não é chamado pra duas quadras na mesma hora.
    [Fact]
    public async Task Ninguem_e_chamado_pra_duas_quadras_no_mesmo_horario()
    {
        var (ctx, torneio, _, _, _) = await TorneioNoMeioDoCaminhoAsync();
        using var __ = ctx;

        var jogos = await JogosAsync(ctx, torneio.Id);

        var pessoasDaDupla = (await ctx.Duplas
                .Where(d => d.Categoria!.TorneioId == torneio.Id)
                .Select(d => new { d.Id, d.Jogador1Id, d.Jogador2Id })
                .ToListAsync())
            .ToDictionary(
                d => d.Id,
                d => new[] { d.Jogador1Id, d.Jogador2Id }
                    .Where(x => x != null).Select(x => x!.Value).ToArray());

        foreach (var noHorario in jogos.Where(j => j.HorarioPrevisto != null).GroupBy(j => j.HorarioPrevisto))
        {
            var pessoas = noHorario
                .SelectMany(j => pessoasDaDupla.GetValueOrDefault(j.Dupla1Id, Array.Empty<int>())
                    .Concat(pessoasDaDupla.GetValueOrDefault(j.Dupla2Id, Array.Empty<int>())))
                .ToList();

            Assert.Equal(pessoas.Count, pessoas.Distinct().Count());
        }
    }
}
