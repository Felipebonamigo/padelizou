using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A VOTAÇÃO DO MVP: CHAMAR GENTE PRA VOTAR E A ORGANIZAÇÃO ACOMPANHAR (23/11/2026).
// 🗣️ Felipe, com o print da página do MVP da Wallau: *"crie um botão para compartilhar link (para
// chamar as pessoas para votarem) e deixe os organizadores (e eu) vendo como está a votação"*.
//
// ⚠️ A cédula CONTINUA sem número enquanto a votação está aberta (o efeito manada de 15/09 não
// mudou): a parcial mora num campo À PARTE, preenchido só pra quem passa na régua de organizador
// (EhOrganizadorAsync — organizador, admin raiz, admin nomeado).
public class MvpParcialECompartilharTests
{
    private static readonly DateTime Agora = new(2026, 11, 23, 9, 30, 0);

    private static async Task<(DbPadelContext ctx, Torneio torneio, Jogador org, Dupla campea, Dupla vice)> VotacaoAbertaAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        torneio.UsaVotacaoDeMvp = true;
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();
        duplas[0].UltimaFase = CampeoesDoTorneio.FaseDeCampeao;
        duplas[1].UltimaFase = "Final";
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id,
            VencedorId = duplas[0].Id, Status = "Finalizada", HorarioFimReal = Agora.AddHours(-2), Fase = "Final", Codigo = "P1",
        });
        await ctx.SaveChangesAsync();
        return (ctx, torneio, org, duplas[0], duplas[1]);
    }

    private static void Votos(DbPadelContext ctx, int torneioId, int candidatoId, params int[] votantes)
    {
        foreach (var v in votantes)
            ctx.VotosDeMvp.Add(new VotoDeMvp { TorneioId = torneioId, VotanteId = v, CandidatoId = candidatoId, CriadoEm = Agora.AddHours(-1) });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Com_a_parcial_liberada_a_organizacao_ve_os_votos_e_a_cedula_continua_zerada()
    {
        var (ctx, torneio, _, campea, vice) = await VotacaoAbertaAsync();
        int j1 = campea.Jogador1Id, j2 = campea.Jogador2Id ?? 0;
        Votos(ctx, torneio.Id, j2, vice.Jogador1Id, vice.Jogador2Id ?? 0);
        Votos(ctx, torneio.Id, j1, j2);

        var v = await MvpDoTorneio.DoTorneioAsync(ctx, torneio.Id, null, Agora, verParcial: true);

        Assert.True(v!.Aberta);
        var parcial = Assert.Single(v.Cedulas).Parcial;
        Assert.Equal(new[] { (j2, 2), (j1, 1) }, parcial.Select(c => (c.JogadorId, c.Votos)));
        Assert.All(v.Candidatos, c => Assert.Equal(0, c.Votos));   // a cédula não muda
    }

    [Fact]
    public async Task Sem_a_parcial_liberada_ninguem_ve_numero_com_a_votacao_aberta()
    {
        var (ctx, torneio, _, campea, vice) = await VotacaoAbertaAsync();
        Votos(ctx, torneio.Id, campea.Jogador1Id, vice.Jogador1Id);

        var v = await MvpDoTorneio.DoTorneioAsync(ctx, torneio.Id, vice.Jogador1Id, Agora);

        Assert.Empty(Assert.Single(v!.Cedulas).Parcial);
    }

    [Fact]
    public async Task So_quem_organiza_recebe_a_parcial_na_pagina()
    {
        var (ctx, torneio, org, campea, vice) = await VotacaoAbertaAsync();
        Votos(ctx, torneio.Id, campea.Jogador1Id, vice.Jogador1Id);

        var doOrganizador = Assert.IsType<ViewResult>(await TestInfra.NovoTorneiosController(ctx, org.Id).Mvp(torneio.Id));
        var doJogador = Assert.IsType<ViewResult>(await TestInfra.NovoTorneiosController(ctx, vice.Jogador1Id).Mvp(torneio.Id));

        Assert.NotEmpty(Assert.IsType<VotacaoDeMvp>(doOrganizador.Model).Cedulas[0].Parcial);
        Assert.Empty(Assert.IsType<VotacaoDeMvp>(doJogador.Model).Cedulas[0].Parcial);
    }

    [Fact]
    public void A_pagina_tem_o_botao_de_compartilhar_e_o_bloco_da_parcial()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) dir = dir.Parent;
        var view = TestInfra.SemComentarios(File.ReadAllText(Path.Combine(
            dir?.FullName ?? throw new DirectoryNotFoundException(), "Padelizou", "Views", "Torneios", "Mvp.cshtml")));

        Assert.Contains("pdz-compartilhar-texto", view);
        Assert.Contains("compartilhar-texto.js", view);
        Assert.Contains(".Parcial", view);
    }
}
