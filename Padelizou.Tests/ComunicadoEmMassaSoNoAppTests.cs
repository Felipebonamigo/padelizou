using System;
using System.IO;
using System.Threading.Tasks;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — O "AVISAR TODO MUNDO" É SÓ NOTIFICAÇÃO.
//
// 🗣️ Felipe, depois de mandar o aviso da 2ª Etapa ER PADEL TOUR e perguntar por onde ele
// tinha saído: *"acho que esse botão ai, tem q ser só push"* / *"e notificação do sistema la
// nos avisos"*.
//
// 🕳️ O que existia: a ação chamava `EnviarParaJogadorAsync` SEM passar alcance, então caía no
// padrão `SoApp` — que manda e-mail (AlcanceDoAviso.VaiNoEmail é true pra ele). Um comunicado
// de dia de jogo ia pra caixa de e-mail de todos os inscritos, e a cota do Gmail já estourou
// duas vezes por volume desse tipo (ver AlcanceDoAviso, 09/08/2026). É exatamente a pergunta
// que decide o `AppSemEmail`: *"ela faz alguma coisa por causa deste aviso?"* — "chuva,
// atrasou 1h" é pra quem já está a caminho da quadra, e essa pessoa está com o telefone na
// mão, não na caixa de entrada.
//
// 🕳️ E o modal de confirmação PROMETIA três canais — "por notificação, e-mail e WhatsApp" —
// sendo que o WhatsApp nunca saiu: `SoApp` não passa por `VaiNoWhatsApp()`. O organizador
// clicava acreditando ter avisado no canal onde o pessoal realmente conversa.
//
// ⚠️ `AppSemEmail`, e não o `ApenasPush` do AvisoPendente: este último é o desvio do PLACAR AO
// VIVO, que pula a Caixa de Avisos. Aqui a caixa é metade do pedido ("e notificação do sistema
// la nos avisos") e é o único canal que não depende de aparelho registrado — eram 4 em 128.
public class ComunicadoEmMassaSoNoAppTests
{
    // ── POR ONDE O AVISO SAI ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_comunicado_sai_sem_email_e_sem_whatsapp()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id, push: push);

        await controller.Comunicar(torneio.Id, "As chaves saíram!", categoriaId: null);

        // 4 inscritos (2 duplas), cada um com o alcance que NÃO abre o e-mail.
        await push.Received(4).EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);

        // E nenhum aviso com qualquer outro alcance: `SoApp` (o padrão do método) manda
        // e-mail, e os dois `AppEWhatsApp*` queimam o número que a Meta restringe.
        await push.DidNotReceive().EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Is<AlcanceDoAviso>(a => a != AlcanceDoAviso.AppSemEmail));
    }

    [Fact]
    public void O_alcance_escolhido_cala_o_email_e_o_whatsapp()
    {
        // A régua que o teste acima depende, dita na cara: se um dia `AppSemEmail` passar a
        // abrir um desses canais, é aqui que o comunicado em massa avisa.
        Assert.False(AlcanceDoAviso.AppSemEmail.VaiNoEmail());
        Assert.False(AlcanceDoAviso.AppSemEmail.VaiNoWhatsApp());
    }

    // ── O QUE A TELA PROMETE ─────────────────────────────────────────────────────────────

    [Fact]
    public void O_modal_de_confirmacao_nao_promete_canal_que_nao_sai()
    {
        var form = FormDoComunicado();

        Assert.DoesNotContain("WhatsApp", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("e-mail", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", form, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void O_modal_diz_os_dois_canais_que_saem_de_verdade()
    {
        var form = FormDoComunicado();

        Assert.Contains("notificação", form, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Avisos", form, StringComparison.OrdinalIgnoreCase);
        // A ação continua sem volta, e o aviso disso não pode sumir na reescrita.
        Assert.Contains("Não dá pra desfazer", form, StringComparison.Ordinal);
    }

    // O bloco do "Avisar todo mundo" — do título do card até o fim do formulário —, já sem os
    // comentários do Razor (ver TestInfra.SemComentarios: senão o teste passa achando a
    // palavra na explicação em vez de no que a tela mostra).
    private static string FormDoComunicado()
    {
        var fonte = TestInfra.SemComentarios(File.ReadAllText(
            Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_FerramentasDoOrganizador.cshtml")));

        int inicio = fonte.IndexOf("Avisar todo mundo", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o bloco do 'Avisar todo mundo' no parcial.");

        int fim = fonte.IndexOf("</form>", inicio, StringComparison.Ordinal);
        Assert.True(fim >= 0, "Não achei o fim do formulário do comunicado.");

        return fonte[inicio..fim];
    }

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Padelizou.csproj não encontrado a partir do bin.");
    }
}
