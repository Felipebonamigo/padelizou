using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// 20/08/2026 — A CORTESIA DO PLANO DO PROFESSOR, pedido do Felipe (permuta com o Gabriel).
//
// O que está sendo protegido aqui NÃO é a tela, são três coisas que só aparecem meses depois:
//
//   • A RECEITA. Cortesia não cria `Pagamento`, e todo número de dinheiro do projeto sai de
//     `Pagamento`. Se um dia isso mudar, a tela do admin passa a mostrar dinheiro que não entrou
//     no banco — e o MEI e a comissão de parceiro seguem esse número.
//   • A TAXA DA AULA. A cortesia derruba a taxa de 10% pra 3%. Se `CondicoesDeAssinante` parar
//     de reconhecê-la, o parceiro paga taxa cheia sem ninguém perceber.
//   • O SILÊNCIO. Um professor de permuta receber "pague sua mensalidade" é constrangedor com
//     alguém que combinou tudo por fora.
public class CortesiaDoProfessorTests
{
    private static readonly PlanoProfessorSettings Cfg = new();
    private static readonly DateTime Agora = new(2026, 08, 20, 12, 0, 0);

    private static Jogador Professor(
        string? plano = null, DateTime? testeInicio = null,
        DateTime? pagaAte = null, DateTime? cortesiaAte = null) =>
        new()
        {
            Nome = "Prof Permuta",
            Cpf = "88800000000",
            IsProfessor = true,
            PlanoProfessor = plano,
            TesteProfessorInicio = testeInicio,
            AssinaturaProfessorPagaAte = pagaAte,
            CortesiaProfessorAte = cortesiaAte,
        };

    // ── A régua pura ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cortesia_da_a_taxa_de_assinante_sem_ninguem_ter_pagado()
    {
        var prof = Professor(cortesiaAte: Agora.AddYears(1));

        Assert.Equal(PlanoDoProfessor.Situacao.Cortesia, PlanoDoProfessor.SituacaoDe(prof, Agora, Cfg));
        Assert.True(PlanoDoProfessor.CondicoesDeAssinante(prof, Agora, Cfg));

        // ⚠️ É esta linha que faz a permuta valer alguma coisa pro professor.
        var pix = PlanoDoProfessor.CobrancaDaAula(prof, CobrancaDoTorneio.EscolhaPix, Agora, Cfg);
        Assert.Equal(Cfg.PercentualAssinantePix, pix.Percentual);

        // E nada de dinheiro foi inventado no caminho.
        Assert.Null(prof.AssinaturaProfessorPagaAte);
    }

    // ⚠️ O PISO. O professor pode ter escolhido "Avulso" na tela dele antes (ou depois) de
    // ganhar a cortesia — e esse clique NÃO pode apagar o presente que a gente deu.
    [Fact]
    public void Cortesia_ganha_do_Avulso_que_o_proprio_professor_escolheu()
    {
        var prof = Professor(plano: PlanoDoProfessor.Avulso, cortesiaAte: Agora.AddMonths(6));

        Assert.Equal(PlanoDoProfessor.Situacao.Cortesia, PlanoDoProfessor.SituacaoDe(prof, Agora, Cfg));
        Assert.True(PlanoDoProfessor.CondicoesDeAssinante(prof, Agora, Cfg));
    }

    [Fact]
    public void Cortesia_ganha_de_quem_estava_em_atraso_e_de_teste_vencido()
    {
        var atrasado = Professor(plano: PlanoDoProfessor.Assinante, pagaAte: Agora.AddMonths(-3),
                                 cortesiaAte: Agora.AddMonths(6));
        Assert.Equal(PlanoDoProfessor.Situacao.Cortesia, PlanoDoProfessor.SituacaoDe(atrasado, Agora, Cfg));

        var largado = Professor(testeInicio: Agora.AddDays(-60), cortesiaAte: Agora.AddMonths(6));
        Assert.Equal(PlanoDoProfessor.Situacao.Cortesia, PlanoDoProfessor.SituacaoDe(largado, Agora, Cfg));
    }

    // ⚠️ O TETO — e é o lado que quase ninguém testa. "Assinantes em dia" no /Admin/Professores
    // é lido como proxy de RECEITA. Se a cortesia ganhasse do pagante, um assinante que paga
    // sairia do contador verde e iria pro azul, enquanto o dinheiro dele continuaria em
    // "Já pagou" — e a conta pararia de fechar sem nenhum erro na tela.
    [Fact]
    public void Assinante_que_PAGA_continua_assinante_em_dia_mesmo_com_cortesia_por_cima()
    {
        var prof = Professor(plano: PlanoDoProfessor.Assinante,
                             pagaAte: Agora.AddMonths(3),
                             cortesiaAte: Agora.AddYears(1));

        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmDia, PlanoDoProfessor.SituacaoDe(prof, Agora, Cfg));
    }

    // "Vale até 20/08" vale o dia 20 INTEIRO — comparar com a hora faria a cortesia morrer à
    // meia-noite e um minuto do dia que a tela promete.
    [Fact]
    public void A_cortesia_vale_o_dia_inteiro_do_vencimento_e_acaba_no_dia_seguinte()
    {
        var prof = Professor(cortesiaAte: Agora.Date);

        Assert.Equal(PlanoDoProfessor.Situacao.Cortesia,
            PlanoDoProfessor.SituacaoDe(prof, Agora.Date.AddHours(23).AddMinutes(59), Cfg));

        // No dia seguinte ela acabou — e ele volta ao que já era, sem nenhum job rodar.
        Assert.Equal(PlanoDoProfessor.Situacao.TesteVencidoSemEscolha,
            PlanoDoProfessor.SituacaoDe(prof, Agora.Date.AddDays(1), Cfg));
    }

    [Theory]
    [InlineData(null, "permuta", "Escolha até quando")]
    [InlineData(-1, "permuta", "passado")]
    [InlineData(800, "permuta", "no máximo")]   // ~26 meses: além do teto de 24
    [InlineData(30, "", "motivo")]
    [InlineData(30, "   ", "motivo")]
    public void Cortesia_mal_preenchida_e_recusada(int? dias, string motivo, string trechoDoErro)
    {
        var ate = dias == null ? (DateTime?)null : Agora.AddDays(dias.Value);

        var problema = PlanoDoProfessor.ProblemaParaDarCortesia(ate, motivo, Agora);

        Assert.NotNull(problema);
        Assert.Contains(trechoDoErro, problema);
    }

    [Fact]
    public void Cortesia_bem_preenchida_passa()
    {
        Assert.Null(PlanoDoProfessor.ProblemaParaDarCortesia(
            Agora.AddYears(1), "permuta de serviços", Agora));
    }

    // ── Os avisos ─────────────────────────────────────────────────────────────────────────

    // ⚠️ O CASO QUE MOTIVOU O MUNDO 3. O Gabriel tem `TesteProfessorInicio` (ele abriu a tela
    // uma vez) e nenhum plano. Sem a cortesia ser EXCLUSIVA e vir PRIMEIRO, ele cairia no mundo
    // do teste e receberia "seu período de teste acaba amanhã — escolha um plano", por push E
    // por e-mail, cobrando decisão de quem já combinou tudo com o Felipe.
    [Fact]
    public void Durante_a_cortesia_o_professor_NAO_recebe_aviso_nenhum()
    {
        var prof = Professor(testeInicio: Agora.AddDays(-14), cortesiaAte: Agora.AddYears(1));

        Assert.Null(AvisosDoPlanoDoProfessor.EstagioDevido(prof, Agora, Cfg));

        // Nem no dia em que o teste dele venceria, nem meses depois.
        Assert.Null(AvisosDoPlanoDoProfessor.EstagioDevido(prof, Agora.AddDays(1), Cfg));
        Assert.Null(AvisosDoPlanoDoProfessor.EstagioDevido(prof, Agora.AddMonths(6), Cfg));
    }

    // Calar TAMBÉM no fim reintroduziria o defeito mudo que este serviço veio matar: a taxa dele
    // salta de 3% pra 10% sozinha e ele descobre pelo extrato.
    [Fact]
    public void Quando_a_cortesia_acaba_sai_UM_aviso_sem_pedir_renovacao_e_sem_preco()
    {
        var prof = Professor(cortesiaAte: Agora.Date);
        var depois = Agora.Date.AddDays(2);

        var estagio = AvisosDoPlanoDoProfessor.EstagioDevido(prof, depois, Cfg);
        Assert.Equal(AvisosDoPlanoDoProfessor.CortesiaAcabou, estagio);

        var frase = AvisosDoPlanoDoProfessor.Frase(estagio!.Value, prof, depois, Cfg);
        Assert.Contains(Cfg.PercentualAvulso.ToString("0.#"), frase);
        Assert.DoesNotContain("enove", frase);                                  // "renove"/"Renove"
        Assert.DoesNotContain(Cfg.MensalidadeAssinante.ToString("C"), frase);   // sem preço
        Assert.Contains("cortesia", AvisosDoPlanoDoProfessor.Titulo(estagio.Value).ToLowerInvariant());
    }

    // Quem tem assinatura paga viva por baixo não perdeu nada quando a cortesia acabou —
    // avisá-lo seria inventar um problema que não existe.
    [Fact]
    public void Cortesia_que_acaba_por_cima_de_assinatura_PAGA_nao_avisa_ninguem()
    {
        var prof = Professor(plano: PlanoDoProfessor.Assinante,
                             pagaAte: Agora.AddMonths(6),
                             cortesiaAte: Agora.Date);

        Assert.Null(AvisosDoPlanoDoProfessor.EstagioDevido(prof, Agora.Date.AddDays(2), Cfg));
    }

    // ⚠️ O FURO QUE A COMPARAÇÃO MONOTÔNICA CRIARIA. Com dois mundos dava pra contar com a ordem
    // da vida: o teste (1x) sempre vem antes da assinatura (2x). A cortesia (3x) entra NO MEIO,
    // e a vida continua depois dela. Se "só sobe" valesse na escada inteira, o estágio 30
    // gravado calaria 10, 11, 20, 21 e 22 PARA SEMPRE — e o único outro lugar que zera essa
    // coluna é um pagamento confirmado.
    [Fact]
    public void Depois_da_cortesia_o_professor_volta_a_receber_os_avisos_normais()
    {
        var prof = Professor(testeInicio: Agora.AddDays(-14));
        prof.UltimoLembreteDeAssinatura = AvisosDoPlanoDoProfessor.CortesiaAcabou;

        var estagio = AvisosDoPlanoDoProfessor.EstagioDevido(prof, Agora, Cfg);

        Assert.Equal(AvisosDoPlanoDoProfessor.TesteAcabando, estagio);
    }

    // ── A tela do admin ───────────────────────────────────────────────────────────────────

    // ⚠️ O `_ =>` de RotuloDaSituacao devolve "Teste vencido, sem escolha". Sem a linha da
    // Cortesia, o professor de permuta apareceria com esse rótulo, no balde amarelo — e o
    // compilador não diria nada.
    [Fact]
    public void Na_tela_do_admin_a_cortesia_tem_rotulo_e_contador_PROPRIOS()
    {
        var prof = Professor(cortesiaAte: Agora.AddYears(1));
        prof.CortesiaConcedidaEm = Agora;
        prof.MotivoDaCortesia = "permuta de serviços";

        var linhas = ProfessoresNoAdmin.Montar(
            new[] { prof }, Array.Empty<Pagamento>(), Array.Empty<padelizou.Models.Aula>(), Agora, Cfg);

        Assert.Equal(ProfessoresNoAdmin.RotuloDeCortesia, linhas[0].Situacao);
        Assert.Equal("permuta de serviços", linhas[0].MotivoDaCortesia);

        var resumo = ProfessoresNoAdmin.Resumir(linhas, new List<Padelizou.ViewModels.MesDeAssinatura>(), Agora);
        Assert.Equal(1, resumo.EmCortesia);

        // ⚠️ E NENHUM NÚMERO DE DINHEIRO SE MEXEU. É o desenho inteiro em três asserções.
        Assert.Equal(0, resumo.AssinantesEmDia);
        Assert.Equal(0, resumo.JaPagaramAlgumaVez);
        Assert.Equal(0m, resumo.ReceitaTotal);
    }

    // Sem a quarta ponta de NuncaViuOPlano, quem ganha cortesia sem nunca ter aberto a tela do
    // plano cai no "nunca abriu" — e o rótulo "Cortesia" é ENGOLIDO antes de chegar na tela.
    [Fact]
    public void Quem_ganha_cortesia_sem_nunca_ter_aberto_o_plano_NAO_conta_como_nunca_abriu()
    {
        var prof = Professor(cortesiaAte: Agora.AddYears(1));
        prof.CortesiaConcedidaEm = Agora;

        Assert.False(ProfessoresNoAdmin.NuncaViuOPlano(prof));

        // E continua não contando depois de a cortesia ser tirada: o carimbo fica.
        prof.CortesiaProfessorAte = null;
        Assert.False(ProfessoresNoAdmin.NuncaViuOPlano(prof));
    }

    // ── O POST ────────────────────────────────────────────────────────────────────────────

    private static AdminController Painel(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            Microsoft.Extensions.Options.Options.Create(new RegistroResultadosSettings()));

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
        };
        http.Request.Method = HttpMethods.Post;

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    private static Jogador Gravar(DbPadelContext ctx, Jogador j)
    {
        ctx.Jogadores.Add(j);
        ctx.SaveChanges();
        return j;
    }

    private static Jogador Raiz(DbPadelContext ctx) => Gravar(ctx, new Jogador
    {
        Nome = "Felipe", Cpf = "11100000000", Login = "raiz", IsAdminGeral = true, IsAdminRaiz = true,
    });

    // ⚠️ SÓ O RAIZ. Cortesia é dinheiro por duas portas — a mensalidade que se decide não cobrar
    // e a taxa de cada aula caindo 7 pontos. Administrador nomeado não decide isso.
    [Fact]
    public async Task So_o_raiz_da_cortesia()
    {
        using var ctx = TestInfra.NovoContexto();
        var nomeado = Gravar(ctx, new Jogador
        {
            Nome = "Adm nomeado", Cpf = "22200000000", Login = "adm", IsAdminGeral = true, IsAdminRaiz = false,
        });
        var prof = Gravar(ctx, Professor());

        var resultado = await Painel(ctx, nomeado.Id)
            .DefinirCortesiaDoProfessor(prof.Id, Agora.AddYears(1), "permuta");

        Assert.IsType<ForbidResult>(resultado);
        Assert.Null((await ctx.Jogadores.FindAsync(prof.Id))!.CortesiaProfessorAte);
    }

    [Fact]
    public async Task Dar_cortesia_grava_o_rastro_e_NAO_cria_pagamento_nenhum()
    {
        using var ctx = TestInfra.NovoContexto();
        var raiz = Raiz(ctx);
        var prof = Gravar(ctx, Professor());

        await Painel(ctx, raiz.Id)
            .DefinirCortesiaDoProfessor(prof.Id, new DateTime(2027, 8, 20), "  permuta de serviços  ");

        var depois = (await ctx.Jogadores.FindAsync(prof.Id))!;
        Assert.Equal(new DateTime(2027, 8, 20), depois.CortesiaProfessorAte);
        Assert.Equal("permuta de serviços", depois.MotivoDaCortesia);
        Assert.Equal(raiz.Id, depois.CortesiaConcedidaPorJogadorId);
        Assert.NotNull(depois.CortesiaConcedidaEm);

        // ⚠️ AS DUAS ASSERÇÕES QUE SEGURAM A HONESTIDADE DA RECEITA.
        Assert.False(await ctx.Pagamentos.AnyAsync());
        Assert.Null(depois.AssinaturaProfessorPagaAte);
    }

    // ⚠️ Sem zerar o lembrete, quem já levou o estágio 22 ("sua taxa voltou ao cheio") nunca
    // mais receberia aviso nenhum — nem o do fim da própria cortesia. O único outro lugar que
    // zera essa coluna é um pagamento confirmado.
    [Fact]
    public async Task Dar_cortesia_zera_o_lembrete_de_vencimento()
    {
        using var ctx = TestInfra.NovoContexto();
        var raiz = Raiz(ctx);
        var prof = Professor();
        prof.UltimoLembreteDeAssinatura = AvisosDoPlanoDoProfessor.TaxaVoltouAoCheio;
        Gravar(ctx, prof);

        await Painel(ctx, raiz.Id).DefinirCortesiaDoProfessor(prof.Id, Agora.AddYears(1), "permuta");

        Assert.Null((await ctx.Jogadores.FindAsync(prof.Id))!.UltimoLembreteDeAssinatura);
    }

    [Fact]
    public async Task Tirar_a_cortesia_apaga_so_a_DATA_e_guarda_quem_deu()
    {
        using var ctx = TestInfra.NovoContexto();
        var raiz = Raiz(ctx);
        var prof = Gravar(ctx, Professor());

        var painel = Painel(ctx, raiz.Id);
        await painel.DefinirCortesiaDoProfessor(prof.Id, Agora.AddYears(1), "permuta");
        await Painel(ctx, raiz.Id).TirarCortesiaDoProfessor(prof.Id);

        var depois = (await ctx.Jogadores.FindAsync(prof.Id))!;
        Assert.Null(depois.CortesiaProfessorAte);
        // O rastro fica: é tudo que existe (não há tabela de auditoria no projeto).
        Assert.Equal("permuta", depois.MotivoDaCortesia);
        Assert.Equal(raiz.Id, depois.CortesiaConcedidaPorJogadorId);
        Assert.NotNull(depois.CortesiaConcedidaEm);
    }

    // "Assinou ou não" — o pedido literal do Felipe. E ele NUNCA encosta em "Pago até".
    [Fact]
    public async Task Definir_o_plano_NAO_inventa_mensalidade_paga()
    {
        using var ctx = TestInfra.NovoContexto();
        var raiz = Raiz(ctx);
        var prof = Gravar(ctx, Professor());

        await Painel(ctx, raiz.Id).DefinirPlanoDoProfessor(prof.Id, PlanoDoProfessor.Assinante);

        var depois = (await ctx.Jogadores.FindAsync(prof.Id))!;
        Assert.Equal(PlanoDoProfessor.Assinante, depois.PlanoProfessor);
        Assert.Null(depois.AssinaturaProfessorPagaAte);
        Assert.False(await ctx.Pagamentos.AnyAsync());

        // E volta a "ainda não escolheu" quando o valor não é nenhum dos dois.
        await Painel(ctx, raiz.Id).DefinirPlanoDoProfessor(prof.Id, "");
        Assert.Null((await ctx.Jogadores.FindAsync(prof.Id))!.PlanoProfessor);
    }
}
