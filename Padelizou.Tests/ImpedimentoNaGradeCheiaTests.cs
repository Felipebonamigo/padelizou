using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 🗣️ Felipe, 09/09/2026: "conserta esse furo do impedimento tambem".
//
// 🕳️ O MOTOR DE GRADE CEDIA O IMPEDIMENTO QUANDO MUITAS DUPLAS BLOQUEAVAM O MESMO DIA — e o
// impedimento é COBRADO na inscrição (`Torneio.TaxaPorImpedimento`), então é garantia vendida,
// não preferência. Medido antes do conserto, num torneio sexta+sábado com 16 duplas: com 4
// impedidas, 0 de 30 sorteios saíam com furo; com 6, 2 de 30; **com 8, 28 de 30**.
//
// 📏 A CAUSA ERA A MARGEM ALÉM DA JANELA. `VagasDaGrade.AlcanceNecessario` devolve o FIM da
// janela mais tardia — pro impedimento de sexta, o dia inteiro, então sábado 00:00, que é antes
// de o sábado abrir (`HoraInicioDiasSeguintes`, 08h). O `Montar` seguia daí `MargemDeHorarios`
// = `max(quadras,1)*3` vagas. Margem dimensionada por QUADRA, e não pelo VOLUME de jogos que a
// janela empurrou pro outro lado: com 8 duplas impedidas são muito mais que 3 jogos brigando
// pelas vagas do sábado, elas acabam, e o último recurso do `Encaixar` entra — cede primeiro o
// impedimento, depois a regra de não repetir gente.
//
// ⚠️ VARRE VÁRIOS SORTEIOS DE PROPÓSITO. Desde que o sorteio passou a sortear de verdade
// (09/09/2026), uma execução só passa ou falha por sorte — foi assim que este furo chegou a
// suíte verde na primeira medição.
public class ImpedimentoNaGradeCheiaTests
{
    // 03/07/2026 é SEXTA: o formato em que a janela de sexta (dia inteiro, porque só existe o
    // turno da noite) empurra tudo pro sábado, que é o dia seguinte.
    private static readonly DateTime SextaDeAbertura = new(2026, 7, 3, 9, 0, 0);

    private const int Sorteios = 20;

    [Theory]
    [InlineData(1, 4)]
    [InlineData(1, 6)]
    [InlineData(1, 8)]
    [InlineData(1, 12)]
    // ⚠️ MAIS QUADRAS PIORAVA, não melhorava: cada rodada rende uma vaga por quadra, então a
    // grade de `jogos + margem` termina em MENOS rodadas e sobram menos horários distintos pra
    // dupla escapar da janela dela. É o mesmo formato do furo do Er (auditoria de 09/09).
    [InlineData(2, 8)]
    [InlineData(4, 8)]
    [InlineData(6, 12)]
    // O extremo: TODA a categoria impedida na sexta. O torneio inteiro tem que migrar pro
    // sábado — e migra, desde que a grade alcance lá com vaga pra todo mundo.
    [InlineData(1, 16)]
    public async Task Ninguem_joga_dentro_da_janela_que_pagou_pra_evitar(int quadras, int impedidas)
    {
        var furos = new List<string>();

        for (int sorteio = 1; sorteio <= Sorteios; sorteio++)
        {
            using var ctx = TestInfra.NovoContexto();
            var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 16);
            torneio.DataInicio = SextaDeAbertura;
            torneio.QuantidadeQuadras = quadras;
            await ctx.SaveChangesAsync();

            // Marcado ANTES do sorteio, que é o fluxo real: a dupla escolhe (e paga) a janela na
            // inscrição, muito antes de existir chave.
            var todas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id)
                .OrderBy(d => d.Id).ToListAsync();
            for (int i = 0; i < impedidas; i++) todas[i].ImpedimentoSextaNoite = true;
            await ctx.SaveChangesAsync();

            await TestInfra.NovoTorneiosController(ctx, organizador.Id).GerarChaves(torneio.Id);

            furos.AddRange(await FurosAsync(ctx, torneio, sorteio));
        }

        Assert.True(furos.Count == 0,
            $"{quadras} quadra(s), {impedidas} de 16 impedidas: {furos.Count} jogos dentro da "
            + $"janela paga em {Sorteios} sorteios — {string.Join(" | ", furos.Take(6))}");
    }

    [Fact]
    public async Task E_todo_jogo_continua_ganhando_hora()
    {
        // ⚠️ O OUTRO LADO: alargar a grade não pode virar jogo sem horário. Pedir vagas com
        // sobra é a regra de ouro do arquivo ("nenhuma quadra fica sem jogo"), mas quem paga o
        // preço de um alcance grande demais é a dupla que fica sem hora nenhuma.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 16);
        torneio.DataInicio = SextaDeAbertura;
        torneio.QuantidadeQuadras = 2;
        await ctx.SaveChangesAsync();

        var todas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id)
            .OrderBy(d => d.Id).ToListAsync();
        for (int i = 0; i < 8; i++) todas[i].ImpedimentoSextaNoite = true;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.NotEmpty(jogos);
        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
    }

    private static async Task<List<string>> FurosAsync(DbPadelContext ctx, Torneio torneio, int sorteio)
    {
        var cheio = await ctx.Torneios
            .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
            .FirstAsync(t => t.Id == torneio.Id);
        var janelas = JanelasDeImpedimento.PorDupla(cheio);
        var jogos = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto != null).ToListAsync();

        return (from jogo in jogos
                from duplaId in new[] { jogo.Dupla1Id, jogo.Dupla2Id }
                where janelas.TryGetValue(duplaId, out var janelasDaDupla)
                      && janelasDaDupla.Any(j => jogo.HorarioPrevisto >= j.Inicio
                                              && jogo.HorarioPrevisto < j.Fim)
                select $"sorteio {sorteio}: dupla {duplaId} em {jogo.HorarioPrevisto:ddd dd/MM HH:mm}")
            .ToList();
    }
}
