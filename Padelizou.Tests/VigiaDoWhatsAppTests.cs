using Padelizou.Services;

namespace Padelizou.Tests;

// O vigia do canal de WhatsApp. Nasceu de um estrago real: em 03/08/2026 o canal caiu às
// 18h36 e ficou fora 17 horas — ~200 avisos falharam, um a um, e ninguém soube. A queda em si
// não era grave (um restart resolveu, sem QR); o silêncio era.
public class VigiaDoWhatsAppTests
{
    [Fact]
    public void Canal_conectado_nao_faz_nada()
    {
        Assert.Equal(AcaoDoVigia.NadaAFazer,
            VigiaDoWhatsApp.Decidir(EstadoDoCanal.Conectado, jaTentouReligar: false));
    }

    [Fact]
    public void Canal_desligado_nao_acorda_ninguem()
    {
        // No dev e no localhost ele vive desligado de propósito. E-mail toda madrugada por
        // causa disso ensinaria o Felipe a ignorar justamente este alerta.
        Assert.Equal(AcaoDoVigia.NadaAFazer,
            VigiaDoWhatsApp.Decidir(EstadoDoCanal.Desligado, jaTentouReligar: false));
        Assert.False(DiagnosticoDoCanal.EhProblema(EstadoDoCanal.Desligado));
    }

    [Fact]
    public void Desconectado_tenta_religar_ANTES_de_incomodar()
    {
        // Foi o que resolveu a queda de 03/08: o pareamento estava vivo, só o socket morreu.
        Assert.Equal(AcaoDoVigia.TentarReligar,
            VigiaDoWhatsApp.Decidir(EstadoDoCanal.Desconectado, jaTentouReligar: false));
    }

    [Fact]
    public void Se_religar_nao_resolveu_ai_sim_chama_o_Felipe()
    {
        // É o caso do aparelho removido no celular: a credencial morre junto e só QR resolve.
        Assert.Equal(AcaoDoVigia.ChamarOFelipe,
            VigiaDoWhatsApp.Decidir(EstadoDoCanal.Desconectado, jaTentouReligar: true));
    }

    [Fact]
    public void API_muda_chama_o_Felipe_direto_sem_tentar_religar()
    {
        // Se a Evolution nem responde, o pedido de restart também não chega — insistir só
        // atrasaria o e-mail de quem precisa levantar o container.
        Assert.Equal(AcaoDoVigia.ChamarOFelipe,
            VigiaDoWhatsApp.Decidir(EstadoDoCanal.SemResposta, jaTentouReligar: false));
        Assert.False(DiagnosticoDoCanal.AdiantaReligar(EstadoDoCanal.SemResposta));
    }

    [Fact]
    public void O_alarme_so_acende_nos_estados_que_sao_problema()
    {
        Assert.True(DiagnosticoDoCanal.EhProblema(EstadoDoCanal.Desconectado));
        Assert.True(DiagnosticoDoCanal.EhProblema(EstadoDoCanal.SemResposta));
        Assert.False(DiagnosticoDoCanal.EhProblema(EstadoDoCanal.Conectado));
        Assert.False(DiagnosticoDoCanal.EhProblema(EstadoDoCanal.Desligado));
    }

    [Fact]
    public void Todo_estado_tem_frase_propria()
    {
        var frases = Enum.GetValues<EstadoDoCanal>().Select(DiagnosticoDoCanal.Frase).ToList();

        Assert.All(frases, f => Assert.False(string.IsNullOrWhiteSpace(f)));
        Assert.Equal(frases.Count, frases.Distinct().Count());
    }

    [Fact]
    public void O_email_diz_o_que_ja_foi_tentado_e_o_que_falta_fazer()
    {
        // Alerta que não diz o próximo passo só transfere a angústia.
        var depoisDeTentar = VigiaDoWhatsApp.CorpoDoEmail(EstadoDoCanal.Desconectado, tentouReligar: true);

        Assert.Contains("não resolveu", depoisDeTentar);
        Assert.Contains("QR", depoisDeTentar);
        Assert.Contains("não são reenviados", depoisDeTentar);
    }

    [Fact]
    public void Quando_a_API_esta_muda_o_email_manda_olhar_o_container()
    {
        // Consertos diferentes: aqui não adianta QR nenhum, o problema é uma camada abaixo.
        var corpo = VigiaDoWhatsApp.CorpoDoEmail(EstadoDoCanal.SemResposta, tentouReligar: false);

        Assert.Contains("docker", corpo);
        Assert.DoesNotContain("não resolveu", corpo);   // não tentou religar, não pode dizer que tentou
    }

    [Fact]
    public void A_checagem_e_frequente_e_o_email_nao()
    {
        // 5 em 5 minutos pra achar rápido; e-mail no máximo de 6 em 6 horas pra não virar
        // ruído — o canal fica fora até alguém agir, e seriam 12 e-mails por hora.
        Assert.True(VigiaDoWhatsApp.IntervaloEntreChecagens <= TimeSpan.FromMinutes(10));
        Assert.True(VigiaDoWhatsApp.IntervaloEntreAvisos >= TimeSpan.FromHours(1));
        Assert.True(VigiaDoWhatsApp.EsperaInicial <= TimeSpan.FromMinutes(5));
    }
}
