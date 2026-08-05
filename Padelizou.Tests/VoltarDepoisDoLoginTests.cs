using Padelizou.Services;

namespace Padelizou.Tests;

// Entrar pelo celular jogava a pessoa no PERFIL. Ela estava olhando um torneio, tocava em
// "Entrar" na barra — que é o único caminho fácil no celular — e caía nas próprias
// estatísticas, tendo que achar o torneio de novo numa tela pequena.
//
// O `returnUrl` existia, mas só chegava preenchido quando a tela lembrava de mandar; o botão
// da barra nunca mandou.
public class VoltarDepoisDoLoginTests
{
    private const string Host = "padelizou.com.br";

    // ── Pra onde vai depois de entrar ─────────────────────────────────────────────────────

    [Fact]
    public void Volta_pra_tela_em_que_a_pessoa_estava()
    {
        Assert.Equal("/Torneios/Details/18", VoltarDepoisDoLogin.Destino("/Torneios/Details/18"));
    }

    [Fact]
    public void Sem_destino_vai_pro_INICIO_e_nao_pro_perfil()
    {
        // O pedido do Felipe, em uma linha.
        Assert.Equal("/", VoltarDepoisDoLogin.Destino(null));
        Assert.Equal("/", VoltarDepoisDoLogin.Destino(""));
        Assert.Equal("/", VoltarDepoisDoLogin.Destino("   "));
    }

    [Theory]
    [InlineData("https://site-de-fora.com/x")]
    [InlineData("//site-de-fora.com/x")]      // absoluto disfarçado de relativo
    [InlineData("/\\site-de-fora.com/x")]     // a mesma coisa com barra invertida
    public void Destino_de_fora_do_site_e_ignorado(string destino)
    {
        // "Entre aqui e você cai lá" é golpe clássico: o returnUrl vem da barra de endereços.
        Assert.Equal("/", VoltarDepoisDoLogin.Destino(destino));
    }

    [Theory]
    [InlineData("/Auth/Login")]
    [InlineData("/Auth/Cadastro")]
    [InlineData("/Auth/EsqueciSenha")]
    [InlineData("/Auth/Logout")]
    [InlineData("/AcessoAntecipado/Entrar")]
    public void Nao_volta_pras_telas_de_entrada(string caminho)
    {
        // Cair de novo no login depois de entrar é laço; cair no cadastro ou no portão é
        // mandar quem JÁ entrou pra porta de entrada outra vez.
        Assert.Equal("/", VoltarDepoisDoLogin.Destino(caminho));
    }

    // ── De onde a pessoa veio (o que vai no campo escondido) ──────────────────────────────

    [Fact]
    public void Sem_returnUrl_vale_de_onde_a_pessoa_veio()
    {
        // É o caso do botão "Entrar" da barra, que nunca mandou returnUrl.
        var vindo = VoltarDepoisDoLogin.DeOndeVeio(
            returnUrl: null, referer: $"https://{Host}/Torneios/Details/18", hostAtual: Host);

        Assert.Equal("/Torneios/Details/18", vindo);
    }

    [Fact]
    public void A_query_da_tela_anterior_vai_junto()
    {
        // Quem estava numa busca filtrada volta pra MESMA busca, não pra busca vazia.
        var vindo = VoltarDepoisDoLogin.DeOndeVeio(
            null, $"https://{Host}/Jogadores/Buscar?categoria=3&cidade=Bento", Host);

        Assert.Equal("/Jogadores/Buscar?categoria=3&cidade=Bento", vindo);
    }

    [Fact]
    public void O_returnUrl_explicito_ganha_do_referer()
    {
        // Quem foi EMPURRADO pro login por uma tela que exige conta tem um destino de
        // verdade; o referer nesse caso é só a tela que empurrou.
        var vindo = VoltarDepoisDoLogin.DeOndeVeio(
            returnUrl: "/Torneios/Inscrever/7", referer: $"https://{Host}/Torneios", hostAtual: Host);

        Assert.Equal("/Torneios/Inscrever/7", vindo);
    }

    [Fact]
    public void Referer_de_outro_site_nao_decide_pra_onde_a_pessoa_vai()
    {
        // Senão qualquer site linkaria pro nosso login escolhendo o destino de chegada.
        Assert.Null(VoltarDepoisDoLogin.DeOndeVeio(null, "https://site-de-fora.com/isca", Host));
    }

    [Fact]
    public void Porta_diferente_ja_e_outro_site()
    {
        Assert.Null(VoltarDepoisDoLogin.DeOndeVeio(null, "https://localhost:9999/Torneios", "localhost:5038"));
        Assert.Equal("/Torneios", VoltarDepoisDoLogin.DeOndeVeio(null, "http://localhost:5038/Torneios", "localhost:5038"));
    }

    [Fact]
    public void Sem_referer_nenhum_nao_quebra()
    {
        // O navegador pode não mandar (privacidade, digitou o endereço na mão, abriu o app).
        Assert.Null(VoltarDepoisDoLogin.DeOndeVeio(null, null, Host));
        Assert.Null(VoltarDepoisDoLogin.DeOndeVeio(null, "", Host));
        Assert.Null(VoltarDepoisDoLogin.DeOndeVeio(null, "não é uma URL", Host));
        Assert.Null(VoltarDepoisDoLogin.DeOndeVeio(null, $"https://{Host}/Torneios", hostAtual: null));
    }

    [Fact]
    public void Veio_da_propria_tela_de_login_nao_vira_destino()
    {
        // Acontece de verdade: errou a senha, a página recarrega, e o referer vira o login.
        Assert.Null(VoltarDepoisDoLogin.DeOndeVeio(null, $"https://{Host}/Auth/Login", Host));
    }

    [Fact]
    public void Veio_do_portao_de_acesso_antecipado_tambem_nao()
    {
        // O caminho "Já tem conta? Entrar com meu login" passa pelo portão; voltar pra lá
        // depois de entrar seria mostrar o muro pra quem já está dentro.
        Assert.Null(VoltarDepoisDoLogin.DeOndeVeio(
            null, $"https://{Host}/AcessoAntecipado/Entrar?returnUrl=%2FTorneios", Host));
    }
}
