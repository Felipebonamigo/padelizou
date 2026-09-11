using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A COLOCAÇÃO VIRA NOME NO INSTANTE EM QUE O GRUPO FECHA.
//
// 🗣️ Felipe, 11/09/2026, olhando o torneio do Er com o Grupo B encerrado e o A sem jogar:
// *"Avança sim, pq o grupo b esta definido, tem q por o 1º e 2º nas semifinais"* · *"sempre que
// um grupo finalizar, igual da foto, o Grupo B já está definido, então já pode mudar, na
// semifinal o 1º do B e o 2º do B, e use essa regra, para já ir alterando conforme o jogo for
// finalizando"*.
//
// 🕳️ O QUE A TELA FAZIA: a chave inteira era escrita por colocação ("1º do Grupo A × 2º do Grupo
// B") até o ÚLTIMO jogo da categoria acabar. Com o Grupo B encerrado — três jogos, cada dupla com
// dois — "2º do Grupo B" já tem nome, sobrenome e foto no sistema, e a tela continuava escrevendo
// a frase genérica. Quem jogou não se reconhecia no próprio quadro.
//
// ⚠️ O QUE ISTO **NÃO** É: o jogo da semifinal continua nascendo só quando os DOIS lados existem
// (`Partida.Dupla1Id`/`Dupla2Id` são NOT NULL — vaga vazia no banco exigiria migration). O que
// muda é o RÓTULO da vaga projetada.
//
// ⚠️ E POR GRUPO FECHADO, nunca por jogo solto: no meio do grupo a classificação é provisória (a
// dupla em 1º com um jogo a menos cai pra 2º na rodada seguinte), e trocar o nome a cada placar
// poria no quadro uma dupla que sai dele dez minutos depois.
public class NomeNaVagaAssimQueOGrupoFechaTests
{
    // ── A régua, sozinha ────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_grupo_que_fechou_entrega_os_nomes_e_o_que_ainda_joga_nao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupos, jogos) = DoisGruposDeTresAsync(ctx, fecharB: true);

        var conhecidos = ClassificadosJaConhecidos.De(grupos, jogos, vagasPorGrupo: 2);

        // O Grupo B fechou: 1º e 2º têm nome.
        Assert.True(conhecidos.ContainsKey(("Grupo B", 1)));
        Assert.True(conhecidos.ContainsKey(("Grupo B", 2)));

        // O A não jogou nada: nenhuma vaga dele é definitiva.
        Assert.False(conhecidos.ContainsKey(("Grupo A", 1)));
        Assert.False(conhecidos.ContainsKey(("Grupo A", 2)));
    }

    // A ordem é a MESMA régua do chaveamento (ClassificacaoDeGrupos.Ordenar) — o nome que aparece
    // na vaga tem que ser o da dupla que o robô vai pôr ali.
    [Fact]
    public void O_nome_sai_da_mesma_regua_que_monta_a_chave()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupos, jogos) = DoisGruposDeTresAsync(ctx, fecharB: true);

        var conhecidos = ClassificadosJaConhecidos.De(grupos, jogos, vagasPorGrupo: 2);

        var duplasDoB = grupos.Single(g => g.Nome == "Grupo B").Duplas.ToList();
        var ranking = ClassificacaoDeGrupos.Ordenar(
            duplasDoB, jogos.Where(p => duplasDoB.Any(d => d.Id == p.Dupla1Id)).ToList());

        Assert.Equal(ranking[0].Dupla.NomeDeExibicao, conhecidos[("Grupo B", 1)]);
        Assert.Equal(ranking[1].Dupla.NomeDeExibicao, conhecidos[("Grupo B", 2)]);
    }

    // Um jogo de grupo em quadra segura o grupo inteiro: 2 de 3 encerrados ainda não decide o 2º.
    [Fact]
    public void Um_jogo_pendente_segura_o_grupo_inteiro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupos, jogos) = DoisGruposDeTresAsync(ctx, fecharB: true);

        var ultimo = jogos.Last(p => grupos.Single(g => g.Nome == "Grupo B").Duplas.Any(d => d.Id == p.Dupla1Id));
        ultimo.Status = "AoVivo";
        ultimo.VencedorId = null;

        Assert.Empty(ClassificadosJaConhecidos.De(grupos, jogos, vagasPorGrupo: 2));
    }

    // Só as vagas que CLASSIFICAM ganham nome: o 3º de um grupo de 3 não entra em quadro nenhum.
    [Fact]
    public void Quem_nao_classifica_nao_ganha_vaga()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupos, jogos) = DoisGruposDeTresAsync(ctx, fecharB: true);

        var conhecidos = ClassificadosJaConhecidos.De(grupos, jogos, vagasPorGrupo: 2);

        Assert.False(conhecidos.ContainsKey(("Grupo B", 3)));
    }

    // ── O quadro da aba de chaves ───────────────────────────────────────────────────────────

    [Fact]
    public void O_quadro_projetado_escreve_o_nome_na_vaga_do_grupo_fechado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupos, jogos) = DoisGruposDeTresAsync(ctx, fecharB: true);
        var conhecidos = ClassificadosJaConhecidos.De(grupos, jogos, vagasPorGrupo: 2);

        var rodadas = ChaveProjetada.MontarCompleta(
            grupos.Select(g => g.Nome).ToList(), 2,
            grupos.Select(g => g.Duplas.Count).ToList(),
            cruzamentoDesenhado: null,
            jaConhecidos: conhecidos);

        var lados = rodadas[0].Jogos.SelectMany(j => new[] { j.Lado1, j.Lado2 }).ToList();

        // As duas vagas do Grupo B saem com NOME; as do A continuam por colocação.
        Assert.Contains(conhecidos[("Grupo B", 1)], lados);
        Assert.Contains(conhecidos[("Grupo B", 2)], lados);
        Assert.Contains("1º do Grupo A", lados);
        Assert.Contains("2º do Grupo A", lados);
    }

    // 🗣️ Felipe, com o print da semifinal do Er: *"quando a pessoa tiver 3 nomes cadastradas, Nome
    // sobrenome1 sobrenome2, pega só o primeiro e o ultimo para nao ficar muito espaçado"*. Na vaga
    // do quadro o nome inteiro não cabe — "Marcelo Carvalho Prestes & Enio Gilberto M…" era cortado
    // no meio, e o pedaço que sobrava era o nome do MEIO, o que menos identifica alguém.
    [Fact]
    public void O_nome_da_vaga_sai_pelo_primeiro_e_pelo_ultimo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupos, jogos) = DoisGruposDeTresAsync(ctx, fecharB: true);

        // A dupla de menor Id do Grupo B vence os dois jogos dela: é o 1º do grupo.
        var primeira = grupos.Single(g => g.Nome == "Grupo B").Duplas.OrderBy(d => d.Id).First();
        primeira.Jogador1!.Nome = "EDER CRISTIANO MARCOS";
        primeira.Jogador2!.Nome = "augusto ohlweiler";
        ctx.SaveChanges();

        var conhecidos = ClassificadosJaConhecidos.De(grupos, jogos, vagasPorGrupo: 2);

        Assert.Equal("Eder Marcos & Augusto Ohlweiler", conhecidos[("Grupo B", 1)]);
    }

    // ── A lista de jogos, pelo caminho de verdade ───────────────────────────────────────────

    [Fact]
    public async Task A_previa_da_lista_de_jogos_mostra_quem_ja_classificou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);
        await ctx.Entry(torneio).ReloadAsync();
        torneio.Status = "Fase de Grupos";
        await ctx.SaveChangesAsync();

        var grupos = await ctx.Set<GrupoTorneio>()
            .Where(g => g.CategoriaId == categoria.Id).Include(g => g.Duplas)
            .OrderBy(g => g.Nome).ToListAsync();
        Assert.Equal(2, grupos.Count);

        // Fecha SÓ o Grupo B, como no print do Er.
        var idsDoB = grupos[1].Duplas.Select(d => d.Id).ToHashSet();
        var jogosDoB = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && FasesTorneio.EhFaseDeGrupos(p.Fase)
                     && idsDoB.Contains(p.Dupla1Id))
            .OrderBy(p => p.Id).ToListAsync();
        foreach (var jogo in jogosDoB)
        {
            bool venceA1 = jogo.Dupla1Id < jogo.Dupla2Id;
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, venceA1 ? 9 : 3, venceA1 ? 3 : 9);
        }

        var duplasDoB = await ctx.Duplas.Include(d => d.Jogador1).Include(d => d.Jogador2)
            .Where(d => idsDoB.Contains(d.Id)).ToListAsync();
        var rankingDoB = ClassificacaoDeGrupos.Ordenar(duplasDoB, jogosDoB);

        ctx.ChangeTracker.Clear();

        var resultado = await controller.Jogos(torneio.Id, null, null);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado);

        var queVem = (List<ProximasFasesDaChave.JogoQueVem>)controller.ViewBag.JogosQueVem;
        var rotulos = queVem.SelectMany(j => new[] { j.Lado1.Rotulo, j.Lado2.Rotulo }).ToList();

        // O print do Felipe: o Grupo B fechado aparece com NOME na semifinal.
        Assert.Contains(rankingDoB[0].Dupla.NomeDeExibicao, rotulos);
        Assert.Contains(rankingDoB[1].Dupla.NomeDeExibicao, rotulos);

        // E o Grupo A, que não jogou, continua sendo uma promessa por colocação.
        Assert.Contains(rotulos, r => r.Contains($"do {grupos[0].Nome}"));
    }

    // ── Cenário ─────────────────────────────────────────────────────────────────────────────

    // Dois grupos de três duplas. `fecharB` encerra os três jogos do Grupo B e deixa o A intacto —
    // exatamente o print de 11/09/2026.
    private static (List<GrupoTorneio> Grupos, List<Partida> Jogos) DoisGruposDeTresAsync(
        DbPadelContext ctx, bool fecharB)
    {
        var torneio = new Torneio { Nome = "Er", Codigo = "ER77" };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "4ª Masculina", Codigo = "4M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        var grupos = new List<GrupoTorneio>();
        var jogos = new List<Partida>();
        int seq = 1;

        foreach (var letra in new[] { "A", "B" })
        {
            var grupo = new GrupoTorneio { CategoriaId = categoria.Id, Nome = $"Grupo {letra}" };
            ctx.Add(grupo);
            ctx.SaveChanges();

            var duplas = new List<Dupla>();
            for (int i = 0; i < 3; i++)
            {
                var j1 = TestInfra.NovoJogador(seq++);
                var j2 = TestInfra.NovoJogador(seq++);
                ctx.Jogadores.AddRange(j1, j2);
                var dupla = new Dupla
                {
                    CategoriaId = categoria.Id, Jogador1 = j1, Jogador2 = j2,
                    Grupo = letra, GrupoTorneioId = grupo.Id,
                };
                ctx.Duplas.Add(dupla);
                duplas.Add(dupla);
            }
            ctx.SaveChanges();
            grupo.Duplas = duplas;
            grupos.Add(grupo);

            bool fechado = letra == "B" && fecharB;
            for (int a = 0; a < duplas.Count; a++)
                for (int b = a + 1; b < duplas.Count; b++)
                {
                    var jogo = new Partida
                    {
                        TorneioId = torneio.Id, CategoriaId = categoria.Id,
                        Fase = $"Grupo {letra}", Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
                        Dupla1Id = duplas[a].Id, Dupla2Id = duplas[b].Id,
                        Status = fechado ? "Finalizada" : "Agendada",
                    };
                    if (fechado)
                    {
                        // Vence sempre a de MENOR Id, 9x4: a classificação sai determinada.
                        jogo.GamesDupla1 = 9; jogo.GamesDupla2 = 4;
                        jogo.SetsDupla1 = 1; jogo.SetsDupla2 = 0;
                        jogo.VencedorId = duplas[a].Id;
                    }
                    ctx.Partidas.Add(jogo);
                    jogos.Add(jogo);
                }
            ctx.SaveChanges();
        }

        return (grupos, jogos);
    }
}
