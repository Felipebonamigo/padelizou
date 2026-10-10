using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// DEPOIS DE PÚBLICA, A GRADE NÃO SE REFAZ.
//
// 🗣️ Felipe, 10/10/2026, no dia do NATA PADEL TOUR: *"desabilita o recalcular horário e refazer
// grade então depois que as chaves já estão públicas"* — e antes: *"o torneio tem que se manter
// igual do começo ao fim, sem mudar as chaves depois"* · *"mesmo que atrase muito, não pode mexer
// nos horários previstos"*.
//
// O "Recalcular horários" (ação `RefazerGrade`) joga a grade fora e RE-GRAVA a promessa do
// sorteio; o "Ajustar horários" troca jogos de lugar. Os dois mudam o horário que o jogador viu
// publicado. Até a publicação eles servem pra arrumar a grade; depois dela, o horário é
// compromisso. Trocar UM jogo à mão continua possível — é decisão pontual, e não refaz o resto.
//
// ⚠️ A trava é do SERVIDOR: a tela esconde os botões, mas aba velha aberta e POST à mão não
// passam pela tela.
public class GradePublicadaNaoSeRefazTests
{
    private static async Task<(Torneio torneio, Jogador org, Dictionary<int, DateTime?> antes)> PublicadoAsync(
        DbPadelContext ctx, string status)
    {
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, 9);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        // Embaralha a grade de propósito: um recálculo que rodasse a desfaria, e o teste veria.
        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).ToListAsync();
        for (int i = 0; i < jogos.Count; i++)
            jogos[i].HorarioPrevisto = new DateTime(2026, 7, 4, 8, 0, 0).AddMinutes(37 * (jogos.Count - i));
        torneio.Status = status;
        await ctx.SaveChangesAsync();

        return (torneio, org, jogos.ToDictionary(j => j.Id, j => j.HorarioPrevisto));
    }

    private static async Task<Dictionary<int, DateTime?>> HorariosAsync(DbPadelContext ctx, int torneioId) =>
        await ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneioId)
            .ToDictionaryAsync(p => p.Id, p => p.HorarioPrevisto);

    [Theory]
    [InlineData("Fase de Grupos")]
    [InlineData("Mata-Mata")]
    public async Task Recalcular_horarios_recusa_depois_de_publicada(string status)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, antes) = await PublicadoAsync(ctx, status);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.RefazerGrade(torneio.Id);

        Assert.Equal(antes, await HorariosAsync(ctx, torneio.Id));
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Theory]
    [InlineData("Fase de Grupos")]
    [InlineData("Mata-Mata")]
    public async Task Ajustar_horarios_recusa_depois_de_publicada(string status)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, antes) = await PublicadoAsync(ctx, status);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.AjustarHorarios(torneio.Id);

        Assert.Equal(antes, await HorariosAsync(ctx, torneio.Id));
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Antes_de_publicar_recalcular_continua_funcionando()
    {
        // O avesso: na aprovação, o organizador ainda está arrumando a grade — é a hora dela.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, antes) = await PublicadoAsync(ctx, AprovacaoDeChaves.Pendente);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.RefazerGrade(torneio.Id);

        Assert.NotEqual(antes, await HorariosAsync(ctx, torneio.Id));
        Assert.Null(controller.TempData["Erro"]);
    }

    private static string Fonte(string arquivo)
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "Views", "Torneios", arquivo);
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException(arquivo);
    }

    [Theory]
    [InlineData("Details.cshtml", "asp-action=\"RefazerGrade\"")]
    [InlineData("_JogosDoTorneio.cshtml", "asp-action=\"AjustarHorarios\"")]
    public void A_tela_so_oferece_o_botao_com_a_chave_ainda_nao_publica(string arquivo, string acao)
    {
        // Botão que só serve pra ouvir "não dá" é pior que botão nenhum.
        var fonte = Fonte(arquivo);
        int botao = fonte.IndexOf(acao, StringComparison.Ordinal);
        Assert.True(botao > 0);

        // No Details a guarda pergunta direto (`AprovacaoDeChaves.ChavePublicada(Model)`); no
        // partial da lista de jogos, que não tem o torneio no modelo, pela bandeira que o
        // controller calcula com a MESMA régua (`ViewBag.ChavePublicadaDoTorneio`).
        int guarda = Math.Max(
            fonte.LastIndexOf("ChavePublicada(", botao, StringComparison.Ordinal),
            fonte.LastIndexOf("ChavePublicadaDoTorneio", botao, StringComparison.Ordinal));
        Assert.True(guarda > 0 && botao - guarda < 2500,
            $"O botão {acao} em {arquivo} precisa estar dentro de um teste de AprovacaoDeChaves.ChavePublicada.");
    }
}
