using Microsoft.AspNetCore.Http;
using Padelizou.Middleware;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "ACESSOS HOJE" E "PICO DE ACESSOS NO MINUTO" — pedido do Felipe (18/08/2026).
//
// Até aqui o sistema não guardava rastro nenhum de tráfego. Duas peças novas: o filtro que
// decide o que É uma visita (RegistroDeAcessoMiddleware.DeveRegistrar) e a conta em cima do
// que foi registrado (MetricasDeAcesso).
public class MetricasDeAcessoTests
{
    private static DateTime Hoje => new(2026, 8, 18);

    private static void Acessos(DbPadelContext ctx, params DateTime[] quandos)
    {
        foreach (var q in quandos) ctx.AcessosAoSite.Add(new AcessoAoSite { Quando = q });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Acessos_hoje_conta_so_os_de_hoje()
    {
        using var ctx = TestInfra.NovoContexto();
        Acessos(ctx,
            Hoje.AddHours(9), Hoje.AddHours(14), Hoje.AddHours(20),
            Hoje.AddDays(-1).AddHours(23).AddMinutes(59)); // ontem, quase virando o dia

        var acessosHoje = await MetricasDeAcesso.AcessosHojeAsync(ctx, Hoje.Date);

        Assert.Equal(3, acessosHoje);
    }

    [Fact]
    public async Task Sem_nenhum_acesso_hoje_da_zero_e_nao_estoura()
    {
        using var ctx = TestInfra.NovoContexto();

        Assert.Equal(0, await MetricasDeAcesso.AcessosHojeAsync(ctx, Hoje.Date));
        Assert.Equal(0, await MetricasDeAcesso.PicoDeAcessosNoMinutoAsync(ctx, Hoje.Date));
    }

    // ⚠️ O CORAÇÃO DA MÉTRICA: o pico é o MAIOR minuto, não a soma nem a média. Três acessos
    // espalhados em três minutos diferentes não é o mesmo pico que três acessos no mesmo
    // minuto — e é essa diferença que a métrica existe pra mostrar.
    [Fact]
    public async Task Pico_e_o_maior_minuto_nao_a_soma_do_dia()
    {
        using var ctx = TestInfra.NovoContexto();
        var base_ = Hoje.AddHours(19).AddMinutes(30);
        Acessos(ctx,
            base_.AddSeconds(1), base_.AddSeconds(20), base_.AddSeconds(45), // mesmo minuto: 3
            base_.AddMinutes(1),                                             // minuto seguinte: 1
            base_.AddMinutes(5));                                            // outro minuto: 1

        var pico = await MetricasDeAcesso.PicoDeAcessosNoMinutoAsync(ctx, Hoje.Date);

        Assert.Equal(3, pico);
    }

    // 23:59:59 e 00:00:01 são segundos vizinhos, minutos DIFERENTES — não pode juntar os dois
    // só porque estão perto no relógio. Minuto-relógio é a régua, não janela deslizante.
    [Fact]
    public async Task Minutos_vizinhos_no_relogio_nao_se_misturam()
    {
        using var ctx = TestInfra.NovoContexto();
        var virada = Hoje.AddHours(20);
        Acessos(ctx, virada.AddSeconds(-1), virada.AddSeconds(1));

        var pico = await MetricasDeAcesso.PicoDeAcessosNoMinutoAsync(ctx, Hoje.Date);

        Assert.Equal(1, pico);
    }

    [Fact]
    public async Task Pico_tambem_ignora_acesso_de_ontem()
    {
        using var ctx = TestInfra.NovoContexto();
        var mesmoMinutoOntem = Hoje.AddDays(-1).AddHours(19).AddMinutes(30);
        Acessos(ctx, mesmoMinutoOntem, mesmoMinutoOntem.AddSeconds(10), mesmoMinutoOntem.AddSeconds(20));

        Assert.Equal(0, await MetricasDeAcesso.PicoDeAcessosNoMinutoAsync(ctx, Hoje.Date));
    }

    [Fact]
    public async Task Registrar_grava_uma_linha()
    {
        using var ctx = TestInfra.NovoContexto();

        await MetricasDeAcesso.RegistrarAsync(ctx);
        await MetricasDeAcesso.RegistrarAsync(ctx);

        Assert.Equal(2, ctx.AcessosAoSite.Count());
    }

    // ---- Acessos na SÉRIE da tabela (pedido do Felipe, 20/08/2026) ----
    //
    // ⚠️ O RISCO DA COLUNA NOVA: a tabela mostra 14 dias e o registro de acessos nasceu em
    // 18/08/2026. Sem uma fronteira, os dias anteriores apareceriam como ZERO — o painel
    // afirmaria que ninguém entrou no site em dias nos quais, na verdade, ninguém estava
    // contando. É por isso que PrimeiroAcessoAsync existe e que a coluna é int? e não int.

    [Fact]
    public async Task Primeiro_acesso_e_o_mais_antigo_nao_o_primeiro_inserido()
    {
        using var ctx = TestInfra.NovoContexto();
        Acessos(ctx,
            Hoje.AddHours(20),            // inserido primeiro, mas é o mais NOVO
            Hoje.AddDays(-1).AddHours(9), // este é o começo da medição
            Hoje.AddHours(21));

        Assert.Equal(Hoje.AddDays(-1).AddHours(9), await MetricasDeAcesso.PrimeiroAcessoAsync(ctx));
    }

    [Fact]
    public async Task Sem_nenhum_acesso_registrado_nao_ha_comeco_de_medicao()
    {
        using var ctx = TestInfra.NovoContexto();

        Assert.Null(await MetricasDeAcesso.PrimeiroAcessoAsync(ctx));
    }

    [Fact]
    public async Task Desde_traz_so_o_que_esta_dentro_da_serie()
    {
        using var ctx = TestInfra.NovoContexto();
        var inicioSerie = Hoje.AddDays(-3);
        Acessos(ctx,
            Hoje.AddDays(-10),  // fora da janela
            inicioSerie,        // na borda, entra
            Hoje.AddHours(12));

        var dentro = await MetricasDeAcesso.DesdeAsync(ctx, inicioSerie);

        Assert.Equal(2, dentro.Count);
        Assert.DoesNotContain(Hoje.AddDays(-10), dentro);
    }

    // A regra que a tabela aplica em cima do que veio das duas consultas acima. Está escrita
    // aqui, e não só no controller, porque é ela que separa "—" de "·" na tela.
    private static (int? Acessos, bool Parciais) NaFaixa(
        DateTime? primeiroAcesso, List<DateTime> acessos, DateTime inicio, DateTime fim)
    {
        bool jaSeContava = primeiroAcesso != null && primeiroAcesso < fim;
        return (
            jaSeContava ? acessos.Count(d => d >= inicio && d < fim) : null,
            primeiroAcesso != null && primeiroAcesso >= inicio && primeiroAcesso < fim);
    }

    [Fact]
    public async Task Dia_anterior_a_medicao_fica_NULO_e_nao_zero()
    {
        using var ctx = TestInfra.NovoContexto();
        var comecouEm = Hoje.AddHours(20).AddMinutes(29);
        Acessos(ctx, comecouEm);

        var primeiro = await MetricasDeAcesso.PrimeiroAcessoAsync(ctx);
        var acessos = await MetricasDeAcesso.DesdeAsync(ctx, Hoje.AddDays(-13));

        // Ontem: o site existia, a medição não. Zero aqui seria mentira sobre o passado.
        var ontem = NaFaixa(primeiro, acessos, Hoje.AddDays(-1), Hoje);
        Assert.Null(ontem.Acessos);

        // Hoje: já se contava. Um acesso, e o período está incompleto (começou às 20h29).
        var hoje = NaFaixa(primeiro, acessos, Hoje, Hoje.AddDays(1));
        Assert.Equal(1, hoje.Acessos);
        Assert.True(hoje.Parciais);
    }

    [Fact]
    public async Task Dia_ja_medido_sem_ninguem_e_zero_de_verdade()
    {
        using var ctx = TestInfra.NovoContexto();
        Acessos(ctx, Hoje.AddDays(-2).AddHours(10)); // medição começou dois dias atrás

        var primeiro = await MetricasDeAcesso.PrimeiroAcessoAsync(ctx);
        var acessos = await MetricasDeAcesso.DesdeAsync(ctx, Hoje.AddDays(-13));

        // Ontem já se contava e ninguém entrou: isso é um zero legítimo, não um "·".
        var ontem = NaFaixa(primeiro, acessos, Hoje.AddDays(-1), Hoje);
        Assert.Equal(0, ontem.Acessos);
        Assert.False(ontem.Parciais);
    }

    // Só a fatia em que a medição começou é parcial. A seguinte já é um período inteiro — se
    // ela também levasse o asterisco, o aviso viraria ruído permanente.
    [Fact]
    public async Task So_a_faixa_de_estreia_e_parcial()
    {
        using var ctx = TestInfra.NovoContexto();
        Acessos(ctx, Hoje.AddDays(-2).AddHours(20), Hoje.AddDays(-1).AddHours(9));

        var primeiro = await MetricasDeAcesso.PrimeiroAcessoAsync(ctx);
        var acessos = await MetricasDeAcesso.DesdeAsync(ctx, Hoje.AddDays(-13));

        Assert.True(NaFaixa(primeiro, acessos, Hoje.AddDays(-2), Hoje.AddDays(-1)).Parciais);
        Assert.False(NaFaixa(primeiro, acessos, Hoje.AddDays(-1), Hoje).Parciais);
    }
}

// O FILTRO: o que conta como visita, e o que fica de fora.
//
// ⚠️ Guardado separado do teste de conta porque é aqui que mora o risco de dobrar a métrica
// — ou de zerá-la. `Sec-Fetch-Mode: navigate` é a MESMA distinção que wwwroot/sw.js já faz do
// lado do cliente (request.mode === "navigate"); sem ela, o polling do placar ao vivo a cada
// 20s contaria como acesso novo, e "acessos hoje" mediria robô, não gente.
public class RegistroDeAcessoMiddlewareTests
{
    private static DefaultHttpContext ContextoDe(string metodo, string? secFetchMode, string host = "padelizou.com.br")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = metodo;
        ctx.Request.Host = new HostString(host);
        if (secFetchMode != null) ctx.Request.Headers["Sec-Fetch-Mode"] = secFetchMode;
        return ctx;
    }

    [Fact]
    public void Navegacao_de_verdade_no_site_publico_registra()
    {
        Assert.True(RegistroDeAcessoMiddleware.DeveRegistrar(ContextoDe("GET", "navigate")));
    }

    // O caso que motivou o filtro inteiro: sem ele, o placar ao vivo (que se atualiza sozinho
    // a cada 20s) infla "acessos hoje" com quem nem tocou na tela.
    [Fact]
    public void Fetch_de_polling_nao_registra()
    {
        Assert.False(RegistroDeAcessoMiddleware.DeveRegistrar(ContextoDe("GET", "cors")));
        Assert.False(RegistroDeAcessoMiddleware.DeveRegistrar(ContextoDe("GET", null)));
    }

    [Fact]
    public void Asset_estatico_nao_registra()
    {
        // <img>, <link>, <script> mandam Sec-Fetch-Mode diferente de "navigate".
        Assert.False(RegistroDeAcessoMiddleware.DeveRegistrar(ContextoDe("GET", "no-cors")));
    }

    [Fact]
    public void POST_nao_registra_mesmo_com_navigate()
    {
        // Envio de formulário (login, criar torneio) também manda Sec-Fetch-Mode: navigate —
        // sem a checagem de método, cada submit contaria DUAS vezes (a página que abriu o
        // form + o POST do envio).
        Assert.False(RegistroDeAcessoMiddleware.DeveRegistrar(ContextoDe("POST", "navigate")));
    }

    [Theory]
    [InlineData("dev.padelizou.com.br")]
    [InlineData("admin.padelizou.com.br")]
    public void Dev_e_admin_ficam_de_fora(string host)
    {
        Assert.False(RegistroDeAcessoMiddleware.DeveRegistrar(ContextoDe("GET", "navigate", host)));
    }

    [Fact]
    public void Site_publico_de_producao_registra()
    {
        Assert.True(RegistroDeAcessoMiddleware.DeveRegistrar(ContextoDe("GET", "navigate", "padelizou.com.br")));
    }

}
