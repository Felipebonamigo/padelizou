using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A ESCADA DE AVISOS DO BLOQUEIO (item 3, 22/09/2026). 🗣️ Felipe: *"coloque avisos regulares de
// que vai vencer em 1 semana, 1 dia, 1 hora"* e *"o professor vai avisando todo dia por email e
// push q venceu"*.
//
// ⚠️ O PEDIDO LITERAL ESTOURA A CONTA DE E-MAIL, E ISSO É MEDIDO. `EnviarParaJogadorAsync` já
// manda push E E-MAIL no mesmo funil — os avisos do plano sempre mandaram e-mail, ninguém
// precisava pedir. Diário × 20 dias × N professores seria, com 10 bloqueados, **200 e-mails**
// contra um volume mensal do sistema inteiro de ~300 a 500 (EMAIL.md). A cota do Gmail já
// estourou duas vezes; na segunda, 130 e-mails morreram calados, duas recuperações de senha
// entre eles.
//
// A saída não é inventar canal: é `AlcanceDoAviso.AppSemEmail`, que JÁ EXISTE e nasceu desse
// mesmo estouro. Diário vai por push + caixa de entrada; o e-mail sai em TRÊS marcos — o dia do
// bloqueio, a metade do prazo e a véspera do cancelamento.
public class AvisosDoBloqueioTests
{
    private static PlanoProfessorSettings Cfg => new() { BloqueioAPartirDe = new DateTime(2026, 01, 01) };

    // Venceu em 01/09 → bloqueia 11/09 10h (10 dias) → aulas caem em 01/10 (30 dias).
    private static readonly DateTime Venceu = new(2026, 09, 01);
    private static readonly DateTime Bloqueia = new(2026, 09, 11, 10, 0, 0);

    private static Jogador Largado() => new()
    {
        Nome = "Prof", Cpf = "88800000000", IsProfessor = true,
        PlanoProfessor = PlanoDoProfessor.Assinante,
        AssinaturaProfessorPagaAte = Venceu,
        // Já levou o aviso de que a taxa voltou ao cheio: é daí que a escada do bloqueio parte.
        UltimoLembreteDeAssinatura = AvisosDoPlanoDoProfessor.TaxaVoltouAoCheio,
    };

    private static int? Em(DateTime agora, Jogador? quem = null) =>
        AvisosDoPlanoDoProfessor.EstagioDevido(quem ?? Largado(), agora, Cfg);

    // ── As datas que a régua promete ──────────────────────────────────────────────────────

    [Fact]
    public void O_cancelamento_e_um_mes_do_vencimento_e_nasce_da_mesma_conta_do_bloqueio()
    {
        // ⚠️ Uma fonte só pros dois: o item 4 vai cancelar exatamente nesta data, e uma segunda
        // conta em outro arquivo é como as duas passam a discordar depois de um refactor.
        Assert.Equal(Bloqueia, BloqueioDoProfessor.BloqueiaEm(Largado(), Cfg));
        Assert.Equal(new DateTime(2026, 10, 01, 10, 0, 0), BloqueioDoProfessor.CancelaAulasEm(Largado(), Cfg));
    }

    // ── Antes do bloqueio: 7 dias, 1 dia, 1 hora ──────────────────────────────────────────

    [Fact]
    public void Uma_semana_antes_do_bloqueio()
    {
        Assert.Equal(AvisosDoPlanoDoProfessor.BloqueioEmUmaSemana, Em(Bloqueia.AddDays(-7)));
    }

    [Fact]
    public void Um_dia_antes_do_bloqueio()
    {
        Assert.Equal(AvisosDoPlanoDoProfessor.BloqueioAmanha, Em(Bloqueia.AddDays(-1)));
    }

    [Fact]
    public void Uma_hora_antes_do_bloqueio()
    {
        // ⚠️ AQUI ESTÁ A RAZÃO DE `HoraDoBloqueio` SER 10h. Este aviso só existe se a varredura
        // das 9h — a `PrimeiraHora` civilizada — ainda pegar o professor como não bloqueado.
        var novePontualmente = Bloqueia.Date.AddHours(LembreteDeInscricaoNaoPaga.PrimeiraHora);

        Assert.Equal(AvisosDoPlanoDoProfessor.BloqueioEmUmaHora, Em(novePontualmente));
    }

    [Fact]
    public void Dez_dias_antes_ainda_e_cedo_demais_pra_falar_de_bloqueio()
    {
        Assert.NotEqual(AvisosDoPlanoDoProfessor.BloqueioEmUmaSemana, Em(Bloqueia.AddDays(-10)));
    }

    // ── Depois do bloqueio: todo dia ──────────────────────────────────────────────────────

    [Fact]
    public void Depois_de_bloqueado_sai_um_aviso_por_dia()
    {
        var dia0 = Em(Bloqueia.AddHours(1));
        var dia1 = Em(Bloqueia.AddDays(1).AddHours(1));
        var dia7 = Em(Bloqueia.AddDays(7).AddHours(1));

        Assert.Equal(AvisosDoPlanoDoProfessor.PrimeiroDiaBloqueado, dia0);
        Assert.Equal(AvisosDoPlanoDoProfessor.PrimeiroDiaBloqueado + 1, dia1);
        Assert.Equal(AvisosDoPlanoDoProfessor.PrimeiroDiaBloqueado + 7, dia7);
    }

    [Fact]
    public void O_mesmo_dia_nao_avisa_duas_vezes()
    {
        var prof = Largado();
        prof.UltimoLembreteDeAssinatura = AvisosDoPlanoDoProfessor.PrimeiroDiaBloqueado + 3;

        // ⚠️ A varredura passa de HORA EM HORA. Sem a régua monotônica, o professor bloqueado
        // levaria 12 avisos por dia — o jeito mais rápido de ele desligar a notificação e
        // perder junto o aviso que importa.
        Assert.Null(Em(Bloqueia.AddDays(3).AddHours(2), prof));
        Assert.Equal(AvisosDoPlanoDoProfessor.PrimeiroDiaBloqueado + 4, Em(Bloqueia.AddDays(4), prof));
    }

    [Fact]
    public void Passado_o_cancelamento_a_escada_se_cala()
    {
        // Dali em diante quem fala é o item 4, com o aviso de que as aulas foram canceladas.
        // Continuar repetindo "seu plano venceu" depois disso é ruído sobre fato consumado.
        Assert.Null(Em(new DateTime(2026, 10, 02, 12, 0, 0)));
    }

    // ── O e-mail: três marcos, e não vinte ────────────────────────────────────────────────

    [Fact]
    public void O_diario_vai_por_push_e_caixa_SEM_email()
    {
        var alcance = AvisosDoPlanoDoProfessor.AlcanceDe(
            AvisosDoPlanoDoProfessor.PrimeiroDiaBloqueado + 1, Largado(), Cfg);

        Assert.Equal(AlcanceDoAviso.AppSemEmail, alcance);
    }

    [Fact]
    public void O_email_sai_no_bloqueio_na_metade_e_na_vespera_do_cancelamento()
    {
        // Bloqueio 11/09, cancelamento 01/10: 20 dias de prazo → metade no dia 10, véspera no 19.
        foreach (var dia in new[] { 0, 10, 19 })
        {
            Assert.Equal(AlcanceDoAviso.SoApp, AvisosDoPlanoDoProfessor.AlcanceDe(
                AvisosDoPlanoDoProfessor.PrimeiroDiaBloqueado + dia, Largado(), Cfg));
        }
    }

    [Fact]
    public void Os_avisos_de_antes_do_bloqueio_levam_email()
    {
        // Três no total, todos acionáveis: é exatamente o que o e-mail existe pra carregar.
        foreach (var estagio in new[] { AvisosDoPlanoDoProfessor.BloqueioEmUmaSemana,
                                        AvisosDoPlanoDoProfessor.BloqueioAmanha,
                                        AvisosDoPlanoDoProfessor.BloqueioEmUmaHora })
        {
            Assert.Equal(AlcanceDoAviso.SoApp, AvisosDoPlanoDoProfessor.AlcanceDe(estagio, Largado(), Cfg));
        }
    }

    // ── As bordas ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Com_o_bloqueio_dormente_a_escada_antiga_segue_intacta()
    {
        var prof = Largado();
        prof.UltimoLembreteDeAssinatura = null;
        var dormente = new PlanoProfessorSettings();   // BloqueioAPartirDe nulo

        // ⚠️ O dia do deploy: nenhum aviso novo, nenhum aviso a menos. O estágio devido em
        // 02/09 continua sendo o da CARÊNCIA, como era antes de tudo isto existir.
        Assert.Equal(AvisosDoPlanoDoProfessor.VenceuNaCarencia,
            AvisosDoPlanoDoProfessor.EstagioDevido(prof, new DateTime(2026, 09, 02, 12, 0, 0), dormente));
    }

    [Fact]
    public void Quem_volta_a_pagar_sai_da_escada_do_bloqueio()
    {
        var voltou = Largado();
        voltou.AssinaturaProfessorPagaAte = new DateTime(2026, 12, 01);

        // Não há bloqueio à vista: nada a dizer sobre ele.
        Assert.Null(Em(Bloqueia.AddDays(2), voltou));
    }

    [Fact]
    public void Entrou_na_escada_do_bloqueio_nao_volta_pra_falar_de_taxa()
    {
        var prof = Largado();
        prof.UltimoLembreteDeAssinatura = AvisosDoPlanoDoProfessor.BloqueioAmanha;

        // ⚠️ "Sua agenda fecha amanhã" seguido de "sua taxa voltou ao cheio" é ordem
        // DECRESCENTE de urgência — a pessoa lê o segundo e acha que o primeiro foi resolvido.
        var estagio = Em(Bloqueia.AddHours(2), prof);

        Assert.True(estagio == null || estagio >= AvisosDoPlanoDoProfessor.BloqueioEmUmaSemana,
            $"Voltou pra faixa de taxa: estágio {estagio}");
    }

    // ── O fio entre a régua e o entregador ────────────────────────────────────────────────

    [Fact]
    public async Task A_varredura_entrega_o_diario_SEM_email_e_o_marco_COM()
    {
        // ⚠️ O TESTE DO FIO, e não da régua: `AlcanceDe` pode estar perfeita e o serviço de
        // fundo continuar chamando `EnviarParaJogadorAsync` sem passar o alcance — o padrão é
        // `SoApp`, então o diário sairia por e-mail e nada reclamaria. Foi assim que a cota
        // estourou as duas vezes: ninguém decidiu mandar, o padrão mandou.
        foreach (var (dia, esperado) in new[]
                 {
                     (0, AlcanceDoAviso.SoApp),        // marco: o dia do bloqueio
                     (1, AlcanceDoAviso.AppSemEmail),  // diário
                 })
        {
            using var ctx = TestInfra.NovoContexto();
            var prof = Largado();
            prof.UltimoLembreteDeAssinatura = AvisosDoPlanoDoProfessor.PrimeiroDiaBloqueado + dia - 1;
            ctx.Jogadores.Add(prof);
            await ctx.SaveChangesAsync();

            var push = Substitute.For<IPushNotificationService>();
            var agora = Bloqueia.AddDays(dia).Date.AddHours(12);   // hora civilizada

            var enviados = await AvisosDoPlanoDoProfessorBackgroundService.VarrerAsync(ctx, push, Cfg, agora);

            Assert.Equal(1, enviados);
            var chamada = push.ReceivedCalls().Last(c =>
                c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync));
            Assert.Equal(esperado, (AlcanceDoAviso)chamada.GetArguments()[4]!);
        }
    }

    [Fact]
    public void Cada_estagio_novo_tem_titulo_e_frase_proprios()
    {
        // ⚠️ O `_ =>` do Titulo devolve o texto da assinatura vencida. Estágio novo sem linha
        // própria manda o título errado SEM ERRO NENHUM — foi o que quase aconteceu com a
        // cortesia, e o comentário do próprio arquivo avisa.
        var prof = Largado();

        foreach (var estagio in new[] { AvisosDoPlanoDoProfessor.BloqueioEmUmaSemana,
                                        AvisosDoPlanoDoProfessor.BloqueioAmanha,
                                        AvisosDoPlanoDoProfessor.BloqueioEmUmaHora,
                                        AvisosDoPlanoDoProfessor.PrimeiroDiaBloqueado,
                                        AvisosDoPlanoDoProfessor.PrimeiroDiaBloqueado + 5 })
        {
            var titulo = AvisosDoPlanoDoProfessor.Titulo(estagio);
            var frase = AvisosDoPlanoDoProfessor.Frase(estagio, prof, Bloqueia, Cfg);

            Assert.DoesNotContain("venceu em", titulo);
            Assert.Contains("agenda", titulo + " " + frase, StringComparison.OrdinalIgnoreCase);
        }
    }
}
