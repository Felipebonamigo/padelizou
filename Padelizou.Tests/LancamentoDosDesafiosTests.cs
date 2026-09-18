using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// O que precisa estar escrito ANTES de os Desafios abrirem pro público (16/09/2026).
//
// ⚠️ Nenhum destes testes mede código: medem PROMESSA. Um módulo que publica nome, categoria,
// clubes e a semana em que a pessoa vai jogar — e que deixa um jogador incluir outro numa dupla
// sem pedir licença — não pode entrar no ar com a Política de Privacidade calada sobre isso. E o
// resumo semanal obedece a um interruptor que, na tela, dizia só "Buscar Jogo": quem quisesse
// parar o resumo não acharia o botão.
public class LancamentoDosDesafiosTests
{
    [Fact]
    public void O_interruptor_que_cala_o_resumo_semanal_diz_que_cala()
    {
        // O resumo dos Desafios obedece a `NotificarAvisoJogo` (ver ResumoSemanalDoMural). Se o
        // rótulo não citar os Desafios, a pessoa que quer parar o resumo procura e não acha — e
        // desliga TODAS as notificações, que é o desfecho que a régua de avisos existe pra evitar.
        var tela = Tela(Path.Combine("Views", "Auth", "_PreferenciasFields.cshtml"));

        var rotulo = Regex.Match(tela,
            @"<label[^>]*for=""notifAvisoJogo""[^>]*>(.*?)</label>", RegexOptions.Singleline);

        Assert.True(rotulo.Success, "o rótulo do interruptor notifAvisoJogo sumiu da tela");
        Assert.Contains("Desafios", rotulo.Groups[1].Value);
    }

    [Fact]
    public void O_interruptor_que_impede_ser_incluido_numa_dupla_diz_que_impede()
    {
        // Quem desliga `AceitaConvitesJogo` não pode ser posto numa dupla dos Desafios sem pedir
        // (ver DesafiosController.ParceiroEscolhidoAsync). Se o rótulo fala só de "jogos avulsos
        // de grupos", a pessoa que não quer ser incluída não sabe que é ESTE o botão.
        var tela = Tela(Path.Combine("Views", "Auth", "_PreferenciasFields.cshtml"));

        var rotulo = Regex.Match(tela,
            @"<label[^>]*for=""aceitaConvites""[^>]*>(.*?)</label>", RegexOptions.Singleline);

        Assert.True(rotulo.Success, "o rótulo do interruptor aceitaConvites sumiu da tela");
        Assert.Contains("Desafios", rotulo.Groups[1].Value);
    }

    [Fact]
    public void A_politica_de_privacidade_declara_o_que_os_desafios_mostram()
    {
        var politica = Tela(Path.Combine("Views", "Home", "Privacy.cshtml"));

        Assert.Contains("Desafios", politica);

        // As três afirmações que a política precisa fazer, e que o código sustenta:
        //  · o anúncio aparece só pra quem tem conta aberta (DesafiosController é [Authorize] e a
        //    PortaDosDesafios recusa usuário nulo);
        //  · um jogador pode incluir outro na dupla, e o incluído é avisado e pode sair;
        //  · nenhum contato sai nas telas dos Desafios.
        var trecho = TrechoDosDesafios(politica);
        Assert.Contains("conta aberta", trecho);
        Assert.Contains("sair da dupla", trecho);
        Assert.Contains("WhatsApp", trecho);
    }

    // O parágrafo da política que fala dos Desafios — pra os asserts acima não passarem por
    // acharem as palavras em outra seção qualquer.
    private static string TrechoDosDesafios(string politica)
    {
        var inicio = politica.IndexOf("Desafios", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "a política não cita os Desafios");

        var fim = politica.IndexOf("</p>", inicio, StringComparison.Ordinal);
        return fim < 0 ? politica[inicio..] : politica[inicio..fim];
    }

    private static string Tela(string caminhoRelativo) =>
        Regex.Replace(File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo)),
            @"@\*.*?\*@", "", RegexOptions.Singleline);

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
            {
                return Path.Combine(dir.FullName, "Padelizou");
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Não achei a pasta Padelizou/Views subindo a partir dos testes.");
    }
}
