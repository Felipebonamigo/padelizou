using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// Ligar o aviso no celular é a única parte do produto que roda INTEIRA no navegador: a
// permissão vive lá, a inscrição vive lá, e nada disso passa por um controller que dê pra
// testar do jeito normal. O que estes testes guardam são as três decisões cujo defeito seria
// MUDO — o site continua abrindo, nenhum log aparece, e o estrago só existe no celular de
// outra pessoa:
//
//   1. a reinscrição silenciosa religando o aviso de quem DESLIGOU de propósito;
//   2. ela pedindo permissão sozinha, no carregamento (no iPhone isso é recusado de saída e
//      queima a única chance que temos de perguntar);
//   3. o convite de "ligue os avisos" nascendo órfão no _Layout, ou insistindo com quem já
//      disse não.
//
// Mesmo espírito do PwaArquivosTests: arquivo que ninguém compila, ligado por fora.
public class AvisoNoCelularTests
{
    // ⚠️ Este é o teste que não pode ser apagado. Desligar o aviso pelo perfil cancela a
    // inscrição no navegador, mas a PERMISSÃO continua concedida — não existe como devolvê-la
    // por código. Sem esta conferência, a reinscrição silenciosa veria "permissão concedida,
    // sem inscrição" e religaria o aviso de quem acabou de desligar.
    [Fact]
    public void A_reinscricao_silenciosa_respeita_quem_desligou_o_aviso()
    {
        var corpo = CorpoDaReinscricao();

        Assert.True(corpo.Contains("pushDesligadoNesteAparelho()"),
            "pdzReinscreverPushSePreciso() não confere mais a marca de 'eu desliguei'. "
            + "Do jeito que está, ela religa o push de quem desligou de propósito — e a pessoa "
            + "não tem como entender por que voltou.");
    }

    // A reinscrição roda em TODO carregamento de página. Se ela chamar requestPermission(), a
    // caixa de permissão abre sozinha, sem gesto nenhum: no iPhone o navegador recusa na hora
    // (e permissão negada é definitiva — não dá pra perguntar de novo).
    [Fact]
    public void A_reinscricao_silenciosa_nao_pede_permissao_nenhuma()
    {
        var corpo = CorpoDaReinscricao();

        Assert.True(corpo.Contains("Notification.permission !== \"granted\""),
            "pdzReinscreverPushSePreciso() precisa sair fora quando a permissão não foi concedida.");

        Assert.False(corpo.Contains("requestPermission"),
            "pdzReinscreverPushSePreciso() passou a pedir permissão. Ela roda sozinha no "
            + "carregamento: pedir daí abre a caixa sem a pessoa ter tocado em nada, e o iPhone "
            + "recusa de saída — perdendo a única chance de perguntar.");
    }

    // Desligar tem que deixar rastro ANTES da rede: se o Unsubscribe falhar no meio, o que não
    // pode acontecer é a reinscrição achar que foi o navegador que perdeu a inscrição sozinho.
    [Fact]
    public void Desligar_o_aviso_deixa_a_marca_no_aparelho()
    {
        var push = Arquivo(Path.Combine("wwwroot", "js", "push.js"));

        var inicio = push.IndexOf("async function desativarNotificacoesPush", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei desativarNotificacoesPush() no push.js.");

        var corpo = push[inicio..push.IndexOf("async function statusNotificacoesPush", inicio, StringComparison.Ordinal)];

        Assert.True(corpo.Contains("marcarPushDesligado(true)"),
            "desativarNotificacoesPush() não marca mais o aparelho como desligado — "
            + "a reinscrição silenciosa vai religar o aviso no próximo carregamento.");
    }

    // O convite só faz sentido pra quem JÁ instalou (fora do app instalado quem convida é o
    // modal de instalação) e só quando a permissão nunca foi respondida: 'granted' já é
    // resolvido calado, e 'denied' não pode virar insistência a cada abertura do app.
    [Fact]
    public void O_convite_de_ligar_os_avisos_so_aparece_na_hora_certa()
    {
        var modal = SemComentarioRazor(Arquivo(Path.Combine("Views", "Shared", "_AtivarAvisosModal.cshtml")));

        Assert.Contains("if (!pdzAppInstalado()) return;", modal);
        Assert.Contains("Notification.permission !== 'default'", modal);
        // A marca por aparelho segue existindo; desde 10/08/2026 quem decide se ela cala o
        // convite é a régua compartilhada (ConviteDeInstalarAppTests), e não um booleano.
        Assert.Contains("pdzPodeConvidar(CHAVE_CONVITE_AVISOS)", modal);
    }

    // Duas ligações à distância, as duas mudas se quebrarem: sem o pdzJogadorLogado a
    // reinscrição nunca roda (e o push volta a morrer calado), e sem a partial o convite
    // simplesmente não existe em tela nenhuma.
    [Fact]
    public void O_layout_liga_o_convite_e_avisa_que_tem_alguem_logado()
    {
        var layout = SemComentarioRazor(Arquivo(Path.Combine("Views", "Shared", "_Layout.cshtml")));

        Assert.Contains("window.pdzJogadorLogado = true", layout);
        Assert.Contains("_AtivarAvisosModal", layout);

        // O convite usa as classes .pdz-pwa-* declaradas no irmão dele. Os dois têm que
        // continuar saindo juntos, senão o modal aparece sem estilo nenhum.
        Assert.Contains("_InstalarPwaModal", layout);
    }

    private static string CorpoDaReinscricao()
    {
        var push = Arquivo(Path.Combine("wwwroot", "js", "push.js"));

        var inicio = push.IndexOf("async function pdzReinscreverPushSePreciso", StringComparison.Ordinal);
        Assert.True(inicio >= 0,
            "Não achei pdzReinscreverPushSePreciso() no push.js. Se ela foi renomeada, atualize "
            + "este teste — não o apague: é ele que impede o aviso de voltar sozinho pra quem desligou.");

        // Do começo da função até o registro no DOMContentLoaded, que vem logo depois dela.
        var fim = push.IndexOf("document.addEventListener", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "O push.js mudou de formato: não achei o fim da reinscrição.");

        return push[inicio..fim];
    }

    // Comentário Razor não chega ao navegador — e os daqui citam os próprios trechos que os
    // testes procuram. Sem o corte, a explicação passaria por implementação.
    private static string SemComentarioRazor(string texto) =>
        Regex.Replace(texto, @"@\*.*?\*@", "", RegexOptions.Singleline);

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

    // Sobe do diretório de saída dos testes até achar o projeto web.
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
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
