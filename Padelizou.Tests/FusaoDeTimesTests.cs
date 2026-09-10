using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Juntar dois times que são o mesmo time escrito de dois jeitos.
//
// A régua é UMA só, e vale igual no perfil e na migration que limpa a base: sobrevive o de
// MAIS jogadores, e no empate o de menor Id. Duas réguas dariam resultados diferentes pro
// mesmo par de times dependendo de quem chamou — e a fusão apaga uma linha, então divergir
// aqui não é detalhe de estilo.
//
// ⚠️ O nome do sobrevivente NÃO muda na fusão. Como a colisão é por `lower(nome)`, o nome que
// a pessoa digitou só pode diferir do que já está lá na caixa das letras — e aplicá-lo deixaria
// alguém trocar a grafia de um time que não administra só por digitar o nome dele.
public class FusaoDeTimesTests
{
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Times.AddRange(
            new Time { Id = 10, Nome = "ER Padel", Logo = "/uploads/logos-time/er.png" },
            new Time { Id = 11, Nome = "Er padel" });
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Rafael", Cpf = "1", TimeId = 10 },
            new Jogador { Id = 2, Nome = "Bruno", Cpf = "2", TimeId = 10 },
            new Jogador { Id = 3, Nome = "Camila", Cpf = "3", TimeId = 11 });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task Sobrevive_o_time_com_mais_jogadores_e_o_nome_dele_fica()
    {
        using var ctx = Cenario();

        var sobrevivente = await FusaoDeTimes.FundirAsync(ctx, 11, 10);
        await ctx.SaveChangesAsync();

        Assert.Equal(10, sobrevivente.Id);
        Assert.Equal("ER Padel", sobrevivente.Nome);
        Assert.Single(ctx.Times);
    }

    [Fact]
    public async Task Empate_no_numero_de_jogadores_faz_sobreviver_o_de_menor_Id()
    {
        using var ctx = Cenario();
        (await ctx.Jogadores.FindAsync(2))!.TimeId = null;   // sobra 1 jogador em cada
        await ctx.SaveChangesAsync();

        // Chamado com o 11 na frente de propósito: se o desempate fosse "ganha o primeiro
        // argumento" em vez do menor Id, este teste passaria a devolver 11.
        var sobrevivente = await FusaoDeTimes.FundirAsync(ctx, 11, 10);
        await ctx.SaveChangesAsync();

        Assert.Equal(10, sobrevivente.Id);
    }

    [Fact]
    public async Task Os_jogadores_do_absorvido_passam_a_vestir_a_camisa_do_sobrevivente()
    {
        using var ctx = Cenario();

        await FusaoDeTimes.FundirAsync(ctx, 10, 11);
        await ctx.SaveChangesAsync();

        Assert.Equal(10, (await ctx.Jogadores.FindAsync(3))!.TimeId);
        Assert.Equal(3, ctx.Jogadores.Count(j => j.TimeId == 10));
    }

    [Fact]
    public async Task A_dupla_time_de_torneio_antigo_aponta_pro_sobrevivente()
    {
        // O vínculo da Dupla com o time é só o escudo na tela, mas deixá-lo apontando pra um
        // Id apagado tiraria o escudo de um torneio já encerrado.
        using var ctx = Cenario();
        ctx.Duplas.Add(new Dupla { Id = 100, TimeId = 11 });
        await ctx.SaveChangesAsync();

        await FusaoDeTimes.FundirAsync(ctx, 10, 11);
        await ctx.SaveChangesAsync();

        Assert.Equal(10, (await ctx.Duplas.FindAsync(100))!.TimeId);
    }

    [Fact]
    public async Task A_historia_de_transferencias_segue_o_sobrevivente()
    {
        using var ctx = Cenario();
        ctx.TransferenciasDeTime.Add(new TransferenciaDeTime
        {
            Id = 500, JogadorId = 3, TimeAnteriorId = null, TimeNovoId = 11, Em = DateTime.Now,
        });
        await ctx.SaveChangesAsync();

        await FusaoDeTimes.FundirAsync(ctx, 10, 11);
        await ctx.SaveChangesAsync();

        Assert.Equal(10, (await ctx.TransferenciasDeTime.FindAsync(500))!.TimeNovoId);
    }

    [Fact]
    public async Task Transferencia_que_viraria_do_time_pra_ele_mesmo_e_apagada()
    {
        // Quem já tinha passado de uma grafia pra outra viraria "saiu do ER Padel e entrou no
        // ER Padel" depois do repontamento — a linha que o TransferenciasDeTime.Registrar se
        // recusa a criar justamente por não querer dizer nada.
        using var ctx = Cenario();
        ctx.TransferenciasDeTime.Add(new TransferenciaDeTime
        {
            Id = 501, JogadorId = 3, TimeAnteriorId = 10, TimeNovoId = 11, Em = DateTime.Now,
        });
        await ctx.SaveChangesAsync();

        await FusaoDeTimes.FundirAsync(ctx, 10, 11);
        await ctx.SaveChangesAsync();

        Assert.Null(await ctx.TransferenciasDeTime.FindAsync(501));
    }

    [Fact]
    public async Task A_administracao_do_absorvido_nao_e_herdada()
    {
        // ⚠️ Quem administrava o time absorvido NÃO passa a mandar no sobrevivente: a Camila
        // comandava 2 pessoas, e herdar daria a ela o comando de 21. Reparo de dado não pode
        // virar promoção — é a mesma razão pela qual digitar o nome de um time existente no
        // cadastro não dá cargo nenhum (DefinirTimeAsync).
        using var ctx = Cenario();
        ctx.TimeAdministradores.Add(new TimeAdministrador
        {
            TimeId = 11, JogadorId = 3, ConcedidoPorId = 3, ConcedidoEm = DateTime.Now,
        });
        await ctx.SaveChangesAsync();

        await FusaoDeTimes.FundirAsync(ctx, 10, 11);
        await ctx.SaveChangesAsync();

        Assert.Empty(ctx.TimeAdministradores);
    }

    [Fact]
    public async Task Quem_ja_administrava_o_sobrevivente_continua_administrando()
    {
        using var ctx = Cenario();
        ctx.TimeAdministradores.Add(new TimeAdministrador
        {
            TimeId = 10, JogadorId = 1, ConcedidoPorId = 1, ConcedidoEm = DateTime.Now,
        });
        await ctx.SaveChangesAsync();

        await FusaoDeTimes.FundirAsync(ctx, 10, 11);
        await ctx.SaveChangesAsync();

        Assert.Single(ctx.TimeAdministradores.Where(a => a.TimeId == 10 && a.JogadorId == 1));
    }

    [Fact]
    public async Task Sede_que_o_sobrevivente_ainda_nao_tem_e_trazida()
    {
        using var ctx = Cenario();
        ctx.Clubes.AddRange(new Clube { Id = 1, Nome = "Er Padel" }, new Clube { Id = 2, Nome = "Radar" });
        ctx.TimeSedes.AddRange(
            new TimeSede { TimeId = 10, ClubeId = 1 },
            new TimeSede { TimeId = 11, ClubeId = 2 });
        await ctx.SaveChangesAsync();

        await FusaoDeTimes.FundirAsync(ctx, 10, 11);
        await ctx.SaveChangesAsync();

        Assert.Equal(new[] { 1, 2 }, ctx.TimeSedes.Where(s => s.TimeId == 10).Select(s => s.ClubeId).OrderBy(c => c));
    }

    [Fact]
    public async Task Sede_repetida_nao_estoura_a_chave_composta()
    {
        // TimeSede tem chave (TimeId, ClubeId): mover cego a sede que os dois já têm
        // duplicaria a chave e derrubaria a gravação inteira.
        using var ctx = Cenario();
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Er Padel" });
        ctx.TimeSedes.AddRange(
            new TimeSede { TimeId = 10, ClubeId = 1 },
            new TimeSede { TimeId = 11, ClubeId = 1 });
        await ctx.SaveChangesAsync();

        await FusaoDeTimes.FundirAsync(ctx, 10, 11);
        await ctx.SaveChangesAsync();

        Assert.Single(ctx.TimeSedes);
        Assert.Equal(10, ctx.TimeSedes.Single().TimeId);
    }
}
