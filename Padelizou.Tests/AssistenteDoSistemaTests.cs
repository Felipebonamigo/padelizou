using System.Text.RegularExpressions;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// ASSISTENTE DO SISTEMA: vê tudo, não muda nada (08/08/2026, pedido do Felipe pro Foka).
//
// `IsAdminRaiz` e `IsAdminGeral` sempre significaram ver E editar juntos. O assistente é a
// terceira resposta, e ela só entra do lado da leitura.
public class AssistenteDoSistemaTests
{
    private static Jogador Com(bool raiz = false, bool geral = false, bool assistente = false) =>
        new()
        {
            Nome = "Fulano", Cpf = "11144477735",
            IsAdminRaiz = raiz, IsAdminGeral = geral, IsAssistente = assistente,
        };

    [Fact]
    public void Assistente_ve_tudo()
    {
        Assert.True(PoderesNoSistema.PodeOlharTudo(Com(assistente: true)));
    }

    [Fact]
    public void Assistente_NAO_edita_nada()
    {
        // ⚠️ O teste mais importante do arquivo. Se ele cair, o assistente virou administrador.
        Assert.False(PoderesNoSistema.PodeEditarTudo(Com(assistente: true)));
    }

    [Fact]
    public void A_credencial_IsAdmin_do_cracha_NAO_inclui_o_assistente()
    {
        // O claim `IsAdmin` é lido por controllers e views como "pode mexer". Somar o
        // assistente nele abriria os caminhos de escrita todos de uma vez.
        var cracha = IdentidadeJogador.ClaimsDe(Com(assistente: true));

        Assert.Equal("false", cracha.First(c => c.Type == "IsAdmin").Value);
        Assert.Equal("true", cracha.First(c => c.Type == "Assistente").Value);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Admin_de_verdade_continua_editando(bool raiz, bool geral)
    {
        var admin = Com(raiz: raiz, geral: geral);

        Assert.True(PoderesNoSistema.PodeEditarTudo(admin));
        Assert.True(PoderesNoSistema.PodeOlharTudo(admin));
        Assert.False(PoderesNoSistema.SoOlha(admin));
        Assert.Equal("true", IdentidadeJogador.ClaimsDe(admin).First(c => c.Type == "IsAdmin").Value);
    }

    [Fact]
    public void Admin_que_TAMBEM_e_assistente_nao_vira_so_leitura()
    {
        // A flag soma, não substitui: marcar o próprio dono como assistente não pode tirar o
        // poder dele.
        Assert.False(PoderesNoSistema.SoOlha(Com(raiz: true, assistente: true)));
    }

    [Fact]
    public void Jogador_comum_nao_ve_nem_edita()
    {
        var comum = Com();

        Assert.False(PoderesNoSistema.PodeOlharTudo(comum));
        Assert.False(PoderesNoSistema.PodeEditarTudo(comum));
        Assert.False(PoderesNoSistema.SoOlha(comum));
    }

    [Fact]
    public void Ninguem_logado_nao_ve_nem_edita()
    {
        Assert.False(PoderesNoSistema.PodeOlharTudo(null));
        Assert.False(PoderesNoSistema.PodeEditarTudo(null));
    }

    [Fact]
    public void Toda_gravacao_do_painel_admin_esta_sob_HttpPost()
    {
        // ⚠️ ESTE TESTE GUARDA A PREMISSA DO DESENHO INTEIRO.
        //
        // O assistente entra no painel pela trava do VERBO: ObterJogadorAdminAsync aceita ele
        // quando o método é GET. Isso só é seguro porque hoje NENHUMA ação de GET grava — foi
        // conferido uma a uma. No dia em que alguém puser um SaveChangesAsync num GET do
        // painel, o assistente ganha esse poder calado.
        //
        // O teste lê o próprio código-fonte porque a premissa é sobre a FORMA do controller,
        // não sobre o comportamento de uma chamada — não há como pegar isso rodando o app.
        var pasta = PastaDoProjeto();
        var arquivos = Directory.GetFiles(Path.Combine(pasta, "Controllers"), "AdminController*.cs");

        Assert.NotEmpty(arquivos);

        foreach (var arquivo in arquivos)
        {
            string verboAtual = "";
            int linha = 0;

            foreach (var texto in File.ReadLines(arquivo))
            {
                linha++;
                var limpa = texto.Trim();

                if (limpa.StartsWith("[HttpGet]")) verboAtual = "GET";
                else if (limpa.StartsWith("[HttpPost]")) verboAtual = "POST";

                if (limpa.Contains("SaveChangesAsync") && verboAtual == "GET")
                {
                    Assert.Fail(
                        $"{Path.GetFileName(arquivo)}:{linha} grava dentro de uma ação [HttpGet]. "
                        + "O assistente do sistema entra no painel por GET (ObterJogadorAdminAsync) "
                        + "justamente porque GET não grava — isso acabou de deixar de ser verdade. "
                        + "Mova a gravação pra um [HttpPost] ou exija PoderesNoSistema.PodeEditarTudo ali.");
                }
            }
        }
    }

    // Sobe do diretório de saída dos testes até achar o projeto web.
    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Controllers");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Não achei a pasta do projeto subindo a partir de {AppContext.BaseDirectory}");
    }
}
