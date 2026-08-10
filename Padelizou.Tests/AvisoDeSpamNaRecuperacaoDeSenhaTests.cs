using Xunit;

namespace Padelizou.Tests;

// 10/08/2026 — o remetente do site virou `nao-responda@padelizou.com.br`, um domínio nascido no
// mesmo dia. Domínio novo não tem reputação, então cair em spam é o caso COMUM nas primeiras
// semanas, não a exceção.
//
// ⚠️ E a tela de recuperar senha é a pior de todas pra isso acontecer em silêncio: quem está ali
// já não consegue entrar. Se ela diz "está a caminho" e o e-mail não aparece na caixa de entrada,
// a pessoa conclui que o site está quebrado e vai embora — foi exatamente o desfecho de 09/08,
// quando 2 recuperações de senha do mesmo jogador se perderam e ele ficou sem conta.
//
// O aviso existia, mas como nota de rodapé em cinza claro DEPOIS da caixa verde. Nota de rodapé
// ninguém lê. Estes testes existem pra que uma futura faxina de layout não o devolva pra lá.
public class AvisoDeSpamNaRecuperacaoDeSenhaTests
{
    private static string Tela() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Auth", "EsqueciSenha.cshtml"));

    [Fact]
    public void A_tela_manda_olhar_o_spam()
    {
        Assert.Contains("caixa de spam", Tela(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void E_o_aviso_esta_em_DESTAQUE_nao_em_letra_miuda()
    {
        // `alert-warning` é o que faz a pessoa parar e ler. Se isto quebrar porque o aviso virou
        // `small text-muted` de novo, o teste está certo em quebrar: em cinza claro embaixo da
        // caixa verde, ele não cumpre função nenhuma.
        var tela = Tela();
        var alerta = tela.IndexOf("alert-warning", StringComparison.Ordinal);

        Assert.True(alerta >= 0, "O aviso de spam saiu do destaque (alert-warning) na tela de recuperar senha.");
        Assert.Contains("spam", tela[alerta..], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void O_remetente_vem_da_CONFIGURACAO_e_nao_escrito_na_mao()
    {
        // O endereço mora no systemd (ver EMAIL.md) e já mudou uma vez — de `padelizou@gmail.com`
        // pro domínio próprio. Cravado na tela, ele viraria mentira na próxima troca, e mentira
        // justamente na frase que manda a pessoa procurar o e-mail.
        var tela = Tela();

        Assert.DoesNotContain("nao-responda@", tela, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@gmail.com", tela, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RemetenteEmail", tela, StringComparison.Ordinal);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Não achei a pasta do projeto subindo a partir de {AppContext.BaseDirectory}");
    }
}
