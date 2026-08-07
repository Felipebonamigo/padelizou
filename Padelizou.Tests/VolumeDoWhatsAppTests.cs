using Microsoft.Extensions.Logging.Abstractions;
using Padelizou.Services;

namespace Padelizou.Tests;

// O TETO DO CANAL (07/08/2026, junto com o religamento depois da restrição da Meta).
//
// O conserto de 04/08 matou a RAJADA (7–16s entre mensagens) e deixou o VOLUME livre: 11,5s
// de média são ~313 mensagens/hora ininterruptas, e o WHATSAPP.md já chama ~500/h de zona de
// risco. Estes testes são a régua do que passa e do que é barrado.
public class VolumeDoWhatsAppTests
{
    private static readonly DateTime Meio = new(2026, 8, 10, 14, 0, 0);

    private static VolumeDoWhatsApp Novo() => new(NullLogger<VolumeDoWhatsApp>.Instance);

    // Número veterano: fora do aquecimento, com o teto do dia no alto. Quem quer testar a
    // janela da HORA precisa disto — senão o teto do dia (30, aquecendo) barra antes e o
    // teste mede a regra errada.
    private static VolumeDoWhatsApp EmRegime()
    {
        var volume = Novo();
        using var ctx = TestInfra.NovoContexto();
        volume.MarcarConectadoAsync(ctx, Meio.AddDays(-30)).GetAwaiter().GetResult();
        return volume;
    }

    // ⚠️ O TESTE QUE JUSTIFICA A CLASSE. Sem teto, o espaçamento sozinho deixa passar um
    // volume que já queimou o número uma vez.
    [Fact]
    public void O_teto_da_hora_barra_o_que_o_espacamento_deixaria_passar()
    {
        var volume = EmRegime();

        var passaram = Enumerable.Range(0, TetoDoWhatsApp.PorHora + 20)
            .Count(i => volume.RegistrarSePuder(Meio.AddSeconds(i * 11)));

        Assert.Equal(TetoDoWhatsApp.PorHora, passaram);
        Assert.Equal(20, volume.BloqueadasPeloTeto);
    }

    [Fact]
    public void Passada_a_hora_a_janela_anda_e_o_canal_volta_a_enviar()
    {
        // Janela DESLIZANTE, não balde que vira na hora cheia: com balde, 60 mensagens às
        // 13h59 e mais 60 às 14h01 passariam — 120 em dois minutos, que é a rajada de volta.
        var volume = EmRegime();

        for (var i = 0; i < TetoDoWhatsApp.PorHora; i++) volume.RegistrarSePuder(Meio);

        Assert.False(volume.RegistrarSePuder(Meio.AddMinutes(59)));
        Assert.True(volume.RegistrarSePuder(Meio.AddMinutes(61)));
    }

    [Fact]
    public void O_teto_do_dia_segura_o_gotejar_que_a_hora_nao_pega()
    {
        // 60/h respeitado por 24h seguidas dariam 1.440 mensagens num dia — cada hora
        // "comportada", o dia inteiro absurdo.
        var volume = EmRegime();

        var passaram = 0;
        for (var i = 0; i < 400; i++)
        {
            // Uma a cada 2 minutos, começando à meia-noite: nunca estoura a hora.
            if (volume.RegistrarSePuder(Meio.Date.AddMinutes(i * 2))) passaram++;
        }

        Assert.Equal(TetoDoWhatsApp.PorDiaEmRegime, passaram);
    }

    [Fact]
    public void O_dia_vira_a_meia_noite_e_o_teto_recomeca()
    {
        var volume = EmRegime();

        for (var i = 0; i < TetoDoWhatsApp.PorDiaEmRegime; i++)
            volume.RegistrarSePuder(Meio.Date.AddMinutes(i * 2));

        Assert.False(volume.RegistrarSePuder(Meio.Date.AddHours(23)));
        Assert.True(volume.RegistrarSePuder(Meio.Date.AddDays(1).AddHours(9)));
    }

    // ⚠️ A REGRA DO LADO SEGURO. "Não sei desde quando este número envia" e "este número é
    // veterano" não podem dar no mesmo resultado: o custo de esperar uma semana é zero perto
    // do custo de perder o número.
    [Fact]
    public void Sem_saber_desde_quando_o_numero_envia_o_teto_e_o_BAIXO()
    {
        Assert.Equal(TetoDoWhatsApp.PorDiaAquecendo, TetoDoWhatsApp.PorDia(Meio, null));
        Assert.True(TetoDoWhatsApp.AindaAquecendo(Meio, null));
    }

    [Theory]
    [InlineData(0, TetoDoWhatsApp.PorDiaAquecendo)]     // acabou de conectar
    [InlineData(6, TetoDoWhatsApp.PorDiaAquecendo)]     // véspera de terminar
    [InlineData(7, TetoDoWhatsApp.PorDiaEmRegime)]      // uma semana: regime
    [InlineData(60, TetoDoWhatsApp.PorDiaEmRegime)]
    public void O_aquecimento_dura_uma_semana_e_ai_o_teto_sobe(int diasDesdeQueConectou, int esperado)
    {
        var comecou = Meio.AddDays(-diasDesdeQueConectou);

        Assert.Equal(esperado, TetoDoWhatsApp.PorDia(Meio, comecou));
    }

    [Fact]
    public async Task O_relogio_do_aquecimento_comeca_uma_vez_so()
    {
        // O vigia chama isto de 5 em 5 minutos enquanto o canal está conectado. Reescrevendo a
        // data a cada checagem, o fim do aquecimento seria empurrado pra sempre e o número
        // nunca sairia do teto de 30/dia.
        using var ctx = TestInfra.NovoContexto();
        var volume = Novo();

        await volume.MarcarConectadoAsync(ctx, Meio);
        await volume.MarcarConectadoAsync(ctx, Meio.AddDays(3));

        Assert.Equal(Meio, volume.AquecimentoComecouEm);
        Assert.Single(ctx.ConfiguracoesDoSistema.Where(c => c.Chave == VolumeDoWhatsApp.ChaveDoAquecimento));
    }

    [Fact]
    public async Task A_data_do_aquecimento_sobrevive_ao_restart()
    {
        // Em memória, cada deploy reiniciaria o aquecimento — e deploy acontece toda semana.
        using var ctx = TestInfra.NovoContexto();
        await Novo().MarcarConectadoAsync(ctx, Meio);

        var depoisDoRestart = Novo();
        await depoisDoRestart.CarregarAsync(ctx);

        Assert.Equal(Meio, depoisDoRestart.AquecimentoComecouEm);
        Assert.False(depoisDoRestart.AindaAquecendo(Meio.AddDays(8)));
    }

    [Fact]
    public void Enquanto_aquece_o_teto_do_dia_e_de_trinta()
    {
        var volume = Novo();   // nunca conectou: aquecendo

        var passaram = Enumerable.Range(0, 50)
            .Count(i => volume.RegistrarSePuder(Meio.Date.AddMinutes(i * 5)));

        Assert.Equal(TetoDoWhatsApp.PorDiaAquecendo, passaram);
        Assert.Equal(20, volume.BloqueadasPeloTeto);
    }

    [Fact]
    public void O_que_foi_barrado_e_contado_nunca_sumido_calado()
    {
        // O erro de 03/08 não foi o canal cair — foi ~200 avisos falharem sem ninguém saber.
        var volume = EmRegime();

        for (var i = 0; i < TetoDoWhatsApp.PorHora + 5; i++) volume.RegistrarSePuder(Meio);

        Assert.Equal(5, volume.BloqueadasPeloTeto);
        Assert.Equal(Meio, volume.UltimaBloqueada);
        Assert.Equal(TetoDoWhatsApp.PorHora, volume.NaUltimaHora(Meio));
        Assert.Equal(TetoDoWhatsApp.PorHora, volume.NoDia(Meio));
    }
}
