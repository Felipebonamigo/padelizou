using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// A assinatura do clube: Rede (grátis), Gestão e Fiscal.
//
// Os testes cobrem as duas coisas que não podem falhar — **o dinheiro** (uma cobrança aberta
// por vez, o que foi comprado é o que se recebe) e **a porta** (quem parou de pagar para de
// entrar, e quem está em teste ou carência continua entrando).
public class PlanoDoClubeTests
{
    private static readonly PlanoClubeSettings Cfg = new();
    private static readonly DateTime Hoje = new(2026, 8, 19, 10, 0, 0);

    // ---------- A régua, pura ----------

    [Fact]
    public void Clube_sem_plano_nao_abre_o_bar_mas_continua_com_a_rede()
    {
        var clube = new Clube { Nome = "Chakra" };

        Assert.Equal(PlanoDoClube.Situacao.SemPlano, PlanoDoClube.SituacaoDe(clube, Hoje, Cfg));
        Assert.False(PlanoDoClube.LiberaGestao(clube, Hoje, Cfg));
        Assert.False(PlanoDoClube.LiberaFiscal(clube, Hoje, Cfg));

        // A rede é grátis e não passa por aqui — este teste existe pra registrar que "sem
        // plano" nunca foi "sem sistema".
        Assert.Equal("Clube Rede", PlanoDoClube.RotuloDoPlano(clube.PlanoDoClube));
    }

    [Fact]
    public void Teste_de_15_dias_abre_o_bar_e_acaba_no_dia_certo()
    {
        var clube = new Clube { PlanoDoClube = PlanoDoClube.Gestao, TesteDoClubeInicio = Hoje };

        Assert.True(PlanoDoClube.LiberaGestao(clube, Hoje, Cfg));
        Assert.True(PlanoDoClube.LiberaGestao(clube, Hoje.AddDays(15), Cfg));   // o 15º ainda vale
        Assert.False(PlanoDoClube.LiberaGestao(clube, Hoje.AddDays(16), Cfg));
    }

    [Fact]
    public void Carencia_de_7_dias_segura_o_balcao_de_quem_esqueceu_o_boleto()
    {
        // O estrago de fechar cedo demais não se desfaz pagando depois: a venda do sábado que
        // não foi registrada não volta.
        var clube = new Clube
        {
            PlanoDoClube = PlanoDoClube.Gestao,
            AssinaturaClubePagaAte = Hoje
        };

        Assert.True(PlanoDoClube.LiberaGestao(clube, Hoje.AddDays(7), Cfg));
        Assert.Equal(PlanoDoClube.Situacao.EmDia, PlanoDoClube.SituacaoDe(clube, Hoje.AddDays(7), Cfg));

        Assert.False(PlanoDoClube.LiberaGestao(clube, Hoje.AddDays(8), Cfg));
        Assert.Equal(PlanoDoClube.Situacao.EmAtraso, PlanoDoClube.SituacaoDe(clube, Hoje.AddDays(8), Cfg));
    }

    [Fact]
    public void Fiscal_inclui_gestao_mas_gestao_nao_inclui_fiscal()
    {
        // Não existe emitir nota de uma venda que o sistema não registra — por isso o Fiscal
        // carrega o Gestão junto, sempre.
        var fiscal = new Clube { PlanoDoClube = PlanoDoClube.Fiscal, AssinaturaClubePagaAte = Hoje.AddMonths(1) };
        Assert.True(PlanoDoClube.LiberaGestao(fiscal, Hoje, Cfg));
        Assert.True(PlanoDoClube.LiberaFiscal(fiscal, Hoje, Cfg));

        var gestao = new Clube { PlanoDoClube = PlanoDoClube.Gestao, AssinaturaClubePagaAte = Hoje.AddMonths(1) };
        Assert.True(PlanoDoClube.LiberaGestao(gestao, Hoje, Cfg));
        Assert.False(PlanoDoClube.LiberaFiscal(gestao, Hoje, Cfg));
    }

    [Fact]
    public void Fiscal_vencido_nao_emite_nem_com_o_plano_certo()
    {
        var clube = new Clube
        {
            PlanoDoClube = PlanoDoClube.Fiscal,
            AssinaturaClubePagaAte = Hoje.AddDays(-30)
        };

        Assert.False(PlanoDoClube.LiberaFiscal(clube, Hoje, Cfg));
    }

    [Fact]
    public void Pagar_adiantado_soma_no_fim_e_pagar_atrasado_conta_de_hoje()
    {
        // Atraso não vira crédito: quem sumiu dois meses não recebe os dois de volta.
        var adiantado = PlanoDoClube.NovaDataPagaAte(Hoje.AddDays(10), Hoje, CicloDeAssinatura.Mensal);
        Assert.Equal(Hoje.AddDays(10).AddMonths(1), adiantado);

        var atrasado = PlanoDoClube.NovaDataPagaAte(Hoje.AddDays(-60), Hoje, CicloDeAssinatura.Mensal);
        Assert.Equal(Hoje.AddMonths(1), atrasado);

        var anual = PlanoDoClube.NovaDataPagaAte(null, Hoje, CicloDeAssinatura.Anual);
        Assert.Equal(Hoje.AddMonths(12), anual);
    }

    [Fact]
    public void Precos_e_economia_do_anual_saem_da_configuracao()
    {
        Assert.Equal(99m, PlanoDoClube.ValorDo(PlanoDoClube.Gestao, CicloDeAssinatura.Mensal, Cfg));
        Assert.Equal(990m, PlanoDoClube.ValorDo(PlanoDoClube.Gestao, CicloDeAssinatura.Anual, Cfg));
        Assert.Equal(199m, PlanoDoClube.ValorDo(PlanoDoClube.Fiscal, CicloDeAssinatura.Mensal, Cfg));
        Assert.Equal(1990m, PlanoDoClube.ValorDo(PlanoDoClube.Fiscal, CicloDeAssinatura.Anual, Cfg));

        // "Dois meses grátis" não pode ser escrito na tela — vira mentira quando o preço muda.
        Assert.Equal(198m, PlanoDoClube.EconomiaDoAnual(PlanoDoClube.Gestao, Cfg));
        Assert.Equal(398m, PlanoDoClube.EconomiaDoAnual(PlanoDoClube.Fiscal, Cfg));
    }

    [Fact]
    public void Ciclo_desconhecido_e_mensal_e_a_conta_e_a_mesma_do_professor()
    {
        // A extração do CicloDeAssinatura não podia mudar o comportamento do professor.
        Assert.Equal(CicloDeAssinatura.Mensal, CicloDeAssinatura.Valido(null));
        Assert.Equal(CicloDeAssinatura.Mensal, CicloDeAssinatura.Valido("qualquer coisa"));
        Assert.Equal(1, CicloDeAssinatura.MesesDe(null));
        Assert.Equal(12, CicloDeAssinatura.MesesDe(CicloDeAssinatura.Anual));

        Assert.Equal(
            PlanoDoProfessor.NovaDataPagaAte(Hoje.AddDays(5), Hoje, CicloDeAssinatura.Anual),
            CicloDeAssinatura.NovaDataPagaAte(Hoje.AddDays(5), Hoje, CicloDeAssinatura.Anual));
    }

    // ---------- A trava do dinheiro ----------

    [Fact]
    public void Assinatura_de_clube_e_receita_100_por_cento_nossa()
    {
        // É o que autoriza o Pix direto: sem repasse a terceiro, não movimenta dinheiro alheio
        // e o valor cheio é faturamento nosso de verdade.
        Assert.True(PixDireto.AceitaPix(PixDireto.TipoAssinaturaClube));

        // E a trava continua fechada pro que tem repasse.
        Assert.False(PixDireto.AceitaPix("Torneio"));
        Assert.False(PixDireto.AceitaPix("Aula"));
        Assert.False(PixDireto.AceitaPix("Jogo"));
    }

    [Fact]
    public void Cobranca_com_repasse_nao_passa_por_pix_nem_disfarcada_de_assinatura_de_clube()
    {
        // Cinto e suspensório: mesmo marcada como Pix, uma cobrança com recebedor é recusada.
        var comRepasse = new Pagamento
        {
            Tipo = PixDireto.TipoAssinaturaClube,
            MetodoPagamento = PixDireto.Metodo,
            Status = "Pendente",
            RecebedorId = 7,
            ValorRepasse = 50m
        };

        Assert.NotNull(PixDireto.ProblemaParaConfirmar(comRepasse));
    }

    // ---------- De ponta a ponta ----------

    [Fact]
    public async Task Visitar_a_tela_nao_comeca_o_relogio_mas_escolher_um_plano_comeca()
    {
        // ⚠️ Até 24/08/2026 era a VISITA que começava o relógio. Mudou porque a agenda passou
        // a redirecionar pra cá gente que só clicou em "Financeiro" curiosa — visitar deixou
        // de significar "quero testar". O teste não corre desde a criação do clube, mas
        // também não corre por curiosidade: só quando o dono decide ESCOLHER.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        Assert.Null(clube.TesteDoClubeInicio);

        var c = Controller(ctx, dono.Id);
        await c.Index(clube.Id);
        Assert.Null(ctx.Clubes.Find(clube.Id)!.TesteDoClubeInicio);

        await c.Escolher(clube.Id, PlanoDoClube.Gestao);
        Assert.NotNull(ctx.Clubes.Find(clube.Id)!.TesteDoClubeInicio);
    }

    [Fact]
    public async Task Reescolher_o_plano_nao_reinicia_o_relogio_do_teste()
    {
        // Trocar de Gestão pra Fiscal (ou escolher de novo o mesmo) não pode dar 15 dias
        // novos de graça pra quem já os usou.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        var c = Controller(ctx, dono.Id);

        await c.Escolher(clube.Id, PlanoDoClube.Gestao);
        var inicio = ctx.Clubes.Find(clube.Id)!.TesteDoClubeInicio;
        Assert.NotNull(inicio);

        await c.Escolher(clube.Id, PlanoDoClube.Gestao);
        Assert.Equal(inicio, ctx.Clubes.Find(clube.Id)!.TesteDoClubeInicio);
    }

    [Fact]
    public async Task Escolher_plano_nao_cobra_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        var c = Controller(ctx, dono.Id);

        await c.Escolher(clube.Id, PlanoDoClube.Fiscal);

        Assert.Equal(PlanoDoClube.Fiscal, ctx.Clubes.Find(clube.Id)!.PlanoDoClube);
        Assert.Empty(ctx.Pagamentos);
    }

    // ---------------------------------------------------------------------------------
    // O Fiscal desligado NÃO se vende.
    //
    // Estes quatro testes nasceram de uma demonstração: com `Fiscal__Habilitado=false` a tela
    // de plano seguia oferecendo o Clube Fiscal com o botão funcionando. Escolher gravava o
    // plano, Pagar gerava cobrança de R$ 199 — e o módulo, sendo em construção, devolvia um
    // "essa parte não é sua" pra quem tinha acabado de pagar.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Fiscal_desligado_nao_esta_a_venda_mas_a_gestao_continua()
    {
        Assert.False(PlanoDoClube.EstaAVenda(PlanoDoClube.Fiscal, fiscalHabilitado: false));
        Assert.True(PlanoDoClube.EstaAVenda(PlanoDoClube.Fiscal, fiscalHabilitado: true));

        // O Gestão é o bar, que já roda: o interruptor do fiscal não manda nele.
        Assert.True(PlanoDoClube.EstaAVenda(PlanoDoClube.Gestao, fiscalHabilitado: false));
        Assert.True(PlanoDoClube.EstaAVenda(PlanoDoClube.Gestao, fiscalHabilitado: true));

        Assert.False(PlanoDoClube.EstaAVenda(null, fiscalHabilitado: true));
    }

    [Fact]
    public async Task Escolher_o_Fiscal_desligado_avisa_e_nao_grava_o_plano()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        var c = Controller(ctx, dono.Id, fiscalLigado: false);

        await c.Escolher(clube.Id, PlanoDoClube.Fiscal);

        Assert.Null(ctx.Clubes.Find(clube.Id)!.PlanoDoClube);
        Assert.NotNull(c.TempData["Erro"]);
    }

    [Fact]
    public async Task Com_o_Fiscal_desligado_a_Gestao_continua_sendo_escolhida()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        var c = Controller(ctx, dono.Id, fiscalLigado: false);

        await c.Escolher(clube.Id, PlanoDoClube.Gestao);

        Assert.Equal(PlanoDoClube.Gestao, ctx.Clubes.Find(clube.Id)!.PlanoDoClube);
    }

    [Fact]
    public async Task Clube_que_ja_escolheu_o_Fiscal_nao_e_cobrado_depois_que_ele_sai_do_ar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        // Escolheu com o módulo no ar; o interruptor baixou depois.
        clube.PlanoDoClube = PlanoDoClube.Fiscal;
        await ctx.SaveChangesAsync();

        var c = Controller(ctx, dono.Id, fiscalLigado: false);
        await c.Pagar(clube.Id, CicloDeAssinatura.Mensal);

        Assert.Empty(ctx.Pagamentos);
        Assert.NotNull(c.TempData["Erro"]);

        // E o que ele escolheu continua escolhido: não se cobra, mas também não se apaga.
        Assert.Equal(PlanoDoClube.Fiscal, ctx.Clubes.Find(clube.Id)!.PlanoDoClube);
    }

    [Fact]
    public async Task Quem_nao_manda_no_clube_nao_mexe_no_plano()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, _) = await SemearAsync(ctx);

        var estranho = new Jogador { Id = 9, Nome = "Estranho", Cpf = "9" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var c = Controller(ctx, estranho.Id);

        Assert.IsType<ForbidResult>(await c.Index(clube.Id));
        Assert.IsType<ForbidResult>(await c.Escolher(clube.Id, PlanoDoClube.Gestao));
        Assert.IsType<ForbidResult>(await c.Pagar(clube.Id, CicloDeAssinatura.Mensal));
    }

    [Fact]
    public async Task Pagar_sem_escolher_plano_avisa_em_vez_de_cobrar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);
        var c = Controller(ctx, dono.Id);

        await c.Pagar(clube.Id, CicloDeAssinatura.Mensal);

        Assert.Empty(ctx.Pagamentos);
        Assert.NotNull(c.TempData["Erro"]);
    }

    [Fact]
    public async Task O_dinheiro_confirmado_estende_a_vigencia_e_grava_o_plano_comprado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        var pagamentos = ServicoDePagamentos(ctx);

        var pagamento = new Pagamento
        {
            Tipo = PixDireto.TipoAssinaturaClube,
            JogadorId = dono.Id,
            RecebedorId = null,
            Valor = 99m,
            ValorRepasse = 0m,
            Comissao = 99m,
            MetodoPagamento = PixDireto.Metodo,
            Status = "Confirmado",
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(
                new DadosAssinaturaClube(clube.Id, PlanoDoClube.Gestao, CicloDeAssinatura.Mensal))
        };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        await pagamentos.EfetivarAsync(pagamento);

        var salvo = ctx.Clubes.Find(clube.Id)!;
        Assert.Equal(PlanoDoClube.Gestao, salvo.PlanoDoClube);
        Assert.NotNull(salvo.AssinaturaClubePagaAte);
        Assert.True(salvo.AssinaturaClubePagaAte > DateTime.Now.AddDays(27));
        Assert.Equal(clube.Id, ctx.Pagamentos.Find(pagamento.Id)!.ReferenciaId);
    }

    [Fact]
    public async Task O_plano_que_vale_e_o_que_foi_COMPRADO_nao_o_que_esta_na_tela()
    {
        // Entre gerar a cobrança e o dinheiro cair, o dono pode ter clicado em outro plano.
        // Quem pagou Gestão recebe Gestão.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        clube.PlanoDoClube = PlanoDoClube.Fiscal;   // trocou na tela depois de gerar
        await ctx.SaveChangesAsync();

        var pagamento = new Pagamento
        {
            Tipo = PixDireto.TipoAssinaturaClube,
            JogadorId = dono.Id,
            Valor = 99m,
            Comissao = 99m,
            Status = "Confirmado",
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(
                new DadosAssinaturaClube(clube.Id, PlanoDoClube.Gestao, CicloDeAssinatura.Mensal))
        };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        await ServicoDePagamentos(ctx).EfetivarAsync(pagamento);

        Assert.Equal(PlanoDoClube.Gestao, ctx.Clubes.Find(clube.Id)!.PlanoDoClube);
    }

    [Fact]
    public async Task Pagamento_confirmado_zera_os_avisos_pro_ciclo_seguinte_avisar()
    {
        // Sem o zeramento o clube seria avisado UMA VEZ NA VIDA.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        clube.UltimoLembreteDoPlano = AvisosDoPlanoDoClube.AcessoFechou;
        await ctx.SaveChangesAsync();

        var pagamento = new Pagamento
        {
            Tipo = PixDireto.TipoAssinaturaClube,
            JogadorId = dono.Id,
            Valor = 99m,
            Comissao = 99m,
            Status = "Confirmado",
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(
                new DadosAssinaturaClube(clube.Id, PlanoDoClube.Gestao, CicloDeAssinatura.Mensal))
        };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        await ServicoDePagamentos(ctx).EfetivarAsync(pagamento);

        Assert.Null(ctx.Clubes.Find(clube.Id)!.UltimoLembreteDoPlano);
    }

    // ---------- A porta do bar ----------

    [Fact]
    public async Task Clube_sem_plano_vai_pra_tela_de_assinatura_e_nao_pra_um_403()
    {
        // Quem chegou aqui acabou de mostrar interesse clicando no produto: um 403 é perder a
        // venda na porta.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        var bar = ControllerDoBar(ctx, dono.Id);
        var resposta = await bar.Index(clube.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("PlanoClube", redirect.ControllerName);
    }

    [Fact]
    public async Task Clube_em_dia_entra_no_bar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        clube.PlanoDoClube = PlanoDoClube.Gestao;
        clube.AssinaturaClubePagaAte = DateTime.Now.AddDays(20);
        await ctx.SaveChangesAsync();

        var bar = ControllerDoBar(ctx, dono.Id);
        Assert.IsType<ViewResult>(await bar.Index(clube.Id));
    }

    [Fact]
    public async Task Admin_do_padelizou_que_administra_o_clube_entra_sem_assinatura_nenhuma()
    {
        // É ele quem atende o clube que ligou dizendo que o balcão travou — um suporte que não
        // enxerga a tela do cliente não é suporte.
        //
        // ⚠️ Ser admin do Padelizou NÃO abre a porta de qualquer clube sozinho: isso é regra
        // anterior a este trabalho (ModuloDoBar sempre exigiu dono ou administrador do clube),
        // e o teste a registra em vez de mudá-la. O que a assinatura acrescentou foi só a
        // terceira pergunta — e é dela que o admin fica isento.
        using var ctx = TestInfra.NovoContexto();
        var (clube, _) = await SemearAsync(ctx);

        var admin = new Jogador { Id = 2, Nome = "Felipe", Cpf = "2", IsAdminRaiz = true };
        ctx.Jogadores.Add(admin);
        ctx.ClubeAdministradores.Add(new ClubeAdministrador { ClubeId = clube.Id, JogadorId = admin.Id });
        await ctx.SaveChangesAsync();

        // Clube sem plano nenhum: o dono seria mandado pra tela de assinatura.
        var bar = ControllerDoBar(ctx, admin.Id);
        Assert.IsType<ViewResult>(await bar.Index(clube.Id));
    }

    [Fact]
    public async Task Admin_do_padelizou_sozinho_nao_abre_clube_que_nao_administra()
    {
        // Registrando a regra que já existia, pra ninguém "consertá-la" achando que a
        // assinatura a quebrou.
        using var ctx = TestInfra.NovoContexto();
        var (clube, _) = await SemearAsync(ctx);

        var admin = new Jogador { Id = 2, Nome = "Felipe", Cpf = "2", IsAdminRaiz = true };
        ctx.Jogadores.Add(admin);
        await ctx.SaveChangesAsync();

        var bar = ControllerDoBar(ctx, admin.Id);
        Assert.IsType<ForbidResult>(await bar.Index(clube.Id));
    }

    [Fact]
    public async Task Quem_nao_manda_no_clube_leva_403_e_nao_descobre_se_ele_paga()
    {
        // A ordem é permissão ANTES de plano: perguntar o plano primeiro transformaria o gate
        // num detector de clientes pagantes pra qualquer pessoa logada.
        using var ctx = TestInfra.NovoContexto();
        var (clube, _) = await SemearAsync(ctx);

        var estranho = new Jogador { Id = 9, Nome = "Estranho", Cpf = "9" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var bar = ControllerDoBar(ctx, estranho.Id);
        Assert.IsType<ForbidResult>(await bar.Index(clube.Id));
    }

    [Fact]
    public async Task Modulo_fiscal_em_construcao_nao_oferece_plano_que_ainda_nao_emite()
    {
        // ⚠️ Em construção é "sem permissão", nunca "sem plano". Mandar o dono pra tela de
        // assinatura enquanto o módulo está em obra seria vender um Fiscal que ainda não emite
        // nada — vender o que não existe é pior do que não vender. Este teste existe porque a
        // primeira versão do código fazia exatamente isso.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        clube.PlanoDoClube = PlanoDoClube.Fiscal;
        clube.AssinaturaClubePagaAte = DateTime.Now.AddMonths(1);
        await ctx.SaveChangesAsync();

        var emObra = ControllerDoBar(ctx, dono.Id, fiscalLigado: false);
        var recusa = await emObra.Fiscal(clube.Id);

        // O que este teste guarda é o NEGATIVO: em obra, ninguém é mandado pra tela de plano.
        Assert.IsNotType<RedirectToActionResult>(recusa);
        Assert.Equal("FiscalEmConstrucao", Assert.IsType<ViewResult>(recusa).ViewName);

        var ligado = ControllerDoBar(ctx, dono.Id, fiscalLigado: true);
        Assert.Null(Assert.IsType<ViewResult>(await ligado.Fiscal(clube.Id)).ViewName);
    }

    [Fact]
    public async Task Clube_da_gestao_que_clica_em_fiscal_recebe_o_convite_e_nao_um_403()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        clube.PlanoDoClube = PlanoDoClube.Gestao;
        clube.AssinaturaClubePagaAte = DateTime.Now.AddMonths(1);
        await ctx.SaveChangesAsync();

        var bar = ControllerDoBar(ctx, dono.Id, fiscalLigado: true);
        var redirect = Assert.IsType<RedirectToActionResult>(await bar.Fiscal(clube.Id));
        Assert.Equal("PlanoClube", redirect.ControllerName);
    }

    [Fact]
    public async Task Uma_cobranca_aberta_por_CLUBE_mesmo_com_duas_pessoas_clicando()
    {
        // O erro que o clube não desfaz sozinho: o dono gera a cobrança, some, e o
        // administrador gera outra. Duas faturas do mesmo mês pro mesmo bar, pagas pelas duas
        // pessoas — dinheiro que a gente teria que devolver. Por isso a busca é pelo CLUBE.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        var socio = new Jogador { Id = 5, Nome = "Sócio", Cpf = "5" };
        ctx.Jogadores.Add(socio);
        ctx.ClubeAdministradores.Add(new ClubeAdministrador { ClubeId = clube.Id, JogadorId = socio.Id });

        clube.PlanoDoClube = PlanoDoClube.Gestao;

        // O dono já deixou uma cobrança aberta.
        ctx.Pagamentos.Add(new Pagamento
        {
            Tipo = PixDireto.TipoAssinaturaClube,
            JogadorId = dono.Id,
            Valor = 99m,
            Comissao = 99m,
            MetodoPagamento = PixDireto.Metodo,
            Status = "Pendente",
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(
                new DadosAssinaturaClube(clube.Id, PlanoDoClube.Gestao, CicloDeAssinatura.Mensal))
        });
        await ctx.SaveChangesAsync();

        // Agora o sócio tenta gerar a dele.
        var c = Controller(ctx, socio.Id);
        var resposta = await c.Pagar(clube.Id, CicloDeAssinatura.Mensal);

        // Continua UMA só, e ele foi levado pra ela.
        Assert.Single(ctx.Pagamentos);
        var redirect = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("Pix", redirect.ActionName);
    }

    [Fact]
    public async Task Trocar_de_ciclo_com_cobranca_aberta_avisa_em_vez_de_gerar_a_segunda()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        clube.PlanoDoClube = PlanoDoClube.Gestao;
        ctx.Pagamentos.Add(new Pagamento
        {
            Tipo = PixDireto.TipoAssinaturaClube,
            JogadorId = dono.Id,
            Valor = 99m,
            Comissao = 99m,
            MetodoPagamento = PixDireto.Metodo,
            Status = "Pendente",
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(
                new DadosAssinaturaClube(clube.Id, PlanoDoClube.Gestao, CicloDeAssinatura.Mensal))
        });
        await ctx.SaveChangesAsync();

        var c = Controller(ctx, dono.Id);
        await c.Pagar(clube.Id, CicloDeAssinatura.Anual);   // valor diferente

        Assert.Single(ctx.Pagamentos);
        Assert.NotNull(c.TempData["Erro"]);
    }

    [Fact]
    public async Task Cobranca_aberta_de_OUTRO_clube_nao_atrapalha()
    {
        // A marca no JSON tem que casar o clube certo: um `"ClubeId":1` não pode ser
        // confundido com `"ClubeId":10`.
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        var outro = new Clube { Id = 10, Nome = "Outra Arena", DonoId = dono.Id, PlanoDoClube = PlanoDoClube.Gestao };
        ctx.Clubes.Add(outro);
        ctx.Pagamentos.Add(new Pagamento
        {
            Tipo = PixDireto.TipoAssinaturaClube,
            JogadorId = dono.Id,
            Valor = 99m,
            Comissao = 99m,
            MetodoPagamento = PixDireto.Metodo,
            Status = "Pendente",
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(
                new DadosAssinaturaClube(outro.Id, PlanoDoClube.Gestao, CicloDeAssinatura.Mensal))
        });

        clube.PlanoDoClube = PlanoDoClube.Gestao;
        await ctx.SaveChangesAsync();

        // O clube 1 não tem cobrança aberta — a do clube 10 não conta pra ele.
        var c = Controller(ctx, dono.Id);
        await c.Index(clube.Id);

        Assert.Null(c.ViewBag.CobrancaAberta);
    }

    // ---------- Os avisos ----------

    [Fact]
    public void Aviso_sai_7_dias_antes_e_o_dia_do_vencimento_ainda_e_vencendo()
    {
        var clube = new Clube
        {
            Nome = "Chakra",
            PlanoDoClube = PlanoDoClube.Gestao,
            AssinaturaClubePagaAte = Hoje.AddDays(7)
        };

        Assert.Equal(AvisosDoPlanoDoClube.VaiVencer, AvisosDoPlanoDoClube.EstagioDevido(clube, Hoje, Cfg));

        // Oito dias antes ainda é cedo demais.
        Assert.Null(AvisosDoPlanoDoClube.EstagioDevido(clube, Hoje.AddDays(-1), Cfg));

        // No dia do vencimento ainda é "vence hoje", não "venceu": quem paga hoje não perdeu nada.
        var noDia = new Clube { Nome = "Chakra", PlanoDoClube = PlanoDoClube.Gestao, AssinaturaClubePagaAte = Hoje };
        Assert.Equal(AvisosDoPlanoDoClube.VaiVencer, AvisosDoPlanoDoClube.EstagioDevido(noDia, Hoje, Cfg));
    }

    [Fact]
    public void Os_tres_avisos_da_assinatura_saem_na_ordem_e_uma_vez_cada()
    {
        var clube = new Clube
        {
            Nome = "Chakra",
            PlanoDoClube = PlanoDoClube.Gestao,
            AssinaturaClubePagaAte = Hoje
        };

        Assert.Equal(AvisosDoPlanoDoClube.VaiVencer, AvisosDoPlanoDoClube.EstagioDevido(clube, Hoje, Cfg));
        clube.UltimoLembreteDoPlano = AvisosDoPlanoDoClube.VaiVencer;

        // Repetir o mesmo dia não manda de novo.
        Assert.Null(AvisosDoPlanoDoClube.EstagioDevido(clube, Hoje, Cfg));

        // Dentro da carência: venceu, mas o bar continua aberto.
        Assert.Equal(AvisosDoPlanoDoClube.VenceuNaCarencia,
            AvisosDoPlanoDoClube.EstagioDevido(clube, Hoje.AddDays(3), Cfg));
        clube.UltimoLembreteDoPlano = AvisosDoPlanoDoClube.VenceuNaCarencia;

        // Passada a carência: fechou.
        Assert.Equal(AvisosDoPlanoDoClube.AcessoFechou,
            AvisosDoPlanoDoClube.EstagioDevido(clube, Hoje.AddDays(9), Cfg));
    }

    [Fact]
    public void Vencimento_velho_nao_vira_aviso_no_dia_em_que_o_servico_sobe()
    {
        // Disparar hoje um aviso sobre algo de maio é a definição de spam.
        var clube = new Clube
        {
            Nome = "Chakra",
            PlanoDoClube = PlanoDoClube.Gestao,
            AssinaturaClubePagaAte = Hoje.AddDays(-90)
        };

        Assert.Null(AvisosDoPlanoDoClube.EstagioDevido(clube, Hoje, Cfg));
    }

    [Fact]
    public void Estagio_so_sobe_quando_varios_venceram_de_uma_vez()
    {
        // Serviço fora do ar por uma semana: manda o de AGORA, não os três seguidos.
        var clube = new Clube
        {
            Nome = "Chakra",
            PlanoDoClube = PlanoDoClube.Gestao,
            AssinaturaClubePagaAte = Hoje,
            UltimoLembreteDoPlano = null
        };

        Assert.Equal(AvisosDoPlanoDoClube.AcessoFechou,
            AvisosDoPlanoDoClube.EstagioDevido(clube, Hoje.AddDays(9), Cfg));
    }

    [Fact]
    public void Teste_acabando_fala_diferente_pra_quem_ja_escolheu_o_plano()
    {
        // Dizer "escolha um plano" pra quem já escolheu o faria procurar um botão que não é o
        // dele.
        var escolheu = new Clube
        {
            Nome = "Chakra", PlanoDoClube = PlanoDoClube.Gestao,
            TesteDoClubeInicio = Hoje.AddDays(-12)
        };
        var naoEscolheu = new Clube { Nome = "Chakra", TesteDoClubeInicio = Hoje.AddDays(-12) };

        var f1 = AvisosDoPlanoDoClube.Frase(AvisosDoPlanoDoClube.TesteAcabando, escolheu, Hoje, Cfg);
        var f2 = AvisosDoPlanoDoClube.Frase(AvisosDoPlanoDoClube.TesteAcabando, naoEscolheu, Hoje, Cfg);

        Assert.Contains("Gere a cobrança", f1);
        Assert.Contains("Escolha um plano", f2);
        Assert.NotEqual(f1, f2);
    }

    [Fact]
    public async Task Varredura_avisa_o_dono_e_nao_repete_no_tick_seguinte()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, dono) = await SemearAsync(ctx);

        clube.PlanoDoClube = PlanoDoClube.Gestao;
        clube.AssinaturaClubePagaAte = Hoje.AddDays(3);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();

        // 10h é hora civilizada; às 3h da manhã ninguém é acordado.
        var enviados = await AvisosDoPlanoDoClubeBackgroundService.VarrerAsync(ctx, push, Cfg, Hoje);
        Assert.Equal(1, enviados);

        await push.Received(1).EnviarParaJogadorAsync(dono.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());

        // O segundo tick do mesmo dia não repete.
        Assert.Equal(0, await AvisosDoPlanoDoClubeBackgroundService.VarrerAsync(ctx, push, Cfg, Hoje));
    }

    [Fact]
    public async Task Varredura_nao_acorda_ninguem_de_madrugada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, _) = await SemearAsync(ctx);

        clube.PlanoDoClube = PlanoDoClube.Gestao;
        clube.AssinaturaClubePagaAte = Hoje.AddDays(3);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var madrugada = new DateTime(2026, 8, 19, 3, 0, 0);

        Assert.Equal(0, await AvisosDoPlanoDoClubeBackgroundService.VarrerAsync(ctx, push, Cfg, madrugada));
    }

    [Fact]
    public async Task Clube_que_nunca_entrou_no_plano_nao_e_varrido()
    {
        using var ctx = TestInfra.NovoContexto();
        await SemearAsync(ctx);   // sem teste começado, sem assinatura

        var push = Substitute.For<IPushNotificationService>();
        Assert.Equal(0, await AvisosDoPlanoDoClubeBackgroundService.VarrerAsync(ctx, push, Cfg, Hoje));
    }

    // ---------- Apoio ----------

    private static async Task<(Clube Clube, Jogador Dono)> SemearAsync(DbPadelContext ctx)
    {
        var dono = new Jogador { Id = 1, Nome = "Dono do Clube", Cpf = "1" };
        var clube = new Clube { Id = 1, Nome = "Chakra Padel", DonoId = 1 };

        ctx.Jogadores.Add(dono);
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();

        return (clube, dono);
    }

    private static IPagamentoInscricaoService ServicoDePagamentos(DbPadelContext ctx) =>
        new PagamentoInscricaoService(
            ctx,
            Substitute.For<IAsaasService>(),
            Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()),
            Options.Create(new PlanoProfessorSettings()),
            Options.Create(Cfg));

    private static PlanoClubeController Controller(DbPadelContext ctx, int usuarioId,
        bool fiscalLigado = true)
    {
        var c = new PlanoClubeController(ctx, ServicoDePagamentos(ctx), Options.Create(Cfg),
            Options.Create(new FiscalSettings { Habilitado = fiscalLigado }));
        Vestir(c, usuarioId);
        return c;
    }

    private static BarController ControllerDoBar(DbPadelContext ctx, int usuarioId, bool fiscalLigado = true)
    {
        var modulo = new ModuloDoBar(ctx, Options.Create(new BarSettings { Habilitado = true }),
            Options.Create(Cfg));

        var c = new BarController(ctx, modulo,
            new ModuloFiscal(ctx, modulo, Options.Create(new FiscalSettings { Habilitado = fiscalLigado }),
                Options.Create(Cfg)),
            TestInfra.CepQueNaoResponde(),
            new NotasDoClube(ctx, NullLogger<NotasDoClube>.Instance),
            new MedidorDeFranquia(ctx, Options.Create(new PlanoClubeSettings())),
            new EmissorFiscalDesligado(),
            NullLogger<BarController>.Instance);

        Vestir(c, usuarioId);
        return c;
    }

    private static void Vestir(Controller c, int usuarioId)
    {
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "Teste")),
            },
        };
        c.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            c.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
    }
}
