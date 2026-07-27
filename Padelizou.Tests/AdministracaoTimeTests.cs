using Padelizou.Services;

namespace Padelizou.Tests;

// Regra do Felipe: um time pode ter VÁRIOS administradores. Quem coloca o primeiro é um
// admin do Padelizou; a partir daí um administrador do time coloca o próximo.
//
// Importa porque os 44 times importados do ranking do "Quanto Tá" nasceram sem ninguém: se
// qualquer um pudesse se autodeclarar administrador, o primeiro a passar por ali levava o
// SINDAQUA.
public class AdministracaoTimeTests
{
    // ── Quem pode mexer no time ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(true,  true,  true)]   // admin do sistema que também administra o time
    [InlineData(true,  false, true)]   // admin do sistema, mesmo sem ser do time
    [InlineData(false, true,  true)]   // administrador do time
    [InlineData(false, false, false)]  // nem uma coisa nem outra
    public void Manda_no_time_quem_e_admin_do_sistema_ou_do_proprio_time(
        bool doSistema, bool doTime, bool esperado)
    {
        Assert.Equal(esperado, AdministracaoTime.PodeGerenciar(doSistema, doTime));
    }

    // ── Incluir ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Admin_do_sistema_coloca_o_primeiro_administrador()
    {
        // O caso dos 44 times importados: ninguém administra ainda.
        Assert.Null(AdministracaoTime.ProblemaParaConceder(
            quemPedeEhAdminDoSistema: true, quemPedeEhAdminDoTime: false, alvoJaEAdmin: false));
    }

    [Fact]
    public void Administrador_do_time_inclui_outro()
    {
        Assert.Null(AdministracaoTime.ProblemaParaConceder(
            quemPedeEhAdminDoSistema: false, quemPedeEhAdminDoTime: true, alvoJaEAdmin: false));
    }

    [Fact]
    public void Quem_nao_administra_nao_inclui_ninguem()
    {
        // Sem isto, entrar no time bastaria pra assumir o comando dele.
        var problema = AdministracaoTime.ProblemaParaConceder(false, false, false);

        Assert.NotNull(problema);
        Assert.Contains("administrador", problema!);
    }

    [Fact]
    public void Nao_inclui_duas_vezes_a_mesma_pessoa()
    {
        var problema = AdministracaoTime.ProblemaParaConceder(true, true, alvoJaEAdmin: true);

        Assert.NotNull(problema);
        Assert.Contains("já administra", problema!);
    }

    // ── Remover ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Com_dois_administradores_da_pra_remover_um()
    {
        Assert.Null(AdministracaoTime.ProblemaParaRemover(
            quemPedeEhAdminDoSistema: false, quemPedeEhAdminDoTime: true,
            alvoEAdmin: true, quantosAdministradores: 2));
    }

    [Fact]
    public void Quem_nao_administra_nao_remove_ninguem()
    {
        var problema = AdministracaoTime.ProblemaParaRemover(false, false, true, 3);

        Assert.NotNull(problema);
    }

    [Fact]
    public void Nao_remove_quem_nao_e_administrador()
    {
        var problema = AdministracaoTime.ProblemaParaRemover(true, true, alvoEAdmin: false, quantosAdministradores: 2);

        Assert.NotNull(problema);
        Assert.Contains("não administra", problema!);
    }

    [Fact]
    public void O_ultimo_administrador_nao_se_remove_sozinho()
    {
        // Time sem administrador volta a depender de um admin do Padelizou pra qualquer
        // coisa — é um pé na porta que a pessoa fecharia sem perceber.
        var problema = AdministracaoTime.ProblemaParaRemover(
            quemPedeEhAdminDoSistema: false, quemPedeEhAdminDoTime: true,
            alvoEAdmin: true, quantosAdministradores: 1);

        Assert.NotNull(problema);
        Assert.Contains("único administrador", problema!);
    }

    [Fact]
    public void Admin_do_sistema_consegue_esvaziar_a_administracao()
    {
        // A saída de emergência: se o único administrador sumiu (vendeu o time, brigou,
        // parou de jogar), alguém tem que conseguir tirar ele de lá.
        Assert.Null(AdministracaoTime.ProblemaParaRemover(
            quemPedeEhAdminDoSistema: true, quemPedeEhAdminDoTime: false,
            alvoEAdmin: true, quantosAdministradores: 1));
    }
}
