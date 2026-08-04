using Microsoft.Extensions.Logging.Abstractions;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A fila que espaça os envios de WhatsApp. Existe porque em 03/08/2026 o sistema despejou os
// avisos de 24 cadastros em minutos e a Meta restringiu o número: o total do dia não era
// absurdo — a RAJADA era.
public class FilaDeWhatsAppTests
{
    private static FilaDeWhatsApp Nova() => new(NullLogger<FilaDeWhatsApp>.Instance);

    [Fact]
    public void O_que_entra_sai_na_mesma_ordem()
    {
        // Ordem importa: "chaves saíram" antes de "seu jogo é o próximo" conta uma história;
        // ao contrário, confunde.
        var fila = Nova();
        fila.Enfileirar("51999998888", "primeira");
        fila.Enfileirar("51999998888", "segunda");

        Assert.True(fila.TentarLer(out var a));
        Assert.True(fila.TentarLer(out var b));
        Assert.Equal("primeira", a!.Texto);
        Assert.Equal("segunda", b!.Texto);
        Assert.False(fila.TentarLer(out _));
    }

    [Fact]
    public void Fila_cheia_descarta_em_vez_de_travar_quem_gerou_o_aviso()
    {
        // Uma ação do jogador (marcar placar, se inscrever) não pode ficar pendurada
        // esperando espaço numa fila de notificação. Aviso é perecível: perder o excedente é
        // melhor do que segurar a tela — ou estourar a memória do servidor.
        var fila = Nova();

        for (var i = 0; i < RitmoDoWhatsApp.TamanhoMaximo; i++)
            Assert.True(fila.Enfileirar("51999998888", $"msg {i}"));

        Assert.False(fila.Enfileirar("51999998888", "essa sobra"));
        Assert.Equal(1, fila.Descartadas);
        Assert.Equal(RitmoDoWhatsApp.TamanhoMaximo, fila.Pendentes);
    }

    [Fact]
    public void O_intervalo_e_sorteado_dentro_da_faixa_e_nao_e_sempre_igual()
    {
        // Cadência exata de relógio é assinatura de robô, e é barato não ter essa assinatura.
        var sorteio = new Random(42);
        var intervalos = Enumerable.Range(0, 40)
            .Select(_ => RitmoDoWhatsApp.ProximoIntervalo(sorteio))
            .ToList();

        Assert.All(intervalos, i =>
        {
            Assert.True(i >= RitmoDoWhatsApp.IntervaloMinimo);
            Assert.True(i <= RitmoDoWhatsApp.IntervaloMaximo);
        });
        Assert.True(intervalos.Distinct().Count() > 1, "intervalo fixo é assinatura de robô");
    }

    [Fact]
    public void O_respiro_e_de_segundos_e_nao_de_minutos()
    {
        // Uma noite de torneio gera dezenas de avisos. Se o intervalo fosse de minutos, o
        // "seu jogo é o próximo" chegaria depois do jogo.
        Assert.True(RitmoDoWhatsApp.IntervaloMinimo >= TimeSpan.FromSeconds(5));
        Assert.True(RitmoDoWhatsApp.IntervaloMaximo <= TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Uma_noite_de_torneio_escoa_em_tempo_aceitavel()
    {
        // 03/08 teve 24 cadastros na primeira hora. Mesmo dobrando isso, a fila precisa
        // esvaziar em minutos — senão trocamos a restrição por um aviso que chega tarde.
        var escoa = RitmoDoWhatsApp.TempoParaEscoar(50);

        Assert.True(escoa < TimeSpan.FromMinutes(20), $"50 avisos levariam {escoa}");
    }

    [Fact]
    public void Receber_por_WhatsApp_nasce_DESMARCADO()
    {
        // Único canal assim, e de propósito: mandar pra quem não pediu derruba o número do
        // Padelizou pra todo mundo. O e-mail não tem esse custo e segue marcado.
        var novo = new PreferenciasViewModel();

        Assert.False(novo.NotificarWhatsApp);
        Assert.True(novo.NotificarEmail);
    }

    [Fact]
    public void Aviso_nasce_sem_WhatsApp_quando_ninguem_diz_nada()
    {
        // A trava que impede a história de 04/08 de se repetir: quem escrever um aviso novo
        // amanhã e esquecer do canal não coloca ninguém no WhatsApp por distração.
        Assert.Equal(AlcanceDoAviso.SoApp, default(AlcanceDoAviso));
    }
}
