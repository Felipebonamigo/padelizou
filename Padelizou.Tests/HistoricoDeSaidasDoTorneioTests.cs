using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// O ORGANIZADOR PRECISA SABER QUEM SAIU — E DEPOIS CONSEGUIR OLHAR (Felipe, 25/09/2026): *"o
// organizador receber a notificação quando alguem ou alguma dupla cancelar sua inscrição do
// torneio, e ter um histórico para isso, para ver quem desistiu"*.
//
// ⚠️ NÃO HAVIA HISTÓRICO NENHUM PRA MOSTRAR: `TirarDaInscricaoAsync` faz `Duplas.Remove(dupla)`.
// Depois do cancelamento não sobrava nome, data, nem se estava paga — não era registro escondido,
// era ausência de dado. Por isso esta tabela nasceu.
//
// ⚠️ UMA LINHA POR SAÍDA, E NÃO POR PESSOA. A dupla que sai inteira abre UMA vaga, não duas —
// com uma linha por pessoa, a contagem de vagas abertas dobraria. E responde "quem desistiu" do
// mesmo jeito, porque os dois nomes estão na linha.
//
// ⚠️ SÃO QUATRO PORTAS DE SAÍDA. Registrar só a que o pedido nomeia daria um histórico que mente
// por omissão — o mesmo padrão que custou a vitrine e a Home em 20/09.
public class HistoricoDeSaidasDoTorneioTests
{
    private static async Task<(DbPadelContext ctx, Torneio t, Categoria cat, Jogador a, Jogador b, Jogador org)>
        MontarAsync(bool paga = false)
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio { Nome = "Copa de Verão", Codigo = "CV01", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var cat = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", TorneioId = torneio.Id };
        ctx.Categorias.Add(cat);

        var a = new Jogador { Nome = "Maickel Souza", Cpf = "11144477735" };
        var b = new Jogador { Nome = "Otávio Wunsch", Cpf = "22255588846" };
        var org = new Jogador { Nome = "Felipe", Cpf = "33366699957" };
        ctx.Jogadores.AddRange(a, b, org);
        await ctx.SaveChangesAsync();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = org.Id });
        await ctx.SaveChangesAsync();

        return (ctx, torneio, cat, a, b, org);
    }

    private static Dupla Inscrita(DbPadelContext ctx, Categoria cat, Jogador a, Jogador? b, bool paga = false)
    {
        var dupla = new Dupla { CategoriaId = cat.Id, Jogador1Id = a.Id, Jogador2Id = b?.Id, Pago = paga };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();
        return dupla;
    }

    // ── A régua pura: o que vira linha ────────────────────────────────────────────────────

    [Fact]
    public void A_dupla_inteira_que_sai_vira_UMA_linha_com_os_dois_nomes()
    {
        var saida = RegistroDeSaida.Montar(
            torneioId: 1, categoriaId: 2, jogador1Id: 10, jogador2Id: 11,
            quemPediuId: 10, motivo: MotivoDaSaida.Desistiu,
            observacao: "  lesão no joelho  ", estavaPaga: true, abriuVaga: true, agora: new DateTime(2026, 9, 25));

        Assert.Equal(10, saida.Jogador1Id);
        Assert.Equal(11, saida.Jogador2Id);
        Assert.True(saida.AbriuVaga);
        Assert.True(saida.EstavaPaga);
        Assert.Equal(MotivoDaSaida.Desistiu, saida.Motivo);

        // Aparado: espaço em volta vira ruído na lista do organizador.
        Assert.Equal("lesão no joelho", saida.Observacao);
    }

    [Fact]
    public void Observacao_em_branco_vira_NULO_e_nao_string_vazia()
    {
        // ⚠️ O campo é opcional, então o caminho comum é vir vazio. Gravar "" faria a tela ter
        // que saber distinguir "não disse nada" de "disse nada" — duas formas do mesmo estado é
        // como nasce o `if` que uma das telas esquece.
        foreach (var vazio in new[] { null, "", "   " })
        {
            var saida = RegistroDeSaida.Montar(1, 2, 10, null, 10, MotivoDaSaida.Desistiu,
                vazio, estavaPaga: false, abriuVaga: true, agora: DateTime.Now);
            Assert.Null(saida.Observacao);
        }
    }

    [Fact]
    public void Quando_o_sistema_tira_por_falta_de_pagamento_nao_ha_quem_pediu()
    {
        // ⚠️ `QuemPediuId` nulo é a assinatura do automático (LembreteInscricaoNaoPaga /
        // PagamentoExpirado). Pôr o id do organizador aqui faria a lista dizer que ELE removeu
        // alguém que ele nunca tocou.
        var saida = RegistroDeSaida.Montar(1, 2, 10, 11, quemPediuId: null,
            MotivoDaSaida.NaoPagou, null, estavaPaga: false, abriuVaga: true, agora: DateTime.Now);

        Assert.Null(saida.QuemPediuId);
        Assert.Equal(MotivoDaSaida.NaoPagou, saida.Motivo);
    }

    // ── As quatro portas ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Desistir_da_inscricao_inteira_deixa_rastro()
    {
        var (ctx, t, cat, a, b, _) = await MontarAsync();
        using var _c = ctx;
        var dupla = Inscrita(ctx, cat, a, b, paga: true);

        var controller = TestInfra.NovoTorneiosController(ctx, a.Id);
        await controller.Desistir(dupla.Id, EscolhaDeQuemSai.ADuplaInteira, "não vamos conseguir ir");

        // A inscrição sumiu (é o comportamento de sempre) e o rastro ficou.
        Assert.Empty(await ctx.Duplas.ToListAsync());

        var saida = Assert.Single(await ctx.SaidasDoTorneio.ToListAsync());
        Assert.Equal(t.Id, saida.TorneioId);
        Assert.Equal(cat.Id, saida.CategoriaId);
        Assert.Equal(MotivoDaSaida.Desistiu, saida.Motivo);
        Assert.Equal(a.Id, saida.QuemPediuId);
        Assert.True(saida.EstavaPaga);
        Assert.True(saida.AbriuVaga);
        Assert.Equal("não vamos conseguir ir", saida.Observacao);
    }

    [Fact]
    public async Task So_eu_saio_registra_a_pessoa_e_NAO_conta_vaga_aberta()
    {
        var (ctx, _, cat, a, b, _) = await MontarAsync();
        using var _c = ctx;
        var dupla = Inscrita(ctx, cat, a, b);

        var controller = TestInfra.NovoTorneiosController(ctx, a.Id);
        await controller.Desistir(dupla.Id, EscolhaDeQuemSai.SoEu, null);

        // ⚠️ A INSCRIÇÃO CONTINUA DE PÉ: o parceiro segue inscrito, sem dupla fechada. Contar
        // vaga aberta aqui faria o organizador sair atrás de alguém pra um lugar que não vagou.
        Assert.Single(await ctx.Duplas.ToListAsync());

        var saida = Assert.Single(await ctx.SaidasDoTorneio.ToListAsync());
        Assert.Equal(a.Id, saida.Jogador1Id);
        Assert.Null(saida.Jogador2Id);       // o que FICOU não entra: ele não saiu
        Assert.False(saida.AbriuVaga);
    }

    [Fact]
    public async Task O_organizador_removendo_tambem_entra_no_historico()
    {
        var (ctx, _, cat, a, b, org) = await MontarAsync();
        using var _c = ctx;
        var dupla = Inscrita(ctx, cat, a, b);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.RemoverDupla(dupla.Id);

        // ⚠️ Ele não é AVISADO do próprio clique, mas o registro fica: com dois organizadores,
        // um não tem como saber o que o outro fez.
        var saida = Assert.Single(await ctx.SaidasDoTorneio.ToListAsync());
        Assert.Equal(MotivoDaSaida.RemovidoPeloOrganizador, saida.Motivo);
        Assert.Equal(org.Id, saida.QuemPediuId);
    }

    // ── O aviso ao organizador ────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_organizador_e_avisado_mesmo_quando_a_inscricao_NAO_estava_paga()
    {
        var (ctx, _, cat, a, b, org) = await MontarAsync();
        using var _c = ctx;
        var dupla = Inscrita(ctx, cat, a, b, paga: false);

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, a.Id, push: push);
        await controller.Desistir(dupla.Id, EscolhaDeQuemSai.ADuplaInteira, null);

        // ⚠️ ERA ISTO QUE FALTAVA. `AvisarOrganizadorDeSaidaPagaAsync` abria com
        // `if (!eraPaga) return;` — o aviso nasceu pro ESTORNO, não pra vaga, e quem desistia
        // sem ter pago saía em silêncio. A vaga aberta é o que faz o organizador correr atrás
        // de quem a preencha, e ela abre pagando ou não.
        var avisados = push.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync))
            .Select(c => (int)c.GetArguments()[0]!)
            .ToList();

        Assert.Contains(org.Id, avisados);
    }

    [Fact]
    public async Task O_organizador_NAO_e_avisado_do_proprio_clique()
    {
        var (ctx, _, cat, a, b, org) = await MontarAsync();
        using var _c = ctx;
        var dupla = Inscrita(ctx, cat, a, b);

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);
        await controller.RemoverDupla(dupla.Id);

        var avisados = push.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync))
            .Select(c => (int)c.GetArguments()[0]!)
            .ToList();

        // Quem ele removeu é avisado; ele, não. Aviso do próprio clique é como se ensina
        // alguém a ignorar o canal.
        Assert.DoesNotContain(org.Id, avisados);
    }

    // ── A tela ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_nao_organiza_nao_ve_o_historico()
    {
        var (ctx, t, cat, a, b, _) = await MontarAsync();
        using var _c = ctx;

        // ⚠️ REGRA 0. A lista diz quem desistiu e quanto tinha pago — é informação do
        // organizador sobre os inscritos dele, e `EhOrganizadorAsync` é a régua de sempre.
        var resultado = await TestInfra.NovoTorneiosController(ctx, a.Id).Desistencias(t.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
    }

    [Fact]
    public async Task O_organizador_ve_as_saidas_do_torneio_dele_e_so_dele()
    {
        var (ctx, t, cat, a, b, org) = await MontarAsync();
        using var _c = ctx;

        var outro = new Torneio { Nome = "Outra Copa", Codigo = "OC01", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(outro);
        await ctx.SaveChangesAsync();

        ctx.SaidasDoTorneio.AddRange(
            RegistroDeSaida.Montar(t.Id, cat.Id, a.Id, b.Id, a.Id, MotivoDaSaida.Desistiu,
                "lesão", true, true, new DateTime(2026, 9, 20)),
            RegistroDeSaida.Montar(t.Id, cat.Id, b.Id, null, b.Id, MotivoDaSaida.Desistiu,
                null, false, false, new DateTime(2026, 9, 24)),
            RegistroDeSaida.Montar(outro.Id, cat.Id, a.Id, null, a.Id, MotivoDaSaida.Desistiu,
                null, false, true, new DateTime(2026, 9, 25)));
        await ctx.SaveChangesAsync();

        var vista = await TestInfra.NovoTorneiosController(ctx, org.Id).Desistencias(t.Id);
        var modelo = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model;
        var linhas = Assert.IsAssignableFrom<IReadOnlyList<SaidaNaTela>>(modelo);

        // ⚠️ A saída do OUTRO torneio não pode aparecer: a lista é da gestão de UM torneio, e
        // vazar a de outro mostra ao organizador nome de quem não é inscrito dele.
        Assert.Equal(2, linhas.Count);

        // Da mais recente pra mais antiga — quem abre esta tela quer saber o que mudou hoje.
        Assert.Equal(new DateTime(2026, 9, 24), linhas[0].SaiuEm);

        // O nome é resolvido na LEITURA (não há cópia congelada no histórico — ver o modelo).
        Assert.Contains(a.Nome, linhas[1].QuemSaiu);
        Assert.Contains(b.Nome, linhas[1].QuemSaiu);
        Assert.Equal("lesão", linhas[1].Observacao);
        Assert.True(linhas[1].AbriuVaga);
    }

    [Fact]
    public async Task So_eu_saio_nao_acorda_o_organizador()
    {
        var (ctx, _, cat, a, b, org) = await MontarAsync();
        using var _c = ctx;
        var dupla = Inscrita(ctx, cat, a, b);

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, a.Id, push: push);
        await controller.Desistir(dupla.Id, EscolhaDeQuemSai.SoEu, null);

        var avisados = push.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync))
            .Select(c => (int)c.GetArguments()[0]!)
            .ToList();

        // Nenhuma vaga abriu — não há o que ele faça. Fica no histórico, que é onde mora o
        // que é bom saber e não pede ação.
        Assert.DoesNotContain(org.Id, avisados);
    }
}
