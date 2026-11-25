using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// TROCAR O TIPO DO TORNEIO NA GESTÃO (25/11/2026). 🗣️ Felipe, com o print do "Editar Dados do
// Torneio" da X Copa Treta: *"permita aqui alterar o tipo de torneio, pq o que querem fazer já
// está criado [...] (padrão, americano, treta, etc)"*.
//
// Treta não é um `Formato` gravado (TRETA.md): é o Oficial com fora do ranking, tie-break no
// 5x5 e DTN nas categorias. Por isso Oficial ↔ Treta troca a qualquer hora (até a DTN nascer),
// e Oficial/Treta ↔ Americano só sem inscrição nem jogo — a inscrição mora em tabelas
// diferentes (Dupla × InscricaoAmericana), e trocar com gente dentro a perderia.
public class TrocarTipoDoTorneioTests
{
    private static (DbPadelContext ctx, Torneio torneio, Categoria cat, Jogador org) Montar(int duplas = 0)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, cat, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: duplas, status: "Inscrições Abertas");
        org.IsOrganizadorTorneio = true;
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return (ctx, torneio, cat, org);
    }

    private static Task Editar(DbPadelContext ctx, Torneio torneio, int orgId, string? tipo) =>
        TestInfra.NovoTorneiosController(ctx, orgId).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null,
            dataInicio: torneio.DataInicio, precoInscricao: torneio.PrecoInscricao,
            clubeId: torneio.ClubeId, quantidadeQuadras: 1, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: torneio.RestricaoCategoria, capa: null,
            tempoPrevistoPartidaMinutos: 50, tipoDoTorneio: tipo);

    private static async Task<Torneio> Salvo(DbPadelContext ctx, int id)
    {
        ctx.ChangeTracker.Clear();
        return await ctx.Torneios.Include(t => t.Categorias).AsNoTracking().FirstAsync(t => t.Id == id);
    }

    [Fact]
    public async Task Oficial_vira_Treta_mesmo_com_inscritos()
    {
        var (ctx, torneio, _, org) = Montar(duplas: 4);
        using var _ = ctx;

        await Editar(ctx, torneio, org.Id, TrocaDoTipoDoTorneio.Treta);

        var salvo = await Salvo(ctx, torneio.Id);
        Assert.Equal(FormatoDoTorneio.Padrao, salvo.Formato);
        Assert.True(salvo.ForaDoRanking);
        Assert.True(salvo.TieBreakNaPenultima);
        Assert.All(salvo.Categorias, c => Assert.Equal(1, c.DuplasNoDtnPorGrupo));
        Assert.Equal(TrocaDoTipoDoTorneio.Treta, TrocaDoTipoDoTorneio.TipoAtual(salvo, salvo.Categorias));
    }

    [Fact]
    public async Task Treta_volta_a_Oficial_e_desliga_a_festa()
    {
        var (ctx, torneio, _, org) = Montar(duplas: 4);
        using var _ = ctx;
        await Editar(ctx, torneio, org.Id, TrocaDoTipoDoTorneio.Treta);

        await Editar(ctx, torneio, org.Id, FormatoDoTorneio.Padrao);

        var salvo = await Salvo(ctx, torneio.Id);
        Assert.False(salvo.ForaDoRanking);
        Assert.False(salvo.TieBreakNaPenultima);
        Assert.All(salvo.Categorias, c => Assert.Equal(0, c.DuplasNoDtnPorGrupo));
        Assert.Equal(FormatoDoTorneio.Padrao, TrocaDoTipoDoTorneio.TipoAtual(salvo, salvo.Categorias));
    }

    [Fact]
    public async Task Com_a_DTN_ja_montada_nao_volta_a_Oficial()
    {
        var (ctx, torneio, cat, org) = Montar(duplas: 4);
        using var _ = ctx;
        await Editar(ctx, torneio, org.Id, TrocaDoTipoDoTorneio.Treta);
        ctx.Categorias.Add(new Categoria
        {
            Nome = "DTN x", Codigo = "X-DTN", TorneioId = torneio.Id, PapelDaChave = ChaveDtn.Dtn,
            CategoriaDeOrigemId = cat.Id, ChaveDireta = true,
        });
        await ctx.SaveChangesAsync();

        await Editar(ctx, torneio, org.Id, FormatoDoTorneio.Padrao);

        Assert.True((await Salvo(ctx, torneio.Id)).ForaDoRanking);
    }

    [Fact]
    public async Task Vazio_o_Oficial_vira_Americano()
    {
        var (ctx, torneio, _, org) = Montar(duplas: 0);
        using var _ = ctx;

        await Editar(ctx, torneio, org.Id, FormatoDoTorneio.Americano);

        Assert.Equal(FormatoDoTorneio.Americano, (await Salvo(ctx, torneio.Id)).Formato);
    }

    [Fact]
    public async Task Com_inscritos_nao_troca_entre_Oficial_e_Americano()
    {
        var (ctx, torneio, _, org) = Montar(duplas: 2);
        using var _ = ctx;

        await Editar(ctx, torneio, org.Id, FormatoDoTorneio.Americano);

        Assert.Equal(FormatoDoTorneio.Padrao, (await Salvo(ctx, torneio.Id)).Formato);
    }

    [Fact]
    public async Task Tipo_inventado_e_recusado_e_campo_ausente_mantem()
    {
        var (ctx, torneio, _, org) = Montar(duplas: 0);
        using var _ = ctx;

        await Editar(ctx, torneio, org.Id, "Qualquer");
        Assert.Equal(FormatoDoTorneio.Padrao, (await Salvo(ctx, torneio.Id)).Formato);

        await Editar(ctx, torneio, org.Id, null);
        Assert.Equal(FormatoDoTorneio.Padrao, (await Salvo(ctx, torneio.Id)).Formato);
    }

    [Fact]
    public void Americano_vira_Oficial_so_pra_quem_pode_criar_Oficial()
    {
        var semPerfil = new Jogador { Nome = "x", Cpf = "1" };
        var torneio = new Torneio { Nome = "x", Codigo = "x", Formato = FormatoDoTorneio.Americano };

        Assert.NotNull(TrocaDoTipoDoTorneio.Problema(torneio, new List<Categoria>(), FormatoDoTorneio.Padrao,
            temInscricaoOuJogo: false, quem: semPerfil));
        Assert.Null(TrocaDoTipoDoTorneio.Problema(torneio, new List<Categoria>(), FormatoDoTorneio.AmericanoDeDuplas,
            temInscricaoOuJogo: false, quem: semPerfil));
    }
}
