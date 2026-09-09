using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 🗣️ RECLAMAÇÃO DE USUÁRIO, 09/09/2026: "ao tentar colocar que o jogador só pode sexta a noite
// por exemplo, nao consegue por os 2 jogos no sabado de manha. E também não consegue ver qual
// impedimento foi solicitado pelo usuário, e qual pelo organizador".
//
// 🕳️ AS DUAS QUEIXAS SÃO O MESMO DEFEITO, e ele era de DESENHO, não de código torto: em
// 08/09/2026 o impedimento e a concentração nasceram como UMA escolha só — um `<select>`, um
// enum, e um `Aplicar` que zerava um ao gravar o outro. O comentário de lá dizia, com todas as
// letras, "não existe estado com os dois marcados, e nenhuma tela precisa saber disso".
//
// Precisava. Elas são coisas de DONOS diferentes:
//   • o IMPEDIMENTO é o que o JOGADOR pede ("não posso sexta à noite") e paga por isso;
//   • a CONCENTRAÇÃO é o que o ORGANIZADOR faz por cima ("põe os 2 jogos no sábado de manhã").
//
// Sendo uma escolha só, o organizador que atendia o pedido do jogador APAGAVA o pedido — e
// depois não tinha como saber de quem tinha sido cada coisa. Agora são duas colunas
// independentes (já eram, no banco), dois formulários e dois rótulos.
public class ImpedimentoEConcentracaoConvivemTests
{
    // ⚠️ O TESTE QUE NOMEIA A RECLAMAÇÃO. O jogador pediu "não posso sexta à noite"; o
    // organizador põe "os 2 jogos no sábado de manhã". Os DOIS ficam.
    [Fact]
    public async Task Organizador_concentra_sem_apagar_o_impedimento_do_jogador()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        dupla.ImpedimentoSextaNoite = true;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarConcentracaoOrganizador(dupla.Id, TurnoDeConcentracao.SabadoManha);

        var salva = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.True(salva!.ImpedimentoSextaNoite);                                  // o pedido do jogador FICA
        Assert.Equal(TurnoDeConcentracao.SabadoManha, salva.ConcentrarJogosEm);     // e a do organizador entra
    }

    [Fact]
    public async Task E_o_contrario_tambem_mexer_no_impedimento_nao_apaga_a_concentracao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        dupla.ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarImpedimentoOrganizador(dupla.Id, TurnoDoImpedimento.SextaNoite);

        var salva = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.True(salva!.ImpedimentoSextaNoite);
        Assert.Equal(TurnoDeConcentracao.SabadoManha, salva.ConcentrarJogosEm);
    }

    [Fact]
    public async Task Tirar_a_concentracao_nao_leva_o_impedimento_junto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        dupla.ImpedimentoSextaNoite = true;
        dupla.ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarConcentracaoOrganizador(dupla.Id, TurnoDeConcentracao.Nenhuma);

        var salva = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.Null(salva!.ConcentrarJogosEm);
        Assert.True(salva.ImpedimentoSextaNoite);
    }

    // 💰 A CONCENTRAÇÃO CONTINUA DE GRAÇA (decisão do Felipe, 08/09) — e agora isso é literal, e
    // não uma conta: ela não mexe mais no `ValorInscricao`, porque não mexe mais no impedimento.
    // Antes ela ABAIXAVA o valor de quem tinha impedimento pago, e isso era efeito colateral de
    // as duas dividirem o mesmo campo.
    [Fact]
    public async Task Concentrar_nao_mexe_no_valor_de_quem_tem_impedimento_pago()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.TaxaPorImpedimento = 25m;
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        dupla.ImpedimentoSextaNoite = true;
        dupla.Pago = true;
        dupla.ValorInscricao = 325m;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarConcentracaoOrganizador(dupla.Id, TurnoDeConcentracao.SabadoManha);

        var salva = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.Equal(325m, salva!.ValorInscricao);
        Assert.True(salva.Pago);
    }

    [Fact]
    public async Task So_organizador_concentra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000022" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .AlterarConcentracaoOrganizador(dupla.Id, TurnoDeConcentracao.SabadoManha);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Null((await ctx.Duplas.FindAsync(dupla.Id))!.ConcentrarJogosEm);
    }

    [Fact]
    public async Task Nao_concentra_depois_do_sorteio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Chaves em Sorteio");
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToListAsync();
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Codigo = "P1",
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, Status = "Agendada",
        });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarConcentracaoOrganizador(duplas[0].Id, TurnoDeConcentracao.SabadoManha);

        Assert.Null((await ctx.Duplas.FindAsync(duplas[0].Id))!.ConcentrarJogosEm);
    }

    // O organizador mexeu por fora: os DOIS da dupla sabem, mesma régua do impedimento.
    [Fact]
    public async Task Os_dois_da_dupla_sao_avisados_da_concentracao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoTorneiosController(ctx, org.Id, push: push)
            .AlterarConcentracaoOrganizador(dupla.Id, TurnoDeConcentracao.SabadoManha);

        await push.Received(1).EnviarParaJogadorAsync(dupla.Jogador1Id, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.Received(1).EnviarParaJogadorAsync(dupla.Jogador2Id!.Value, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── As duas travas juntas, na grade ───────────────────────────────────────────────────

    // ⚠️ CONVIVER NÃO É SÓ GRAVAR: as duas precisam CHEGAR na grade. O impedimento vale em toda
    // fase; a concentração, só nos grupos. Elas já entram por parâmetros diferentes no
    // `Encaixar` — o que faltava era existirem ao mesmo tempo pra alguém passar.
    [Fact]
    public void As_duas_janelas_saem_juntas_da_mesma_dupla()
    {
        var torneio = new Torneio { DataInicio = new DateTime(2026, 8, 14, 18, 0, 0) };  // sexta
        var dupla = new Dupla { Id = 3, ImpedimentoSextaNoite = true, ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha };

        var impedimento = JanelasDeImpedimento.PorDupla(torneio, new[] { dupla });
        var concentracao = ConcentracaoDeJogos.PorDupla(torneio, new[] { dupla });

        Assert.True(impedimento.ContainsKey(3));
        Assert.True(concentracao.ContainsKey(3));
    }

    // ⚠️ E ELAS PODEM SE CONTRADIZER — "não posso sábado de manhã" + "os 2 jogos no sábado de
    // manhã" não sobra horário nenhum. A grade cede (jogo sem hora é pior), mas em silêncio: o
    // organizador precisa ser avisado NA TELA, senão ele acha que mandou e não mandou.
    [Fact]
    public void Impedimento_e_concentracao_no_mesmo_turno_sao_apontados_como_conflito()
    {
        var torneio = new Torneio { DataInicio = new DateTime(2026, 8, 14, 18, 0, 0) };
        var briga = new Dupla { ImpedimentoSabadoManha = true, ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha };

        Assert.True(ConcentracaoDeJogos.Conflita(torneio, briga));
    }

    [Fact]
    public void Turnos_diferentes_nao_sao_conflito()
    {
        var torneio = new Torneio { DataInicio = new DateTime(2026, 8, 14, 18, 0, 0) };
        var ok = new Dupla { ImpedimentoSextaNoite = true, ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha };

        Assert.False(ConcentracaoDeJogos.Conflita(torneio, ok));
    }

    [Fact]
    public void Sem_uma_das_duas_nao_ha_conflito()
    {
        var torneio = new Torneio { DataInicio = new DateTime(2026, 8, 14, 18, 0, 0) };

        Assert.False(ConcentracaoDeJogos.Conflita(torneio, new Dupla { ImpedimentoSabadoManha = true }));
        Assert.False(ConcentracaoDeJogos.Conflita(torneio,
            new Dupla { ConcentrarJogosEm = TurnoDeConcentracao.SabadoManha }));
    }
}
