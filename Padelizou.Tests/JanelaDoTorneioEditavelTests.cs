using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// A JANELA do torneio — quando começa, quando acaba e a que horas se joga em cada dia — só
// dava pra dizer na tela de CRIAÇÃO. Depois de publicado, o organizador ficava sem nada:
// "consegui a quadra até domingo", "na verdade a sexta começa 19h" e "o clube fecha 22h" são
// justamente as coisas que o clube remarca DEPOIS do torneio anunciado.
//
// A data de início já era editável sozinha, o que deixava a tela pela metade: dava pra mexer
// no primeiro dia e não no último, nem nos horários que a grade usa pra montar os jogos.
public class JanelaDoTorneioEditavelTests
{
    private static Task<IActionResult> EditarAsync(
        DbPadelContext ctx, Torneio torneio, int quemEdita,
        DateTime? dataInicio = null, DateTime? dataFim = null, bool dataFimInformada = true,
        TimeSpan? horaInicioDoDia = null, TimeSpan? horaInicioDiasSeguintes = null,
        TimeSpan? horaFimDoDia = null) =>
        TestInfra.NovoTorneiosController(ctx, quemEdita).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null,
            dataInicio: dataInicio ?? torneio.DataInicio,
            precoInscricao: torneio.PrecoInscricao, clubeId: torneio.ClubeId,
            quantidadeQuadras: torneio.QuantidadeQuadras, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: null, capa: null,
            dataFim: dataFim, dataFimInformada: dataFimInformada,
            horaInicioDoDia: horaInicioDoDia, horaInicioDiasSeguintes: horaInicioDiasSeguintes,
            horaFimDoDia: horaFimDoDia);

    [Fact]
    public async Task O_dia_de_termino_e_os_horarios_passam_a_valer()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");

        await EditarAsync(ctx, torneio, org.Id,
            dataInicio: new DateTime(2026, 11, 12),
            dataFim: new DateTime(2026, 11, 15),
            horaInicioDoDia: new TimeSpan(19, 0, 0),
            horaInicioDiasSeguintes: new TimeSpan(9, 0, 0),
            horaFimDoDia: new TimeSpan(22, 30, 0));

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(new DateTime(2026, 11, 12), salvo!.DataInicio);
        Assert.Equal(new DateTime(2026, 11, 15), salvo.DataFim);
        Assert.Equal(new TimeSpan(19, 0, 0), salvo.HoraInicioDoDia);
        Assert.Equal(new TimeSpan(9, 0, 0), salvo.HoraInicioDiasSeguintes);
        Assert.Equal(new TimeSpan(22, 30, 0), salvo.HoraFimDoDia);
    }

    [Fact]
    public async Task Campo_de_termino_em_branco_APAGA_o_limite()
    {
        // Tirar o limite é uma decisão real ("consegui a quadra pelo tempo que precisar"), e
        // sem isso o organizador nunca mais se livraria de uma data que ele mesmo pôs.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        torneio.DataFim = new DateTime(2026, 7, 3);
        await ctx.SaveChangesAsync();

        await EditarAsync(ctx, torneio, org.Id, dataFim: null, dataFimInformada: true);

        Assert.Null((await ctx.Torneios.FindAsync(torneio.Id))!.DataFim);
    }

    [Fact]
    public async Task Aba_antiga_nao_apaga_o_termino_nem_os_horarios()
    {
        // Página aberta antes deste deploy não manda nenhum dos campos novos. Sem a marca
        // `dataFimInformada`, esse nulo seria lido como "apaga" — e os horários virariam
        // 00:00, com a grade abrindo à meia-noite.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        torneio.DataFim = new DateTime(2026, 7, 3);
        torneio.HoraInicioDoDia = new TimeSpan(18, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(23, 50, 0);
        await ctx.SaveChangesAsync();

        await EditarAsync(ctx, torneio, org.Id, dataFim: null, dataFimInformada: false);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(new DateTime(2026, 7, 3), salvo!.DataFim);
        Assert.Equal(new TimeSpan(18, 0, 0), salvo.HoraInicioDoDia);
        Assert.Equal(new TimeSpan(23, 50, 0), salvo.HoraFimDoDia);
    }

    [Fact]
    public async Task Terminar_antes_de_comecar_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");

        await EditarAsync(ctx, torneio, org.Id,
            dataInicio: new DateTime(2026, 11, 12),
            dataFim: new DateTime(2026, 11, 10));

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Null(salvo!.DataFim);
        // A recusa vem ANTES de qualquer gravação: nada do resto pode ter entrado junto.
        Assert.Equal(new DateTime(2026, 7, 1, 9, 0, 0), salvo.DataInicio);
    }

    [Theory]
    // Último jogo começando antes de o primeiro dia abrir…
    [InlineData(18, 0, 8, 0, 7, 0)]
    // …e antes de os DEMAIS dias abrirem: a grade não teria como virar o dia, e empilharia o
    // torneio inteiro numa data só, madrugada adentro. O erro típico é 07:00 no lugar de 19:00.
    [InlineData(18, 0, 8, 0, 7, 30)]
    public async Task Janela_invertida_e_recusada(
        int inicioH, int inicioM, int seguintesH, int seguintesM, int fimH, int fimM)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        torneio.HoraFimDoDia = new TimeSpan(23, 50, 0);
        await ctx.SaveChangesAsync();

        await EditarAsync(ctx, torneio, org.Id,
            horaInicioDoDia: new TimeSpan(inicioH, inicioM, 0),
            horaInicioDiasSeguintes: new TimeSpan(seguintesH, seguintesM, 0),
            horaFimDoDia: new TimeSpan(fimH, fimM, 0));

        Assert.Equal(new TimeSpan(23, 50, 0), (await ctx.Torneios.FindAsync(torneio.Id))!.HoraFimDoDia);
    }

    // A recusa olha o resultado FINAL, não o campo digitado: mexer num horário só é o jeito
    // mais fácil de deixar a janela torta em relação aos outros dois.
    [Fact]
    public async Task Mexer_num_horario_so_ainda_e_conferido_contra_os_outros()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        torneio.HoraInicioDoDia = new TimeSpan(18, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(23, 50, 0);
        await ctx.SaveChangesAsync();

        // Só a abertura do 1º dia veio — e ela passa do último jogo já gravado.
        await EditarAsync(ctx, torneio, org.Id, horaInicioDoDia: new TimeSpan(23, 55, 0));

        Assert.Equal(new TimeSpan(18, 0, 0), (await ctx.Torneios.FindAsync(torneio.Id))!.HoraInicioDoDia);
    }
}
