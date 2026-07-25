using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Inscrição sem parceiro, troca de parceiro e trava de uma-categoria-por-jogador.
public class InscricaoFlexivelTests
{
    private static (Torneio torneio, Categoria catA, Categoria catB) MontarTorneioDuasCategorias(
        DbPadelContext ctx, bool permiteMultiplas)
    {
        var torneio = new Torneio
        {
            Nome = "Copa Teste", Codigo = "CT1", Status = "Inscrições Abertas",
            PermiteMultiplasCategorias = permiteMultiplas,
        };
        var catA = new Categoria { Nome = "2ª Masculina", Codigo = "2M", Torneio = torneio };
        var catB = new Categoria { Nome = "3ª Masculina", Codigo = "3M", Torneio = torneio };
        ctx.Torneios.Add(torneio);
        ctx.Categorias.AddRange(catA, catB);
        ctx.SaveChanges();
        return (torneio, catA, catB);
    }

    // ---------- Dupla sem parceiro ----------

    [Fact]
    public void Dupla_sem_parceiro_e_valida_e_se_marca_como_incompleta()
    {
        using var ctx = TestInfra.NovoContexto();
        var solo = new Jogador { Nome = "Solo", Cpf = "1" };
        ctx.Jogadores.Add(solo);
        var (_, catA, _) = MontarTorneioDuasCategorias(ctx, permiteMultiplas: true);

        var dupla = new Dupla { CategoriaId = catA.Id, Jogador1Id = solo.Id, Jogador2Id = null };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges(); // a coluna aceita nulo — não estoura

        Assert.False(dupla.Completa);
        Assert.Null(dupla.Jogador2Id);
    }

    [Fact]
    public async Task Dupla_sem_parceiro_nao_quebra_o_calculo_de_pontos()
    {
        using var ctx = TestInfra.NovoContexto();
        var solo = new Jogador { Nome = "Solo", Cpf = "1" };
        ctx.Jogadores.Add(solo);
        var (_, catA, _) = MontarTorneioDuasCategorias(ctx, permiteMultiplas: true);
        ctx.Duplas.Add(new Dupla { CategoriaId = catA.Id, Jogador1Id = solo.Id, Jogador2Id = null, UltimaFase = "Grupos" });
        ctx.SaveChanges();

        var svc = new EstatisticasService(ctx);

        // As duas rotas de pontuação precisam sobreviver ao parceiro nulo.
        var pontos = await svc.ObterPontosPorJogadorAsync(new[] { solo.Id });
        var resumo = await svc.ObterResumoJogadorAsync(solo.Id);

        Assert.Equal(10, pontos[solo.Id]);
        Assert.Equal(10, resumo.Pontos);
    }

    // ---------- Uma categoria por jogador ----------

    [Fact]
    public async Task Com_multiplas_ligadas_o_jogador_entra_nas_duas_categorias()
    {
        using var ctx = TestInfra.NovoContexto();
        var j1 = new Jogador { Nome = "J1", Cpf = "1" };
        var j2 = new Jogador { Nome = "J2", Cpf = "2" };
        ctx.Jogadores.AddRange(j1, j2);
        var (torneio, catA, _) = MontarTorneioDuasCategorias(ctx, permiteMultiplas: true);
        ctx.Duplas.Add(new Dupla { CategoriaId = catA.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id });
        ctx.SaveChanges();

        var motivo = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(
            ctx, torneio, new[] { j1.Id, j2.Id });

        Assert.Null(motivo);
    }

    [Fact]
    public async Task Com_multiplas_desligadas_o_jogador_e_barrado_na_segunda_categoria()
    {
        using var ctx = TestInfra.NovoContexto();
        var j1 = new Jogador { Nome = "Ana Souza", Cpf = "1" };
        var j2 = new Jogador { Nome = "J2", Cpf = "2" };
        var forasteiro = new Jogador { Nome = "Novato", Cpf = "3" };
        ctx.Jogadores.AddRange(j1, j2, forasteiro);
        var (torneio, catA, _) = MontarTorneioDuasCategorias(ctx, permiteMultiplas: false);
        ctx.Duplas.Add(new Dupla { CategoriaId = catA.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id });
        ctx.SaveChanges();

        var motivo = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(
            ctx, torneio, new[] { j1.Id, forasteiro.Id });

        Assert.NotNull(motivo);
        Assert.Contains("Ana Souza", motivo);
        Assert.Contains("2ª Masculina", motivo);
        Assert.DoesNotContain("Novato", motivo); // quem não está inscrito não é citado
    }

    [Fact]
    public async Task Trava_de_categoria_tambem_enxerga_inscricao_de_americano()
    {
        using var ctx = TestInfra.NovoContexto();
        var j = new Jogador { Nome = "Carlos", Cpf = "1" };
        ctx.Jogadores.Add(j);
        var (torneio, catA, _) = MontarTorneioDuasCategorias(ctx, permiteMultiplas: false);
        ctx.InscricoesAmericanas.Add(new InscricaoAmericana { CategoriaId = catA.Id, JogadorId = j.Id });
        ctx.SaveChanges();

        var motivo = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(ctx, torneio, new[] { j.Id });

        Assert.NotNull(motivo);
        Assert.Contains("Carlos", motivo);
    }

    [Fact]
    public async Task Trava_ignora_a_propria_categoria_na_troca_de_parceiro()
    {
        using var ctx = TestInfra.NovoContexto();
        var j1 = new Jogador { Nome = "J1", Cpf = "1" };
        var j2 = new Jogador { Nome = "J2", Cpf = "2" };
        ctx.Jogadores.AddRange(j1, j2);
        var (torneio, catA, _) = MontarTorneioDuasCategorias(ctx, permiteMultiplas: false);
        ctx.Duplas.Add(new Dupla { CategoriaId = catA.Id, Jogador1Id = j1.Id, Jogador2Id = j2.Id });
        ctx.SaveChanges();

        // Trocar o parceiro DENTRO da mesma categoria não pode se auto-bloquear.
        var motivo = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(
            ctx, torneio, new[] { j1.Id }, ignorarCategoriaId: catA.Id);

        Assert.Null(motivo);
    }

    // ---------- Sorteio ----------

    [Fact]
    public async Task Sorteio_ignora_dupla_incompleta_e_lista_de_espera()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        // Uma dupla sem parceiro e uma na lista de espera, além das 4 completas.
        var solo = new Jogador { Nome = "Solo", Cpf = "88800000001" };
        var espera1 = new Jogador { Nome = "Espera1", Cpf = "88800000002" };
        var espera2 = new Jogador { Nome = "Espera2", Cpf = "88800000003" };
        ctx.Jogadores.AddRange(solo, espera1, espera2);
        ctx.SaveChanges();

        ctx.Duplas.Add(new Dupla { CategoriaId = categoria.Id, Jogador1Id = solo.Id, Jogador2Id = null });
        ctx.Duplas.Add(new Dupla { CategoriaId = categoria.Id, Jogador1Id = espera1.Id, Jogador2Id = espera2.Id, EmListaDeEspera = true });
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.GerarChaves(torneio.Id);

        // Só as 4 duplas prontas entraram em grupo; as outras 2 seguem inscritas e sem grupo.
        var comGrupo = await ctx.Duplas.CountAsync(d => d.CategoriaId == categoria.Id && d.Grupo != null);
        Assert.Equal(4, comGrupo);

        var incompleta = await ctx.Duplas.FirstAsync(d => d.Jogador1Id == solo.Id);
        Assert.Null(incompleta.Grupo);

        var naEspera = await ctx.Duplas.FirstAsync(d => d.Jogador1Id == espera1.Id);
        Assert.Null(naEspera.Grupo);
    }
}
