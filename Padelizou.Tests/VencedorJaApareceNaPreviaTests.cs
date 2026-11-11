using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;
using static Padelizou.Services.ProximasFasesDaChave;

namespace Padelizou.Tests;

// QUEM JÁ VENCEU APARECE COM NOME NO JOGO PREVISTO SEGUINTE (11/11/2026).
//
// 🗣️ Felipe: *"a dupla que já definiu seu jogo […] já pode ir pro chaveamento prévio — o grupo A
// tinha 3 jogos, já jogaram os 3, o primeiro e segundo que passaram já aparecem nas quartas de
// final respectivas previstas (ou caso seja outra fase também, e assim com as demais fases do
// mata-mata)"*.
//
// Do grupo pras quartas já era assim (ClassificadosJaConhecidos, 11/09/2026). Faltava DENTRO do
// mata-mata: a Quartas 1 acabou, a Quartas 2 ainda não — a Semifinal 1 prevista continuava dizendo
// "Vencedor Quartas de Final 1" em vez do nome de quem venceu. Agora o vencedor ocupa a vaga na
// prévia no instante em que o jogo dele termina, em qualquer fase. (O jogo de verdade continua
// nascendo só quando os DOIS lados existem — isto é rótulo, não partida.)
public class VencedorJaApareceNaPreviaTests
{
    private static PartidaDaChave Jogo(int id, string fase, string d1, string d2, string hora, string? vencedor = null) =>
        new(id, fase, d1, d2, DateTime.Parse($"2026-08-08 {hora}"), Vencedor: vencedor);

    private static List<JogoQueVem> Montar(IReadOnlyList<PartidaDaChave> partidas) =>
        Agendar(new[] { ProximasFasesDaChave.Montar(partidas, Array.Empty<string>()) },
            new ConfiguracaoDaGrade(30, 2, Array.Empty<string>(), TimeSpan.FromHours(23), TimeSpan.FromHours(8)));

    [Fact]
    public void A_quartas_que_acabou_poe_o_vencedor_na_semifinal_prevista()
    {
        var quartas = new[]
        {
            Jogo(1, "Quartas de Final", "Ana/Bia", "Cris/Duda", "19:00", vencedor: "Ana/Bia"),
            Jogo(2, "Quartas de Final", "Edu/Fabi", "Gabi/Helo", "19:00"),
            Jogo(3, "Quartas de Final", "Ivo/Jo", "Kim/Lu", "19:30"),
            Jogo(4, "Quartas de Final", "Max/Nina", "Oto/Pia", "19:30", vencedor: "Oto/Pia"),
        };

        var jogos = Montar(quartas);
        var semi1 = jogos.Single(j => j.Fase == "Semifinal" && j.Numero == 1);
        var semi2 = jogos.Single(j => j.Fase == "Semifinal" && j.Numero == 2);

        // Primeiro cruza com último: Semi 1 = Venc. Q1 × Venc. Q4; Semi 2 = Venc. Q2 × Venc. Q3.
        Assert.Equal("Ana/Bia", semi1.Lado1.Rotulo);
        Assert.Equal("Oto/Pia", semi1.Lado2.Rotulo);
        Assert.Equal("Vencedor Quartas de Final 2", semi2.Lado1.Rotulo);
    }

    [Fact]
    public void A_semifinal_que_acabou_poe_o_vencedor_na_final_prevista()
    {
        var semis = new[]
        {
            Jogo(1, "Semifinal", "Ana/Bia", "Oto/Pia", "20:00", vencedor: "Oto/Pia"),
            Jogo(2, "Semifinal", "Edu/Fabi", "Ivo/Jo", "20:00"),
        };

        var final = Assert.Single(Montar(semis));

        Assert.Equal("Oto/Pia", final.Lado1.Rotulo);
        Assert.Equal("Vencedor Semifinal 2", final.Lado2.Rotulo);
    }

    // PELO FLUXO DE VERDADE: sorteia, aprova, joga todos os grupos e SÓ a Quartas 1 — a Semifinal
    // prevista já mostra o nome de quem venceu a Quartas 1.
    [Fact]
    public async Task No_torneio_de_verdade_o_vencedor_da_quartas_ja_aparece_na_semifinal_prevista()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 12);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id, sortearMesmoSemCaber: true);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id, avisarJogadores: false);

        foreach (var jogo in await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).ToListAsync())
            await TestInfra.FinalizarComPlacarAsync(ctx, TestInfra.NovoTorneiosController(ctx, org.Id), jogo, 9, 4);

        var todas = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        var numero = ReservasDeHorario.NumeroNaFase(todas);
        var abertura = todas.Where(p => !FasesTorneio.EhFaseDeGrupos(p.Fase)).ToList();
        Assert.NotEmpty(abertura);
        var primeiraFase = abertura.First().Fase;
        var jogo1 = abertura.Single(p => p.Fase == primeiraFase && numero[p.Id] == 1);
        await TestInfra.FinalizarComPlacarAsync(ctx, TestInfra.NovoTorneiosController(ctx, org.Id), jogo1, 9, 2);

        var vencedor = await ctx.Duplas.Include(d => d.Jogador1).Include(d => d.Jogador2)
            .SingleAsync(d => d.Id == jogo1.Dupla1Id);
        todas = await ctx.Partidas.Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .Where(p => p.TorneioId == torneio.Id).ToListAsync();
        var projecao = await new RoboDoChaveamento(ctx, TestInfra.EstatisticasFalsas())
            .ProjetarProximasFasesAsync(torneio.Id, todas);

        Assert.Contains(projecao.Jogos, j => j.Lado1.Rotulo == vencedor.NomeDeExibicao || j.Lado2.Rotulo == vencedor.NomeDeExibicao);
    }
}
