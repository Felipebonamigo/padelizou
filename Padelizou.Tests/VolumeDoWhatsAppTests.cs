using Microsoft.Extensions.Logging.Abstractions;
using Padelizou.Services;

namespace Padelizou.Tests;

// O TETO DO CANAL.
//
// O conserto de 04/08 matou a RAJADA (7–16s entre mensagens) e deixou o VOLUME livre. Estes
// testes são a régua do que passa e do que é barrado.
//
// ⚠️ Os números foram REFEITOS em 07/08/2026, e o motivo importa mais que eles: a primeira
// versão (60/hora, 300/dia) veio de prática genérica de anti-spam e não da conta do produto.
// Um torneio de 100 participantes manda ~450 mensagens no dia — e o aviso de "chaves saíram"
// sozinho são 100 de uma vez, o que estourava o teto da hora logo na primeira ação do dia.
// O teto não existe pra apertar o uso normal; existe pra um laço infinito não torrar o número
// numa madrugada.
public class VolumeDoWhatsAppTests
{
    private static readonly DateTime Meio = new(2026, 8, 10, 14, 0, 0);

    private static VolumeDoWhatsApp Novo() => new(NullLogger<VolumeDoWhatsApp>.Instance);

    // ⚠️ O TESTE QUE GUARDA A CONTA DO PRODUTO. Se alguém baixar os tetos sem refazer a conta,
    // é aqui que quebra — e o número do torneio está escrito junto pra não virar chute de novo.
    [Fact]
    public void Um_dia_de_torneio_de_100_participantes_cabe_inteiro()
    {
        // 100 do "chaves saíram" (1 por jogador) + ~350 do "seu jogo é o próximo"
        // (4 por partida × ~88 partidas) + 100 do lembrete de 24h.
        const int diaDeTorneioCheio = 100 + 350 + 100;

        var volume = Novo();

        var passaram = 0;
        for (var i = 0; i < diaDeTorneioCheio; i++)
        {
            // Espalhadas pelas 10 horas de um sábado de torneio.
            if (volume.RegistrarSePuder(Meio.Date.AddHours(8).AddSeconds(i * 65))) passaram++;
        }

        Assert.Equal(diaDeTorneioCheio, passaram);
        Assert.Equal(0, volume.BloqueadasPeloTeto);
    }

    // ⚠️ E O QUE O TETO REALMENTE EXISTE PRA PEGAR: aviso saindo em laço. Ninguém manda 2.000
    // mensagens legítimas num dia com 72 contas cadastradas.
    [Fact]
    public void Um_laco_infinito_e_barrado_antes_de_torrar_o_numero()
    {
        var volume = Novo();

        var passaram = Enumerable.Range(0, 2000)
            .Count(i => volume.RegistrarSePuder(Meio.Date.AddSeconds(i * 10)));

        Assert.Equal(TetoDoWhatsApp.PorDia, passaram);
        Assert.True(volume.BloqueadasPeloTeto > 0);
    }

    [Fact]
    public void O_pico_da_hora_tem_teto_proprio()
    {
        var volume = Novo();

        var passaram = Enumerable.Range(0, TetoDoWhatsApp.PorHora + 20)
            .Count(i => volume.RegistrarSePuder(Meio.AddSeconds(i * 2)));

        Assert.Equal(TetoDoWhatsApp.PorHora, passaram);
        Assert.Equal(20, volume.BloqueadasPeloTeto);
    }

    // As 100 mensagens de "chaves saíram" viram uma rajada só — é o pico real do sistema, e
    // precisa caber com folga no teto da hora.
    [Fact]
    public void As_chaves_de_um_torneio_de_100_saem_de_uma_vez_sem_barrar()
    {
        var volume = Novo();

        var passaram = Enumerable.Range(0, 100)
            .Count(i => volume.RegistrarSePuder(Meio.AddSeconds(i * 11)));

        Assert.Equal(100, passaram);
        Assert.Equal(0, volume.BloqueadasPeloTeto);
    }

    [Fact]
    public void Passada_a_hora_a_janela_anda_e_o_canal_volta_a_enviar()
    {
        // Janela DESLIZANTE, não balde que vira na hora cheia: com balde, o teto inteiro às
        // 13h59 e mais um tanto às 14h01 passariam — o dobro em dois minutos, que é a rajada
        // de volta.
        var volume = Novo();

        for (var i = 0; i < TetoDoWhatsApp.PorHora; i++) volume.RegistrarSePuder(Meio);

        Assert.False(volume.RegistrarSePuder(Meio.AddMinutes(59)));
        Assert.True(volume.RegistrarSePuder(Meio.AddMinutes(61)));
    }

    [Fact]
    public void O_dia_vira_a_meia_noite_e_o_teto_recomeca()
    {
        var volume = Novo();

        for (var i = 0; i < TetoDoWhatsApp.PorDia; i++)
            volume.RegistrarSePuder(Meio.Date.AddSeconds(i * 30));

        Assert.False(volume.RegistrarSePuder(Meio.Date.AddHours(23)));
        Assert.True(volume.RegistrarSePuder(Meio.Date.AddDays(1).AddHours(9)));
    }

    [Fact]
    public void O_que_foi_barrado_e_contado_nunca_sumido_calado()
    {
        // O erro de 03/08 não foi o canal cair — foi ~200 avisos falharem sem ninguém saber.
        var volume = Novo();

        for (var i = 0; i < TetoDoWhatsApp.PorHora + 5; i++) volume.RegistrarSePuder(Meio);

        Assert.Equal(5, volume.BloqueadasPeloTeto);
        Assert.Equal(Meio, volume.UltimaBloqueada);
        Assert.Equal(TetoDoWhatsApp.PorHora, volume.NaUltimaHora(Meio));
        Assert.Equal(TetoDoWhatsApp.PorHora, volume.NoDia(Meio));
    }

    // ⚠️ O AVISO VEM ANTES DA PERDA. Descobrir pelo contador de barradas é descobrir depois de
    // a mensagem já ter sido descartada — e num dia de torneio o que se perde é o
    // "seu jogo é o próximo".
    [Fact]
    public void Aos_80_por_cento_o_Felipe_e_avisado_antes_de_perder_mensagem()
    {
        var volume = Novo();

        var quaseNoTeto = TetoDoWhatsApp.PorHora * TetoDoWhatsApp.PorcentoParaAvisar / 100;

        for (var i = 0; i < quaseNoTeto - 1; i++) volume.RegistrarSePuder(Meio);
        Assert.False(volume.PertoDoTeto(Meio));

        volume.RegistrarSePuder(Meio);
        Assert.True(volume.PertoDoTeto(Meio));

        // E nada foi perdido até aqui: o alerta é aviso, não lápide.
        Assert.Equal(0, volume.BloqueadasPeloTeto);
    }

    [Fact]
    public void O_teto_do_dia_tambem_dispara_o_aviso_sozinho()
    {
        // Um gotejar constante nunca estoura a hora e ainda assim chega no teto do dia.
        var volume = Novo();

        var quaseNoTeto = TetoDoWhatsApp.PorDia * TetoDoWhatsApp.PorcentoParaAvisar / 100;
        for (var i = 0; i < quaseNoTeto; i++) volume.RegistrarSePuder(Meio.Date.AddSeconds(i * 60));

        Assert.True(volume.PertoDoTeto(Meio.Date.AddSeconds(quaseNoTeto * 60)));
    }
}
