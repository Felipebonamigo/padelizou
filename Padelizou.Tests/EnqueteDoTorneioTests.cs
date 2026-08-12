using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A ENQUETE PÓS-TORNEIO: quem jogou dá nota pro clube e pra organização, na mesma janela de 7
// dias do MVP. Existe por causa de 2027 — o "Melhor Clube do ano" precisa de um ano de coleta.
//
// O que estes testes guardam:
//   · a janela é a MESMA do MVP, mas o interruptor UsaVotacaoDeMvp NÃO manda aqui;
//   · quem responde é quem JOGOU (lista de espera e sem-parceiro ficam de fora);
//   · responder de novo TROCA a resposta, nunca soma outra;
//   · a média só existe com 3+ respostas — antes disso é uma pessoa com voz de consenso.
public class EnqueteDoTorneioTests
{
    private static readonly DateTime Domingo = new(2026, 8, 9, 20, 0, 0);

    // Um torneio finalizado com o último jogo AGORA: janela aberta.
    private static async Task<(Torneio torneio, List<Dupla> duplas)> MontarAsync(DbPadelContext ctx)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();
        duplas[0].UltimaFase = "Campeao";
        duplas[1].UltimaFase = "Final";

        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, VencedorId = duplas[0].Id,
            Status = "Finalizada", HorarioFimReal = Domingo, Fase = "Final", Codigo = "P1",
        });
        await ctx.SaveChangesAsync();
        return (torneio, duplas);
    }

    [Fact]
    public async Task Quem_jogou_avalia_e_a_resposta_fica_gravada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, duplas[1].Jogador1Id, notaClube: 5, notaOrganizacao: 4, Domingo.AddHours(2));

        Assert.Null(recusa);
        var gravada = Assert.Single(ctx.AvaliacoesDeTorneio);
        Assert.Equal(5, gravada.NotaClube);
        Assert.Equal(4, gravada.NotaOrganizacao);
    }

    [Fact]
    public async Task Responder_de_novo_TROCA_a_resposta_em_vez_de_somar_outra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);
        var quem = duplas[1].Jogador1Id;

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, 5, 5, Domingo.AddHours(1));
        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, quem, 2, 3, Domingo.AddHours(3));

        var unica = Assert.Single(ctx.AvaliacoesDeTorneio);
        Assert.Equal(2, unica.NotaClube);
        Assert.Equal(3, unica.NotaOrganizacao);
        Assert.NotNull(unica.AtualizadoEm);
    }

    [Fact]
    public async Task So_quem_JOGOU_responde()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await MontarAsync(ctx);

        var deFora = new Jogador { Nome = "So Assistiu", Cpf = "55500000010" };
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, deFora.Id, 5, 5, Domingo.AddHours(1));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.AvaliacoesDeTorneio);
    }

    [Fact]
    public async Task Fora_da_janela_de_7_dias_a_enquete_recusa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, duplas[1].Jogador1Id, 4, 4,
            Domingo.AddDays(MvpDoTorneio.DiasParaVotar));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.AvaliacoesDeTorneio);
    }

    [Fact]
    public async Task O_interruptor_do_MVP_nao_manda_na_enquete()
    {
        // O organizador desligou a DISPUTA entre jogadores — a coleta sobre o clube é nossa,
        // e continua valendo. Sem isso, o dado do "Melhor Clube 2027" nasceria com buraco
        // exatamente nos torneios de confraternização, que são clube como qualquer outro.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);
        ctx.Torneios.First(t => t.Id == torneio.Id).UsaVotacaoDeMvp = false;
        await ctx.SaveChangesAsync();

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, duplas[1].Jogador1Id, 4, 5, Domingo.AddHours(2));

        Assert.Null(recusa);
        Assert.Single(ctx.AvaliacoesDeTorneio);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(6, 5)]
    [InlineData(5, 0)]
    public async Task Nota_fora_de_1_a_5_recusa(int clube, int organizacao)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, duplas[1].Jogador1Id, clube, organizacao, Domingo.AddHours(1));

        Assert.NotNull(recusa);
        Assert.Empty(ctx.AvaliacoesDeTorneio);
    }

    [Fact]
    public async Task A_media_so_aparece_com_3_respostas_e_sai_certa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, duplas) = await MontarAsync(ctx);

        // Os 4 que jogaram: 3 respondem.
        var jogadores = new[]
        {
            duplas[0].Jogador1Id, duplas[0].Jogador2Id!.Value, duplas[1].Jogador1Id,
        };
        var notas = new[] { (5, 4), (4, 4), (3, 1) };

        for (int i = 0; i < 2; i++)
            await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, jogadores[i],
                notas[i].Item1, notas[i].Item2, Domingo.AddHours(1));

        // Com 2 respostas: contagem sim, média não.
        var cedo = await EnqueteDoTorneio.ResumoAsync(ctx, torneio.Id);
        Assert.Equal(2, cedo.Respostas);
        Assert.False(cedo.TemMedia);

        await EnqueteDoTorneio.AvaliarAsync(ctx, torneio.Id, jogadores[2],
            notas[2].Item1, notas[2].Item2, Domingo.AddHours(1));

        var resumo = await EnqueteDoTorneio.ResumoAsync(ctx, torneio.Id);
        Assert.Equal(3, resumo.Respostas);
        Assert.True(resumo.TemMedia);
        Assert.Equal(4.0, resumo.MediaClube);          // (5+4+3)/3
        Assert.Equal(3.0, resumo.MediaOrganizacao);    // (4+4+1)/3
    }

    [Fact]
    public async Task Quem_ficou_na_lista_de_espera_nao_avalia()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await MontarAsync(ctx);
        var categoria = ctx.Categorias.First(c => c.TorneioId == torneio.Id);

        var esperou = new Jogador { Nome = "Quem Esperou", Cpf = "55500000011" };
        ctx.Jogadores.Add(esperou);
        await ctx.SaveChangesAsync();
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = esperou.Id, Jogador2Id = esperou.Id,
            EmListaDeEspera = true,
        });
        await ctx.SaveChangesAsync();

        var recusa = await EnqueteDoTorneio.AvaliarAsync(
            ctx, torneio.Id, esperou.Id, 5, 5, Domingo.AddHours(1));

        Assert.NotNull(recusa);
    }
}
